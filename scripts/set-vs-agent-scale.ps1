[CmdletBinding(DefaultParameterSetName = 'Set')]
param(
    [Parameter(Mandatory)][string]$DataPath,
    [Parameter(Mandatory, ParameterSetName = 'Set')][ValidateRange(0.5, 3)][double]$Scale,
    [Parameter(ParameterSetName = 'Set')][string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) ('.tmp\vs-scale-' + [Guid]::NewGuid().ToString('N'))),
    [Parameter(Mandatory, ParameterSetName = 'Restore')][string]$RestoreReceipt
)

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Vintage Story profile process checks require Windows and PowerShell 7.' }

function Assert-NoReparsePoint([string]$Path) {
    $item = Get-Item -LiteralPath $Path -Force
    while ($null -ne $item) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse points are not allowed: $($item.FullName)" }
        $item = if ($item -is [IO.DirectoryInfo]) { $item.Parent } else { $item.Directory }
    }
}

function Assert-ProfileStopped {
    $profilePattern = '(?i)(?:^|[\s"''=])' + [regex]::Escape($profile) + '(?=$|[\s"''\\/])'
    foreach ($process in @(Get-CimInstance Win32_Process -Filter "Name='Vintagestory.exe' OR Name='VSCrashReporter.exe'")) {
        if ([string]::IsNullOrWhiteSpace($process.CommandLine)) { throw 'A game process has unreadable ownership; close it before editing settings.' }
        if ($process.CommandLine -match $profilePattern) { throw 'This profile is running; close it before editing settings.' }
        if ($process.Name -eq 'Vintagestory.exe' -and $process.CommandLine -notmatch '(?i)(?:^|\s)--dataPath(?:\s|=)') {
            throw 'A game client has no explicit profile; close it before editing settings.'
        }
    }
}

function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Write-Settings([byte[]]$Bytes, [string]$ExpectedHash) {
    Assert-NoReparsePoint $settings
    Assert-ProfileStopped
    if ((Get-Sha256 $settings) -ne $ExpectedHash) { throw 'Settings changed since preflight; nothing was overwritten.' }
    $temporary = Join-Path $profile ('clientsettings.' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        [IO.File]::WriteAllBytes($temporary, $Bytes)
        [IO.File]::Replace($temporary, $settings, [NullString]::Value)
    } finally {
        if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
    }
}

$profile = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $DataPath).Path).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (-not (Test-Path -LiteralPath $profile -PathType Container)) { throw 'DataPath must be an existing profile directory.' }
$settings = Join-Path $profile 'clientsettings.json'
if (-not (Test-Path -LiteralPath $settings -PathType Leaf)) { throw 'The profile clientsettings.json is missing.' }
Assert-NoReparsePoint $settings
Assert-ProfileStopped

if ($PSCmdlet.ParameterSetName -eq 'Restore') {
    $receiptPath = (Resolve-Path -LiteralPath $RestoreReceipt).Path
    Assert-NoReparsePoint $receiptPath
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
    $backup = Join-Path (Split-Path -Parent $receiptPath) 'clientsettings.json'
    if ($receipt.schema -ne 1 -or $receipt.profile -ne $profile -or $receipt.settings -ne $settings -or $receipt.backup -ne $backup -or
        $receipt.beforeSha256 -notmatch '^[0-9a-f]{64}$' -or $receipt.afterSha256 -notmatch '^[0-9a-f]{64}$') {
        throw 'The scale receipt does not describe this profile and its adjacent backup.'
    }
    Assert-NoReparsePoint $backup
    if ((Get-Sha256 $backup) -ne $receipt.beforeSha256) { throw 'The settings backup hash differs.' }
    $currentHash = Get-Sha256 $settings
    if ($currentHash -eq $receipt.beforeSha256) { Write-Host 'Original profile settings are already restored.'; return }
    # shortcut: refuse later settings edits, restore only before the game rewrites settings or make a fresh scale change.
    if ($currentHash -ne $receipt.afterSha256) { throw 'Settings changed after the scale edit; restoring the full backup would discard them.' }
    Write-Settings ([IO.File]::ReadAllBytes($backup)) $receipt.afterSha256
    if ((Get-Sha256 $settings) -ne $receipt.beforeSha256) { throw 'Restored settings do not match the original backup.' }
    Write-Host "Original profile settings restored from $receiptPath"
    return
}

$bytes = [IO.File]::ReadAllBytes($settings)
$beforeHash = Get-Sha256 $settings
$hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xef -and $bytes[1] -eq 0xbb -and $bytes[2] -eq 0xbf
$utf8 = [Text.UTF8Encoding]::new($false, $true)
$offset = if ($hasBom) { 3 } else { 0 }
$raw = $utf8.GetString($bytes, $offset, $bytes.Length - $offset)
$before = $raw | ConvertFrom-Json
if ($null -eq $before.floatSettings.guiScale -or [regex]::Matches($raw, '"guiScale"\s*:').Count -ne 1 -or
    [regex]::Matches($raw, '"floatSettings"\s*:').Count -ne 1) { throw 'Expected exactly one floatSettings.guiScale setting.' }
$matches = [regex]::Matches($raw, '(?s)"floatSettings"\s*:\s*\{[^{}]*?"guiScale"\s*:\s*(?<value>-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?)(?=\s*[,}])')
if ($matches.Count -ne 1) { throw 'Expected one native numeric floatSettings.guiScale value.' }
$value = $matches[0].Groups['value']
$scaleText = $Scale.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
$changed = $raw.Substring(0, $value.Index) + $scaleText + $raw.Substring($value.Index + $value.Length)
$after = $changed | ConvertFrom-Json
if ($after.floatSettings.guiScale -ne $Scale) { throw 'Requested scale did not parse correctly.' }
$after.floatSettings.guiScale = $before.floatSettings.guiScale
if (($after | ConvertTo-Json -Depth 100 -Compress) -cne ($before | ConvertTo-Json -Depth 100 -Compress)) { throw 'Scale edit changed another parsed setting.' }
$changedBytes = $utf8.GetBytes($changed)
if ($hasBom) { $changedBytes = [byte[]](@(0xef, 0xbb, 0xbf) + $changedBytes) }
$afterHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($changedBytes)).ToLowerInvariant()
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory; scale backups are never overwritten.' }
$outputParent = Split-Path -Parent $output
if (-not (Test-Path -LiteralPath $outputParent -PathType Container)) { throw 'The output parent directory must exist.' }
Assert-NoReparsePoint $outputParent
New-Item -ItemType Directory -Path $output | Out-Null
$backup = Join-Path $output 'clientsettings.json'
[IO.File]::WriteAllBytes($backup, $bytes)
if ((Get-Sha256 $backup) -ne $beforeHash) { throw 'The settings backup hash differs.' }
$receiptPath = Join-Path $output 'scale-receipt.json'
$receipt = [ordered]@{
    schema = 1; configuredAtUtc = [DateTime]::UtcNow.ToString('o'); profile = $profile; settings = $settings; backup = $backup
    guiScaleBefore = $before.floatSettings.guiScale; guiScaleAfter = $Scale; beforeSha256 = $beforeHash; afterSha256 = $afterHash
    state = 'prepared'
}
$receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding utf8
Write-Settings $changedBytes $beforeHash
if ((Get-Sha256 $settings) -ne $afterHash) { throw "Saved scale hash differs; the original backup and receipt remain at $output" }
$receipt.state = 'applied'
$receipt | ConvertTo-Json | Set-Content -LiteralPath $receiptPath -Encoding utf8
Write-Host "GUI scale configured: $scaleText; restore receipt: $receiptPath"
