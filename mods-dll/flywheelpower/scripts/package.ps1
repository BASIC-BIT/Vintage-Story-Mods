$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectFile = Join-Path $projectRoot 'flywheelpower.csproj'
$modInfoFile = Join-Path $projectRoot 'modinfo.json'
$readmeFile = Join-Path $projectRoot 'README.md'
$assetsDir = Join-Path $projectRoot 'assets'

[xml]$projectXml = Get-Content -LiteralPath $projectFile -Raw
$targetFramework = $projectXml.Project.PropertyGroup |
    ForEach-Object {
        if ($_.TargetFramework) {
            if ($_.TargetFramework -is [System.Xml.XmlElement]) {
                $_.TargetFramework.InnerText
            } else {
                [string]$_.TargetFramework
            }
        }
    } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if (-not $targetFramework) {
    throw "Could not determine target framework from $projectFile"
}

$outputDir = Join-Path $projectRoot "bin/Release/$targetFramework"
$dllFile = Join-Path $outputDir 'flywheelpower.dll'
$pdbFile = Join-Path $outputDir 'flywheelpower.pdb'
$modInfo = Get-Content -LiteralPath $modInfoFile -Raw | ConvertFrom-Json
$version = $modInfo.version -replace '[.-]', '_'
$zipFile = Join-Path $projectRoot "flywheelpower_$version.zip"

foreach ($file in @($modInfoFile, $readmeFile, $dllFile, $pdbFile)) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "Required package file not found: $file"
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

Remove-Item -LiteralPath $zipFile -Force -ErrorAction SilentlyContinue
$zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @($modInfoFile, $readmeFile, $dllFile, $pdbFile)) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $file,
            [System.IO.Path]::GetFileName($file)) | Out-Null
    }

    Get-ChildItem -LiteralPath $assetsDir -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring($projectRoot.Path.Length + 1).Replace('\', '/')
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, $relativePath) | Out-Null
    }
}
finally {
    $zip.Dispose()
}

Write-Host "Successfully created Flywheel Power package at $zipFile"
