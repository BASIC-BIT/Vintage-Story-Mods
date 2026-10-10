param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedSourceTreeHash,
    [Parameter(Mandatory = $true)][string]$Vsctl,
    [Parameter(Mandatory = $true)][string]$DataPath,
    [string]$PipeName = 'vintage-story-agentcontrol-profile2',
    [string[]]$Scenarios,
    [ValidateRange(0, 10)][double]$PreviewTime = 0.75,
    [string]$OutputDirectory = (Join-Path (Split-Path -Parent $PSScriptRoot) ('.tmp\wizard-native-captures-' + [Guid]::NewGuid().ToString('N')))
)

$ErrorActionPreference = 'Stop'
$DataPath = (Resolve-Path -LiteralPath $DataPath).Path
$scenes = @(
    'wizard-hub', 'wizard-chat-default', 'wizard-chat-edited', 'wizard-chat-presentation', 'wizard-chat-language', 'wizard-chat-ranges', 'wizard-chat-tabs',
    'wizard-travel-default', 'wizard-travel-edited', 'wizard-notifications-default', 'wizard-notifications-edited', 'wizard-notifications-sleep',
    'wizard-review', 'wizard-error', 'wizard-restart-dedicated', 'wizard-restart-integrated'
)
if ($Scenarios) {
    foreach ($scene in $Scenarios) { if ($scene -notin $scenes) { throw "Unknown wizard capture scene: $scene" } }
    $scenes = @($Scenarios)
}
if (!(Test-Path -LiteralPath $Vsctl -PathType Leaf)) { throw "Existing vsctl executable missing: $Vsctl" }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new output directory, captures are never overwritten.' }
$captureRoot = [IO.Path]::GetFullPath((Join-Path $DataPath 'Screenshots\thebasics-setup'))
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot | Out-Null
$actionFile = Join-Path $outputRoot 'actions.json'

function Invoke-Vsctl([string]$Command, [object[]]$Actions) {
    $cliArgs = @($Command, '--pipe', $PipeName)
    if ($null -ne $Actions) {
        ConvertTo-Json -InputObject @($Actions) -Depth 12 | Set-Content -LiteralPath $actionFile -Encoding utf8
        $cliArgs += @('--file', $actionFile)
    }
    $raw = & $Vsctl @cliArgs
    if ($LASTEXITCODE -ne 0) { throw "vsctl $Command failed (exit $LASTEXITCODE)." }
    $response = ($raw -join "`n") | ConvertFrom-Json
    if (!$response.ok) { throw "vsctl $Command failed: $($response.error.message)" }
    if ($Command -eq 'execute' -and $response.result.status -ne 'completed') {
        throw "Agent Control batch $($response.result.status): $($response.result.error)"
    }
    return $response.result
}

$hello = Invoke-Vsctl 'hello' $null
if ($hello.protocolVersion -ne '1.0' -or !$hello.mutationGranted) {
    throw 'An enabled Agent Control session with its mutation grant is required.'
}
$extensions = Invoke-Vsctl 'extensions' $null
foreach ($operation in @('thebasics.setup.open', 'thebasics.setup.capture', 'thebasics.setup.poll')) {
    if ($operation -notin @($extensions.operation)) { throw "Capture operation unavailable: $operation" }
}
Set-Content -LiteralPath (Join-Path $outputRoot 'hello.json') -Value ($hello | ConvertTo-Json -Depth 8) -Encoding utf8
$captures = @()
foreach ($scene in $scenes) {
    # ponytail: capture must finish before its batch ends; use async extension completion if slow mesh loads become common.
    $receipt = Invoke-Vsctl 'execute' @(
        @{ type = 'extension.invoke'; operation = 'thebasics.setup.open'; arguments = @{} },
        @{ type = 'wait'; durationMs = 2000 },
        @{ type = 'extension.invoke'; operation = 'thebasics.setup.capture'; arguments = @{ scenario = $scene; previewTime = $PreviewTime } },
        @{ type = 'wait'; durationMs = 2000 }
    )
    $receipt | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath (Join-Path $outputRoot "$scene.receipt.json") -Encoding utf8
    $jobId = $receipt.actions[2].detail.jobId
    if ($jobId -notmatch '^[0-9a-f]{32}$') { throw "Capture returned an invalid job ID for $scene." }
    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    do {
        $poll = Invoke-Vsctl 'execute' @(
            @{ type = 'extension.invoke'; operation = 'thebasics.setup.poll'; arguments = @{ jobId = $jobId } }
        )
        $job = $poll.actions[0].detail
        if ($job.status -eq 'failed') { throw "Native capture failed for ${scene}: $($job.error)" }
        if ($job.status -eq 'completed') { break }
        if ([DateTime]::UtcNow -ge $deadline) { throw "Capture poll timed out for $scene." }
        Start-Sleep -Milliseconds 250
    } while ($true)
    $directory = [IO.Path]::GetFullPath($job.directory)
    if ($directory -ne [IO.Path]::GetFullPath((Join-Path $captureRoot $jobId))) {
        throw 'Capture directory does not match the fixed profile/job directory.'
    }
    $manifestPath = Join-Path $directory "$scene.json"
    $manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
    if ($manifest.sourceTreeHash -ne $ExpectedSourceTreeHash -or $manifest.coverage -ne 'native-client' -or !$manifest.environment.sourceIdentityVerified) {
        throw "Capture source identity or native coverage mismatch for $scene. Stage and relaunch the current stamped package."
    }
    if ($manifest.scenario -ne $scene -or $manifest.previewTime -ne $PreviewTime) { throw "Capture scene identity mismatch for $scene." }
    Copy-Item -LiteralPath (Join-Path $directory "$scene.png") -Destination (Join-Path $outputRoot "$scene.png")
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $outputRoot "$scene.json")
    $captures += @{ scenario = $scene; file = "$scene.png"; manifest = "$scene.json" }
    Write-Host "$scene captured ($($manifest.width)x$($manifest.height), scale $($manifest.scale))."
}
@{ schema = 1; sourceTreeHash = $ExpectedSourceTreeHash; coverage = 'native-client'; dataPath = $DataPath; pipeName = $PipeName; captures = $captures } |
    ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $outputRoot 'captures.json') -Encoding utf8
Write-Host "Native capture set: $outputRoot"
