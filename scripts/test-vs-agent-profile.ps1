#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$AgentControlSource,
    [string]$OutputRoot = (Join-Path (Split-Path -Parent $PSScriptRoot) ('.tmp/profile-workflow-tests-' + [Guid]::NewGuid().ToString('N')))
)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputRoot) { throw 'Use a new test output directory.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$testRoot = (Resolve-Path -LiteralPath $OutputRoot).Path
$prepare = Join-Path $PSScriptRoot 'prepare-vs-agent-profile.ps1'
$launch = Join-Path $PSScriptRoot 'start-vs-agent-client.ps1'
$game = Join-Path $testRoot 'game'
New-Item -ItemType Directory -Path (Join-Path $game 'Lib') | Out-Null
foreach ($file in @('Vintagestory.exe', 'VintagestoryAPI.dll', 'Lib/protobuf-net.dll', 'vsctl.exe')) { Set-Content -LiteralPath (Join-Path $game $file) -Value 'fixture' }
$global:VsAgentProfileTest_checks = 0
$global:VsAgentProfileTest_clients = @()
$global:VsAgentProfileTest_nativeCalls = @()
$global:VsAgentProfileTest_dotnetCalls = @()
$global:VsAgentProfileTest_stopIds = @()
$global:VsAgentProfileTest_nextId = 100
$global:VsAgentProfileTest_mode = 'ready'
$global:VsAgentProfileTest_rpcKilled = $false
$global:VsAgentProfileTest_failCopy = $false
$global:VsAgentProfileTest_gameVersion = '1.22.7'
$global:VsAgentProfileTest_missingAcceptance = $false

function Assert([bool]$Condition, [string]$Message) { if (!$Condition) { throw $Message }; $global:VsAgentProfileTest_checks++ }
function Reject([scriptblock]$Action, [string]$Pattern) {
    try { & $Action | Out-Null } catch { Assert ($_.Exception.Message -match $Pattern) "Unexpected rejection: $($_.Exception.Message)"; return }
    throw "Expected rejection matching: $Pattern"
}
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path).Hash }
function New-Profile([string]$Name, [switch]$Empty) {
    $path = Join-Path $testRoot ($Name + '/Profile2')
    New-Item -ItemType Directory -Path (Join-Path $path 'Mods'), (Join-Path $path 'ModConfig') | Out-Null
    if (!$Empty) {
        Set-Content -LiteralPath (Join-Path $path 'ModConfig/agentcontrol.json') -Value '{"PipeName":"old","EnableOnStartup":false,"GrantMutationOnEnable":false,"unknown":{"keep":[1,"yes"]}}'
        $metadata = Join-Path $testRoot 'modinfo.json'
        Set-Content -LiteralPath $metadata -Value '{"modid":"agentcontrol","version":"0.0.9"}'
        Compress-Archive -LiteralPath $metadata -DestinationPath (Join-Path $path 'Mods/renamed-helper.zip')
    }
    Set-Content -LiteralPath (Join-Path $path 'Mods/unrelated.txt') -Value 'preserve me'
    return $path
}
function Client([int]$Id, [string]$Profile, [string]$Executable = (Join-Path $game 'Vintagestory.exe')) {
    [pscustomobject]@{ ProcessId = $Id; Name = 'Vintagestory.exe'; ExecutablePath = $Executable; CommandLine = "`"$Executable`" --dataPath `"$Profile`" --fullscreen off"; CreationDate = "created-$Id" }
}

# These mocks execute the workflow against real temporary files, without launching or stopping native clients.
function Get-CimInstance {
    param([string]$ClassName, [string]$Filter)
    if ($Filter -match '^ProcessId=(\d+)$') {
        if ($global:VsAgentProfileTest_mode -eq 'reused-pid') { return Client ([int]$Matches[1]) (Join-Path $testRoot 'other/Profile2') }
        return @($global:VsAgentProfileTest_clients | Where-Object ProcessId -eq ([int]$Matches[1]))
    }
    return $global:VsAgentProfileTest_clients
}
function Stop-Process { param([int]$Id, [switch]$Force); $global:VsAgentProfileTest_stopIds += $Id; $global:VsAgentProfileTest_clients = @($global:VsAgentProfileTest_clients | Where-Object ProcessId -ne $Id) }
function Wait-Process { param([int]$Id, [int]$Timeout, $ErrorAction) }
function Get-Item {
    param([string]$LiteralPath)
    $item = Microsoft.PowerShell.Management\Get-Item -LiteralPath $LiteralPath
    if ($LiteralPath -eq (Join-Path $game 'Vintagestory.exe')) {
        return [pscustomobject]@{ Attributes = $item.Attributes; VersionInfo = @{ FileVersion = $global:VsAgentProfileTest_gameVersion } }
    }
    return $item
}
function Copy-Item {
    param([string]$LiteralPath, [string]$Destination, [switch]$Force)
    if ($global:VsAgentProfileTest_failCopy -and $Destination -match 'Profile2[/\\]Mods[/\\]agentcontrol_0_1_0.zip$') { $global:VsAgentProfileTest_failCopy = $false; throw 'Injected staging copy failure' }
    Microsoft.PowerShell.Management\Copy-Item -LiteralPath $LiteralPath -Destination $Destination -Force:$Force
}
function Test-DotNet {
    $arguments = @($args)
    $global:VsAgentProfileTest_dotnetCalls += ,$arguments
    $global:LASTEXITCODE = 0
    if ($arguments[0] -eq '--list-sdks') { '10.0.300 [mock-sdk]'; return }
    if ($arguments[0] -eq 'test') {
        $results = $arguments[[Array]::IndexOf($arguments, '--results-directory') + 1]
        New-Item -ItemType Directory -Force -Path $results | Out-Null
        $cases = @('StartupEnable_WaitsForReadinessAndCannotUndoCancellationOrDisable', 'TerminalExecution_CancelsExtensionOwnerWithoutLosingItsReceipt(completed,false)', 'TerminalExecution_CancelsExtensionOwnerWithoutLosingItsReceipt(failed,false)', 'TerminalExecution_CancelsExtensionOwnerWithoutLosingItsReceipt(timed_out,false)', 'TerminalExecution_CancelsExtensionOwnerWithoutLosingItsReceipt(cancelled,false)', 'TerminalExecution_CancelsExtensionOwnerWithoutLosingItsReceipt(completed,true)')
        if ($global:VsAgentProfileTest_missingAcceptance) { $cases = @('UnrelatedCase') }
        $records = @($cases | ForEach-Object { '<UnitTestResult testName="' + $_ + '" outcome="Passed" />' }) -join ''
        Set-Content -LiteralPath (Join-Path $results 'agentcontrol.trx') -Value ('<TestRun><Results>' + $records + '</Results><ResultSummary><Counters passed="23" failed="0" notExecuted="0" /></ResultSummary></TestRun>')
    }
    if ($arguments[0] -in @('test', 'build')) {
        foreach ($project in @('agentcontrol', 'agentcontrol.abstractions')) {
            $bin = Join-Path (Get-Location).Path "mods-dll/$project/bin/Release/net10.0"
            New-Item -ItemType Directory -Force -Path $bin | Out-Null
            foreach ($suffix in @('dll', 'pdb')) { Set-Content -LiteralPath (Join-Path $bin "$project.$suffix") -Value 'fixture assembly' }
        }
    }
    if ($arguments[0] -eq 'publish') {
        $destination = $arguments[[Array]::IndexOf($arguments, '--output') + 1]
        New-Item -ItemType Directory -Force -Path $destination | Out-Null
        Set-Content -LiteralPath (Join-Path $destination 'vsctl.exe') -Value 'fixture CLI'
    }
}
function Start-Process {
    param([string]$FilePath, $ArgumentList, [string]$WorkingDirectory, [string]$WindowStyle, [string]$RedirectStandardOutput, [string]$RedirectStandardError, [switch]$PassThru)
    $global:VsAgentProfileTest_nativeCalls += [pscustomobject]@{ file = $FilePath; arguments = $ArgumentList; style = $WindowStyle }
    $global:VsAgentProfileTest_nextId++
    $process = [pscustomobject]@{ Id = $global:VsAgentProfileTest_nextId; HasExited = $false; ExitCode = 0; IsRpc = $false; IsForward = $false }
    if ([IO.Path]::GetFileName($FilePath) -eq 'vsctl.exe') {
        $process.IsRpc = $true
        $result = if ($ArgumentList[0] -eq 'hello') {
            @{ protocolVersion = '1.0'; mutationGranted = ($global:VsAgentProfileTest_mode -ne 'no-grant'); session = '[redacted]' }
        } else { @{ connected = ($global:VsAgentProfileTest_mode -ne 'disconnected') } }
        @{ ok = $true; result = $result } | ConvertTo-Json | Set-Content -LiteralPath $RedirectStandardOutput
        Set-Content -LiteralPath $RedirectStandardError -Value ''
    } elseif ($ArgumentList -match ' -c ') { $process.IsForward = $true }
    else {
        $ArgumentList -match '--dataPath "(?<profile>[^"]+)"' | Out-Null
        $global:VsAgentProfileTest_launchedProfile = $Matches.profile
        $global:VsAgentProfileTest_menuLog = $RedirectStandardOutput
        $global:VsAgentProfileTest_menuProcess = $process
        $global:VsAgentProfileTest_clients += Client $process.Id $global:VsAgentProfileTest_launchedProfile
        Set-Content -LiteralPath $RedirectStandardOutput -Value 'Loading shaders...'
    }
    $process | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value {
        param([int]$Timeout)
        if ($this.IsRpc -and $global:VsAgentProfileTest_mode -eq 'rpc-hang' -and !$global:VsAgentProfileTest_rpcKilled) { return $false }
        $this.HasExited = $true
        return $true
    }
    $process | Add-Member -MemberType ScriptMethod -Name Kill -Value { $global:VsAgentProfileTest_rpcKilled = $true; $this.HasExited = $true }
    return $process
}
function Start-Sleep {
    param([int]$Milliseconds)
    if ($global:VsAgentProfileTest_menuLog -and $global:VsAgentProfileTest_mode -ne 'menu-timeout') {
        @('Loaded Shaderprogramm for render pass guigear.', 'Will search the following paths for mods:', (Join-Path $global:VsAgentProfileTest_launchedProfile 'Mods')) | Set-Content -LiteralPath $global:VsAgentProfileTest_menuLog
        if ($global:VsAgentProfileTest_mode -eq 'other-appears' -and $global:VsAgentProfileTest_clients.Count -eq 1) { $global:VsAgentProfileTest_clients += Client 999 (Join-Path $testRoot 'other/Profile20') }
    }
    Microsoft.PowerShell.Utility\Start-Sleep -Milliseconds 10
}
function Prepare-Fixture([string]$Profile, [string]$Name) {
    & $prepare -AgentControlSource $AgentControlSource -DataPath $Profile -GameInstall $game -OutputRoot (Join-Path $testRoot $Name) -DotNetPath Test-DotNet | Out-Null
    return Join-Path $testRoot "$Name/profile-receipt.json"
}
function Launch-Fixture([string]$Profile, [string]$Name, [switch]$Restart) {
    & $launch -DataPath $Profile -GameInstall $game -OutputRoot (Join-Path $testRoot $Name) -VsCtlPath (Join-Path $game 'vsctl.exe') -ServerAddress 'qa.invalid:30000' -RestartOwnedClient:$Restart -MenuTimeoutSeconds 1 -BridgeTimeoutSeconds 1
}

$profile = New-Profile 'main'
$configPath = Join-Path $profile 'ModConfig/agentcontrol.json'
$oldHelper = Join-Path $profile 'Mods/renamed-helper.zip'
$configHash = Hash $configPath
$helperHash = Hash $oldHelper
$receiptPath = Prepare-Fixture $profile 'prepare'
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
$installedConfigBytes = [IO.File]::ReadAllBytes($configPath)
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
Assert ($receipt.state -eq 'installed' -and $receipt.testsPassed -eq 23) 'Preparation did not save a complete installation receipt.'
Assert ($config.EnableOnStartup -and $config.GrantMutationOnEnable -and $config.MaxBatchDurationMs -eq 60000) 'Startup grant/timing differs.'
Assert ($config.unknown.keep[1] -eq 'yes') 'Unknown config fields were lost.'
Assert (!(Test-Path -LiteralPath $oldHelper)) 'Renamed older helper was not replaced.'
Assert ((Hash (Join-Path $profile 'Mods/agentcontrol_0_1_0.zip')).ToLowerInvariant() -eq $receipt.packageSha256) 'Staged package hash differs.'
Assert ((Get-Content -LiteralPath (Join-Path $profile 'Mods/unrelated.txt') -Raw).Trim() -eq 'preserve me') 'An unrelated mod changed.'
Reject { & $prepare -AgentControlSource $AgentControlSource -DataPath $profile -GameInstall $game -OutputRoot (Join-Path $testRoot 'prepare') -DotNetPath Test-DotNet } 'new output directory'
$existingBackup = Join-Path $testRoot 'existing-backup'
New-Item -ItemType Directory -Path $existingBackup | Out-Null
$beforeCalls = $global:VsAgentProfileTest_dotnetCalls.Count
Reject { & $prepare -AgentControlSource $AgentControlSource -DataPath $profile -GameInstall $game -OutputRoot (Join-Path $testRoot 'existing-backup-case') -BackupRoot $existingBackup -DotNetPath Test-DotNet } 'new backup directory'
Assert ($global:VsAgentProfileTest_dotnetCalls.Count -eq $beforeCalls) 'An existing backup refusal unnecessarily built or changed files.'
$global:VsAgentProfileTest_missingAcceptance = $true
Reject { Prepare-Fixture $profile 'missing-acceptance' } 'required startup and terminal cancellation'
Assert ((Hash $configPath).ToLowerInvariant() -eq @($receipt.files | Where-Object relativePath -eq 'ModConfig/agentcontrol.json')[0].afterSha256) 'A missing native acceptance case modified the installed profile.'
$global:VsAgentProfileTest_missingAcceptance = $false

$collision = New-Profile 'collision' -Empty
$collisionMetadata = Join-Path $testRoot 'collision/modinfo.json'
Set-Content -LiteralPath $collisionMetadata -Value '{"modid":"unrelated","version":"1.0.0"}'
$collisionPackage = Join-Path $collision 'Mods/agentcontrol_0_1_0.zip'
Compress-Archive -LiteralPath $collisionMetadata -DestinationPath $collisionPackage
$collisionHash = Hash $collisionPackage
$beforeCalls = $global:VsAgentProfileTest_dotnetCalls.Count
Reject { Prepare-Fixture $collision 'prepare-collision' } 'belongs to another mod'
Assert ((Hash $collisionPackage) -eq $collisionHash -and $global:VsAgentProfileTest_dotnetCalls.Count -eq $beforeCalls) 'Preparation overwrote another mod at the reserved helper filename.'

$global:VsAgentProfileTest_clients = @(Client 12 $profile)
$beforeCalls = $global:VsAgentProfileTest_dotnetCalls.Count
Reject { Prepare-Fixture $profile 'running' } 'Profile2 is running'
Assert ($global:VsAgentProfileTest_dotnetCalls.Count -eq $beforeCalls -and !(Test-Path -LiteralPath (Join-Path $testRoot 'running'))) 'Running-profile refusal changed files or built source.'
$global:VsAgentProfileTest_clients = @()
Set-Content -LiteralPath $configPath -Value '{"userEdit":true}'
Reject { & $prepare -DataPath $profile -RestoreReceipt $receiptPath } 'Profile changed'
Assert ((Get-Content -LiteralPath $configPath -Raw) -match 'userEdit') 'Restore overwrote a later edit.'
[IO.File]::WriteAllBytes($configPath, $installedConfigBytes)
$installedRaw = Get-Content -LiteralPath $configPath -Raw
$backupHelper = Join-Path $receipt.backupRoot 'Mods/renamed-helper.zip'
$backupBytes = [IO.File]::ReadAllBytes($backupHelper)
Set-Content -LiteralPath $backupHelper -Value 'corrupted'
Reject { & $prepare -DataPath $profile -RestoreReceipt $receiptPath } 'backup.*changed'
Assert ((Get-Content -LiteralPath $configPath -Raw) -ceq $installedRaw) 'Invalid backup caused partial restoration.'
[IO.File]::WriteAllBytes($backupHelper, $backupBytes)
& $prepare -DataPath $profile -RestoreReceipt $receiptPath | Out-Null
Assert ((Hash $configPath) -eq $configHash -and (Hash $oldHelper) -eq $helperHash) 'Exact original bytes were not restored.'
Assert (!(Test-Path -LiteralPath (Join-Path $profile 'Mods/agentcontrol_0_1_0.zip'))) 'Restoration left a newly installed helper.'

$empty = New-Profile 'empty' -Empty
$emptyReceipt = Prepare-Fixture $empty 'prepare-empty'
& $prepare -DataPath $empty -RestoreReceipt $emptyReceipt | Out-Null
Assert (!(Test-Path -LiteralPath (Join-Path $empty 'ModConfig/agentcontrol.json'))) 'Restore did not preserve originally missing config.'
$rollback = New-Profile 'rollback'
$rollbackHash = Hash (Join-Path $rollback 'Mods/renamed-helper.zip')
$global:VsAgentProfileTest_failCopy = $true
Reject { Prepare-Fixture $rollback 'prepare-rollback' } 'preparation failed'
Assert ((Hash (Join-Path $rollback 'Mods/renamed-helper.zip')) -eq $rollbackHash) 'Staging failure lost the original helper.'
Assert ((Get-Content -LiteralPath (Join-Path $testRoot 'prepare-rollback/profile-receipt.json') -Raw | ConvertFrom-Json).state -eq 'rolled-back') 'Failure receipt did not identify rollback.'

$launchProfile = New-Profile 'launch' -Empty
@{ PipeName='vintage-story-agentcontrol-profile2'; EnableOnStartup=$true; GrantMutationOnEnable=$true } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $launchProfile 'ModConfig/agentcontrol.json')
$global:VsAgentProfileTest_gameVersion = '1.23.0'
Reject { Launch-Fixture $launchProfile 'launch-unknown-version' } 'verified for Vintage Story 1.22.7'
Assert ($global:VsAgentProfileTest_nativeCalls.Count -eq 0) 'An unverified game version was launched.'
$global:VsAgentProfileTest_gameVersion = '1.22.7'
$global:VsAgentProfileTest_clients = @(Client 19 $launchProfile (Join-Path $testRoot 'different-install/Vintagestory.exe'))
Reject { Launch-Fixture $launchProfile 'launch-wrong-exe' -Restart } 'other or unidentifiable'
Assert ($global:VsAgentProfileTest_stopIds.Count -eq 0) 'A different game executable was stopped.'
$global:VsAgentProfileTest_clients = @(Client 20 ($launchProfile + '0'))
Reject { Launch-Fixture $launchProfile 'launch-other' } 'other or unidentifiable'
Assert ($global:VsAgentProfileTest_stopIds.Count -eq 0 -and $global:VsAgentProfileTest_nativeCalls.Count -eq 0) 'Another profile was stopped or launch was attempted.'
$global:VsAgentProfileTest_clients = @(Client 21 $launchProfile)
Reject { Launch-Fixture $launchProfile 'launch-no-restart' } 'already running'
$global:VsAgentProfileTest_mode = 'reused-pid'
Reject { Launch-Fixture $launchProfile 'launch-reused-pid' -Restart } 'changed ownership'
Assert ($global:VsAgentProfileTest_stopIds.Count -eq 0) 'A reused PID was stopped.'
$global:VsAgentProfileTest_mode = 'ready'
Launch-Fixture $launchProfile 'launch-success' -Restart | Out-Null
Assert ($global:VsAgentProfileTest_stopIds.Count -eq 1 -and $global:VsAgentProfileTest_stopIds[0] -eq 21) 'Restart was not scoped to the owned client.'
$launchReceipt = Get-Content -LiteralPath (Join-Path $testRoot 'launch-success/launch-receipt.json') -Raw | ConvertFrom-Json
Assert ($launchReceipt.state -eq 'connected' -and $launchReceipt.menuReadyAtUtc -and $launchReceipt.bridgeReadyAtUtc) 'Readiness receipt is incomplete.'
Assert ($global:VsAgentProfileTest_nativeCalls[0].arguments -notmatch ' -c ' -and $global:VsAgentProfileTest_nativeCalls[1].arguments -match ' -c qa.invalid:30000') 'Launch did not connect through menu-first forwarding.'
Assert ((Get-Content -LiteralPath (Join-Path $testRoot 'launch-success/hello.json') -Raw | ConvertFrom-Json).session -eq '[redacted]') 'Saved hello is not redacted.'
$global:VsAgentProfileTest_clients = @(Client 22 $launchProfile)
$nativeCount = $global:VsAgentProfileTest_nativeCalls.Count
& $launch -DataPath $launchProfile -GameInstall $game -OutputRoot (Join-Path $testRoot 'stop-only') -StopOnly | Out-Null
Assert ($global:VsAgentProfileTest_clients.Count -eq 0 -and $global:VsAgentProfileTest_stopIds[-1] -eq 22) 'Stop-only did not stop the exact owned client.'
Assert ($global:VsAgentProfileTest_nativeCalls.Count -eq $nativeCount) 'Stop-only launched a native process.'
Assert ((Get-Content -LiteralPath (Join-Path $testRoot 'stop-only/launch-receipt.json') -Raw | ConvertFrom-Json).state -eq 'stopped') 'Stop-only receipt did not record stopped state.'
Reject { & $launch -DataPath $launchProfile -GameInstall $game -OutputRoot (Join-Path $testRoot 'stop-forward-refusal') -StopOnly -ServerAddress 'qa.invalid:30000' } 'cannot forward'
$global:VsAgentProfileTest_clients = @(Client 23 ($launchProfile + '0'))
Reject { & $launch -DataPath $launchProfile -GameInstall $game -OutputRoot (Join-Path $testRoot 'stop-other-refusal') -StopOnly } 'other or unidentifiable'
Assert ($global:VsAgentProfileTest_stopIds[-1] -eq 22 -and $global:VsAgentProfileTest_nativeCalls.Count -eq $nativeCount) 'Stop-only changed another client.'

