[CmdletBinding()]
param([string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) ('.tmp\vs-agent-visuals-test-' + [Guid]::NewGuid().ToString('N'))))

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Profile settings behavior checks require PowerShell 7 on Windows.' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new test output directory.' }
$root = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $root | Out-Null
$checks = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Assertion failed: $Message" }
    $script:checks++
}
function Assert-Throws([scriptblock]$Action, [string]$Pattern) {
    $failure = $null
    try { & $Action } catch { $failure = $_.Exception.Message }
    Assert-True ($null -ne $failure -and $failure -match $Pattern) "Expected rejection /$Pattern/, got: $failure"
}

# Shadow process discovery only in this test process; installed profiles and running clients are never touched.
$global:visualTestProcesses = @()
function Get-CimInstance { param($ClassName, $Filter) return $global:visualTestProcesses }
$scaleScript = Join-Path $PSScriptRoot 'set-vs-agent-scale.ps1'
$profile = Join-Path $root 'Profile2'
New-Item -ItemType Directory -Path $profile | Out-Null
$settings = Join-Path $profile 'clientsettings.json'
$raw = "{`r`n  `"floatSettings`": {`"volume`":0.7, `"guiScale`":1.125},`r`n  `"name`":`"Pip é 森`", `"other`": [1,true,null]`r`n}`r`n"
foreach ($bom in @($false, $true)) {
    $encoding = [Text.UTF8Encoding]::new($bom, $true)
    [IO.File]::WriteAllText($settings, $raw, $encoding)
    $original = [Convert]::ToBase64String([IO.File]::ReadAllBytes($settings))
    $receiptDirectory = Join-Path $root "scale-$bom"
    & $scaleScript -DataPath $profile -Scale 1.25 -OutputDirectory $receiptDirectory
    $expected = $encoding.GetPreamble() + $encoding.GetBytes($raw.Replace('"guiScale":1.125', '"guiScale":1.25'))
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($settings)) -ceq [Convert]::ToBase64String($expected)) 'Only numeric GUI scale bytes change; BOM, Unicode and line endings remain.'
    $receipt = Join-Path $receiptDirectory 'scale-receipt.json'
    & $scaleScript -DataPath $profile -RestoreReceipt $receipt
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($settings)) -ceq $original) 'Receipt restores exact original bytes.'
    & $scaleScript -DataPath $profile -RestoreReceipt $receipt
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($settings)) -ceq $original) 'Repeated restore is idempotent.'
}
$global:visualTestProcesses = @([pscustomobject]@{Name = 'Vintagestory.exe'; CommandLine = 'game.exe --dataPath "' + $profile + '"'})
Assert-Throws { & $scaleScript -DataPath $profile -Scale 1 -OutputDirectory (Join-Path $root 'running') } 'profile is running'
$global:visualTestProcesses = @([pscustomobject]@{Name = 'Vintagestory.exe'; CommandLine = $null})
Assert-Throws { & $scaleScript -DataPath $profile -Scale 1 -OutputDirectory (Join-Path $root 'unreadable') } 'unreadable ownership'
$global:visualTestProcesses = @([pscustomobject]@{Name = 'Vintagestory.exe'; CommandLine = 'game.exe'})
Assert-Throws { & $scaleScript -DataPath $profile -Scale 1 -OutputDirectory (Join-Path $root 'default-profile') } 'no explicit profile'
$global:visualTestProcesses = @([pscustomobject]@{Name = 'Vintagestory.exe'; CommandLine = 'game.exe --dataPath "' + $profile + '0"'})
$conflictDirectory = Join-Path $root 'prefix-safe'
& $scaleScript -DataPath $profile -Scale 1 -OutputDirectory $conflictDirectory
Assert-True ((Get-Content -LiteralPath $settings -Raw | ConvertFrom-Json).floatSettings.guiScale -eq 1) 'Profile20 does not block stopped Profile2.'
$global:visualTestProcesses = @()
Assert-Throws { & $scaleScript -DataPath $profile -Scale 1.25 -OutputDirectory $conflictDirectory } 'new output directory'
[IO.File]::AppendAllText($settings, ' ')
$changedHash = (Get-FileHash -LiteralPath $settings).Hash
Assert-Throws { & $scaleScript -DataPath $profile -RestoreReceipt (Join-Path $conflictDirectory 'scale-receipt.json') } 'discard them'
Assert-True ((Get-FileHash -LiteralPath $settings).Hash -eq $changedHash) 'Conflicting settings are preserved.'
[IO.File]::WriteAllText($settings, $raw.Replace('"guiScale":1.125', '"guiScale":1.125,"guiScale":1.25'))
Assert-Throws { & $scaleScript -DataPath $profile -Scale 1 -OutputDirectory (Join-Path $root 'duplicates') } 'exactly one'
[IO.File]::WriteAllText($settings, $raw.Replace('"guiScale":1.125', '"guiScale":"large"'))
Assert-Throws { & $scaleScript -DataPath $profile -Scale 1 -OutputDirectory (Join-Path $root 'nonnumeric') } 'numeric'
[IO.File]::WriteAllText($settings, $raw)
$junction = Join-Path $root 'ProfileLink'
New-Item -ItemType Junction -Path $junction -Target $profile | Out-Null
Assert-Throws { & $scaleScript -DataPath $junction -Scale 1 -OutputDirectory (Join-Path $root 'linked') } 'Reparse points'
Remove-Item -LiteralPath $junction -Force
$backup = Join-Path $conflictDirectory 'clientsettings.json'
[IO.File]::AppendAllText($backup, ' ')
Assert-Throws { & $scaleScript -DataPath $profile -RestoreReceipt (Join-Path $conflictDirectory 'scale-receipt.json') } 'backup hash'

