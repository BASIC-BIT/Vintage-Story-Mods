[CmdletBinding()]
param([string]$PowerShell7 = 'pwsh')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('vs-qa-stage-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot | Out-Null
$stamp = 'a' * 64
$dll = Join-Path $testRoot 'thebasics.dll'
$fixtureType = 'QaStamp' + [Guid]::NewGuid().ToString('N')
Add-Type -TypeDefinition ('[assembly: System.Reflection.AssemblyMetadata("GuiCaptureSourceTreeHash", "' + $stamp + '")] public class ' + $fixtureType + ' {}') -OutputAssembly $dll
$fixtureZip = Join-Path $testRoot 'thebasics_test_2.zip'
$zip = [IO.Compression.ZipFile]::Open($fixtureZip, [IO.Compression.ZipArchiveMode]::Create)
try { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $dll, 'thebasics.dll') } finally { $zip.Dispose() }
$fixtureHash = (Get-FileHash -LiteralPath $fixtureZip -Algorithm SHA256).Hash.ToLowerInvariant()

class QaTransferResult { [void] Check() {} }
class QaSftpFixture {
    [string]$Root
    [string]$Output
    [bool]$CorruptUpload
    [bool]$FailRemoval
    [bool]$ChangeAfterBackup
    [int]$ListCount
    [Collections.Generic.List[string]]$Events = [Collections.Generic.List[string]]::new()
    QaSftpFixture([string]$root, [string]$output) { $this.Root = $root; $this.Output = $output }
    [string] Local([string]$remote) { return Join-Path $this.Root $remote.TrimStart('/') }
    [object] GetFileInfo([string]$remote) {
        $item = Get-Item -LiteralPath ($this.Local($remote))
        return [pscustomobject]@{ Name = $item.Name; FileType = $(if ($item.PSIsContainer) { 'd' } else { '-' }); IsDirectory = $item.PSIsContainer }
    }
    [object] ListDirectory([string]$remote) {
        $this.ListCount++
        if ($this.ChangeAfterBackup -and $this.ListCount -eq 2) { [IO.File]::AppendAllText(($this.Local('/data/Mods/thebasics_test_1.zip')), 'changed') }
        $files = @(Get-ChildItem -LiteralPath ($this.Local($remote)) | ForEach-Object { [pscustomobject]@{ Name = $_.Name; FileType = $(if ($_.PSIsContainer) { 'd' } else { '-' }); IsDirectory = $_.PSIsContainer } })
        return @{ Files = $files }
    }
    [QaTransferResult] GetFiles([string]$remote, [string]$destination) {
        $this.Events.Add('get:' + $remote)
        Copy-Item -LiteralPath ($this.Local($remote)) -Destination $destination -Force
        return [QaTransferResult]::new()
    }
    [QaTransferResult] PutFiles([string]$source, [string]$remote) {
        $index = Get-Content -Raw -LiteralPath (Join-Path $this.Output 'backup-index.json') | ConvertFrom-Json
        if (@($index.files | Where-Object kind -eq 'client').Count -ne 1 -or @($index.files | Where-Object kind -eq 'server').Count -ne 3) { throw 'Mutation began before complete backups.' }
        foreach ($entry in $index.files) {
            if ((Get-FileHash -LiteralPath $entry.backup -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw 'Backup hash differs before mutation.' }
        }
        if (!(Test-Path -LiteralPath (Join-Path $this.Output 'RESTORE.md'))) { throw 'Recovery mapping missing before mutation.' }
        $this.Events.Add('put:' + $remote)
        Copy-Item -LiteralPath $source -Destination ($this.Local($remote)) -Force
        if ($this.CorruptUpload) { [IO.File]::WriteAllText(($this.Local($remote)), 'corrupt') }
        return [QaTransferResult]::new()
    }
    [QaTransferResult] RemoveFiles([string]$remote) {
        $this.Events.Add('remove:' + $remote)
        if ($this.FailRemoval) { throw 'Injected failure with fixture-sftp-secret.' }
        Remove-Item -LiteralPath ($this.Local($remote))
        return [QaTransferResult]::new()
    }
    [void] Dispose() { $this.Events.Add('dispose') }
}

function Assert([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message } }
function New-QaFixture([string]$Name) {
    $root = Join-Path $testRoot $Name
    $remote = Join-Path $root 'remote'
    $profile = Join-Path $root 'profile'
    foreach ($relative in @('data/Mods', 'data/ModConfig', 'data/Logs')) { [void](New-Item -ItemType Directory -Path (Join-Path $remote $relative) -Force) }
    [void](New-Item -ItemType Directory -Path (Join-Path $profile 'Mods') -Force)
    [IO.File]::WriteAllText((Join-Path $remote 'data/Mods/thebasics_test_1.zip'), 'server-old')
    [IO.File]::WriteAllText((Join-Path $profile 'Mods/thebasics_test_1.zip'), 'client-old')
    [IO.File]::WriteAllText((Join-Path $remote 'data/ModConfig/the_basics.json'), '{"test":true}')
    [IO.File]::WriteAllText((Join-Path $remote 'data/Logs/server-main.log'), '1.1.2026 12:00:00 [Notification] Entering runphase RunGame')
    $script:Package = $fixtureZip
    $script:ExpectedSha256 = $fixtureHash
    $script:ExpectedSourceTreeHash = $stamp
    $script:BuildReceipt = Join-Path $root 'build.json'
    @{ Package = $fixtureZip; Sha256 = $fixtureHash; SourceTreeHash = $stamp; Version = 'test.2' } | ConvertTo-Json | Set-Content -LiteralPath $script:BuildReceipt -Encoding UTF8
    $script:OutputDirectory = Join-Path $root 'stage'
    $script:ClientDataPaths = @($profile)
    $credentials = Join-Path $root 'credentials.env'
    @'
PTERO_BASE_URL=https://pt.basicbit.net
PTERO_SERVER_ID=8982de16
PTERO_TOKEN=ptlc_fixture-token
SFTP_HOST=pt.basicbit.net
SFTP_PORT=2022
SFTP_USERNAME=test.8982de16
SFTP_PASSWORD=fixture-sftp-secret
SFTP_HOST_KEY_FINGERPRINT=ssh-ed25519 255 fixture-pin
'@ | Set-Content -LiteralPath $credentials -Encoding UTF8
    $script:CredentialFiles = @($credentials)
    $script:Restart = $false
    $script:RestartTimeoutSeconds = 30
    $script:sessionFixture = [QaSftpFixture]::new($remote, $script:OutputDirectory)
    $script:apiEvents = [Collections.Generic.List[string]]::new()
    $script:afterLogPolls = 0
    @{ root = $root; remote = $remote; profile = $profile }
}

function Expect-Failure([scriptblock]$Action, [string]$Pattern) {
    $failed = $false
    try { & $Action | Out-Null } catch { $failed = $true; Assert ($_.Exception.Message -like $Pattern) ('Unexpected failure: ' + $_.Exception.Message) }
    Assert $failed 'Expected failure did not happen.'
}

try {
    $fixture = New-QaFixture 'success'
    . (Join-Path $PSScriptRoot 'stage-vs-qa-package.ps1') -Package $Package -ExpectedSha256 $ExpectedSha256 -ExpectedSourceTreeHash $ExpectedSourceTreeHash -BuildReceipt $BuildReceipt -OutputDirectory $OutputDirectory -ClientDataPaths $ClientDataPaths -CredentialFiles $CredentialFiles -PowerShell7 $PowerShell7
    function New-QaSftpSession { $script:sessionFixture }
    function Invoke-QaApi($Credentials, [string]$Operation, [string]$Method = 'Get', [string]$Body) {
        $script:apiEvents.Add($Operation)
        if ($Operation -eq 'power') {
            Assert ($Method -eq 'Post' -and $Body -eq '{"signal":"restart"}') 'Incorrect restart operation.'
            Assert ($script:sessionFixture.Events -contains 'remove:/data/Mods/thebasics_test_1.zip') 'Restart preceded verified staging.'
            Assert (!(Test-Path -LiteralPath (Join-Path $script:ClientDataPaths[0] 'Mods/thebasics_test_1.zip'))) 'Restart preceded client staging.'
            return @{}
        }
        if ($Operation -eq 'resources') { return @{ attributes = @{ current_state = 'running' } } }
        if ($Operation -like 'files/contents*') {
            $script:afterLogPolls++
            # The first Running response has only an old marker and must not pass.
            if ($script:afterLogPolls -eq 1) { return '1.1.2026 12:00:00 [Notification] Entering runphase RunGame' }
            return '1.1.2026 12:01:00 [Notification] Entering runphase RunGame'
        }
        throw 'Unexpected API request.'
    }
    function Start-Sleep { }
    $result = Invoke-QaPackageStage
    Assert ($result.status -eq 'staged') 'Normal stage did not finish.'
    Assert ($apiEvents -notcontains 'power') 'Restart happened without opt-in.'
    Assert ((Get-FileHash -LiteralPath (Join-Path $fixture.profile 'Mods/thebasics_test_2.zip') -Algorithm SHA256).Hash.ToLowerInvariant() -eq $fixtureHash) 'Client bytes differ.'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.remote 'data/ModConfig/the_basics.json')) -eq '{"test":true}') 'Config was modified.'
    Write-Host 'PASS: compiled stamp, full verified backups before mutation, staged readback, config preservation, restart opt-in.'

    [void](New-QaFixture 'hash-mismatch')
    $ExpectedSha256 = 'b' * 64
    Expect-Failure { Invoke-QaPackageStage } '*SHA256*'
    Assert ($sessionFixture.Events.Count -eq 0 -and !(Test-Path -LiteralPath $OutputDirectory)) 'Hash failure reached staging.'
    Write-Host 'PASS: package hash mismatch fails before remote/profile changes.'

    [void](New-QaFixture 'stamp-mismatch')
    $ExpectedSourceTreeHash = 'b' * 64
    @{ Package = $Package; Sha256 = $ExpectedSha256; SourceTreeHash = $ExpectedSourceTreeHash } | ConvertTo-Json | Set-Content -LiteralPath $BuildReceipt
    Expect-Failure { Invoke-QaPackageStage } '*Compiled GUI source stamp*'
    Assert ($sessionFixture.Events.Count -eq 0) 'Receipt-only stamp was trusted.'
    Write-Host 'PASS: receipt metadata cannot substitute for the compiled stamp.'

    [void](New-QaFixture 'wrong-identity')
    (Get-Content -Raw -LiteralPath $CredentialFiles[0]).Replace('8982de16', 'other-server') | Set-Content -LiteralPath $CredentialFiles[0]
    Expect-Failure { Invoke-QaPackageStage } '*QA Client API credentials*'
    Assert ($sessionFixture.Events.Count -eq 0) 'Wrong identity reached remote staging.'
    Write-Host 'PASS: alternate server identity fails closed.'

    $fixture = New-QaFixture 'corrupt-upload'
    $sessionFixture.CorruptUpload = $true
    Expect-Failure { Invoke-QaPackageStage } '*uploaded bytes differ*'
    Assert (Test-Path -LiteralPath (Join-Path $fixture.remote 'data/Mods/thebasics_test_1.zip')) 'Old server version removed after failed upload.'
    Assert ([IO.File]::ReadAllText((Join-Path $fixture.profile 'Mods/thebasics_test_1.zip')) -eq 'client-old') 'Client changed after failed server verification.'
    $failedReceipt = Get-Content -Raw -LiteralPath (Join-Path $OutputDirectory 'stage-receipt.json') | ConvertFrom-Json
    Assert ($failedReceipt.status -eq 'failed' -and $failedReceipt.mutationStarted) 'Partial mutation receipt missing.'
    Write-Host 'PASS: corrupt upload preserves old versions and records partial mutation.'

    [void](New-QaFixture 'changing-remote')
    $sessionFixture.ChangeAfterBackup = $true
    Expect-Failure { Invoke-QaPackageStage } '*Remote mod changed after backup*'
    Assert (!($sessionFixture.Events | Where-Object { $_ -like 'put:*' -or $_ -like 'remove:*' })) 'Changed remote mod was overwritten.'
    Write-Host 'PASS: remote change after backup prevents mutation.'

    [void](New-QaFixture 'redacted-error')
    $sessionFixture.FailRemoval = $true
    Expect-Failure { Invoke-QaPackageStage } '*Injected failure with*redacted*'
    Assert (!(Get-Content -Raw -LiteralPath (Join-Path $OutputDirectory 'stage-receipt.json')).Contains('fixture-sftp-secret')) 'Secret leaked into failure receipt.'
    Write-Host 'PASS: failure journal remains and secret-bearing errors are redacted.'

    [void](New-QaFixture 'restart')
    $Restart = $true
    $result = Invoke-QaPackageStage
    Assert ($result.status -eq 'restarted' -and $afterLogPolls -eq 2) 'Old Running log was accepted as restart proof.'
    Assert ($result.freshRunGameMarker.Contains('12:01:00')) 'Fresh boot evidence missing.'
    Write-Host 'PASS: restart occurs last and requires a fresh RunGame marker.'

    $fixture = New-QaFixture 'unsafe-local'
    $junction = Join-Path $fixture.root 'linked-profile'
    [void](New-Item -ItemType Junction -Path $junction -Target $fixture.profile)
    $ClientDataPaths = @($junction)
    Expect-Failure { Invoke-QaPackageStage } '*Reparse points*'
    Assert ($sessionFixture.Events.Count -eq 0) 'Linked profile reached staging.'
    [IO.Directory]::Delete($junction)
    Write-Host 'PASS: explicit profiles cannot redirect through reparse points.'
} finally {
    $resolvedRoot = [IO.Path]::GetFullPath($testRoot)
    if (!$resolvedRoot.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing test cleanup outside the temporary directory.' }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
