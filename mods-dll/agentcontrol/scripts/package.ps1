$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$outputDir = Join-Path $projectRoot "bin/Release/net10.0"
$abstractionsOutputDir = Join-Path $projectRoot "../agentcontrol.abstractions/bin/Release/net10.0"
$modInfoFile = Join-Path $projectRoot "modinfo.json"
$modInfo = Get-Content -LiteralPath $modInfoFile -Raw | ConvertFrom-Json
$version = $modInfo.version -replace '\.', '_' -replace '-', '_'
$zipFile = Join-Path $projectRoot "agentcontrol_$version.zip"
$files = @(
    $modInfoFile,
    (Join-Path $outputDir "agentcontrol.dll"),
    (Join-Path $outputDir "agentcontrol.pdb"),
    (Join-Path $abstractionsOutputDir "agentcontrol.abstractions.dll"),
    (Join-Path $abstractionsOutputDir "agentcontrol.abstractions.pdb"),
    (Join-Path $projectRoot "README.md"),
    (Join-Path $projectRoot "RUNBOOK.md")
)
$exampleFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot "examples") -File

foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath $file)) {
        throw "Required package file not found: $file"
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path -LiteralPath $zipFile) {
    Remove-Item -LiteralPath $zipFile -Force
}
$zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $file,
            [System.IO.Path]::GetFileName($file)) | Out-Null
    }
    foreach ($file in $exampleFiles) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $zip,
            $file.FullName,
            "examples/$($file.Name)") | Out-Null
    }
} finally {
    $zip.Dispose()
}
Write-Host "Successfully created Agent Control package at $zipFile"
