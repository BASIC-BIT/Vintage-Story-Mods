[CmdletBinding()]
param(
    [string]$GamePath = $env:VINTAGE_STORY,
    [string]$Output,
    [string]$Scenario = 'all',
    [double]$Scale = 1,
    [int]$Width = 1600,
    [int]$Height = 1000,
    [string]$Baseline,
    [string]$DotNet = $(if ($env:DOTNET_EXE) { $env:DOTNET_EXE } else { 'dotnet' }),
    [switch]$Wizard,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
if (-not $GamePath) { throw 'Set VINTAGE_STORY or pass -GamePath with a complete game installation.' }
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
foreach ($required in @('VintagestoryAPI.dll', 'VintagestoryLib.dll', 'Lib/cairo-sharp.dll', 'assets/game/lang/en.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $GamePath $required))) { throw "Missing game dependency: $required" }
}
if (-not $Output) { $Output = Join-Path $repo '.superpowers/sdd/gui-preview/output' }
$Output = [IO.Path]::GetFullPath($Output)
$assets = Join-Path $repo 'mods-dll/thebasics/assets'
$previousGame = $env:VINTAGE_STORY
$previousAssets = $env:THEBASICS_GUI_ASSETS
$previousOutput = $env:THEBASICS_GUI_OUTPUT
try {
    $env:VINTAGE_STORY = $GamePath
    if ($Test) {
        $env:THEBASICS_GUI_ASSETS = $assets
        $env:THEBASICS_GUI_OUTPUT = $Output
        & $DotNet test (Join-Path $repo 'mods-dll/thebasics.Tests/thebasics.Tests.csproj') '-p:SkipPostBuildPackage=true' --filter 'FullyQualifiedName~GuiPreview'
    } else {
        $sourceTreeHash = & (Join-Path $repo 'scripts/gui-source-identity.ps1') -Repo $repo
        $project = Join-Path $repo 'tools/GuiPreview/GuiPreview.csproj'
        & $DotNet build $project '-p:SkipPostBuildPackage=true' "-p:TheBasicsSourceTreeHash=$sourceTreeHash" --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw 'GUI preview build failed.' }
        $scales = if ($Wizard) { @(1, 1.25) } else { @($Scale) }
        if ($Wizard -and $Scenario -eq 'all') { $Scenario = 'wizard-all' }
        foreach ($captureScale in $scales) {
            $captureOutput = if ($Wizard) { Join-Path $Output ('scale-' + $captureScale.ToString([Globalization.CultureInfo]::InvariantCulture)) } else { $Output }
            $arguments = @('render', '--game', $GamePath, '--assets', $assets, '--output', $captureOutput, '--scenario', $Scenario,
                '--scale', $captureScale.ToString([Globalization.CultureInfo]::InvariantCulture), '--width', "$Width", '--height', "$Height", '--source-tree-hash', $sourceTreeHash)
            if ($Baseline) {
                $captureBaseline = if ($Wizard) { Join-Path $Baseline ('scale-' + $captureScale.ToString([Globalization.CultureInfo]::InvariantCulture)) } else { $Baseline }
                $arguments += @('--baseline', [IO.Path]::GetFullPath($captureBaseline))
            }
            & $DotNet (Join-Path $repo 'tools/GuiPreview/bin/Debug/net10.0/TheBasics.GuiPreview.dll') @arguments
            if ($LASTEXITCODE -ne 0) { throw "GUI preview failed (exit $LASTEXITCODE)." }
        }
    }
    if ($LASTEXITCODE -ne 0) { throw "GUI preview failed (exit $LASTEXITCODE)." }
} finally {
    $env:VINTAGE_STORY = $previousGame
    $env:THEBASICS_GUI_ASSETS = $previousAssets
    $env:THEBASICS_GUI_OUTPUT = $previousOutput
}
