$ErrorActionPreference = 'Stop'

$projectRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectFile = Join-Path $projectRoot 'flywheelpower.csproj'
$modInfoFile = Join-Path $projectRoot 'modinfo.json'
$readmeFile = Join-Path $projectRoot 'README.md'
$assetsDir = Join-Path $projectRoot 'assets'
$materialGenerator = Join-Path $PSScriptRoot 'generate-material-content.py'
$componentGenerator = Join-Path $PSScriptRoot 'generate-component-shapes.py'

& python $componentGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw 'Generated Flywheel component shapes are stale.'
}

& python $materialGenerator --check
if ($LASTEXITCODE -ne 0) {
    throw 'Generated Flywheel material content is stale.'
}

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

$fixedEntryTimestamp = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
$packageFiles = @(
    foreach ($file in @($modInfoFile, $readmeFile, $dllFile, $pdbFile)) {
        [pscustomobject]@{
            SourcePath = [string]$file
            EntryName = [System.IO.Path]::GetFileName($file)
            IsText = [System.IO.Path]::GetExtension([string]$file) -in @('.json', '.md')
        }
    }

    foreach ($file in Get-ChildItem -LiteralPath $assetsDir -Recurse -File) {
        [pscustomobject]@{
            SourcePath = $file.FullName
            EntryName = $file.FullName.Substring($projectRoot.Path.Length + 1).Replace('\', '/')
            IsText = [System.IO.Path]::GetExtension($file.FullName) -in @('.json', '.md')
        }
    }
) | Sort-Object -Property EntryName -CaseSensitive

Remove-Item -LiteralPath $zipFile -Force -ErrorAction SilentlyContinue
$zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $packageFiles) {
        $entry = $zip.CreateEntry($file.EntryName, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $fixedEntryTimestamp
        $entryStream = $entry.Open()
        try {
            if ($file.IsText) {
                $text = [System.IO.File]::ReadAllText($file.SourcePath)
                $normalizedText = $text.Replace("`r`n", "`n").Replace("`r", "`n")
                $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($normalizedText)
                $entryStream.Write($bytes, 0, $bytes.Length)
            }
            else {
                $sourceStream = [System.IO.File]::OpenRead($file.SourcePath)
                try {
                    $sourceStream.CopyTo($entryStream)
                }
                finally {
                    $sourceStream.Dispose()
                }
            }
        }
        finally {
            $entryStream.Dispose()
        }
    }
}
finally {
    $zip.Dispose()
}

$expectedEntries = @(
    'assets/game/lang/en.json',
    'assets/flywheelpower/blocktypes/flywheel.json',
    'assets/flywheelpower/blocktypes/compactflywheel.json',
    'assets/flywheelpower/blocktypes/flywheelpart.json',
    'assets/flywheelpower/blocktypes/flywheelstand.json',
    'assets/flywheelpower/itemtypes/bearingfittings.json',
    'assets/flywheelpower/itemtypes/flywheelbearing.json',
    'assets/flywheelpower/itemtypes/flywheelrim.json',
    'assets/flywheelpower/itemtypes/flywheelweb.json',
    'assets/flywheelpower/lang/en.json',
    'assets/flywheelpower/recipes/grid/flywheel-assembly.json',
    'assets/flywheelpower/recipes/grid/flywheel-components.json',
    'assets/flywheelpower/recipes/grid/flywheel-stands.json',
    'assets/flywheelpower/recipes/smithing/bearingfittings.json',
    'assets/flywheelpower/shapes/block/compact-flywheel-frame-horizontal.json',
    'assets/flywheelpower/shapes/block/compact-flywheel-frame-vertical.json',
    'assets/flywheelpower/shapes/block/compact-flywheel-wheel-coupled.json',
    'assets/flywheelpower/shapes/block/flywheel-axle.json',
    'assets/flywheelpower/shapes/block/flywheel-frame-horizontal.json',
    'assets/flywheelpower/shapes/block/flywheel-frame-vertical.json',
    'assets/flywheelpower/shapes/block/flywheel-wheel-coupled.json',
    'assets/flywheelpower/shapes/item/bearing-fitting.json',
    'assets/flywheelpower/shapes/item/flywheel-bearing-compact.json',
    'assets/flywheelpower/shapes/item/flywheel-bearing-full.json',
    'assets/flywheelpower/shapes/item/flywheel-rim-compact.json',
    'assets/flywheelpower/shapes/item/flywheel-rim-full.json',
    'assets/flywheelpower/shapes/item/flywheel-web-full.json',
    'flywheelpower.dll',
    'flywheelpower.pdb',
    'modinfo.json',
    'README.md'
)
$releasedRendererCodes = @(
    foreach ($size in @(
        @{
            Name = 'full'
            Wheels = @('wood', 'copper', 'tinbronze', 'bismuthbronze', 'blackbronze', 'iron', 'meteoriciron', 'steel')
            Hubs = @('iron', 'meteoriciron', 'steel')
        },
        @{
            Name = 'compact'
            Wheels = @('wood', 'stone', 'copper', 'tinbronze', 'bismuthbronze', 'blackbronze', 'iron', 'meteoriciron', 'steel')
            Hubs = @('copper', 'tinbronze', 'bismuthbronze', 'blackbronze', 'iron', 'meteoriciron', 'steel')
        }
    )) {
        foreach ($wheel in $size.Wheels) {
            foreach ($hub in $size.Hubs) {
                $wheelTier = if ($wheel -in @('wood', 'stone')) {
                    0
                } elseif ($wheel -eq 'copper') {
                    1
                } elseif ($wheel -in @('tinbronze', 'bismuthbronze', 'blackbronze')) {
                    2
                } elseif ($wheel -in @('iron', 'meteoriciron')) {
                    3
                } else {
                    4
                }
                $hubTier = if ($hub -eq 'copper') {
                    1
                } elseif ($hub -in @('tinbronze', 'bismuthbronze', 'blackbronze')) {
                    2
                } elseif ($hub -in @('iron', 'meteoriciron')) {
                    3
                } else {
                    4
                }
                if ($hubTier -ge $wheelTier) {
                    "flywheelpower-$($size.Name)-$wheel-$($hub)hub"
                }
            }
        }
    }
)

