# Upload VS Dependencies Script
# This script copies VS DLLs to a local clone of the vs-build-dependencies repository

param(
    [string]$VsInstallPath = $env:VINTAGE_STORY,
    [string]$TargetRepo = "git@github.com:BASIC-BIT/vs-build-dependencies.git",
    [string]$WorkDir = ".\temp-vs-deps",
    [string]$VsVersion = "1.20.12"  # Update this when VS version changes
)

# Validate VS installation path
if (-not $VsInstallPath) {
    Write-Host "Error: VINTAGE_STORY environment variable not set and no path provided" -ForegroundColor Red
    Write-Host "Usage: .\upload-vs-dependencies.ps1 [-VsInstallPath 'C:\Path\To\VintageStory']"
    exit 1
}

if (-not (Test-Path $VsInstallPath)) {
    Write-Host "Error: Vintage Story installation not found at: $VsInstallPath" -ForegroundColor Red
    exit 1
}

Write-Host "Using VS installation: $VsInstallPath" -ForegroundColor Green

# Define required DLLs based on your project references
$RequiredDlls = @{
    # Core DLLs
    "VintagestoryAPI.dll" = "$VsInstallPath\VintagestoryAPI.dll"
    "VintagestoryLib.dll" = "$VsInstallPath\VintagestoryLib.dll"
    
    # Mod DLLs
    "VSSurvivalMod.dll" = "$VsInstallPath\Mods\VSSurvivalMod.dll"
    "VSEssentials.dll" = "$VsInstallPath\Mods\VSEssentials.dll"
    "VSCreativeMod.dll" = "$VsInstallPath\Mods\VSCreativeMod.dll"
    
    # Library DLLs
    "cairo-sharp.dll" = "$VsInstallPath\Lib\cairo-sharp.dll"
    "protobuf-net.dll" = "$VsInstallPath\Lib\protobuf-net.dll"
    "0Harmony.dll" = "$VsInstallPath\Lib\0Harmony.dll"
}

# Validate all DLLs exist
Write-Host "Validating VS DLLs..." -ForegroundColor Yellow
$missingDlls = @()
foreach ($dll in $RequiredDlls.Keys) {
    $path = $RequiredDlls[$dll]
    if (-not (Test-Path $path)) {
        $missingDlls += "$dll at $path"
    } else {
        Write-Host "Found: $dll" -ForegroundColor Green
    }
}

