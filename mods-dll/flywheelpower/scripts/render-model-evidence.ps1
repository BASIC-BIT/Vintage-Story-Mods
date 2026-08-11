param(
    [string]$OutputDirectory = "",
    [string]$AssetsRoot = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")
$renderer = Join-Path $solutionRoot ".opencode\skills\vintage-story-model-renderer\scripts\vintage_story_model_renderer"
$manifestDirectory = Join-Path $projectRoot "model-render"
$previewGenerator = Join-Path $projectRoot "scripts\generate-preview-shapes.py"
$componentGenerator = Join-Path $projectRoot "scripts\generate-component-shapes.py"
$representationGenerator = Join-Path $projectRoot "scripts\generate-component-representation-manifests.py"
$materialGenerator = Join-Path $projectRoot "scripts\generate-material-content.py"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot "output\model-renders"
}

if ([string]::IsNullOrWhiteSpace($AssetsRoot) -and -not [string]::IsNullOrWhiteSpace($env:VINTAGE_STORY)) {
    $candidate = Join-Path $env:VINTAGE_STORY "assets"
    if (Test-Path -LiteralPath $candidate -PathType Container) {
        $AssetsRoot = $candidate
    }
}

if ([string]::IsNullOrWhiteSpace($AssetsRoot) -or -not (Test-Path -LiteralPath $AssetsRoot -PathType Container)) {
    throw "Provide -AssetsRoot or set VINTAGE_STORY to an installation containing the assets directory."
}
$assetsRootPath = (Resolve-Path -LiteralPath $AssetsRoot).Path

python $previewGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw "Inventory/held preview shapes are stale. Run python $previewGenerator before rendering."
}

python $componentGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw "Inventory/held component shapes are stale. Run python $componentGenerator before rendering."
}

python $representationGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw "Collectible representation manifests are stale. Run python $representationGenerator before rendering."
}

python $materialGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw "Generated material content is stale. Run python $materialGenerator before rendering."
}

foreach ($manifest in Get-ChildItem -LiteralPath $manifestDirectory -Filter "*.json" | Sort-Object Name) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($manifest.Name)
    $target = Join-Path $OutputDirectory $name
    python $renderer --manifest $manifest.FullName --output-dir $target --assets-root $assetsRootPath --fail-on-coplanar-overlap
    if ($LASTEXITCODE -ne 0) {
        throw "Model rendering failed for $($manifest.FullName)"
    }

    $metadataPath = Join-Path $target "render-metadata.json"
    $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json
    $unresolvedTextures = @($metadata.unresolvedTextures | Where-Object { $_ })
    if ($unresolvedTextures.Count -gt 0) {
        throw "Model rendering left unresolved textures for $($manifest.Name): $($unresolvedTextures -join ', ')"
    }
    if ($metadata.renderedImageCount -ne 24) {
        throw "Model rendering produced $($metadata.renderedImageCount) primary images for $($manifest.Name), expected 24."
    }
}

Write-Output "Rendered Flywheel model evidence to $OutputDirectory"
