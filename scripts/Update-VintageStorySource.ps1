[CmdletBinding()]
param(
    [string]$Version,
    [string]$WorkspaceRoot,
    [switch]$ForceDownload,
    [switch]$ForceExtract,
    [switch]$ForceDecompile
)

$ErrorActionPreference = "Stop"

function Get-DefaultWorkspaceRoot {
    if (-not [string]::IsNullOrWhiteSpace($env:VS_WORKSPACE_ROOT)) {
        return $env:VS_WORKSPACE_ROOT
    }

    $canonicalRoot = "C:\bench\vs"
    if (Test-Path $canonicalRoot) {
        return $canonicalRoot
    }

    $repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
    return Split-Path -Parent $repoRoot
}

if ([string]::IsNullOrWhiteSpace($WorkspaceRoot)) {
    $WorkspaceRoot = Get-DefaultWorkspaceRoot
}

$WorkspaceRoot = (New-Item -ItemType Directory -Force -Path $WorkspaceRoot).FullName

$metadataUrl = "https://api.vintagestory.at/stable.json"
$metadata = Invoke-RestMethod -Uri $metadataUrl

if ([string]::IsNullOrWhiteSpace($Version)) {
    $latest = $metadata.PSObject.Properties |
        Where-Object { $_.Value.windowsserver -and $_.Value.windowsserver.latest -eq 1 } |
        Select-Object -First 1

    if (-not $latest) {
        throw "Could not identify latest Windows server package from $metadataUrl"
    }

    $Version = $latest.Name
}

$versionEntry = $metadata.PSObject.Properties |
    Where-Object { $_.Name -eq $Version } |
    Select-Object -First 1

if (-not $versionEntry) {
    throw "Vintage Story version '$Version' was not found in $metadataUrl"
}

$server = $versionEntry.Value.windowsserver
if (-not $server) {
    throw "Vintage Story version '$Version' does not include a Windows server package"
}

$versionRoot = Join-Path $WorkspaceRoot "source\vintagestory\$Version"
$downloadDir = Join-Path $versionRoot "downloads"
$binDir = Join-Path $versionRoot "bin"
$decompiledRoot = Join-Path $versionRoot "decompiled"
$zipPath = Join-Path $downloadDir $server.filename

New-Item -ItemType Directory -Force -Path $downloadDir, $binDir, $decompiledRoot | Out-Null

if ($ForceDownload -or -not (Test-Path $zipPath)) {
    Write-Host "Downloading Vintage Story $Version from $($server.urls.cdn)"
    Invoke-WebRequest -Uri $server.urls.cdn -OutFile $zipPath
}

$actualMd5 = (Get-FileHash -Path $zipPath -Algorithm MD5).Hash.ToLowerInvariant()
$expectedMd5 = [string]$server.md5
if ($expectedMd5 -and $actualMd5 -ne $expectedMd5.ToLowerInvariant()) {
    throw "MD5 mismatch for $zipPath. Expected $expectedMd5, got $actualMd5"
}

$binHasFiles = Test-Path (Join-Path $binDir "VintagestoryServer.dll")
if ($ForceExtract -or -not $binHasFiles) {
    if ($ForceExtract -and (Test-Path $binDir)) {
        Remove-Item -Path (Join-Path $binDir "*") -Recurse -Force
    }

    Write-Host "Extracting $zipPath to $binDir"
    Expand-Archive -Path $zipPath -DestinationPath $binDir -Force
}

$ilspy = Get-Command ilspycmd -ErrorAction SilentlyContinue
if (-not $ilspy) {
    throw "ilspycmd is required. Install it with: dotnet tool install -g ilspycmd"
}

$assemblies = @(
    "VintagestoryAPI.dll",
    "VintagestoryLib.dll",
    "VintagestoryServer.dll",
    "Mods\VSSurvivalMod.dll",
    "Mods\VSEssentials.dll",
    "Mods\VSCreativeMod.dll"
)

$referencePaths = @(
    $binDir,
    (Join-Path $binDir "Lib"),
    (Join-Path $binDir "Mods")
)

foreach ($assembly in $assemblies) {
    $dllPath = Join-Path $binDir $assembly
    if (-not (Test-Path $dllPath)) {
        Write-Warning "Skipping missing assembly: $dllPath"
        continue
    }

    $assemblyName = [IO.Path]::GetFileNameWithoutExtension($dllPath)
    $outDir = Join-Path $decompiledRoot $assemblyName
    $projectPath = Join-Path $outDir "$assemblyName.csproj"

    if ($ForceDecompile -and (Test-Path $outDir)) {
        Remove-Item -Path $outDir -Recurse -Force
    }

    if ((Test-Path $projectPath) -and -not $ForceDecompile) {
        Write-Host "Skipping existing decompile output: $outDir"
        continue
    }

    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $arguments = @("--disable-updatecheck", "--nested-directories", "-p", "-o", $outDir)
    foreach ($referencePath in $referencePaths) {
        $arguments += @("-r", $referencePath)
    }
    $arguments += $dllPath

    Write-Host "Decompiling $assemblyName"
    & $ilspy.Source @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "ilspycmd failed while decompiling $dllPath"
    }
}

Write-Host "Vintage Story $Version source is ready at $versionRoot"
