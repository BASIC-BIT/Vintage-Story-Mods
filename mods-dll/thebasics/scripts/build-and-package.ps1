# Build and Package Script for The BASICs Mod
# This script ensures a fresh build before packaging

# Get absolute paths
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")  # thebasics project root
$repoRoot = Resolve-Path (Join-Path $projectRoot "..\..")
$workspaceRoot = Split-Path -Parent $repoRoot
if ((Split-Path -Leaf $workspaceRoot) -eq "work") {
    $workspaceRoot = Split-Path -Parent $workspaceRoot
}
$workspaceDotnet = Join-Path $workspaceRoot ".dotnet\dotnet.exe"
$dotnet = if ($env:DOTNET_EXE) { $env:DOTNET_EXE } elseif (Test-Path $workspaceDotnet) { $workspaceDotnet } else { "dotnet" }

Write-Host "Building The BASICs mod..."

# Clean build directory to ensure fresh build
$binDir = Join-Path $projectRoot "bin"
if (Test-Path $binDir) {
    Write-Host "Cleaning build directory..."
    Remove-Item -Path $binDir -Recurse -Force
}

# Build the project using standard MSBuild output location
Write-Host "Compiling project..."
$buildResult = & $dotnet build "$projectRoot/thebasics.csproj" --configuration Release /p:SkipPostBuildPackage=true
Write-Host $buildResult

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Exiting..." -ForegroundColor Red
    exit 1
}

Write-Host "Build successful! Running package script..." -ForegroundColor Green

# Run the package script
& "$PSScriptRoot/package.ps1"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build and package completed successfully!" -ForegroundColor Green
} else {
    Write-Host "Package script failed!" -ForegroundColor Red
    exit 1
}
