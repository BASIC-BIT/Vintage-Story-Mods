#requires -Version 7.0
[CmdletBinding(DefaultParameterSetName = 'Prepare')]
param(
    [Parameter(Mandatory, ParameterSetName = 'Prepare')][string]$AgentControlSource,
    [Parameter(Mandatory)][string]$DataPath,
    [Parameter(Mandatory, ParameterSetName = 'Prepare')][string]$GameInstall,
    [Parameter(Mandatory, ParameterSetName = 'Prepare')][string]$OutputRoot,
    [Parameter(ParameterSetName = 'Prepare')][string]$BackupRoot,
    [Parameter(ParameterSetName = 'Prepare')][string]$DotNetPath = 'dotnet',
    [Parameter(ParameterSetName = 'Prepare')][string]$SdkVersion,
    [Parameter(ParameterSetName = 'Prepare')][switch]$BuildOnly,
    [Parameter(Mandatory, ParameterSetName = 'Restore')][string]$RestoreReceipt
)

$ErrorActionPreference = 'Stop'
$baseCommit = 'ffab5508d10153f0dce3fe0073a94b9172d3a568'
$profile = [IO.Path]::GetFullPath($DataPath).TrimEnd('\', '/')
if ([IO.Path]::GetFileName($profile) -cne 'Profile2') { throw 'Use the explicitly disposable Profile2 data directory.' }

function Assert-PlainPath([string]$Path) {
    $candidate = [IO.Path]::GetFullPath($Path)
    while ($candidate) {
        if (Test-Path -LiteralPath $candidate) {
            if ((Get-Item -LiteralPath $candidate).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Reparse points are not supported: $candidate"
            }
        }
        $candidate = [IO.Path]::GetDirectoryName($candidate)
    }
}

function Get-Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Get-ModId([string]$Path) {
    $zip = [IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $info = $zip.GetEntry('modinfo.json')
        if (!$info) { return $null }
        $reader = [IO.StreamReader]::new($info.Open())
        try { return ($reader.ReadToEnd() | ConvertFrom-Json).modid } finally { $reader.Dispose() }
    } finally { $zip.Dispose() }
}

function Assert-ProfileStopped {
    $pattern = '(?i)(?:^|\s)--dataPath(?:=|\s+)(?:"(?<path>[^"]+)"|(?<path>[^\s]+))'
    foreach ($client in @(Get-CimInstance Win32_Process -Filter "Name='Vintagestory.exe'")) {
        if (!$client.CommandLine) { throw 'A running client has no readable command line; close clients before changing a profile.' }
        if ($client.CommandLine -match $pattern -and [IO.Path]::GetFullPath($Matches.path).TrimEnd('\', '/') -eq $profile) {
            throw 'Profile2 is running. Stop its owned client before preparing or restoring it.'
        }
    }
}

function Save-Receipt {
    $receipt | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM
}

function Restore-OriginalFiles {
    foreach ($entry in $receipt.files) {
        $target = Join-Path $profile $entry.relativePath
        if ($entry.existed) { Copy-Item -LiteralPath (Join-Path $receipt.backupRoot $entry.relativePath) -Destination $target -Force }
        elseif (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
        if ($entry.existed -and (Get-Hash $target) -ne $entry.beforeSha256) { throw "Restored hash differs: $target" }
    }
}

Assert-PlainPath $profile
if ($PSCmdlet.ParameterSetName -eq 'Restore') {
    $receiptPath = (Resolve-Path -LiteralPath $RestoreReceipt).Path
    $receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json -AsHashtable
    if ($receipt.schema -ne 1 -or $receipt.profile -ne $profile -or $receipt.state -ne 'installed') { throw 'This is not an installed receipt for the specified Profile2.' }
    Assert-PlainPath $receipt.backupRoot
    Assert-ProfileStopped
    foreach ($entry in $receipt.files) {
        if ($entry.relativePath -notmatch '^Mods[/\\][^/\\]+\.zip$' -and $entry.relativePath -ne 'ModConfig/agentcontrol.json') {
            throw 'Receipt contains an unmanaged profile path.'
        }
        $target = Join-Path $profile $entry.relativePath
        Assert-PlainPath $target
        if ($entry.existed) {
            $backup = Join-Path $receipt.backupRoot $entry.relativePath
            Assert-PlainPath $backup
            if ($entry.beforeSha256 -notmatch '^[0-9a-f]{64}$' -or (Get-Hash $backup) -ne $entry.beforeSha256) { throw 'An original backup is missing or has changed.' }
            if ($entry.relativePath -ne 'ModConfig/agentcontrol.json' -and (Get-ModId $backup) -ne 'agentcontrol') { throw 'An original mod backup is not Agent Control.' }
        }
        if ($entry.afterSha256) {
            if (!(Test-Path -LiteralPath $target) -or (Get-Hash $target) -ne $entry.afterSha256) { throw 'Profile changed after installation; restoration will not overwrite it.' }
            if ($entry.relativePath -ne 'ModConfig/agentcontrol.json' -and (Get-ModId $target) -ne 'agentcontrol') { throw 'An installed mod is not Agent Control.' }
        } elseif (Test-Path -LiteralPath $target) { throw 'A previously removed helper was replaced; restoration will not overwrite it.' }
    }
    Restore-OriginalFiles
    $receipt.state = 'restored'
    $receipt.restoredAtUtc = [DateTime]::UtcNow.ToString('o')
    Save-Receipt
    Write-Output "Profile2 restored; receipt: $receiptPath"
    return
}

$source = (Resolve-Path -LiteralPath $AgentControlSource).Path
$game = (Resolve-Path -LiteralPath $GameInstall).Path
foreach ($dependency in @('VintagestoryAPI.dll', 'Lib/protobuf-net.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path $game $dependency) -PathType Leaf)) { throw "Game dependency missing: $dependency" }
}
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory; recovered builds are never overwritten.' }
Assert-PlainPath $output
if ($output.StartsWith($profile + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Keep build output outside Profile2.' }
if (!$BuildOnly) {
    if (!(Test-Path -LiteralPath $profile -PathType Container)) { throw 'Create and sign into the disposable Profile2 first.' }
    Assert-ProfileStopped
    $reservedPackage = Join-Path $profile 'Mods/agentcontrol_0_1_0.zip'
    Assert-PlainPath $reservedPackage
    if ((Test-Path -LiteralPath $reservedPackage) -and (Get-ModId $reservedPackage) -ne 'agentcontrol') { throw 'The reserved helper filename belongs to another mod; no profile files were changed.' }
    if (!$BackupRoot) { $BackupRoot = Join-Path $output 'profile-backup' }
    $BackupRoot = [IO.Path]::GetFullPath($BackupRoot)
    Assert-PlainPath $BackupRoot
    if ((Test-Path -LiteralPath $BackupRoot) -or $BackupRoot.StartsWith($profile + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Use a new backup directory outside Profile2.' }
}
$pin = & git -c "safe.directory=$source" -C $source rev-parse "$baseCommit^{commit}"
if ($LASTEXITCODE -ne 0 -or ($pin -join '').Trim() -ne $baseCommit) { throw 'The pinned Agent Control base is unavailable in that checkout.' }
New-Item -ItemType Directory -Path $output | Out-Null
$recovered = Join-Path $output 'agentcontrol-source'
$archive = Join-Path $output 'agentcontrol-source.zip'
& git -c "safe.directory=$source" -C $source archive --format=zip "--output=$archive" $baseCommit Directory.Build.props mods-dll/agentcontrol mods-dll/agentcontrol.abstractions mods-dll/agentcontrol.Tests tools/vsctl
if ($LASTEXITCODE -ne 0) { throw 'Pinned Agent Control archive failed.' }
Expand-Archive -LiteralPath $archive -DestinationPath $recovered
# A nested archive must not inherit its parent's Git prefix and silently skip every patch.
& git -C $recovered init --quiet
if ($LASTEXITCODE -ne 0) { throw 'Cannot isolate Git patch application in the recovered archive.' }
$patch = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs/agent-context/2026-10-06-profile2-startup.patch'
& git -c "safe.directory=$recovered" -C $recovered apply --check --ignore-space-change $patch
if ($LASTEXITCODE -ne 0) { throw 'The tracked startup/cancellation patch no longer applies to the pinned source.' }
& git -c "safe.directory=$recovered" -C $recovered apply --ignore-space-change $patch
if ($LASTEXITCODE -ne 0) { throw 'Applying the tracked startup/cancellation patch failed.' }
if (!(Test-Path -LiteralPath (Join-Path $recovered 'mods-dll/agentcontrol.Tests/StartupEnableTests.cs'))) { throw 'The startup acceptance test was not emitted by the tracked patch.' }
$dotnet = (Get-Command $DotNetPath -ErrorAction Stop).Source
if (!$dotnet) { $dotnet = $DotNetPath }
if (!$SdkVersion) {
    $sdks = & $dotnet --list-sdks
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the chosen .NET SDK.' }
    $SdkVersion = @($sdks | ForEach-Object { if ($_ -match '^(10\.\d+\.\d+)\s') { $Matches[1] } } | Sort-Object { [Version]$_ })[-1]
}
if ($SdkVersion -notmatch '^10\.\d+\.\d+$') { throw 'An installed .NET 10 SDK is required.' }
@{ sdk = @{ version = $SdkVersion; rollForward = 'disable' } } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $recovered 'global.json') -Encoding utf8NoBOM
$previousGame = $env:VINTAGE_STORY
Push-Location $recovered
try {
    $env:VINTAGE_STORY = $game
    foreach ($step in @(
        @{ name = 'test'; arguments = @('test', 'mods-dll/agentcontrol.Tests/agentcontrol.Tests.csproj', '-c', 'Release', '-p:SkipPostBuildPackage=true', '--logger', 'trx;LogFileName=agentcontrol.trx', '--results-directory', (Join-Path $output 'tests')) },
        @{ name = 'build'; arguments = @('build', 'mods-dll/agentcontrol/agentcontrol.csproj', '-c', 'Release', '-p:SkipPostBuildPackage=true') },
        @{ name = 'publish'; arguments = @('publish', 'tools/vsctl/vsctl.csproj', '-c', 'Release', '--output', (Join-Path $output 'vsctl')) }
    )) {
        $arguments = $step.arguments
        & $dotnet @arguments *> (Join-Path $output ($step.name + '.log'))
        if ($LASTEXITCODE -ne 0) { throw "Agent Control $($step.name) failed; inspect $output/$($step.name).log" }
    }
    [xml]$testResult = Get-Content -LiteralPath (Join-Path $output 'tests/agentcontrol.trx') -Raw
    $counters = $testResult.TestRun.ResultSummary.Counters
    if ([int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0 -or [int]$counters.passed -lt 23) { throw 'Startup/cancellation acceptance tests did not all pass.' }
    $passedTests = @($testResult.GetElementsByTagName('UnitTestResult') | Where-Object outcome -eq 'Passed')
    if (@($passedTests | Where-Object testName -like '*StartupEnable_WaitsForReadinessAndCannotUndoCancellationOrDisable*').Count -ne 1 -or
        @($passedTests | Where-Object testName -like '*TerminalExecution_CancelsExtensionOwnerWithoutLosingItsReceipt*').Count -ne 5) { throw 'The required startup and terminal cancellation acceptance cases were not executed.' }
    & (Join-Path $recovered 'mods-dll/agentcontrol/scripts/package.ps1') *> (Join-Path $output 'package.log')
} finally {
    $env:VINTAGE_STORY = $previousGame
    Pop-Location
}
$package = Join-Path $recovered 'mods-dll/agentcontrol/agentcontrol_0_1_0.zip'
$packageHash = Get-Hash $package
$receiptPath = Join-Path $output 'profile-receipt.json'
$receipt = [ordered]@{
    schema = 1; state = 'built'; preparedAtUtc = [DateTime]::UtcNow.ToString('o'); profile = $profile
    sourceCommit = $baseCommit; patchSha256 = Get-Hash $patch; sdkVersion = $SdkVersion; gameInstall = $game
    package = $package; packageSha256 = $packageHash; vsctl = Join-Path $output 'vsctl/vsctl.exe'
    testsPassed = [int]$counters.passed; backupRoot = $BackupRoot; files = @()
}
Save-Receipt
if ($BuildOnly) { Write-Output "Recovered helper built; receipt: $receiptPath"; return }
Assert-ProfileStopped
New-Item -ItemType Directory -Path $BackupRoot | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$oldHelpers = @()
foreach ($item in @(Get-ChildItem -LiteralPath (Join-Path $profile 'Mods') -ErrorAction SilentlyContinue)) {
    if ($item.PSIsContainer) {
        $modinfo = Join-Path $item.FullName 'modinfo.json'
        if ((Test-Path -LiteralPath $modinfo) -and (Get-Content -LiteralPath $modinfo -Raw | ConvertFrom-Json).modid -eq 'agentcontrol') { throw 'Remove or separately back up the unpacked Agent Control directory before staging.' }
        continue
    }
    if ($item.Extension -ne '.zip') { continue }
    if ((Get-ModId $item.FullName) -eq 'agentcontrol') { $oldHelpers += ('Mods/' + $item.Name) }
}
$configPath = Join-Path $profile 'ModConfig/agentcontrol.json'
Assert-PlainPath $configPath
$config = if (Test-Path -LiteralPath $configPath) { Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable } else { @{} }
if ($config -isnot [Collections.IDictionary]) { throw 'Agent Control config must be a JSON object.' }
foreach ($pair in @{ PipeName = 'vintage-story-agentcontrol-profile2'; EnableOnStartup = $true; GrantMutationOnEnable = $true; MaxActionDurationMs = 30000; MaxBatchDurationMs = 60000 }.GetEnumerator()) { $config[$pair.Key] = $pair.Value }
foreach ($relative in @(@($oldHelpers) + @('Mods/agentcontrol_0_1_0.zip', 'ModConfig/agentcontrol.json') | Select-Object -Unique)) {
    $target = Join-Path $profile $relative
    Assert-PlainPath $target
    $existed = Test-Path -LiteralPath $target
    $beforeHash = if ($existed) { Get-Hash $target } else { $null }
    if ($existed) {
        $backup = Join-Path $BackupRoot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $backup) | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
        if ((Get-Hash $backup) -ne $beforeHash -or (Get-Hash $target) -ne $beforeHash) { throw 'Original file changed while backing up Profile2.' }
    }
    $receipt.files += @{ relativePath = $relative; existed = $existed; beforeSha256 = $beforeHash; afterSha256 = $null }
}
$receipt.state = 'backed-up'
Save-Receipt
Assert-ProfileStopped
foreach ($entry in $receipt.files) {
    $target = Join-Path $profile $entry.relativePath
    if ($entry.existed -and (Get-Hash $target) -ne $entry.beforeSha256) { throw 'Profile changed after backup; no installation writes were made.' }
    if (!$entry.existed -and (Test-Path -LiteralPath $target)) { throw 'A target appeared after backup; no installation writes were made.' }
}
try {
    foreach ($relative in $oldHelpers) { Remove-Item -LiteralPath (Join-Path $profile $relative) -Force }
    New-Item -ItemType Directory -Force -Path (Join-Path $profile 'Mods'), (Join-Path $profile 'ModConfig') | Out-Null
    Copy-Item -LiteralPath $package -Destination (Join-Path $profile 'Mods/agentcontrol_0_1_0.zip')
    $config | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $configPath -Encoding utf8NoBOM
    if ((Get-Hash (Join-Path $profile 'Mods/agentcontrol_0_1_0.zip')) -ne $packageHash) { throw 'Installed helper hash differs.' }
    if ((Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json -AsHashtable | ConvertTo-Json -Depth 100 -Compress) -cne ($config | ConvertTo-Json -Depth 100 -Compress)) { throw 'Installed config readback differs.' }
    foreach ($entry in $receipt.files) {
        $target = Join-Path $profile $entry.relativePath
        if (Test-Path -LiteralPath $target) { $entry.afterSha256 = Get-Hash $target }
    }
    $receipt.state = 'installed'
    Save-Receipt
} catch {
    $failure = $_
    try { Restore-OriginalFiles; $receipt.state = 'rolled-back' } catch { $receipt.state = 'recovery-required' }
    Save-Receipt
    throw "Profile preparation failed ($($failure.Exception.Message)); recovery: $receiptPath"
}
Write-Output "Profile2 prepared; restore with -DataPath '$profile' -RestoreReceipt '$receiptPath'"
