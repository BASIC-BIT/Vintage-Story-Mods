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