if ($missingDlls.Count -gt 0) {
    Write-Host "Error: Missing DLLs:" -ForegroundColor Red
    $missingDlls | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

# Clean up any existing work directory
if (Test-Path $WorkDir) {
    Write-Host "Cleaning up existing work directory..." -ForegroundColor Yellow
    Remove-Item -Path $WorkDir -Recurse -Force
}

# Clone the repository
Write-Host "Cloning vs-build-dependencies repository..." -ForegroundColor Yellow
try {
    git clone $TargetRepo $WorkDir
    if ($LASTEXITCODE -ne 0) {
        throw "Git clone failed"
    }
} catch {
    Write-Host "Error cloning repository: $_" -ForegroundColor Red
    Write-Host "Make sure you have SSH access to the repository and git is in PATH" -ForegroundColor Yellow
    exit 1
}

# Navigate to the work directory
Push-Location $WorkDir

try {
    # Create version directory
    $versionDir = $VsVersion
    if (-not (Test-Path $versionDir)) {
        New-Item -ItemType Directory -Path $versionDir | Out-Null
        Write-Host "Created version directory: $versionDir" -ForegroundColor Green
    }
    
    # Create subdirectories
    $coreDir = "$versionDir\core"
    $modsDir = "$versionDir\mods"
    $libDir = "$versionDir\lib"
    
    @($coreDir, $modsDir, $libDir) | ForEach-Object {
        if (-not (Test-Path $_)) {
            New-Item -ItemType Directory -Path $_ | Out-Null
        }
    }
    
    # Copy DLLs to appropriate directories
    Write-Host "Copying DLLs..." -ForegroundColor Yellow
    
    # Core DLLs
    Copy-Item $RequiredDlls["VintagestoryAPI.dll"] "$coreDir\" -Force
    Copy-Item $RequiredDlls["VintagestoryLib.dll"] "$coreDir\" -Force
    Write-Host "Copied core DLLs" -ForegroundColor Green
    
    # Mod DLLs
    Copy-Item $RequiredDlls["VSSurvivalMod.dll"] "$modsDir\" -Force
    Copy-Item $RequiredDlls["VSEssentials.dll"] "$modsDir\" -Force
    Copy-Item $RequiredDlls["VSCreativeMod.dll"] "$modsDir\" -Force
    Write-Host "Copied mod DLLs" -ForegroundColor Green
    
    # Library DLLs
    Copy-Item $RequiredDlls["cairo-sharp.dll"] "$libDir\" -Force
    Copy-Item $RequiredDlls["protobuf-net.dll"] "$libDir\" -Force
    Copy-Item $RequiredDlls["0Harmony.dll"] "$libDir\" -Force
    Write-Host "Copied library DLLs" -ForegroundColor Green
    
    # Create README for the version
    $readmeContent = @"
# Vintage Story Build Dependencies - Version $VsVersion

This directory contains the necessary DLLs for building Vintage Story mods against version $VsVersion.

## Directory Structure

- core/ - Core game DLLs (VintagestoryAPI.dll, VintagestoryLib.dll)
- mods/ - Base mod DLLs (VSSurvivalMod.dll, VSEssentials.dll, VSCreativeMod.dll)  
- lib/ - Library DLLs (cairo-sharp.dll, protobuf-net.dll, 0Harmony.dll)

## Legal Notice

These DLLs are proprietary to Anego Studios and are provided solely for the purpose of mod development.
They should not be redistributed outside of this controlled environment.

## Usage in CI

Download these DLLs to your build environment and reference them in your project files.

Generated on: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
From VS installation: $VsInstallPath
"@
    
    $readmeContent | Out-File -FilePath "$versionDir\README.md" -Encoding UTF8
    
    # Create a version info file
    $versionInfo = @{
        "version" = $VsVersion
        "uploadDate" = (Get-Date -Format "yyyy-MM-ddTHH:mm:ssZ")
        "sourceInstallation" = $VsInstallPath
        "dlls" = @{
            "core" = @("VintagestoryAPI.dll", "VintagestoryLib.dll")
            "mods" = @("VSSurvivalMod.dll", "VSEssentials.dll", "VSCreativeMod.dll")
            "lib" = @("cairo-sharp.dll", "protobuf-net.dll", "0Harmony.dll")
        }
    } | ConvertTo-Json -Depth 3
    
    $versionInfo | Out-File -FilePath "$versionDir\version-info.json" -Encoding UTF8
    
    # Check if there are any changes to commit
    git add .
    $gitStatus = git status --porcelain
    
    if (-not $gitStatus) {
        Write-Host "No changes detected - DLLs are already up to date" -ForegroundColor Yellow
    } else {
        # Commit and push
        Write-Host "Committing changes..." -ForegroundColor Yellow
        git commit -m "Upload VS $VsVersion dependencies"
        
        Write-Host "Pushing to repository..." -ForegroundColor Yellow
        git push origin main
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Successfully uploaded VS dependencies!" -ForegroundColor Green
            Write-Host "Repository updated with version: $VsVersion" -ForegroundColor Green
        } else {
            throw "Git push failed"
        }
    }
    
} catch {
    Write-Host "Error during upload: $_" -ForegroundColor Red
    exit 1
} finally {
    # Return to original directory
    Pop-Location
    
    # Clean up work directory
    if (Test-Path $WorkDir) {
        Write-Host "Cleaning up work directory..." -ForegroundColor Yellow
        Remove-Item -Path $WorkDir -Recurse -Force
    }
}

Write-Host "Upload complete!" -ForegroundColor Green