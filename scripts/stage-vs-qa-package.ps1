<#
.SYNOPSIS
Back up and stage a stamped BASICs package on disposable QA server 8982de16.
.DESCRIPTION
Run in Windows PowerShell for the installed WinSCP .NET Framework assembly.
CredentialFiles are explicit dotenv inputs; later files override process variables.
ClientDataPaths are optional, existing profiles only. No client process is launched.
OutputDirectory must be new. backup-index.json, stage-receipt.json and journal.jsonl
remain after failure; RESTORE.md explains recovery from their exact mappings.
Restart is opt-in and requires a fresh RunGame log marker, not just Running state.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Package,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-fA-F]{64}$')][string]$ExpectedSha256,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ExpectedSourceTreeHash,
    [Parameter(Mandatory)][string]$BuildReceipt,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string[]]$ClientDataPaths = @(),
    [string[]]$CredentialFiles = @(),
    [string]$WinScpAssembly = 'C:\Program Files (x86)\WinSCP\WinSCPnet.dll',
    [string]$PowerShell7 = 'pwsh',
    [switch]$Restart,
    [ValidateRange(30, 300)][int]$RestartTimeoutSeconds = 120
)
$ErrorActionPreference = 'Stop'

function Resolve-QaLocalPath([string]$Path, [switch]$Existing) {
    $full = [IO.Path]::GetFullPath($Path)
    if ($Existing -and !(Test-Path -LiteralPath $full)) { throw "Path does not exist: $full" }
    $ancestor = $full
    while ($ancestor) {
        if (Test-Path -LiteralPath $ancestor) {
            if ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Reparse points are not accepted: $ancestor"
            }
        }
        $parent = [IO.Path]::GetDirectoryName($ancestor)
        if ($parent -eq $ancestor) { break }
        $ancestor = $parent
    }
    $full
}

function Get-QaCredentials([string[]]$Files) {
    $values = @{}
    $keys = @('PTERO_BASE_URL', 'PTERO_TOKEN', 'PTERO_SERVER_ID', 'SFTP_HOST', 'SFTP_PORT', 'SFTP_USERNAME', 'SFTP_PASSWORD', 'SFTP_HOST_KEY_FINGERPRINT')
    foreach ($key in $keys) { $values[$key] = [Environment]::GetEnvironmentVariable($key) }
    foreach ($file in $Files) {
        $credentialPath = Resolve-QaLocalPath $file -Existing
        foreach ($line in Get-Content -LiteralPath $credentialPath) {
            if ($line -match '^\s*([A-Z_]+)\s*=\s*(.*?)\s*$' -and $matches[1] -in $keys) {
                $values[$matches[1]] = $matches[2].Trim().Trim('"').Trim("'")
            }
        }
    }
    if ($values.PTERO_BASE_URL -ne 'https://pt.basicbit.net' -and $values.PTERO_BASE_URL -ne 'https://pt.basicbit.net/') { throw 'Only the known disposable QA API identity is accepted.' }
    if ($values.PTERO_SERVER_ID -ne '8982de16' -or $values.PTERO_TOKEN -notlike 'ptlc_*') { throw 'QA Client API credentials are required.' }
    if ($values.SFTP_HOST -notin @('pt.basicbit.net', '15.235.75.126') -or $values.SFTP_USERNAME -notmatch '(^|\.)8982de16$') { throw 'SFTP identity is outside disposable QA.' }
    $port = 0
    if (![int]::TryParse($values.SFTP_PORT, [ref]$port) -or $port -lt 1 -or $port -gt 65535) { throw 'A valid SFTP port is required.' }
    if (!$values.SFTP_PASSWORD -or !$values.SFTP_HOST_KEY_FINGERPRINT -or $values.SFTP_HOST_KEY_FINGERPRINT.Contains('*')) { throw 'SFTP password and a pinned host key are required.' }
    $values
}

function Get-QaCompiledStamp([string]$Path, [string]$Executable) {
    $command = Get-Command $Executable -ErrorAction Stop
    $result = & $command.Source -NoLogo -NoProfile -File (Join-Path $PSScriptRoot 'read-vs-package-source.ps1') -Package $Path
    if ($LASTEXITCODE -ne 0) { throw 'Compiled package metadata verification failed.' }
    $stamp = ($result -join "`n").Trim()
    if ($stamp -notmatch '^[0-9a-f]{64}$') { throw 'Compiled package source stamp is invalid.' }
    $stamp
}

