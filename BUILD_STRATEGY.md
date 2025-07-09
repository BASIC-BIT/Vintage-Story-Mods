# Build Strategy for Vintage Story Mods

## Overview

This document outlines the simplified, consistent build strategy implemented to resolve output directory confusion and eliminate complex path handling.

## The Problem We Solved

Previously, we had inconsistent output directories across different build scenarios:
- CI/CD pipeline: `dotnet build` → various complex locations
- Local `build-and-package.ps1`: Custom output to `solution/output/net7.0/`  
- `package.ps1`: Looking in multiple possible locations
- Project PostBuild: Running packaging with unclear DLL location

This led to:
- Silent packaging failures
- Complex multi-location search logic
- Nested directory structures (e.g., `output/net7.0/net7.0/`)
- Build verification not catching actual issues

## The Solution: Simple & Consistent

**ONE RULE: Everything uses standard MSBuild output directories**

### Build Output Locations

All builds now output to the standard MSBuild location:
```
mods-dll/thebasics/bin/Release/net7.0/thebasics.dll
mods-dll/thebasics/bin/Release/net7.0/thebasics.pdb
```

### Component Responsibilities

#### 1. Project File (`thebasics.csproj`)
- **Debug**: Outputs to `bin/Debug/`
- **Release**: Outputs to `bin/Release/`
- **PostBuild**: Calls `package.ps1` (only in local development)
- **No custom output paths** - uses MSBuild defaults

#### 2. Package Script (`package.ps1`)
- **Input**: Reads from `bin/Release/net7.0/thebasics.dll`
- **Output**: Creates `thebasics.zip` in project root
- **No path searching** - expects DLL in one specific location
- **Fails fast** if DLL not found

#### 3. Build-and-Package Script (`build-and-package.ps1`)
- **Purpose**: Local development convenience script
- **Action**: `dotnet build --configuration Release` + calls `package.ps1`
- **Uses standard MSBuild** - no custom output paths

#### 4. CI/CD Workflow (`.github/workflows/build.yml`)
- **Build**: `dotnet build --configuration Release` (standard)
- **Verification**: Checks DLL exists at expected location
- **Packaging**: Calls `package.ps1` script
- **Failure**: Hard fails if build verification fails

## Build Flows

### Local Development
```
Developer runs: .\scripts\build-and-package.ps1
├── Cleans bin/ directory
├── dotnet build --configuration Release
│   └── Outputs to bin/Release/net7.0/
├── Calls package.ps1
│   ├── Reads from bin/Release/net7.0/thebasics.dll
│   └── Creates thebasics.zip
└── Success!
```

### CI/CD Pipeline
```
GitHub Actions workflow:
├── dotnet build --configuration Release (solution)
├── Test Build Output step
│   ├── Checks bin/Release/net7.0/thebasics.dll exists
│   └── FAILS HARD if missing
├── Package Main Mod step
│   ├── Calls package.ps1
│   └── FAILS HARD if packaging fails
└── Upload Mod Packages
    └── Uploads thebasics.zip
```

## Key Principles

### 1. **Fail Fast**
- Build verification immediately fails if DLL not found
- Package script fails if DLL missing
- No silent failures or fallback searching

### 2. **Single Source of Truth**
- One output location: `bin/Release/net7.0/`
- One package location: project root
- No multiple possible paths

### 3. **Standard MSBuild**
- Use default MSBuild output paths
- No custom OutputPath overrides
- Let .NET SDK handle target framework directories

### 4. **Minimal Complexity**
- No debug logging for normal operations
- No multi-location search logic
- No conditional path handling

## File Locations

```
mods-dll/thebasics/
├── bin/Release/net7.0/           # Build outputs
│   ├── thebasics.dll
│   └── thebasics.pdb
├── scripts/
│   ├── build-and-package.ps1    # Local development
│   └── package.ps1               # Packaging logic
├── thebasics.csproj              # Standard MSBuild config
├── thebasics.zip                 # Package output
└── (source files...)
```

## Environment Variables

- **`GITHUB_ACTIONS=true`**: Disables PostBuild target in CI/CD
- **`VINTAGE_STORY`**: Points to VS DLL dependencies

## Troubleshooting

### DLL Not Found
- Check: Does `bin/Release/net7.0/thebasics.dll` exist?
- Solution: Run clean build (`dotnet clean` then `dotnet build`)

### Package Script Fails
- Check: Is DLL in expected location?
- Check: Are required files (modinfo.json, assets/) present?
- Solution: Fix missing files, don't add fallback logic

### CI/CD Fails
- Check: Did "Test Build Output" step pass?
- Solution: Fix build issues, don't modify verification logic

## What We Removed

- Custom output directory paths in project file
- Multi-location DLL searching in package script
- Complex debug logging and directory scanning
- Unused `output/` directory structure
- Conditional packaging logic

## Benefits

1. **Predictable**: Always know where files will be
2. **Fast**: No searching, no complex logic
3. **Debuggable**: Clear failure points
4. **Maintainable**: Standard .NET practices
5. **Consistent**: Same behavior locally and in CI/CD 