$fixtureScripts = Join-Path $root 'scripts'
New-Item -ItemType Directory -Path $fixtureScripts | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'capture-native-wizard-clip.ps1') -Destination $fixtureScripts
$clipScript = Join-Path $fixtureScripts 'capture-native-wizard-clip.ps1'
$fakeCapture = @'
param($ExpectedSourceTreeHash, $Vsctl, $DataPath, $PipeName, [ValidateSet('wizard-hub')]$Scenarios, [double]$PreviewTime, $OutputDirectory)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$image = [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAMAAAADCAIAAADZSiLoAAAACXBIWXMAAAABAAAAAQBPJcTWAAAAF0lEQVR4nGNgYGD4D8Ug6v9/hv9wgf8Ac6oI+HvFVCcAAAAASUVORK5CYII=')
[IO.File]::WriteAllBytes((Join-Path $OutputDirectory "$Scenarios.png"), $image)
$manifest = @{
    schema = 1; scenario = $Scenarios; width = 3; height = 3; scale = 1; previewTime = $PreviewTime
    sourceTreeHash = $ExpectedSourceTreeHash; coverage = 'native-client'; fixture = @(@{Key = 'Fixture'; Value = 'one'})
    environment = @{sourceIdentityVerified = $true; modBinaryHash = ('b' * 64); gameVersion = 'test'}
    environmentIdentity = @{viewport = @{width = 3; height = 3}; scale = 1; assets = @(@{name='fixture'; sha256=('c' * 64)})}
}
switch ($global:visualFailure) {
    'source' { $manifest.sourceTreeHash = 'd' * 64 }
    'scene' { $manifest.scenario = 'wizard-chat-default' }
    'time' { $manifest.previewTime = -1 }
    'environment' { if ($PreviewTime -gt 0) { $manifest.environment.modBinaryHash = 'd' * 64 } }
    'fixture' { if ($PreviewTime -gt 0) { $manifest.fixture[0].Value = 'changed' } }
    'scale' { if ($PreviewTime -gt 0) { $manifest.scale = 1.25; $manifest.environmentIdentity.scale = 1.25 } }
    'dimensions' { $manifest.width = 4; $manifest.environmentIdentity.viewport.width = 4 }
    'png' { [IO.File]::WriteAllBytes((Join-Path $OutputDirectory "$Scenarios.png"), [byte[]](1,2,3)) }
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory "$Scenarios.json") -Encoding utf8
$set = @{schema=1;sourceTreeHash=$ExpectedSourceTreeHash;coverage='native-client';dataPath=$DataPath;pipeName=$PipeName;captures=@(@{scenario=$Scenarios})}
if ($global:visualFailure -eq 'profile') { $set.dataPath = Join-Path $DataPath 'wrong' }
if ($global:visualFailure -eq 'pipe') { $set.pipeName = 'wrong-pipe' }
$set | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'captures.json') -Encoding utf8
'@
Set-Content -LiteralPath (Join-Path $fixtureScripts 'capture-native-wizard.ps1') -Value $fakeCapture -Encoding utf8
$fakeFfmpeg = Join-Path $fixtureScripts 'ffmpeg.ps1'
@'
if ($global:visualFailure -eq 'encoder') { $global:LASTEXITCODE = 7; return }
[IO.File]::WriteAllBytes($args[-1], [byte[]](1,2,3,4))
$global:LASTEXITCODE = 0
'@ | Set-Content -LiteralPath $fakeFfmpeg -Encoding utf8
$fakeFfprobe = Join-Path $fixtureScripts 'ffprobe.ps1'
@'
$count = @(Get-ChildItem -LiteralPath (Split-Path -Parent $args[-1]) -Filter 'frame-*.png').Count
if ($global:visualFailure -eq 'probe') { $count++ }
@{streams=@(@{codec_name='h264';width=4;height=4;nb_read_frames=$count});format=@{duration=($count / 4.0).ToString([Globalization.CultureInfo]::InvariantCulture)}} | ConvertTo-Json -Depth 8
$global:LASTEXITCODE = 0
'@ | Set-Content -LiteralPath $fakeFfprobe -Encoding utf8
$arguments = @{ExpectedSourceTreeHash = ('a' * 64); Vsctl = 'unused'; DataPath = $profile; DurationSeconds = 0.25; SamplesPerSecond = 4; Ffmpeg = $fakeFfmpeg; Ffprobe = $fakeFfprobe}
$global:visualFailure = ''
$clipDirectory = Join-Path $root 'clip-success'
& $clipScript @arguments -OutputDirectory $clipDirectory
$clip = Get-Content -LiteralPath (Join-Path $clipDirectory 'clip.json') -Raw | ConvertFrom-Json
Assert-True ($clip.scenario -eq 'wizard-hub' -and $clip.frames.Count -eq 2 -and $clip.width -eq 3 -and $clip.encodedWidth -eq 4 -and $clip.videoDurationSeconds -eq 0.5) 'Default hub poses preserve odd full viewport with declared padding.'
Assert-True ($clip.limitation -match 'not a real-time recording') 'Receipt describes sampled-pose evidence limits.'
foreach ($frame in $clip.frames) {
    Assert-True ((Get-FileHash -LiteralPath (Join-Path $clipDirectory $frame.file)).Hash.ToLowerInvariant() -eq $frame.imageSha256) 'Frame image hash is recorded.'
    Assert-True ((Get-FileHash -LiteralPath (Join-Path $clipDirectory $frame.captureManifest)).Hash.ToLowerInvariant() -eq $frame.manifestSha256) 'Capture manifest hash is recorded.'
}
Assert-True ((Get-FileHash -LiteralPath (Join-Path $clipDirectory $clip.video)).Hash.ToLowerInvariant() -eq $clip.videoSha256) 'Video hash is recorded.'
Assert-Throws { & $clipScript @arguments -OutputDirectory $clipDirectory } 'new clip directory'
foreach ($case in @('source','scene','time','environment','fixture','scale','dimensions','png','profile','pipe','encoder','probe')) {
    $global:visualFailure = $case
    Assert-Throws { & $clipScript @arguments -OutputDirectory (Join-Path $root "clip-reject-$case") } 'differ|changed|not a PNG|failed'
}
$global:visualFailure = ''
Assert-Throws { & $clipScript @arguments -Scenario 'unknown' -OutputDirectory (Join-Path $root 'clip-reject-unknown') } 'ValidateSet|does not belong'
$tooMany = $arguments.Clone()
$tooMany.DurationSeconds = 10
$tooMany.SamplesPerSecond = 4
Assert-Throws { & $clipScript @tooMany -OutputDirectory (Join-Path $root 'clip-reject-limit') } 'at most 32'
$realEncoder = Get-Command ffmpeg -CommandType Application -ErrorAction SilentlyContinue
$realProbe = Get-Command ffprobe -CommandType Application -ErrorAction SilentlyContinue
if ($realEncoder -and $realProbe) {
    $realArguments = $arguments.Clone()
    $realArguments.Ffmpeg = $realEncoder.Source
    $realArguments.Ffprobe = $realProbe.Source
    $realDirectory = Join-Path $root 'clip-real-encode'
    & $clipScript @realArguments -OutputDirectory $realDirectory
    $realClip = Get-Content -LiteralPath (Join-Path $realDirectory 'clip.json') -Raw | ConvertFrom-Json
    Assert-True ($realClip.frames.Count -eq 2 -and $realClip.encodedWidth -eq 4 -and $realClip.encodedHeight -eq 4) 'Actual ffmpeg/ffprobe encode and inspect the tiny odd-size pose fixture.'
} else { Write-Warning 'Actual encoder integration skipped because ffmpeg/ffprobe are unavailable; fake tool behavior checks passed.' }
Remove-Variable visualFailure -Scope Global -ErrorAction SilentlyContinue
Remove-Variable visualTestProcesses -Scope Global -ErrorAction SilentlyContinue
Write-Host "$checks visual workflow assertions passed; disposable fixtures: $root"
