$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")
$projectFile = Join-Path $projectRoot "basicconfig.csproj"
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
$dllFile = Join-Path $outputDir "basicconfig.dll"
$pdbFile = Join-Path $outputDir "basicconfig.pdb"
$modInfo = Get-Content $modInfoFile | ConvertFrom-Json
$version = $modInfo.version -replace '\.', '_' -replace '-', '_'
$zipFile = Join-Path $projectRoot "basicconfig_$version.zip"
$logFile = Join-Path $solutionRoot "package.log"

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"[$timestamp] BasicConfig package script started" | Out-File -FilePath $logFile

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
} finally {
    $zip.Dispose()
}

$msg = "Successfully created BasicConfig package at $zipFile"
$msg | Out-File -FilePath $logFile -Append
Write-Host $msg
