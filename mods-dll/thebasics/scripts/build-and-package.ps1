# Build and Package Script for The BASICs Mod
# This script ensures a fresh build before packaging

param(
    [switch]$LocalOnly,
    [ValidatePattern('^$|^[0-9a-f]{64}$')][string]$SourceTreeHash,
    [string]$ReceiptPath
)
$ErrorActionPreference = 'Stop'
if ($ReceiptPath -and (!$LocalOnly -or !$SourceTreeHash)) { throw 'A build receipt requires -LocalOnly and -SourceTreeHash.' }
if ($ReceiptPath -and (Test-Path -LiteralPath $ReceiptPath)) { throw 'Use a new build receipt path.' }

# Get absolute paths
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path  # thebasics project root
$repoRoot = (Resolve-Path (Join-Path $projectRoot "..\..")).Path
$workspaceRoot = Split-Path -Parent $repoRoot
if ((Split-Path -Leaf $workspaceRoot) -eq "work") {
    $workspaceRoot = Split-Path -Parent $workspaceRoot
}
$workspaceDotnet = Join-Path $workspaceRoot ".dotnet\dotnet.exe"
$dotnet = if ($env:DOTNET_EXE) { $env:DOTNET_EXE } elseif (Test-Path $workspaceDotnet) { $workspaceDotnet } else { "dotnet" }

Write-Host "Building The BASICs mod..."

# Clean build directory to ensure fresh build
$binDir = Join-Path $projectRoot "bin"
if (Test-Path -LiteralPath $binDir) {
    $resolvedBin = (Resolve-Path -LiteralPath $binDir).Path
    if ($resolvedBin -ne [IO.Path]::GetFullPath((Join-Path $repoRoot 'mods-dll/thebasics/bin'))) { throw 'Build cleanup escaped the current repository.' }
    foreach ($path in @($projectRoot, $binDir)) {
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Build cleanup refuses linked directories.' }
    }
    Write-Host "Cleaning build directory..."
    Remove-Item -LiteralPath $resolvedBin -Recurse -Force
}

# Build the project using standard MSBuild output location
Write-Host "Compiling project..."
$buildArguments = @('build', "$projectRoot/thebasics.csproj", '--configuration', 'Release', '/p:SkipPostBuildPackage=true')
if ($SourceTreeHash) { $buildArguments += "/p:TheBasicsSourceTreeHash=$SourceTreeHash" }
$buildResult = & $dotnet @buildArguments
Write-Host $buildResult

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed! Exiting..." -ForegroundColor Red
    exit 1
}

Write-Host "Build successful! Running package script..." -ForegroundColor Green

# Run the package script
& "$PSScriptRoot/package.ps1" -LocalOnly:$LocalOnly

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build and package completed successfully!" -ForegroundColor Green
    if ($ReceiptPath) {
        $info = Get-Content -Raw -LiteralPath (Join-Path $projectRoot 'modinfo.json') | ConvertFrom-Json
        $package = Join-Path $projectRoot ('thebasics_' + ($info.version -replace '\.', '_' -replace '-', '_') + '.zip')
        $receipt = [ordered]@{ Schema = 1; Package = $package; Version = $info.version; SourceTreeHash = $SourceTreeHash; Sha256 = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant() }
        $receiptDirectory = Split-Path -Parent ([IO.Path]::GetFullPath($ReceiptPath))
        New-Item -ItemType Directory -Path $receiptDirectory -Force | Out-Null
        $receipt | ConvertTo-Json | Set-Content -LiteralPath $ReceiptPath -Encoding utf8
        Write-Host "Build receipt: $ReceiptPath"
    }
} else {
    Write-Host "Package script failed!" -ForegroundColor Red
    exit 1
}
