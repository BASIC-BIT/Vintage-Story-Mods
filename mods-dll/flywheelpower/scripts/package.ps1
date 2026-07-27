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

$expectedEntries = @(
    'assets/flywheelpower/blocktypes/flywheel.json',
    'assets/flywheelpower/blocktypes/compactflywheel.json',
    'assets/flywheelpower/blocktypes/flywheelpart.json',
    'assets/flywheelpower/lang/en.json',
    'assets/flywheelpower/shapes/block/compact-flywheel-frame-horizontal.json',
    'assets/flywheelpower/shapes/block/compact-flywheel-frame-vertical.json',
    'assets/flywheelpower/shapes/block/compact-flywheel-wheel-coupled.json',
    'assets/flywheelpower/shapes/block/flywheel-axle.json',
    'assets/flywheelpower/shapes/block/flywheel-frame-horizontal.json',
    'assets/flywheelpower/shapes/block/flywheel-frame-vertical.json',
    'assets/flywheelpower/shapes/block/flywheel-wheel-coupled.json',
    'flywheelpower.dll',
    'flywheelpower.pdb',
    'modinfo.json',
    'README.md'
)
$releasedRendererCodes = @(
    'flywheelpower-full-wood-ironhub',
    'flywheelpower-full-iron-ironhub',
    'flywheelpower-full-meteoriciron-ironhub',
    'flywheelpower-full-steel-ironhub',
    'flywheelpower-compact-wood',
    'flywheelpower-compact-stone',
    'flywheelpower-compact-iron',
    'flywheelpower-compact-meteoriciron',
    'flywheelpower-compact-steel'
)

$archive = [System.IO.Compression.ZipFile]::OpenRead($zipFile)
try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    $unexpectedEntries = @($entryNames | Where-Object { $_ -notin $expectedEntries })
    $missingEntries = @($expectedEntries | Where-Object { $_ -notin $entryNames })

    if ($missingEntries.Count -gt 0) {
        throw "Required release entries are missing from package: $($missingEntries -join ', ')"
    }

    if ($unexpectedEntries.Count -gt 0) {
        throw "Unexpected entries were included in package: $($unexpectedEntries -join ', ')"
    }

    $blocktypeText = foreach ($entry in @(
        'assets/flywheelpower/blocktypes/flywheel.json',
        'assets/flywheelpower/blocktypes/compactflywheel.json'
    )) {
        $reader = [System.IO.StreamReader]::new($archive.GetEntry($entry).Open())
        try {
            $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }

    foreach ($rendererCode in $releasedRendererCodes) {
        if (-not ($blocktypeText -match [regex]::Escape($rendererCode))) {
            throw "Released renderer group is missing from packaged blocktypes: $rendererCode"
        }
    }

    if ($blocktypeText -match 'bronze') {
        throw 'Unsupported material or hub mappings were included in packaged blocktypes.'
    }

    $languageReader = [System.IO.StreamReader]::new(
        $archive.GetEntry('assets/flywheelpower/lang/en.json').Open())
    try {
        $languageText = $languageReader.ReadToEnd()
    }
    finally {
        $languageReader.Dispose()
    }

    if ($languageText -match 'sliptransmission|keyedflywheel|blockinfo-shaft|bronze') {
        throw 'Disabled or unsupported player-facing localization was included in the package.'
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Successfully created and verified Flywheel Power package at $zipFile"
