$releaseDir = Join-Path $PSScriptRoot '..\releases'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Compress-Archive -Force -Path $PSScriptRoot/../modinfo.json,$PSScriptRoot/../assets,$PSScriptRoot/../litchimneys.dll,$PSScriptRoot/../litchimneys.pdb -DestinationPath (Join-Path $releaseDir 'litchimneys.zip')