function New-QaSftpSession($Credentials, [string]$Assembly) {
    if ($PSVersionTable.PSEdition -ne 'Desktop') { throw 'Run staging with Windows PowerShell (powershell.exe) for WinSCP.' }
    $dll = Resolve-QaLocalPath $Assembly -Existing
    Add-Type -Path $dll
    $options = [WinSCP.SessionOptions]@{
        Protocol = [WinSCP.Protocol]::Sftp; HostName = $Credentials.SFTP_HOST; PortNumber = [int]$Credentials.SFTP_PORT
        UserName = $Credentials.SFTP_USERNAME; Password = $Credentials.SFTP_PASSWORD
        SshHostKeyFingerprint = $Credentials.SFTP_HOST_KEY_FINGERPRINT; Timeout = [TimeSpan]::FromSeconds(30)
    }
    $session = [WinSCP.Session]::new()
    try { $session.Open($options); $session } catch { $session.Dispose(); throw }
}

function Invoke-QaApi($Credentials, [string]$Operation, [string]$Method = 'Get', [string]$Body) {
    $parameters = @{
        Uri = 'https://pt.basicbit.net/api/client/servers/8982de16/' + $Operation
        Headers = @{ Authorization = 'Bearer ' + $Credentials.PTERO_TOKEN; Accept = 'application/json' }
        Method = $Method; TimeoutSec = 15
    }
    if ($Body) { $parameters.Body = $Body; $parameters.ContentType = 'application/json' }
    if ($Operation -like 'files/contents*') {
        return (Invoke-WebRequest @parameters -UseBasicParsing).Content
    }
    Invoke-RestMethod @parameters
}

function Assert-QaRemoteItem($Session, [string]$Path, [switch]$Directory) {
    $item = $Session.GetFileInfo($Path)
    if ($item.FileType -eq 'l' -or [bool]$item.IsDirectory -ne [bool]$Directory) { throw "Unsafe remote target: $Path" }
    $item
}

function Write-QaJson([string]$Path, $Value) {
    $Value | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $Path -Encoding UTF8
}

function Add-QaJournal([string]$Root, [string]$Action, [string]$Target, [string]$Status) {
    @{ utc = [DateTime]::UtcNow.ToString('o'); action = $Action; target = $Target; status = $Status } |
        ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $Root 'journal.jsonl') -Encoding UTF8
}

