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
# Exercise the real native runner with a fake bridge and a disposable profile directory.
$nativeScript = Join-Path $PSScriptRoot 'capture-native-wizard.ps1'
$fakeVsctl = Join-Path $fixtureScripts 'vsctl.ps1'
$global:nativeVisualProfile = $profile
$global:nativeVisualFailure = ''
$global:nativeVisualCalls = 0
@'
param($Command)
$global:nativeVisualCalls++
$global:LASTEXITCODE = 0
switch ($Command) {
    'hello' {
        $result = @{protocolVersion = $(if ($global:nativeVisualFailure -eq 'protocol') {'2.0'} else {'1.0'}); mutationGranted = $true}
    }
    'extensions' {
        $result = @('thebasics.setup.open','thebasics.setup.capture','thebasics.setup.poll') | ForEach-Object { @{operation = $_} }
    }
    'execute' {
        $fileArgument = [Array]::IndexOf([object[]]$args, '--file')
        if ($fileArgument -lt 0) { throw 'Expected a vsctl action file.' }
        $file = $args[$fileArgument + 1]
        $actions = @(Get-Content -LiteralPath $file -Raw | ConvertFrom-Json)
        $details = @()
        foreach ($action in $actions) {
            $detail = @{}
            if ($action.operation -eq 'thebasics.setup.capture') {
                $global:nativeVisualJob = [Guid]::NewGuid().ToString('N')
                $global:nativeVisualDirectory = Join-Path $global:nativeVisualProfile "Screenshots/thebasics-setup/$global:nativeVisualJob"
                $scene = $action.arguments.scenario
                New-Item -ItemType Directory -Path $global:nativeVisualDirectory | Out-Null
                [IO.File]::WriteAllBytes((Join-Path $global:nativeVisualDirectory "$scene.png"), [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAMAAAADCAIAAADZSiLoAAAACXBIWXMAAAABAAAAAQBPJcTWAAAAF0lEQVR4nGNgYGD4D8Ug6v9/hv9wgf8Ac6oI+HvFVCcAAAAASUVORK5CYII='))
                @{
                    scenario = $scene; previewTime = $action.arguments.previewTime; width = 3; height = 3; scale = 1
                    sourceTreeHash = $(if ($global:nativeVisualFailure -eq 'source') {'b' * 64} else {'a' * 64})
                    coverage = 'native-client'; environment = @{sourceIdentityVerified = ($global:nativeVisualFailure -ne 'unverified')}
                } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $global:nativeVisualDirectory "$scene.json") -Encoding utf8
                $detail = @{jobId = $global:nativeVisualJob}
            }
            if ($action.operation -eq 'thebasics.setup.poll') {
                $detail = @{status = 'completed'; directory = $global:nativeVisualDirectory}
            }
            $details += @{detail = $detail}
        }
        $result = @{status = 'completed'; actions = $details}
    }
}
@{ok = $true; result = $result} | ConvertTo-Json -Depth 15
'@ | Set-Content -LiteralPath $fakeVsctl -Encoding utf8
$newScenes = @(
    'wizard-appearance-bubbles', 'wizard-appearance-names', 'wizard-map-visibility', 'wizard-map-managed', 'wizard-map-hidden', 'wizard-characters-sheets',
    'wizard-roleplay-dice', 'wizard-server-history', 'wizard-server-stats', 'wizard-privileges', 'wizard-notifications-sleep',
    'wizard-chat-obscured', 'wizard-chat-clear', 'wizard-chat-signing-visible', 'wizard-chat-signing-blocked',
    'wizard-chat-language-enabled', 'wizard-chat-language-disabled', 'wizard-travel-damage-cancels', 'wizard-travel-damage-continues',
    'wizard-roleplay-markers-enabled', 'wizard-roleplay-markers-disabled',
    'wizard-language-configured', 'wizard-language-edited', 'wizard-language-invalid', 'wizard-language-grants',
    'wizard-language-color-palette', 'wizard-language-color-custom', 'wizard-language-color-selected'
)
$nativeArguments = @{ExpectedSourceTreeHash = ('a' * 64); Vsctl = $fakeVsctl; DataPath = $profile; Scenarios = $newScenes}
$nativeDirectory = Join-Path $root 'native-new-branches'
& $nativeScript @nativeArguments -OutputDirectory $nativeDirectory
$native = Get-Content -LiteralPath (Join-Path $nativeDirectory 'captures.json') -Raw | ConvertFrom-Json
Assert-True (($native.captures.scenario -join ',') -ceq ($newScenes -join ',')) 'The native runner accepts every new branch fixture in the selected order.'
Assert-True ($native.coverage -eq 'native-client' -and $native.sourceTreeHash -ceq ('a' * 64) -and $native.dataPath -eq $profile -and $native.pipeName -eq 'vintage-story-agentcontrol-profile2') 'New branches retain source, profile, coverage and pipe provenance.'
foreach ($scene in $newScenes) {
    $expectedTime = switch -Wildcard ($scene) {
        'wizard-chat-signing-*' { 8.5; break }
        'wizard-chat-obscured' { 5; break }
        'wizard-chat-clear' { 5; break }
        'wizard-chat-language-*' { 5; break }
        'wizard-travel-damage-*' { 7; break }
        'wizard-notifications-sleep' { 4.5; break }
        default { 0.75 }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $nativeDirectory "$scene.json") -Raw | ConvertFrom-Json
    Assert-True ($manifest.previewTime -eq $expectedTime) 'Capture fixtures use their deterministic comparison pose by default.'
}
$lifecycleScenes = @(
    'wizard-appearance-bubbles-visible', 'wizard-appearance-bubbles-expired', 'wizard-appearance-typing-active', 'wizard-appearance-typing-thinking',
    'wizard-appearance-names-targeted', 'wizard-appearance-names-untargeted', 'wizard-appearance-names-blocked', 'wizard-appearance-names-distant',
    'wizard-characters-autoopen', 'wizard-characters-manual', 'wizard-characters-slots-first', 'wizard-characters-slots-second',
    'wizard-notes-entries', 'wizard-notes-notebook', 'wizard-personal-notebook',
    'wizard-chat-focus-switches', 'wizard-chat-focus-stays', 'wizard-chat-focus-remembers', 'wizard-chat-focus-default'
)
$lifecycleArguments = $nativeArguments.Clone()
$lifecycleArguments.Scenarios = $lifecycleScenes
$lifecycleDirectory = Join-Path $root 'native-lifecycles'
& $nativeScript @lifecycleArguments -OutputDirectory $lifecycleDirectory
$lifecycles = Get-Content -LiteralPath (Join-Path $lifecycleDirectory 'captures.json') -Raw | ConvertFrom-Json
Assert-True (($lifecycles.captures.scenario -join ',') -ceq ($lifecycleScenes -join ',')) 'All 19 lifecycle captures are opt-in and retain their selected order.'
foreach ($scene in $lifecycleScenes) {
    $expectedTime = switch ($scene) {
        'wizard-appearance-bubbles-visible' { 0.5; break }
        'wizard-appearance-bubbles-expired' { 5; break }
        'wizard-appearance-typing-active' { 1; break }
        'wizard-appearance-typing-thinking' { 5.1; break }
        'wizard-appearance-names-targeted' { 0.5; break }
        'wizard-appearance-names-untargeted' { 3.5; break }
        'wizard-appearance-names-distant' { 6.5; break }
        'wizard-appearance-names-blocked' { 9.5; break }
        'wizard-characters-slots-second' { 4.75; break }
        'wizard-chat-focus-switches' { 1.25; break }
        'wizard-chat-focus-stays' { 1.25; break }
        'wizard-chat-focus-remembers' { 7.25; break }
        'wizard-chat-focus-default' { 7.25; break }
        default { 0.75 }
    }
    $manifest = Get-Content -LiteralPath (Join-Path $lifecycleDirectory "$scene.json") -Raw | ConvertFrom-Json
    Assert-True ($manifest.previewTime -eq $expectedTime) "Lifecycle capture $scene uses its deterministic default time."
}
$defaultArguments = $nativeArguments.Clone()
$defaultArguments.Remove('Scenarios')
$defaultDirectory = Join-Path $root 'native-legacy-default'
& $nativeScript @defaultArguments -OutputDirectory $defaultDirectory
$defaultSet = Get-Content -LiteralPath (Join-Path $defaultDirectory 'captures.json') -Raw | ConvertFrom-Json
Assert-True ($defaultSet.captures.Count -eq 26 -and @($defaultSet.captures.scenario | Where-Object { $_ -in $lifecycleScenes }).Count -eq 0) 'The native default remains the 26 legacy captures.'
$colorScenes = @('wizard-chat-colors', 'wizard-chat-colors-custom', 'wizard-nametag-colors', 'wizard-nametag-colors-alpha', 'wizard-nametag-colors-custom')
$colorArguments = $nativeArguments.Clone()
$colorArguments.Scenarios = $colorScenes
$colorDirectory = Join-Path $root 'native-colors'
& $nativeScript @colorArguments -OutputDirectory $colorDirectory
$colors = Get-Content -LiteralPath (Join-Path $colorDirectory 'captures.json') -Raw | ConvertFrom-Json
Assert-True (($colors.captures.scenario -join ',') -ceq ($colorScenes -join ',')) 'Color captures use a separate five-scene batch within the session limit.'
Assert-True (@(@($defaultSet.captures.scenario) + $newScenes + $lifecycleScenes + $colorScenes | Sort-Object -Unique).Count -eq 67) 'The native consumer accepts all 67 distinct wizard scenes.'
$recentArguments = $nativeArguments.Clone()
$recentArguments.Scenarios = @('wizard-sheet-builder', 'wizard-voices', 'wizard-wording', 'wizard-diagnostics')
$recentDirectory = Join-Path $root 'native-current-registry'
& $nativeScript @recentArguments -OutputDirectory $recentDirectory
$recent = Get-Content -LiteralPath (Join-Path $recentDirectory 'captures.json') -Raw | ConvertFrom-Json
Assert-True (($recent.captures.scenario -join ',') -ceq ($recentArguments.Scenarios -join ',')) 'Current sheet, voice, wording and diagnostics scenes are selectable without expanding the default batch.'
$unknownDirectory = Join-Path $root 'native-reject-unknown'
$unknownArguments = $nativeArguments.Clone()
$unknownArguments.Scenarios = @('wizard-not-a-scene')
$callsBeforeUnknown = $global:nativeVisualCalls
Assert-Throws { & $nativeScript @unknownArguments -OutputDirectory $unknownDirectory } 'Unknown wizard capture scene'
Assert-True ($global:nativeVisualCalls -eq $callsBeforeUnknown -and !(Test-Path -LiteralPath $unknownDirectory)) 'Unknown scenes are rejected before bridge calls or output creation.'
$explicitDirectory = Join-Path $root 'native-explicit-time'
& $nativeScript -ExpectedSourceTreeHash ('a' * 64) -Vsctl $fakeVsctl -DataPath $profile -Scenarios 'wizard-chat-signing-blocked' -PreviewTime 0 -OutputDirectory $explicitDirectory
$explicitManifest = Get-Content -LiteralPath (Join-Path $explicitDirectory 'wizard-chat-signing-blocked.json') -Raw | ConvertFrom-Json
Assert-True ($explicitManifest.previewTime -eq 0) 'An explicit zero pose overrides a scene default for sampled clips.'
$lastPoseDirectory = Join-Path $root 'native-last-valid-time'
& $nativeScript -ExpectedSourceTreeHash ('a' * 64) -Vsctl $fakeVsctl -DataPath $profile -Scenarios 'wizard-appearance-typing-thinking' -PreviewTime 10 -OutputDirectory $lastPoseDirectory
$lastPoseManifest = Get-Content -LiteralPath (Join-Path $lastPoseDirectory 'wizard-appearance-typing-thinking.json') -Raw | ConvertFrom-Json
Assert-True ($lastPoseManifest.previewTime -eq 10) 'An explicit ten-second pose remains valid and overrides lifecycle defaults.'
$oversizedDirectory = Join-Path $root 'native-reject-session-limit'
$oversizedArguments = $nativeArguments.Clone()
$oversizedArguments.Scenarios = $newScenes + $newScenes
$callsBeforeRejection = $global:nativeVisualCalls
Assert-Throws { & $nativeScript @oversizedArguments -OutputDirectory $oversizedDirectory } 'at most 32'
Assert-True (!(Test-Path -LiteralPath $oversizedDirectory)) 'Oversized native sets are rejected before bridge calls or output creation.'
Assert-True ($global:nativeVisualCalls -eq $callsBeforeRejection) 'The 32-scene limit rejects the request before any bridge command.'
foreach ($invalidTime in @(10.1, -0.1, [double]::NaN, [double]::PositiveInfinity)) {
    $invalidDirectory = Join-Path $root ('native-reject-time-' + [Guid]::NewGuid().ToString('N'))
    Assert-Throws { & $nativeScript @nativeArguments -PreviewTime $invalidTime -OutputDirectory $invalidDirectory } 'validate argument|0\.\.10'
    Assert-True ($global:nativeVisualCalls -eq $callsBeforeRejection -and !(Test-Path -LiteralPath $invalidDirectory)) 'Nonfinite and out-of-range fixture times never reach the bridge or create output.'
}
foreach ($case in @('protocol', 'source', 'unverified')) {
    $global:nativeVisualFailure = $case
    $rejectedDirectory = Join-Path $root "native-reject-$case"
    Assert-Throws { & $nativeScript @nativeArguments -OutputDirectory $rejectedDirectory } 'protocol|enabled Agent Control session|source identity or native coverage mismatch'
    Assert-True (@(Get-ChildItem -LiteralPath $rejectedDirectory -Filter '*.png').Count -eq 0) 'Unsupported protocols and mismatched source identity never export native screenshots.'
}
foreach ($variable in @('nativeVisualProfile','nativeVisualFailure','nativeVisualJob','nativeVisualDirectory','nativeVisualCalls')) { Remove-Variable -Name $variable -Scope Global -ErrorAction SilentlyContinue }
Remove-Variable visualFailure -Scope Global -ErrorAction SilentlyContinue
Remove-Variable visualTestProcesses -Scope Global -ErrorAction SilentlyContinue
Write-Host "$checks visual workflow assertions passed; disposable fixtures: $root"
