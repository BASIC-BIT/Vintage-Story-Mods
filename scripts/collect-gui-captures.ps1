[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BaselineRepo,
    [Parameter(Mandatory)][string]$CurrentRepo,
    [Parameter(Mandatory)][string]$Output,
    [Parameter(Mandatory)][string]$DependencyDirectory,
    [string]$DotNet = $(if ($env:DOTNET_EXE) { $env:DOTNET_EXE } else { 'dotnet' })
)
$ErrorActionPreference = 'Stop'
$Output = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $Output) { throw 'Use a new capture output directory.' }
$DependencyDirectory = [IO.Path]::GetFullPath($DependencyDirectory)
$game = Join-Path $DependencyDirectory 'game'
New-Item -ItemType Directory -Force -Path $DependencyDirectory | Out-Null
# Public, version-pinned official packages. The client contributes assets only; Windows supplies every runtime library.
$archives = @(
    @{ File = 'vs_server_win-x64_1.22.7.zip'; Hash = 'e72f27de387c53342a6aedafb181fdfb154816e263fec41c31823c9afd69c65c' },
    @{ File = 'vs_client_linux-x64_1.22.7.tar.gz'; Hash = '483cc6827a50c08c5c41ccd0262c6f1223c104e54ca7135c46605278d89f5de1' }
)
foreach ($archive in $archives) {
    $path = Join-Path $DependencyDirectory $archive.File
    if (-not (Test-Path -LiteralPath $path)) { Invoke-WebRequest -Uri ('https://cdn.vintagestory.at/gamefiles/stable/' + $archive.File) -OutFile $path }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $archive.Hash) { throw "Official package checksum mismatch: $($archive.File)" }
}
if (-not (Test-Path -LiteralPath $game)) {
    Expand-Archive -LiteralPath (Join-Path $DependencyDirectory $archives[0].File) -DestinationPath $game
    & tar -xzf (Join-Path $DependencyDirectory $archives[1].File) -C $game --strip-components=1 vintagestory/assets
    if ($LASTEXITCODE -ne 0) { throw 'Official client asset extraction failed.' }
}
foreach ($required in @('VintagestoryAPI.dll', 'VintagestoryLib.dll', 'Lib/libcairo-2.dll', 'Lib/libSkiaSharp.dll',
    'assets/game/lang/en.json', 'assets/game/textures/gui/backgrounds/soil.png', 'assets/game/fonts/Lora-Regular.ttf')) {
    if (-not (Test-Path -LiteralPath (Join-Path $game $required))) { throw "GUI dependency is missing: $required" }
}
$previousGame = $env:VINTAGE_STORY
try {
    $env:VINTAGE_STORY = $game
    foreach ($side in @(@{ Name = 'baseline'; Repo = $BaselineRepo }, @{ Name = 'current'; Repo = $CurrentRepo })) {
        $repo = (Resolve-Path -LiteralPath $side.Repo).Path
        $directory = Join-Path $Output $side.Name
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $sourceHash = & (Join-Path $PSScriptRoot 'gui-source-identity.ps1') -Repo $repo
        $captures = [Collections.Generic.List[object]]::new()
        $hasScenes = Test-Path -LiteralPath (Join-Path $repo 'mods-dll/thebasics/src/ModSystems/ChatUiSystem/SetupWizardCaptureScenes.cs')
        if ($hasScenes) {
            $project = Join-Path $repo 'tools/GuiPreview/GuiPreview.csproj'
            & $DotNet build $project '-p:SkipPostBuildPackage=true' "-p:TheBasicsSourceTreeHash=$sourceHash" --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "$($side.Name) GUI preview build failed." }
            foreach ($scale in @('1', '1.25')) {
                $scaleDirectory = Join-Path $directory ('scale-' + $scale)
                & $DotNet (Join-Path $repo 'tools/GuiPreview/bin/Debug/net10.0/TheBasics.GuiPreview.dll') render --game $game --assets (Join-Path $repo 'mods-dll/thebasics/assets') --output $scaleDirectory --scenario wizard-all --scale $scale --width 1600 --height 1000 --source-tree-hash $sourceHash
                if ($LASTEXITCODE -ne 0) { throw "$($side.Name) GUI render failed at scale $scale." }
                $index = Get-Content -LiteralPath (Join-Path $scaleDirectory 'captures.json') -Raw | ConvertFrom-Json
                foreach ($entry in $index.captures) {
                    $manifest = Get-Content -LiteralPath (Join-Path $scaleDirectory $entry.manifest) -Raw | ConvertFrom-Json
                    $scenario = 'scale-' + $scale + '/' + $entry.scenario
                    $manifest | Add-Member -NotePropertyName sceneId -NotePropertyValue $entry.scenario
                    $manifest.scenario = $scenario
                    $captures.Add(@{ scenario = $scenario; file = 'scale-' + $scale + '/' + $entry.file; manifest = $manifest })
                }
            }
        }
        # A pre-wizard base has no wizard screenshots. Current scenes are then reported as added.
        @{ schema = 1; sourceTreeHash = $sourceHash; coverage = 'layout-only'; captures = $captures.ToArray();
            reference = 'Regenerated source commit, not a human-approved screenshot baseline'; wizardScenesAvailable = $hasScenes } |
            ConvertTo-Json -Depth 100 | Set-Content -LiteralPath (Join-Path $directory 'captures.json') -Encoding utf8
    }
} finally { $env:VINTAGE_STORY = $previousGame }
