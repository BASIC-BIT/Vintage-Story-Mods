param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")
$renderer = Join-Path $solutionRoot ".opencode\skills\vintage-story-model-renderer\scripts\render_vintage_story_model.py"
$manifestDirectory = Join-Path $projectRoot "model-render"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "output\model-renders"
}

foreach ($manifest in Get-ChildItem -LiteralPath $manifestDirectory -Filter "*.json" | Sort-Object Name) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($manifest.Name)
    $target = Join-Path $OutputDirectory $name
    python $renderer --manifest $manifest.FullName --output-dir $target
    if ($LASTEXITCODE -ne 0) {
        throw "Model rendering failed for $($manifest.FullName)"
    }
}

Write-Output "Rendered Flywheel model evidence to $OutputDirectory"
