$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")
$projectFile = Join-Path $projectRoot "thebasicslanguageunderstanding.csproj"
$releaseDir = Join-Path $projectRoot "bin/Release"
$targetFramework = $null
$onnxRuntimeVersion = "1.26.0"

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
$assetsDir = Join-Path $projectRoot "assets"
$dllFile = Join-Path $outputDir "thebasicslanguageunderstanding.dll"
$pdbFile = Join-Path $outputDir "thebasicslanguageunderstanding.pdb"
$modInfo = Get-Content $modInfoFile | ConvertFrom-Json
$version = $modInfo.version -replace '\.', '_' -replace '-', '_'
$zipFile = Join-Path $projectRoot "thebasicslanguageunderstanding_$version.zip"
$logFile = Join-Path $solutionRoot "package.log"
$nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget/packages" }
$onnxPackageRoot = Join-Path $nugetRoot "microsoft.ml.onnxruntime/$onnxRuntimeVersion"
$onnxManagedPackageRoot = Join-Path $nugetRoot "microsoft.ml.onnxruntime.managed/$onnxRuntimeVersion"
$systemNumericsTensorsVersion = "9.0.0"
$systemNumericsTensorsPackageRoot = Join-Path $nugetRoot "system.numerics.tensors/$systemNumericsTensorsVersion"
$onnxManagedOutputDll = Join-Path $outputDir "Microsoft.ML.OnnxRuntime.dll"
$onnxManagedPackageDll = Join-Path $onnxManagedPackageRoot "lib/net8.0/Microsoft.ML.OnnxRuntime.dll"
$onnxManagedDll = if (Test-Path $onnxManagedOutputDll) { $onnxManagedOutputDll } else { $onnxManagedPackageDll }
$systemNumericsTensorsOutputDll = Join-Path $outputDir "System.Numerics.Tensors.dll"
$systemNumericsTensorsPackageDll = Join-Path $systemNumericsTensorsPackageRoot "lib/net9.0/System.Numerics.Tensors.dll"
$systemNumericsTensorsDll = if (Test-Path $systemNumericsTensorsOutputDll) { $systemNumericsTensorsOutputDll } else { $systemNumericsTensorsPackageDll }

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"[$timestamp] The BASICs Language Understanding package script started" | Out-File -FilePath $logFile

foreach ($file in @($modInfoFile, $dllFile, $pdbFile, $onnxManagedDll, $systemNumericsTensorsDll)) {
    if (-not (Test-Path $file)) {
        throw "Required package file not found: $file"
    }
}

$nativeRuntimeFiles = @(
    @{ Source = Join-Path $onnxPackageRoot "runtimes/win-x64/native/onnxruntime.dll"; Entry = "native/onnxruntime.dll" },
    @{ Source = Join-Path $onnxPackageRoot "runtimes/win-x64/native/onnxruntime_providers_shared.dll"; Entry = "native/onnxruntime_providers_shared.dll" },
    @{ Source = Join-Path $onnxPackageRoot "runtimes/linux-x64/native/libonnxruntime.so"; Entry = "native/libonnxruntime.so" },
    @{ Source = Join-Path $onnxPackageRoot "runtimes/linux-x64/native/libonnxruntime_providers_shared.so"; Entry = "native/libonnxruntime_providers_shared.so" },
    @{ Source = Join-Path $onnxPackageRoot "runtimes/osx-arm64/native/libonnxruntime.dylib"; Entry = "native/libonnxruntime.dylib" }
)

foreach ($runtimeFile in $nativeRuntimeFiles) {
    if (-not (Test-Path $runtimeFile.Source)) {
        throw "Required ONNX Runtime native file not found: $($runtimeFile.Source)"
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path $zipFile) {
    Remove-Item $zipFile -Force
}

$zip = [System.IO.Compression.ZipFile]::Open($zipFile, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in @($modInfoFile, $dllFile, $pdbFile, $onnxManagedDll, $systemNumericsTensorsDll)) {
        $entryName = [System.IO.Path]::GetFileName($file)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $file, $entryName) | Out-Null
    }

    foreach ($runtimeFile in $nativeRuntimeFiles) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $runtimeFile.Source, $runtimeFile.Entry) | Out-Null
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

$msg = "Successfully created The BASICs Language Understanding package at $zipFile"
$msg | Out-File -FilePath $logFile -Append
Write-Host $msg
