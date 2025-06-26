# Vintage Story Mods - Build System

This repository uses a custom build system to handle Vintage Story DLL dependencies for both local development and CI/CD.

## Overview

The build process uses a separate repository ([vs-build-dependencies](https://github.com/BASIC-BIT/vs-build-dependencies)) to store the required Vintage Story DLLs, avoiding the need to commit proprietary binaries to this repository.

## Required DLLs

The build system requires these VS DLLs:

### Core DLLs
- `VintagestoryAPI.dll` - Core game API
- `VintagestoryLib.dll` - Game engine access

### Mod DLLs
- `VSSurvivalMod.dll` - Survival mode integration
- `VSEssentials.dll` - Essential game systems
- `VSCreativeMod.dll` - Creative mode support

### Library DLLs
- `cairo-sharp.dll` - Cairo graphics library
- `protobuf-net.dll` - Protocol buffers
- `0Harmony.dll` - Harmony patching library

## Local Development Setup

1. **Install Vintage Story** and set the `VINTAGE_STORY` environment variable to point to your installation directory

2. **Verify your setup:**
   ```powershell
   # Check environment variable
   echo $env:VINTAGE_STORY
   
   # Verify DLLs exist
   Test-Path "$env:VINTAGE_STORY\VintagestoryAPI.dll"
   ```

3. **Build locally:**
   ```powershell
   dotnet restore Vintage-Story-Mods.sln
   dotnet build Vintage-Story-Mods.sln --configuration Release
   ```

## CI/CD Setup

### Uploading Dependencies (Maintainers Only)

When VS updates or you need to refresh dependencies:

1. **Run the upload script:**
   ```powershell
   .\scripts\upload-vs-dependencies.ps1
   ```

2. **Update the VS_VERSION in GitHub Actions** (`.github/workflows/build.yml`) if needed

3. **Optional: Update project files** to use CI-friendly paths:
   ```powershell
   # Preview changes
   .\scripts\update-project-references.ps1 -DryRun
   
   # Apply changes
   .\scripts\update-project-references.ps1
   ```

### GitHub Actions Workflow

The CI workflow:

1. **Downloads VS dependencies** from the vs-build-dependencies repository
2. **Caches dependencies** to speed up subsequent builds
3. **Sets up the build environment** with the correct VINTAGE_STORY path
4. **Builds all mods** in the solution
5. **Packages mods** and uploads as artifacts

## Scripts

### `scripts/upload-vs-dependencies.ps1`
Uploads VS DLLs from your local installation to the vs-build-dependencies repository.

**Parameters:**
- `-VsInstallPath` - Override VS installation path
- `-VsVersion` - Version string for the upload (default: "1.20.12")

**Example:**
```powershell
.\scripts\upload-vs-dependencies.ps1 -VsVersion "1.20.13"
```

### `scripts/update-project-references.ps1`
Updates project files to use CI-compatible DLL paths.

**Parameters:**
- `-DryRun` - Preview changes without applying them
- `-Revert` - Revert to original paths

**Examples:**
```powershell
# Preview what would change
.\scripts\update-project-references.ps1 -DryRun

# Apply CI-friendly paths
.\scripts\update-project-references.ps1

# Revert back to original paths
.\scripts\update-project-references.ps1 -Revert
```

## Troubleshooting

### Local Build Issues

**Error: "Could not load file or assembly 'VintagestoryAPI'"**
- Verify `VINTAGE_STORY` environment variable is set correctly
- Check that VS DLLs exist in the specified path
- Restart your IDE/terminal after setting environment variables

**Error: "The reference assemblies for framework .NETFramework,Version=v4.8 were not found"**
- Install .NET Framework 4.8 Developer Pack
- Some legacy mods target .NET Framework 4.8

### CI Build Issues

**Error: "Version X.X.X not found in dependencies repository"**
- Run the upload script to add the required version
- Update `VS_VERSION` in the GitHub workflow file

**Error: "Missing required DLLs"**
- Check that all DLLs were uploaded correctly
- Verify the vs-build-dependencies repository structure

## Repository Structure

```
vs-build-dependencies/
├── 1.20.12/           # Version directory
│   ├── core/          # Core VS DLLs
│   ├── mods/          # Mod DLLs  
│   ├── lib/           # Library DLLs
│   ├── README.md      # Version-specific info
│   └── version-info.json
├── 1.20.13/           # Future versions...
└── README.md
```

## Version Management

When Vintage Story updates:

1. **Upload new dependencies:**
   ```powershell
   .\scripts\upload-vs-dependencies.ps1 -VsVersion "1.20.13"
   ```

2. **Update workflow file** (`.github/workflows/build.yml`):
   ```yaml
   env:
     VS_VERSION: "1.20.13"  # Update this line
   ```

3. **Test the build** with the new version

4. **Update this documentation** if needed

## Legal Notice

The VS DLLs in the vs-build-dependencies repository are proprietary to Anego Studios and are used solely for mod development purposes. They should not be redistributed outside of this controlled environment. 