foreach ($case in @(@{ mode='menu-timeout'; pattern='menu-ready log deadline' }, @{ mode='other-appears'; pattern='other or unidentifiable' }, @{ mode='no-grant'; pattern='mutation grant' }, @{ mode='disconnected'; pattern='bridge deadline' }, @{ mode='rpc-hang'; pattern='bounded RPC deadline' })) {
    $global:VsAgentProfileTest_clients = @(); $global:VsAgentProfileTest_menuLog = $null; $global:VsAgentProfileTest_mode = $case.mode; $global:VsAgentProfileTest_rpcKilled = $false
    $callCount = $global:VsAgentProfileTest_nativeCalls.Count
    Reject { Launch-Fixture $launchProfile ('launch-' + $case.mode) } $case.pattern
    $failed = Get-Content -LiteralPath (Join-Path $testRoot ('launch-' + $case.mode + '/launch-receipt.json')) -Raw | ConvertFrom-Json
    Assert ($failed.state -eq 'failed') 'A failed launch lacked a durable failure receipt.'
    if ($case.mode -in @('menu-timeout', 'other-appears')) { Assert ($global:VsAgentProfileTest_nativeCalls.Count -eq $callCount + 1) 'Connection was forwarded before safe menu readiness.' }
    if ($case.mode -eq 'rpc-hang') { Assert $global:VsAgentProfileTest_rpcKilled 'A hung RPC was not terminated.' }
}
Write-Output "$global:VsAgentProfileTest_checks profile workflow checks passed; fixture receipts: $testRoot"
