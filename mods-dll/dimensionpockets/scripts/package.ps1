$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")
$projectFile = Join-Path $projectRoot "dimensionpockets.csproj"
$releaseDir = Join-Path $projectRoot "bin/Release"
$targetFramework = $null

if (Test-Path $projectFile) {
    [xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
    $targetFramework = $projectXml.Project.PropertyGroup |
        ForEach-Object {
            if ($_.TargetFramework) {
                if ($_.TargetFramework -is [System.Xml.XmlElement]) { $_.TargetFramework.InnerText } else { [string]$_.TargetFramework }
            } elseif ($_.TargetFrameworks) {
                $frameworks = if ($_.TargetFrameworks -is [System.Xml.XmlElement]) { $_.TargetFrameworks.InnerText } else { [string]$_.TargetFrameworks }
                ($frameworks -split ';') | Select-Object -First 1
            }
        } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Select-Object -First 1
}

if (-not $targetFramework) {
    throw "Could not determine target framework from $projectFile"
}

$outputDir = Join-Path $releaseDir $targetFramework
$modInfoFile = Join-Path $projectRoot "modinfo.json"
$readmeFile = Join-Path $projectRoot "README.md"
$assetsDir = Join-Path $projectRoot "assets"
$modInfo = Get-Content $modInfoFile | ConvertFrom-Json
$modId = $modInfo.modid
$dllFile = Join-Path $outputDir "$modId.dll"
$pdbFile = Join-Path $outputDir "$modId.pdb"
$version = $modInfo.version -replace '\.', '_' -replace '-', '_'
$zipFile = Join-Path $projectRoot "$($modId)_$version.zip"
$logFile = Join-Path $solutionRoot "package.log"

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"[$timestamp] Pocket Dimensions package script started" | Out-File -FilePath $logFile

foreach ($file in @($modInfoFile, $readmeFile, $dllFile, $pdbFile)) {
    if (-not (Test-Path $file)) {
        throw "Required package file not found: $file"
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

$zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @($modInfoFile, $readmeFile, $dllFile, $pdbFile)) {
        $entryName = [System.IO.Path]::GetFileName($file)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file, $entryName) | Out-Null
    }

    if (Test-Path $assetsDir) {
        $assetsDirParent = Split-Path $assetsDir -Parent
        Get-ChildItem -LiteralPath $assetsDir -Recurse -File | ForEach-Object {
            $relativePath = $_.FullName.Substring($assetsDirParent.Length + 1)
            $entryName = $relativePath -replace '\\', '/'
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $entryName) | Out-Null
        }
    }
} finally {
    $zip.Dispose()
}

$msg = "Successfully created Pocket Dimensions package at $zipFile"
$msg | Out-File -FilePath $logFile -Append
Write-Host $msg

# Deploy locally along with the mods pocketdimensions hard-depends on in modinfo.json.
# Without dimensionlib and basicconfig present, Vintage Story refuses to load pocketdimensions
# with ModError.Dependency, so shipping the zip alone is not enough for a local test install.
# Directory selection matches thebasics/scripts/package.ps1 and honours the same override.
$dependencyProjects = @("dimensionlib", "basicconfig")
$packageFiles = @($zipFile)
$packageCleanupPatterns = @("$($modId)*.zip")

foreach ($dependencyId in $dependencyProjects) {
    $dependencyRoot = Join-Path $solutionRoot "mods-dll/$dependencyId"
    $dependencyZip = $null
    if (Test-Path $dependencyRoot) {
        $dependencyZip = Get-ChildItem -LiteralPath $dependencyRoot -Filter "$($dependencyId)_*.zip" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc -Descending |
            Select-Object -First 1
    }

    if ($dependencyZip) {
        $packageFiles += $dependencyZip.FullName
        $packageCleanupPatterns += "$($dependencyId)*.zip"
    } else {
        $msg = "Warning: $dependencyId package not found in $dependencyRoot; dependency zip will not be deployed"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    }
}

$localModsDirectories = @()
if ($env:THEBASICS_LOCAL_MOD_DIRS) {
    $env:THEBASICS_LOCAL_MOD_DIRS.Split(';') | ForEach-Object {
        $p = $_.Trim()
        if (-not [string]::IsNullOrWhiteSpace($p)) {
            $localModsDirectories += $p
        }
    }
}

if ($localModsDirectories.Count -eq 0) {
    $localModsDirectories += (Join-Path $env:APPDATA "VintagestoryData/Mods")
    if ($env:VS_PROFILES_DIR -and (Test-Path $env:VS_PROFILES_DIR)) {
        Get-ChildItem -Path $env:VS_PROFILES_DIR -Directory -Filter "Profile*" -ErrorAction SilentlyContinue |
            Sort-Object Name |
            ForEach-Object { $localModsDirectories += (Join-Path $_.FullName "Mods") }
    }
}

foreach ($localModsDir in $localModsDirectories) {
    try {
        if (-not (Test-Path $localModsDir)) {
            New-Item -ItemType Directory -Path $localModsDir -Force | Out-Null
        }

        foreach ($pattern in $packageCleanupPatterns) {
            Get-ChildItem -Path $localModsDir -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object {
                Remove-Item -Path $_.FullName -Force
            }
        }

        foreach ($packageFile in $packageFiles) {
            $localModFile = Join-Path $localModsDir (Split-Path $packageFile -Leaf)
            Copy-Item -Path $packageFile -Destination $localModFile -Force
            $msg = "Successfully copied package to $localModFile"
            Write-Host $msg
            "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        }
    } catch {
        $msg = "Error copying to local mods directory $localModsDir : $($_.Exception.Message)"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    }
}
