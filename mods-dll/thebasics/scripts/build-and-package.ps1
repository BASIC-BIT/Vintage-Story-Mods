# Build and Package Script for The BASICs Mod
# This script ensures a fresh build before packaging

# Get absolute paths
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")  # thebasics project root
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")  # solution root
$outputDir = Join-Path $solutionRoot "output/net7.0"

Write-Host "Building The BASICs mod..."

# Clean output directory to ensure fresh build
if (Test-Path $outputDir) {
    Write-Host "Cleaning output directory..."
    Remove-Item -Path $outputDir -Recurse -Force
}

# Build the project with explicit output directory
Write-Host "Compiling project..."
$buildResult = dotnet build "$projectRoot/thebasics.csproj" --configuration Debug --output $outputDir

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