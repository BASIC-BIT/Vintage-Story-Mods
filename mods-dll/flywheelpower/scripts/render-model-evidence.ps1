param(
    [string]$OutputDirectory = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")
$renderer = Join-Path $solutionRoot ".opencode\skills\vintage-story-model-renderer\scripts\vintage_story_model_renderer"
$manifestDirectory = Join-Path $projectRoot "model-render"
$previewGenerator = Join-Path $projectRoot "scripts\generate-preview-shapes.py"
$materialGenerator = Join-Path $projectRoot "scripts\generate-material-content.py"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "output\model-renders"
}

python $previewGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw "Inventory/held preview shapes are stale. Run python $previewGenerator before rendering."
}

python $materialGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw "Generated material content is stale. Run python $materialGenerator before rendering."
}

foreach ($manifest in Get-ChildItem -LiteralPath $manifestDirectory -Filter "*.json" | Sort-Object Name) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($manifest.Name)
    $target = Join-Path $OutputDirectory $name
    python $renderer --manifest $manifest.FullName --output-dir $target --fail-on-coplanar-overlap
    if ($LASTEXITCODE -ne 0) {
        throw "Model rendering failed for $($manifest.FullName)"
    }
}

Write-Output "Rendered Flywheel model evidence to $OutputDirectory"