$archive = [System.IO.Compression.ZipFile]::OpenRead($zipFile)
try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    $sortedEntryNames = @($entryNames | Sort-Object -CaseSensitive)
    $unexpectedEntries = @($entryNames | Where-Object { $_ -notin $expectedEntries })
    $missingEntries = @($expectedEntries | Where-Object { $_ -notin $entryNames })

    if ([string]::Join("`n", $entryNames) -cne [string]::Join("`n", $sortedEntryNames)) {
        throw 'Package entries are not in deterministic ordinal order.'
    }

    $unexpectedTimestamps = @(
        $archive.Entries | Where-Object { $_.LastWriteTime.DateTime -ne $fixedEntryTimestamp.DateTime }
    )
    if ($unexpectedTimestamps.Count -gt 0) {
        throw "Package entries do not share the deterministic timestamp: $($unexpectedTimestamps.FullName -join ', ')"
    }

    foreach ($entry in $archive.Entries | Where-Object { [System.IO.Path]::GetExtension($_.FullName) -in @('.json', '.md') }) {
        $stream = $entry.Open()
        try {
            for ($byte = $stream.ReadByte(); $byte -ge 0; $byte = $stream.ReadByte()) {
                if ($byte -eq 13) {
                    throw "Packaged text entry contains a non-normalized carriage return: $($entry.FullName)"
                }
            }
        }
        finally {
            $stream.Dispose()
        }
    }

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

    if ($blocktypeText -match 'flywheelpower-full-[^-]+-(copper|tinbronze|bismuthbronze|blackbronze)hub') {
        throw 'Compact-only copper or bronze hubs were included in full-size renderer groups.'
    }

    if ($blocktypeText -match 'cupronickel|brass|gold|silver|lead') {
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

    if ($languageText -match 'sliptransmission|keyedflywheel|blockinfo-shaft|cupronickel|brass|gold|silver|lead') {
        throw 'Disabled or unsupported player-facing localization was included in the package.'
    }

    $placementLanguageReader = [System.IO.StreamReader]::new(
        $archive.GetEntry('assets/game/lang/en.json').Open())
    try {
        $placementLanguageText = $placementLanguageReader.ReadToEnd()
    }
    finally {
        $placementLanguageReader.Dispose()
    }

    foreach ($placementKey in @(
        'placefailure-flywheelrequiresclearance',
        'placefailure-flywheelrequiresfoundation',
        'placefailure-flywheelrequiresstand'
    )) {
        if ($placementLanguageText -notmatch [regex]::Escape($placementKey)) {
            throw "Engine-domain placement localization is missing from the package: $placementKey"
        }
    }
}
finally {
    $archive.Dispose()
}

Write-Host "Successfully created and verified Flywheel Power package at $zipFile"
