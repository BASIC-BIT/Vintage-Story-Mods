Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead('mods-dll/thebasics/thebasics_5_3_0.zip')
foreach ($entry in $zip.Entries) {
    Write-Host $entry.FullName
}
$zip.Dispose()
