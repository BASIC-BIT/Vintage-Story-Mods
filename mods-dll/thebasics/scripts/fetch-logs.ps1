param(
    [string]$LogType = "all",
    [int]$Days = 7,
    [string]$OutputDir = ""
)

# Get absolute paths
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")  # thebasics project root
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")  # solution root
$logsDir = Join-Path $solutionRoot "logs"
$logFile = Join-Path $solutionRoot "fetch-logs.log"
$envPath = Join-Path $projectRoot ".env"  # Look for .env in the mod folder

# Set default output directory if not provided
if ([string]::IsNullOrEmpty($OutputDir)) {
    $OutputDir = $logsDir
}

# Start logging
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"[$timestamp] Fetch logs script started" | Out-File -FilePath $logFile
"[$timestamp] Project root: $projectRoot" | Out-File -FilePath $logFile -Append
"[$timestamp] Solution root: $solutionRoot" | Out-File -FilePath $logFile -Append
"[$timestamp] Log type: $LogType, Days: $Days" | Out-File -FilePath $logFile -Append
"[$timestamp] Looking for .env file at: $envPath" | Out-File -FilePath $logFile -Append

Write-Host "Fetching server logs..."
"[$timestamp] Fetching server logs..." | Out-File -FilePath $logFile -Append

# Create logs directory if it doesn't exist
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    $msg = "Created logs directory at $OutputDir"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
}

# Load environment variables from .env file in mod folder
$msg = "Looking for .env file at: $envPath"
Write-Host $msg
"[$timestamp] $msg" | Out-File -FilePath $logFile -Append

if (-not (Test-Path $envPath)) {
    $msg = "Error: .env file not found at $envPath - cannot fetch server logs"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    exit 1
}

$msg = "Found .env file, loading environment variables..."
Write-Host $msg
"[$timestamp] $msg" | Out-File -FilePath $logFile -Append

Get-Content $envPath | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') {
        Set-Item -Path "Env:$($matches[1])" -Value $matches[2].Trim()  # Trim whitespace
        $msg = "Loaded environment variable: $($matches[1])"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    }
}

# Check if we have the required environment variables
if (-not ($env:SFTP_HOST -and $env:SFTP_PORT -and $env:SFTP_USERNAME -and $env:SFTP_PASSWORD -and $env:SFTP_HOST_KEY_FINGERPRINT)) {
    $msg = "Error: Missing required SFTP environment variables"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    exit 1
}

$msg = "Attempting SFTP download with host: $($env:SFTP_HOST), port: $($env:SFTP_PORT), username: $($env:SFTP_USERNAME)"
Write-Host $msg
"[$timestamp] $msg" | Out-File -FilePath $logFile -Append

try {
    # Load WinSCP .NET assembly
    $winScpPath = "C:\Program Files (x86)\WinSCP\WinSCPnet.dll"
    if (-not (Test-Path $winScpPath)) {
        $msg = "Error: WinSCP assembly not found at $winScpPath"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        exit 1
    }

    Add-Type -Path $winScpPath

    # Enable WinSCP logging
    $winscp_log = Join-Path $solutionRoot "winscp-fetch.log"
    if (Test-Path $winscp_log) {
        Remove-Item $winscp_log -Force
    }

    # Set up session options
    $sessionOptions = New-Object WinSCP.SessionOptions -Property @{
        Protocol = [WinSCP.Protocol]::Sftp
        HostName = $env:SFTP_HOST
        PortNumber = [int]$env:SFTP_PORT
        UserName = $env:SFTP_USERNAME
        Password = $env:SFTP_PASSWORD
        SshHostKeyFingerprint = $env:SFTP_HOST_KEY_FINGERPRINT
        Timeout = [TimeSpan]::FromSeconds(30)
        FtpMode = [WinSCP.FtpMode]::Passive
    }

    $msg = "Connecting to SFTP server $($env:SFTP_HOST):$($env:SFTP_PORT) as $($env:SFTP_USERNAME)..."
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

    $session = New-Object WinSCP.Session
    $session.SessionLogPath = $winscp_log

    try {
        $session.Open($sessionOptions)

        $msg = "SFTP connection successful!"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

        # Define remote log paths based on log type
        $remotePaths = @()
        switch ($LogType.ToLower()) {
            "server" { 
                $remotePaths += "/data/Logs/server-main.txt"
                $remotePaths += "/data/Logs/server-event.txt"
            }
            "debug" { 
                $remotePaths += "/data/Logs/server-debug.txt"
            }
            "crash" { 
                $remotePaths += "/data/CrashReports/*"
            }
            "mod" { 
                $remotePaths += "/data/Logs/*mod*.txt"
                $remotePaths += "/data/Logs/*thebasics*.txt"
            }
            "all" { 
                $remotePaths += "/data/Logs/*"
                $remotePaths += "/data/CrashReports/*"
            }
            default {
                $remotePaths += "/data/Logs/*"
            }
        }

        # Create timestamped subdirectory for this fetch
        $fetchTimestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"
        $fetchDir = Join-Path $OutputDir $fetchTimestamp
        New-Item -ItemType Directory -Path $fetchDir -Force | Out-Null

        $msg = "Created fetch directory: $fetchDir"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

        $transferOptions = New-Object WinSCP.TransferOptions
        $transferOptions.TransferMode = [WinSCP.TransferMode]::Binary

        $totalFiles = 0
        foreach ($remotePath in $remotePaths) {
            try {
                $msg = "Downloading files from $remotePath..."
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

                $transferResult = $session.GetFiles($remotePath, "$fetchDir\", $false, $transferOptions)
                $transferResult.Check()
                
                foreach ($transfer in $transferResult.Transfers) {
                    $totalFiles++
                    $msg = "Downloaded: $($transfer.FileName) -> $($transfer.Destination)"
                    Write-Host $msg
                    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
                }
            } catch {
                $msg = "Warning: Could not download from $remotePath - $($_.Exception.Message)"
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
            }
        }

        if ($totalFiles -eq 0) {
            $msg = "Warning: No files were downloaded. Check if the remote paths exist and contain files."
            Write-Host $msg
            "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        } else {
            $msg = "Successfully downloaded $totalFiles files to $fetchDir"
            Write-Host $msg
            "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        }

    } catch {
        $msg = "SFTP Error: $($_.Exception.Message)"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        
        if (Test-Path $winscp_log) {
            $msg = "WinSCP Log:"
            Write-Host $msg
            "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
            Get-Content $winscp_log | ForEach-Object {
                Write-Host $_
                "[$timestamp] $_" | Out-File -FilePath $logFile -Append
            }
        }
        
        throw
    }
} catch {
    $msg = "SFTP Download Error: $($_.Exception.Message)"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    exit 1
} finally {
    if ($session) {
        $session.Dispose()
    }
}

$msg = "Log fetch completed successfully!"
Write-Host $msg
"[$timestamp] $msg" | Out-File -FilePath $logFile -Append
exit 0