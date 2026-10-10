#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DataPath,
    [Parameter(Mandatory)][string]$GameInstall,
    [Parameter(Mandatory)][string]$OutputRoot,
    [string]$VsCtlPath,
    [ValidatePattern('^[A-Za-z0-9.-]+:[0-9]{1,5}$')][string]$ServerAddress,
    [switch]$RestartOwnedClient,
    [switch]$StopOnly,
    [switch]$ShowWindow,
    [ValidateRange(1, 300)][int]$MenuTimeoutSeconds = 90,
    [ValidateRange(1, 300)][int]$BridgeTimeoutSeconds = 120,
    [ValidateRange(1, 30)][int]$RpcTimeoutSeconds = 8
)

$ErrorActionPreference = 'Stop'
if ($StopOnly -and $ServerAddress) { throw '-StopOnly cannot forward a server connection.' }
$profile = (Resolve-Path -LiteralPath $DataPath).Path.TrimEnd('\', '/')
if ([IO.Path]::GetFileName($profile) -cne 'Profile2') { throw 'Only the disposable Profile2 client is supported.' }
$game = (Resolve-Path -LiteralPath $GameInstall).Path
$exe = Join-Path $game 'Vintagestory.exe'
if (!(Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Vintagestory.exe is missing from GameInstall.' }
$gameVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
if (!$StopOnly -and $gameVersion -ne '1.22.7') { throw 'Menu-first startup is verified for Vintage Story 1.22.7. Recheck the native startup marker before supporting another version.' }
if ($ServerAddress -and (!$VsCtlPath -or !(Test-Path -LiteralPath $VsCtlPath -PathType Leaf))) { throw 'Provide the recovered vsctl executable when connecting.' }
if ($ServerAddress) {
    $serverPort = [int]($ServerAddress.Split(':')[-1])
    if ($serverPort -lt 1 -or $serverPort -gt 65535) { throw 'Server port must be between 1 and 65535.' }
}
foreach ($target in @($profile, $game, $exe)) {
    $candidate = $target
    while ($candidate) {
        if ((Get-Item -LiteralPath $candidate).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Reparse points are not supported: $candidate" }
        $candidate = [IO.Path]::GetDirectoryName($candidate)
    }
}
$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw 'Use a new output directory, launch receipts are never overwritten.' }
if ($output.StartsWith($profile + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Keep launch output outside Profile2.' }
$candidate = [IO.Path]::GetDirectoryName($output)
while ($candidate) {
    if ((Test-Path -LiteralPath $candidate) -and ((Get-Item -LiteralPath $candidate).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse points are not supported: $candidate" }
    $candidate = [IO.Path]::GetDirectoryName($candidate)
}
$profileArgument = '(?i)(?:^|\s)--dataPath(?:=|\s+)(?:"(?<path>[^"]+)"|(?<path>[^\s]+))'

function Test-OwnedClient($Client) {
    if (!$Client -or !$Client.CommandLine -or !$Client.ExecutablePath -or
        ![string]::Equals($Client.ExecutablePath, $exe, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    if ($Client.CommandLine -notmatch $profileArgument) { return $false }
    return [IO.Path]::GetFullPath($Matches.path).TrimEnd('\', '/') -eq $profile
}

function Get-Clients { @(Get-CimInstance Win32_Process -Filter "Name='Vintagestory.exe'") }

function Assert-OnlyOwnedClients([int]$ExpectedId = 0) {
    $clients = @(Get-Clients)
    if (@($clients | Where-Object { !(Test-OwnedClient $_) }).Count) {
        throw 'Close other or unidentifiable clients before launching or using the global connection-forwarding pipe.'
    }
    if ($ExpectedId -and ($clients.Count -ne 1 -or $clients[0].ProcessId -ne $ExpectedId)) { throw 'The launched Profile2 client is no longer the sole client.' }
    return $clients
}

function Save-Receipt { $receipt | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $receiptPath -Encoding utf8NoBOM }

function Invoke-Bridge([string]$Command, [int]$RemainingMilliseconds) {
    $stdout = Join-Path $output ($Command + '-' + [Guid]::NewGuid().ToString('N') + '.stdout.json')
    $stderr = [IO.Path]::ChangeExtension($stdout, '.stderr.log')
    $rpc = Start-Process -FilePath $VsCtlPath -ArgumentList @($Command, '--pipe', 'vintage-story-agentcontrol-profile2') -WorkingDirectory (Split-Path -Parent $VsCtlPath) -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    $timeout = [Math]::Max(1, [Math]::Min($RpcTimeoutSeconds * 1000, $RemainingMilliseconds))
    if (!$rpc.WaitForExit($timeout)) {
        $rpc.Kill()
        $rpc.WaitForExit(2000) | Out-Null
        throw "Agent Control $Command exceeded its bounded RPC deadline."
    }
    if ($rpc.ExitCode -ne 0) { return $null }
    $response = Get-Content -LiteralPath $stdout -Raw | ConvertFrom-Json
    if (!$response.ok) { return $null }
    return $response.result
}

$clients = @(Assert-OnlyOwnedClients)
if ($clients.Count -and !$RestartOwnedClient -and !$StopOnly) { throw 'Profile2 is already running; use -RestartOwnedClient to relaunch its exact owned process.' }
if ($clients.Count -gt 1) { throw 'Multiple clients claim Profile2; resolve ownership before restarting.' }
New-Item -ItemType Directory -Path $output | Out-Null
$receiptPath = Join-Path $output 'launch-receipt.json'
$receipt = [ordered]@{ schema = 1; state = 'starting'; profile = $profile; executable = $exe; gameVersion = $gameVersion; startedAtUtc = [DateTime]::UtcNow.ToString('o'); serverAddress = $ServerAddress; stoppedProcessIds = @(); processId = $null }
Save-Receipt
try {
    foreach ($client in $clients) {
        $current = Get-CimInstance Win32_Process -Filter "ProcessId=$($client.ProcessId)"
        if (!(Test-OwnedClient $current) -or $current.CreationDate -ne $client.CreationDate) { throw 'A process changed ownership before the Profile2 stop.' }
        Stop-Process -Id $current.ProcessId -Force
        Wait-Process -Id $current.ProcessId -Timeout 20 -ErrorAction SilentlyContinue
        if (Get-CimInstance Win32_Process -Filter "ProcessId=$($current.ProcessId)") { throw 'Owned Profile2 process did not stop.' }
        $receipt.stoppedProcessIds += $current.ProcessId
    }
    if (@(Assert-OnlyOwnedClients).Count) { throw 'A client appeared before launch.' }
    if ($StopOnly) {
        $receipt.state = 'stopped'
        $receipt.stoppedAtUtc = [DateTime]::UtcNow.ToString('o')
        Save-Receipt
        Write-Output "Profile2 stopped without relaunch; receipt: $receiptPath"
        return
    }
    $windowStyle = if ($ShowWindow) { 'Normal' } else { 'Hidden' }
    $menuLog = Join-Path $output 'menu.stdout.log'
    $process = Start-Process -FilePath $exe -ArgumentList "--dataPath `"$profile`" --fullscreen off" -WorkingDirectory $game -WindowStyle $windowStyle -RedirectStandardOutput $menuLog -RedirectStandardError (Join-Path $output 'menu.stderr.log') -PassThru
    $receipt.processId = $process.Id
    $receipt.state = 'waiting-for-menu'
    Save-Receipt
    $deadline = [DateTime]::UtcNow.AddSeconds($MenuTimeoutSeconds)
    do {
        if ($process.HasExited) { throw 'Profile2 exited before the menu was ready.' }
        $log = if (Test-Path -LiteralPath $menuLog) { Get-Content -LiteralPath $menuLog -Raw } else { '' }
        # Native 1.22.7 Stage2 registers controls; Stage4 emits this profile path when loading menu mod metadata.
        if ($log -match 'Loaded Shaderprogramm for render pass guigear\.' -and
            $log -match 'Will search the following paths for mods:' -and
            $log.Contains((Join-Path $profile 'Mods'))) { break }
        if ([DateTime]::UtcNow -ge $deadline) { throw 'Profile2 menu-ready log deadline expired; connection was not forwarded.' }
        Start-Sleep -Milliseconds 250
    } while ($true)
    Assert-OnlyOwnedClients $process.Id | Out-Null
    $receipt.menuReadyAtUtc = [DateTime]::UtcNow.ToString('o')
    $receipt.state = 'menu-ready'
    Save-Receipt
    if (!$ServerAddress) { Write-Output "Profile2 menu ready; receipt: $receiptPath"; return }
    $config = Get-Content -LiteralPath (Join-Path $profile 'ModConfig/agentcontrol.json') -Raw | ConvertFrom-Json
    if (!$config.EnableOnStartup -or !$config.GrantMutationOnEnable -or $config.PipeName -ne 'vintage-story-agentcontrol-profile2') { throw 'Prepare the Profile2 startup bridge before unattended connection.' }
    Assert-OnlyOwnedClients $process.Id | Out-Null
    $forwarder = Start-Process -FilePath $exe -ArgumentList "--dataPath `"$profile`" --fullscreen off -c $ServerAddress" -WorkingDirectory $game -WindowStyle Hidden -PassThru
    if (!$forwarder.WaitForExit(15000)) {
        $forwarder.Kill()
        $forwarder.WaitForExit(2000) | Out-Null
        throw 'Connection forwarding did not exit; inspect the launcher receipt before retrying.'
    }
    if ($forwarder.ExitCode -ne 0) { throw 'Connection-forwarding client returned an error.' }
    $receipt.state = 'waiting-for-bridge'
    Save-Receipt
    $deadline = [DateTime]::UtcNow.AddSeconds($BridgeTimeoutSeconds)
    do {
        if ($process.HasExited) { throw 'Profile2 exited before the bridge was ready.' }
        Assert-OnlyOwnedClients $process.Id | Out-Null
        $remaining = [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds
        if ($remaining -le 0) { throw 'Profile2 enabled bridge deadline expired.' }
        $hello = Invoke-Bridge 'hello' $remaining
        if ($hello) {
            if ($hello.protocolVersion -ne '1.0' -or !$hello.mutationGranted) { throw 'Agent Control protocol or mutation grant is invalid.' }
            $remaining = [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds
            if ($remaining -le 0) { throw 'Profile2 enabled bridge deadline expired.' }
            $observation = Invoke-Bridge 'observe' $remaining
            if ($observation -and $observation.connected) { break }
        }
        Start-Sleep -Milliseconds 250
    } while ($true)
    # Persist only redacted CLI receipts; session secrets stay inside vsctl.
    $hello | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $output 'hello.json') -Encoding utf8NoBOM
    $observation | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $output 'observe.json') -Encoding utf8NoBOM
    $receipt.state = 'connected'
    $receipt.bridgeReadyAtUtc = [DateTime]::UtcNow.ToString('o')
    Save-Receipt
    Write-Output "Profile2 connected with an enabled bridge; receipt: $receiptPath"
} catch {
    $receipt.state = 'failed'
    $receipt.error = $_.Exception.Message
    Save-Receipt
    throw
}