function Assert-QaModStillBackedUp($Session, $Entry, [string]$Root) {
    [void](Assert-QaRemoteItem $Session $Entry.target)
    $verification = Join-Path $Root 'verify-before.bin'
    $Session.GetFiles($Entry.target, $verification).Check()
    if ((Get-FileHash -LiteralPath $verification -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Entry.sha256) { throw 'Remote mod changed after backup.' }
    Remove-Item -LiteralPath $verification
}

function Invoke-QaPackageStage {
    $localPackage = Resolve-QaLocalPath $Package -Existing
    $localReceipt = Resolve-QaLocalPath $BuildReceipt -Existing
    $name = [IO.Path]::GetFileName($localPackage)
    if ($name -notmatch '^thebasics_[a-zA-Z0-9_]+\.zip$') { throw 'Unexpected BASICs package filename.' }
    $hash = (Get-FileHash -LiteralPath $localPackage -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $ExpectedSha256.ToLowerInvariant()) { throw 'Package SHA256 differs from the expected build.' }
    $built = Get-Content -LiteralPath $localReceipt -Raw | ConvertFrom-Json
    if ([IO.Path]::GetFullPath([string]$built.Package) -ne $localPackage -or $built.Sha256 -ne $hash -or $built.SourceTreeHash -ne $ExpectedSourceTreeHash) { throw 'Package differs from the canonical build receipt.' }
    if ((Get-QaCompiledStamp $localPackage $PowerShell7) -ne $ExpectedSourceTreeHash) { throw 'Compiled GUI source stamp differs from the expected source.' }
    $root = Resolve-QaLocalPath $OutputDirectory
    if (Test-Path -LiteralPath $root) { throw 'OutputDirectory must be new; backups are never overwritten.' }
    $profiles = @()
    foreach ($path in $ClientDataPaths) {
        $profile = Resolve-QaLocalPath $path -Existing
        if ($profile.TrimEnd('\', '/') -eq [IO.Path]::GetPathRoot($profile).TrimEnd('\', '/')) { throw 'A client data path cannot be a drive root.' }
        $mods = Resolve-QaLocalPath (Join-Path $profile 'Mods') -Existing
        if (!(Test-Path -LiteralPath $mods -PathType Container) -or $profiles.dataPath -contains $profile) { throw 'Client profiles must be distinct existing directories with Mods directories.' }
        if ($root -eq $profile -or $root.StartsWith($profile.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Backups must be outside the client profile.' }
        $oldFiles = @(Get-ChildItem -LiteralPath $mods -Force | Where-Object Name -like 'thebasics*.zip')
        foreach ($old in $oldFiles) {
            if ($old.PSIsContainer -or $old.Name -notmatch '^thebasics_[a-zA-Z0-9_]+\.zip$') { throw "Unsafe client BASICs filename: $($old.Name)" }
            [void](Resolve-QaLocalPath $old.FullName -Existing)
        }
        $profiles += @{ dataPath = $profile; modsPath = $mods; files = $oldFiles }
    }
    $credentials = Get-QaCredentials $CredentialFiles
    New-Item -ItemType Directory -Path $root | Out-Null
    $receipt = @{ schema = 1; server = '8982de16'; api = 'https://pt.basicbit.net'; package = $name; sha256 = $hash; sourceTreeHash = $ExpectedSourceTreeHash; status = 'preflight'; restartRequested = [bool]$Restart; backupDirectory = $root; clients = @($profiles.dataPath) }
    $index = @{ schema = 1; server = '8982de16'; files = @() }
    $session = $null
    $mutationStarted = $false
    try {
        Write-QaJson (Join-Path $root 'stage-receipt.json') $receipt
        Copy-Item -LiteralPath $localReceipt -Destination (Join-Path $root 'build-receipt.json')
        $packageCopy = Join-Path $root $name
        Copy-Item -LiteralPath $localPackage -Destination $packageCopy
        if ((Get-FileHash -LiteralPath $packageCopy -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Immutable staging copy differs from the build.' }
        $resources = Invoke-QaApi $credentials 'resources'
        $receipt.beforeState = $resources.attributes.current_state
        $session = New-QaSftpSession $credentials $WinScpAssembly
        foreach ($directory in @('/data', '/data/Mods', '/data/ModConfig', '/data/Logs')) { [void](Assert-QaRemoteItem $session $directory -Directory) }
        $remoteMods = @($session.ListDirectory('/data/Mods').Files | Where-Object Name -like 'thebasics*.zip')
        foreach ($old in $remoteMods) {
            if ($old.IsDirectory -or $old.FileType -eq 'l' -or $old.Name -notmatch '^thebasics_[a-zA-Z0-9_]+\.zip$') { throw 'Unsafe remote BASICs entry; nothing has been changed.' }
        }
        $serverBackup = Join-Path $root 'server'
        New-Item -ItemType Directory -Path $serverBackup | Out-Null
        $remotePaths = @($remoteMods | ForEach-Object { '/data/Mods/' + $_.Name }) + @('/data/ModConfig/the_basics.json', '/data/Logs/server-main.log')
        foreach ($remotePath in $remotePaths) {
            [void](Assert-QaRemoteItem $session $remotePath)
            $backup = Join-Path $serverBackup ([IO.Path]::GetFileName($remotePath))
            $session.GetFiles($remotePath, $backup).Check()
            $backupHash = (Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash.ToLowerInvariant()
            # Logs may keep growing; repeat-download verification is for files we may replace.
            if ($remotePath -like '/data/Mods/*' -or $remotePath -like '/data/ModConfig/*') {
                $verification = Join-Path $root 'verify-before.bin'
                $session.GetFiles($remotePath, $verification).Check()
                if ((Get-FileHash -LiteralPath $verification -Algorithm SHA256).Hash.ToLowerInvariant() -ne $backupHash) { throw 'Remote files changed during backup; retry into a new directory.' }
                Remove-Item -LiteralPath $verification
            }
            $index.files += @{ kind = 'server'; target = $remotePath; backup = $backup; sha256 = $backupHash }
        }
        for ($i = 0; $i -lt $profiles.Count; $i++) {
            $clientBackup = Join-Path $root ('client-' + $i)
            New-Item -ItemType Directory -Path $clientBackup | Out-Null
            foreach ($old in $profiles[$i].files) {
                $backup = Join-Path $clientBackup $old.Name
                $beforeHash = (Get-FileHash -LiteralPath $old.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
                Copy-Item -LiteralPath $old.FullName -Destination $backup
                if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash.ToLowerInvariant() -ne $beforeHash -or (Get-FileHash -LiteralPath $old.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ne $beforeHash) { throw 'Client file changed during backup.' }
                $index.files += @{ kind = 'client'; target = $old.FullName; backup = $backup; sha256 = $beforeHash }
            }
        }
        Write-QaJson (Join-Path $root 'backup-index.json') $index
        @'
Recovery after a partial stage

1. Read stage-receipt.json and journal.jsonl to identify started/completed mutations.
2. Stop the disposable QA server before restoring a server mod. Close only clients using the explicitly recorded data paths before restoring their mods.
3. Check each backup's SHA256 against backup-index.json. Restore every affected server/client mod from its exact backup path to its exact target path in that index (SFTP for server, Copy-Item -LiteralPath for client).
4. If the staged package target did not exist in backup-index.json, remove that exact new target after restoring the previous mod. Never delete unrelated mods.
5. Config and log backups are evidence; staging does not modify those files. Do not overwrite a live configuration/log as part of mod rollback.
6. Restart QA and relaunch the recorded profiles only after their previous package set has been restored and verified. Preserve this directory for diagnosis.

No rollback happens automatically. A started journal entry without completed status may have partially written its target. Credentials are not stored here.
'@ | Set-Content -LiteralPath (Join-Path $root 'RESTORE.md') -Encoding UTF8
        $receipt.status = 'backed-up'
        Write-QaJson (Join-Path $root 'stage-receipt.json') $receipt

        # Verify the preflight still describes the exact files about to be overwritten/deleted.
        $nowNames = @($session.ListDirectory('/data/Mods').Files | Where-Object Name -like 'thebasics*.zip' | Select-Object -ExpandProperty Name | Sort-Object)
        $beforeNames = @($remoteMods.Name | Sort-Object)
        if (($nowNames -join "`n") -ne ($beforeNames -join "`n")) { throw 'Remote mod set changed after backup.' }
        foreach ($entry in $index.files | Where-Object { $_.kind -eq 'server' -and $_.target -like '/data/Mods/*' }) {
            Assert-QaModStillBackedUp $session $entry $root
        }
        foreach ($profile in $profiles) {
            [void](Resolve-QaLocalPath $profile.modsPath -Existing)
            $nowNames = @(Get-ChildItem -LiteralPath $profile.modsPath -Force | Where-Object Name -like 'thebasics*.zip' | Select-Object -ExpandProperty Name | Sort-Object)
            if (($nowNames -join "`n") -ne (@($profile.files.Name | Sort-Object) -join "`n")) { throw 'Client mod set changed after backup.' }
            foreach ($entry in $index.files | Where-Object { $_.kind -eq 'client' -and [IO.Path]::GetDirectoryName($_.target) -eq $profile.modsPath }) {
                [void](Resolve-QaLocalPath $entry.target -Existing)
                if ((Get-FileHash -LiteralPath $entry.target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw 'Client mod changed after backup.' }
            }
        }
        $remoteTarget = '/data/Mods/' + $name
        $mutationStarted = $true
        Add-QaJournal $root 'upload' $remoteTarget 'started'
        $session.PutFiles($packageCopy, $remoteTarget).Check()
        $readback = Join-Path $root 'server-upload-readback.zip'
        $session.GetFiles($remoteTarget, $readback).Check()
        if ((Get-FileHash -LiteralPath $readback -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Server uploaded bytes differ from the build; old versions remain available.' }
        Add-QaJournal $root 'upload' $remoteTarget 'completed'
        foreach ($old in $remoteMods | Where-Object Name -ne $name) {
            $target = '/data/Mods/' + $old.Name
            $entry = $index.files | Where-Object { $_.kind -eq 'server' -and $_.target -eq $target }
            Assert-QaModStillBackedUp $session $entry $root
            Add-QaJournal $root 'remove-old' $target 'started'
            $session.RemoveFiles($target).Check()
            Add-QaJournal $root 'remove-old' $target 'completed'
        }
        foreach ($profile in $profiles) {
            $target = Join-Path $profile.modsPath $name
            [void](Resolve-QaLocalPath $target)
            Add-QaJournal $root 'copy-client' $target 'started'
            Copy-Item -LiteralPath $packageCopy -Destination $target -Force
            if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Client staged bytes differ from the build.' }
            Add-QaJournal $root 'copy-client' $target 'completed'
            foreach ($old in $profile.files | Where-Object Name -ne $name) {
                [void](Resolve-QaLocalPath $old.FullName -Existing)
                $entry = $index.files | Where-Object { $_.kind -eq 'client' -and $_.target -eq $old.FullName }
                if ((Get-FileHash -LiteralPath $old.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -ne $entry.sha256) { throw 'Client mod changed after backup.' }
                Add-QaJournal $root 'remove-old' $old.FullName 'started'
                Remove-Item -LiteralPath $old.FullName
                Add-QaJournal $root 'remove-old' $old.FullName 'completed'
            }
        }
        $receipt.status = 'staged'
        Write-QaJson (Join-Path $root 'stage-receipt.json') $receipt
        if ($Restart) {
            $beforeLog = [IO.File]::ReadAllText((Join-Path $serverBackup 'server-main.log'))
            $beforeMarkers = @([regex]::Matches($beforeLog, '(?m)^.*Entering runphase RunGame.*$') | ForEach-Object Value)
            Add-QaJournal $root 'restart' '8982de16' 'started'
            [void](Invoke-QaApi $credentials 'power' 'Post' '{"signal":"restart"}')
            $deadline = [DateTime]::UtcNow.AddSeconds($RestartTimeoutSeconds)
            do {
                Start-Sleep -Seconds 2
                $state = (Invoke-QaApi $credentials 'resources').attributes.current_state
                $log = [string](Invoke-QaApi $credentials 'files/contents?file=%2Fdata%2FLogs%2Fserver-main.log')
                $marker = @([regex]::Matches($log, '(?m)^.*Entering runphase RunGame.*$') | ForEach-Object Value | Where-Object { $_ -notin $beforeMarkers } | Select-Object -Last 1)
                if ($state -eq 'running' -and $marker.Count -eq 1) { break }
                if ([DateTime]::UtcNow -ge $deadline) { throw 'Restart did not produce running state plus a fresh RunGame marker within the deadline.' }
            } while ($true)
            [IO.File]::WriteAllText((Join-Path $root 'server-main.after.log'), $log)
            $receipt.afterState = $state
            $receipt.freshRunGameMarker = $marker[0].Trim()
            $receipt.status = 'restarted'
            Add-QaJournal $root 'restart' '8982de16' 'completed'
        }
        Write-QaJson (Join-Path $root 'stage-receipt.json') $receipt
        [pscustomobject]$receipt
    } catch {
        $failure = $_.Exception.GetBaseException().Message
        foreach ($secret in @($credentials.PTERO_TOKEN, $credentials.SFTP_PASSWORD, $credentials.SFTP_USERNAME) | Where-Object { $_ }) { $failure = $failure.Replace($secret, '[redacted]') }
        $receipt.status = 'failed'
        $receipt.mutationStarted = $mutationStarted
        $receipt.error = $failure
        try { Write-QaJson (Join-Path $root 'stage-receipt.json') $receipt } catch { Write-Warning 'Could not write the failure receipt; preserve the journal and backup directory.' }
        throw "QA staging failed. Recovery evidence: $root. $failure"
    } finally { if ($session) { $session.Dispose() } }
}

if ($MyInvocation.InvocationName -ne '.') { Invoke-QaPackageStage }
