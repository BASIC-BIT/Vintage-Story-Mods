$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$fixture = Join-Path $repo ('.tmp/package-check-' + [Guid]::NewGuid().ToString('N'))
$project = Join-Path $fixture 'mods-dll/thebasics'
New-Item -ItemType Directory -Path (Join-Path $project 'scripts'), (Join-Path $project 'assets/test'), (Join-Path $fixture 'untouched-mods') -Force | Out-Null
foreach ($file in @('build-and-package.ps1', 'package.ps1')) {
    Copy-Item -LiteralPath (Join-Path $repo "mods-dll/thebasics/scripts/$file") -Destination (Join-Path $project 'scripts')
}
Set-Content -LiteralPath (Join-Path $project 'thebasics.csproj') -Value '<Project><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>'
Set-Content -LiteralPath (Join-Path $project 'modinfo.json') -Value '{"modid":"thebasics","version":"1.0.0"}'
Set-Content -LiteralPath (Join-Path $project 'assets/test/fixture.json') -Value '{}'
Set-Content -LiteralPath (Join-Path $fixture 'untouched-mods/thebasics_old.zip') -Value 'keep'
Set-Content -LiteralPath (Join-Path $fixture '.env') -Value 'VS_PACKAGE_TEST_POISON=loaded'
Set-Content -LiteralPath (Join-Path $project '.env') -Value 'SFTP_HOST=unreachable.invalid'
$fakeDotnet = Join-Path $fixture 'dotnet.ps1'
Set-Content -LiteralPath $fakeDotnet -Value @'
$projectFile = $args[1]
$out = Join-Path (Split-Path -Parent $projectFile) 'bin/Release/net10.0'
New-Item -ItemType Directory -Path $out -Force | Out-Null
Set-Content -LiteralPath (Join-Path $out 'thebasics.dll') -Value 'fixture DLL'
Set-Content -LiteralPath (Join-Path $out 'thebasics.pdb') -Value 'fixture PDB'
$global:LASTEXITCODE = 0
'@
function Assert-Check([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function Assert-Rejected([scriptblock]$Action) {
    $rejected = $false
    try { & $Action } catch { $rejected = $true }
    Assert-Check $rejected 'Expected request rejection.'
}
$saved = @{}
foreach ($name in @('DOTNET_EXE', 'THEBASICS_LOCAL_MOD_DIRS', 'VS_PACKAGE_TEST_POISON', 'SFTP_HOST')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name, 'Process') }
try {
    $env:DOTNET_EXE = $fakeDotnet
    $env:THEBASICS_LOCAL_MOD_DIRS = Join-Path $fixture 'untouched-mods'
    Remove-Item Env:VS_PACKAGE_TEST_POISON -ErrorAction SilentlyContinue
    $beforeHost = $env:SFTP_HOST
    $hash = 'a' * 64
    $receiptPath = Join-Path $fixture 'build.json'
    & (Join-Path $project 'scripts/build-and-package.ps1') -LocalOnly -SourceTreeHash $hash -ReceiptPath $receiptPath
    Assert-Check (!$env:VS_PACKAGE_TEST_POISON) 'Local-only package loaded root credentials.'
    Assert-Check ($env:SFTP_HOST -eq $beforeHost) 'Local-only package loaded mod credentials.'
    Assert-Check ((Get-Content -Raw -LiteralPath (Join-Path $fixture 'untouched-mods/thebasics_old.zip')).Trim() -eq 'keep') 'Local-only package changed a deployment target.'
    $receipt = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json
    Assert-Check ($receipt.SourceTreeHash -eq $hash) 'Build receipt lost its source identity.'
    Assert-Check ($receipt.Sha256 -eq (Get-FileHash -LiteralPath $receipt.Package).Hash.ToLowerInvariant()) 'Build receipt package hash differs.'
    $zip = [IO.Compression.ZipFile]::OpenRead($receipt.Package)
    try { Assert-Check (@($zip.Entries.FullName) -contains 'assets/test/fixture.json') 'Package asset names are not portable.' } finally { $zip.Dispose() }
    $sentinel = Join-Path $project 'bin/keep.txt'
    Set-Content -LiteralPath $sentinel -Value 'keep'
    Assert-Rejected { & (Join-Path $project 'scripts/build-and-package.ps1') -LocalOnly -SourceTreeHash $hash -ReceiptPath $receiptPath }
    Assert-Rejected { & (Join-Path $project 'scripts/build-and-package.ps1') -LocalOnly -ReceiptPath (Join-Path $fixture 'missing-hash.json') }
    Assert-Rejected { & (Join-Path $project 'scripts/build-and-package.ps1') -SourceTreeHash $hash -ReceiptPath (Join-Path $fixture 'deploy-receipt.json') }
    Assert-Check (Test-Path -LiteralPath $sentinel) 'Rejected build removed existing outputs.'
    Write-Host "Local-only package checks passed: $fixture"
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name], 'Process') }
}
