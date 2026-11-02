[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedSourceTreeHash,
    [Parameter(Mandatory)][string]$Vsctl,
    [Parameter(Mandatory)][string]$DataPath,
    [string]$PipeName = 'vintage-story-agentcontrol-profile2',
    [string]$Scenario = 'wizard-hub',
    [ValidateRange(0.25, 10)][double]$DurationSeconds = 2,
    [ValidateRange(1, 24)][int]$SamplesPerSecond = 4,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Ffmpeg = 'ffmpeg',
    [string]$Ffprobe = 'ffprobe'
)

$ErrorActionPreference = 'Stop'
$frameCount = [int][Math]::Floor($DurationSeconds * $SamplesPerSecond) + 1
if ($frameCount -gt 32) { throw 'A clip may use at most 32 named poses per Agent Control session.' }
$encoder = (Get-Command $Ffmpeg -ErrorAction Stop).Source
$probeTool = (Get-Command $Ffprobe -ErrorAction Stop).Source
$profile = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $DataPath).Path)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a new clip directory; evidence is never overwritten.' }
New-Item -ItemType Directory -Path $output | Out-Null
$frames = @()
$identity = $null
$width = 0
$height = 0
for ($index = 0; $index -lt $frameCount; $index++) {
    $previewTime = $index / [double]$SamplesPerSecond
    $captureName = 'capture-' + $index.ToString('000')
    $captureDirectory = Join-Path $output $captureName
    & (Join-Path $PSScriptRoot 'capture-native-wizard.ps1') -ExpectedSourceTreeHash $ExpectedSourceTreeHash -Vsctl $Vsctl -DataPath $profile `
        -PipeName $PipeName -Scenarios $Scenario -PreviewTime $previewTime -OutputDirectory $captureDirectory
    $set = Get-Content -LiteralPath (Join-Path $captureDirectory 'captures.json') -Raw | ConvertFrom-Json
    if ($set.sourceTreeHash -ne $ExpectedSourceTreeHash -or $set.coverage -ne 'native-client' -or
        [IO.Path]::GetFullPath($set.dataPath) -ne $profile -or $set.pipeName -ne $PipeName -or
        @($set.captures).Count -ne 1 -or $set.captures[0].scenario -ne $Scenario) { throw 'Capture set source, profile, pipe or scene differs.' }
    $manifestPath = Join-Path $captureDirectory "$Scenario.json"
    $imagePath = Join-Path $captureDirectory "$Scenario.png"
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or $manifest.scenario -ne $Scenario -or $manifest.previewTime -ne $previewTime -or
        $manifest.sourceTreeHash -ne $ExpectedSourceTreeHash -or $manifest.coverage -ne 'native-client' -or
        -not $manifest.environment.sourceIdentityVerified -or $manifest.environment.modBinaryHash -notmatch '^[0-9a-f]{64}$' -or
        [string]::IsNullOrWhiteSpace($manifest.environment.gameVersion) -or $null -eq $manifest.environmentIdentity) {
        throw 'Native pose source, time, scene or environment identity differs.'
    }
    $currentIdentity = @{
        environmentIdentity = $manifest.environmentIdentity; gameVersion = $manifest.environment.gameVersion
        modBinaryHash = $manifest.environment.modBinaryHash; width = $manifest.width; height = $manifest.height; scale = $manifest.scale
        fixture = $manifest.fixture
    } | ConvertTo-Json -Depth 100 -Compress
    if ($index -eq 0) {
        $identity = $currentIdentity
        $width = [int]$manifest.width
        $height = [int]$manifest.height
        if ($width -lt 1 -or $height -lt 1 -or $manifest.scale -le 0 -or
            $manifest.environmentIdentity.viewport.width -ne $width -or $manifest.environmentIdentity.viewport.height -ne $height -or
            $manifest.environmentIdentity.scale -ne $manifest.scale) { throw 'Native pose dimensions or GUI scale are inconsistent.' }
    } elseif ($currentIdentity -cne $identity) { throw 'Scene fixture, viewport, scale or render environment changed between poses.' }
    $imageBytes = [IO.File]::ReadAllBytes($imagePath)
    if ($imageBytes.Length -lt 24 -or [Convert]::ToHexString($imageBytes[0..7]) -ne '89504E470D0A1A0A') { throw 'Native pose is not a PNG.' }
    $imageWidth = [uint32]$imageBytes[16] * 16777216 + [uint32]$imageBytes[17] * 65536 + [uint32]$imageBytes[18] * 256 + $imageBytes[19]
    $imageHeight = [uint32]$imageBytes[20] * 16777216 + [uint32]$imageBytes[21] * 65536 + [uint32]$imageBytes[22] * 256 + $imageBytes[23]
    if ($imageWidth -ne $width -or $imageHeight -ne $height) { throw 'Native PNG dimensions differ from its manifest.' }
    $imageHash = (Get-FileHash -LiteralPath $imagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    $frameName = 'frame-' + $index.ToString('000') + '.png'
    Copy-Item -LiteralPath $imagePath -Destination (Join-Path $output $frameName)
    if ((Get-FileHash -LiteralPath (Join-Path $output $frameName) -Algorithm SHA256).Hash.ToLowerInvariant() -ne $imageHash) { throw 'Copied pose PNG hash differs.' }
    $frames += [ordered]@{
        file = $frameName; previewTime = $previewTime; imageSha256 = $imageHash
        captureManifest = "$captureName/$Scenario.json"; manifestSha256 = (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$videoName = "$Scenario-sampled.mp4"
$video = Join-Path $output $videoName
& $encoder -hide_banner -loglevel error -n -framerate $SamplesPerSecond -i (Join-Path $output 'frame-%03d.png') `
    -vf 'pad=ceil(iw/2)*2:ceil(ih/2)*2' -c:v libx264 -crf 18 -pix_fmt yuv420p -movflags +faststart $video
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $video -PathType Leaf)) { throw 'Native clip encoding failed.' }
$probeRaw = & $probeTool -v error -select_streams v:0 -count_frames -show_entries 'stream=codec_name,width,height,nb_read_frames,r_frame_rate:format=duration' -of json $video
if ($LASTEXITCODE -ne 0) { throw 'Native clip inspection failed.' }
$probe = ($probeRaw -join "`n") | ConvertFrom-Json
$encodedWidth = [int][Math]::Ceiling($width / 2.0) * 2
$encodedHeight = [int][Math]::Ceiling($height / 2.0) * 2
$stream = @($probe.streams)[0]
$expectedDuration = $frameCount / [double]$SamplesPerSecond
$duration = [double]::Parse([string]$probe.format.duration, [Globalization.CultureInfo]::InvariantCulture)
if (@($probe.streams).Count -ne 1 -or $stream.codec_name -ne 'h264' -or $stream.width -ne $encodedWidth -or $stream.height -ne $encodedHeight -or
    [int]$stream.nb_read_frames -ne $frameCount -or [Math]::Abs($duration - $expectedDuration) -gt 0.05) {
    throw 'Encoded clip codec, dimensions, frame count or duration differs.'
}
$probe | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'ffprobe.json') -Encoding utf8
[ordered]@{
    schema = 1; sourceTreeHash = $ExpectedSourceTreeHash; coverage = 'native-client'; dataPath = $profile; pipeName = $PipeName; scenario = $Scenario
    sampledPosesPerSecond = $SamplesPerSecond; requestedPoseDurationSeconds = $DurationSeconds; videoDurationSeconds = $duration
    width = $width; height = $height; encodedWidth = $encodedWidth; encodedHeight = $encodedHeight
    frames = $frames; video = $videoName; videoSha256 = (Get-FileHash -LiteralPath $video -Algorithm SHA256).Hash.ToLowerInvariant()
    limitation = 'Named native poses assembled into a clip; this is not a real-time recording or smoothness, frame-rate or gesture test.'
} | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'clip.json') -Encoding utf8
Write-Host "Native sampled-pose clip: $video"
