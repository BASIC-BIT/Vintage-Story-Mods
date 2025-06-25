# Update Project References Script
# This script updates VS DLL references to work with both local development and CI

param(
    [switch]$DryRun,
    [switch]$Revert
)

$projectFiles = @(
    "mods-dll\thebasics\thebasics.csproj",
    "mods-dll\litchimneys\litchimneys.csproj",
    "mods\thaumstory\thaumstory.csproj",
    "mods\makersmark\makersmark.csproj",
    "mods\DummyTranslocator\DummyTranslocator.csproj",
    "mods\forensicstory\forensicstory.csproj",
    "mods\autorun\autorun.csproj"
)

# Define the path mappings
$pathMappings = @{
    "VintagestoryAPI.dll" = @{
        Original = '$(VINTAGE_STORY)\VintagestoryAPI.dll'
        CI = '$(VINTAGE_STORY)\core\VintagestoryAPI.dll'
    }
    "VintagestoryLib.dll" = @{
        Original = '$(VINTAGE_STORY)\VintagestoryLib.dll'
        CI = '$(VINTAGE_STORY)\core\VintagestoryLib.dll'
    }
    "VSSurvivalMod.dll" = @{
        Original = '$(VINTAGE_STORY)\Mods\VSSurvivalMod.dll'
        CI = '$(VINTAGE_STORY)\mods\VSSurvivalMod.dll'
    }
    "VSEssentials.dll" = @{
        Original = '$(VINTAGE_STORY)\Mods\VSEssentials.dll'
        CI = '$(VINTAGE_STORY)\mods\VSEssentials.dll'
    }
    "VSCreativeMod.dll" = @{
        Original = '$(VINTAGE_STORY)\Mods\VSCreativeMod.dll'
        CI = '$(VINTAGE_STORY)\mods\VSCreativeMod.dll'
    }
    "cairo-sharp.dll" = @{
        Original = '$(VINTAGE_STORY)\Lib\cairo-sharp.dll'
        CI = '$(VINTAGE_STORY)\lib\cairo-sharp.dll'
    }
    "protobuf-net.dll" = @{
        Original = '$(VINTAGE_STORY)\Lib\protobuf-net.dll'
        CI = '$(VINTAGE_STORY)\lib\protobuf-net.dll'
    }
    "0Harmony.dll" = @{
        Original = '$(VINTAGE_STORY)/Lib/0Harmony.dll'  # Note: some projects use forward slashes
        CI = '$(VINTAGE_STORY)\lib\0Harmony.dll'
    }
}

function Update-ProjectFile {
    param(
        [string]$ProjectFile,
        [bool]$RevertMode = $false
    )
    
    if (-not (Test-Path $ProjectFile)) {
        Write-Host "Warning: Project file not found: $ProjectFile" -ForegroundColor Yellow
        return
    }
    
    Write-Host "Processing: $ProjectFile" -ForegroundColor Cyan
    
    $content = Get-Content $ProjectFile -Raw
    $originalContent = $content
    $changes = 0
    
    foreach ($dll in $pathMappings.Keys) {
        $mapping = $pathMappings[$dll]
        
        if ($RevertMode) {
            # Revert CI paths back to original
            if ($content -match [regex]::Escape($mapping.CI)) {
                $content = $content -replace [regex]::Escape($mapping.CI), $mapping.Original
                $changes++
                Write-Host "  ✓ Reverted: $dll" -ForegroundColor Green
            }
        } else {
            # Update to CI-friendly paths
            if ($content -match [regex]::Escape($mapping.Original)) {
                $content = $content -replace [regex]::Escape($mapping.Original), $mapping.CI
                $changes++
                Write-Host "  ✓ Updated: $dll" -ForegroundColor Green
            }
        }
    }
    
    if ($changes -eq 0) {
        Write-Host "  No changes needed" -ForegroundColor Gray
        return
    }
    
    if ($DryRun) {
        Write-Host "  DRY RUN: Would make $changes changes" -ForegroundColor Yellow
        return
    }
    
    # Write the updated content
    $content | Out-File -FilePath $ProjectFile -Encoding UTF8 -NoNewline
    Write-Host "  Applied $changes changes" -ForegroundColor Green
}

Write-Host "VS Project Reference Updater" -ForegroundColor Magenta
Write-Host "=============================" -ForegroundColor Magenta

if ($DryRun) {
    Write-Host "DRY RUN MODE - No files will be modified" -ForegroundColor Yellow
}

if ($Revert) {
    Write-Host "REVERT MODE - Changing CI paths back to original" -ForegroundColor Yellow
}

foreach ($project in $projectFiles) {
    Update-ProjectFile -ProjectFile $project -RevertMode $Revert
}

Write-Host ""
if ($DryRun) {
    Write-Host "Dry run complete. Run without -DryRun to apply changes." -ForegroundColor Yellow
} elseif ($Revert) {
    Write-Host "Revert complete. Project files restored to original paths." -ForegroundColor Green
} else {
    Write-Host "Update complete. Project files now use CI-friendly paths." -ForegroundColor Green
    Write-Host "Note: Local development will still work if your VINTAGE_STORY env var points to the game folder." -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Usage examples:" -ForegroundColor Gray
Write-Host "  .\update-project-references.ps1 -DryRun     # Preview changes" -ForegroundColor Gray
Write-Host "  .\update-project-references.ps1             # Apply changes" -ForegroundColor Gray
Write-Host "  .\update-project-references.ps1 -Revert     # Revert changes" -ForegroundColor Gray 