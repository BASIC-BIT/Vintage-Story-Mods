# Get absolute paths
$projectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")  # thebasics project root
$solutionRoot = Resolve-Path (Join-Path $projectRoot "../..")  # solution root
$outputDir = Join-Path $projectRoot "bin/Release/net7.0"  # Standard MSBuild output location
$assetsDir = Join-Path $projectRoot "assets"
$modInfoFile = Join-Path $projectRoot "modinfo.json"
$dllFile = Join-Path $outputDir "thebasics.dll"
$pdbFile = Join-Path $outputDir "thebasics.pdb"

# Read version from modinfo.json and create versioned filename
$modInfo = Get-Content $modInfoFile | ConvertFrom-Json
$version = $modInfo.version -replace '\.', '_' -replace '-', '_'
$zipFile = Join-Path $projectRoot "thebasics_$version.zip"
$logFile = Join-Path $solutionRoot "package.log"
$envPath = Join-Path $projectRoot ".env"  # Look for .env in the mod folder

# Start logging
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
"[$timestamp] Package script started" | Out-File -FilePath $logFile
"[$timestamp] Project root: $projectRoot" | Out-File -FilePath $logFile -Append
"[$timestamp] Solution root: $solutionRoot" | Out-File -FilePath $logFile -Append
"[$timestamp] Looking for .env file at: $envPath" | Out-File -FilePath $logFile -Append

Write-Host "Building mod package..."
"[$timestamp] Building mod package..." | Out-File -FilePath $logFile -Append

# Verify required files exist
if (-not (Test-Path $modInfoFile)) {
    $msg = "Error: modinfo.json not found at $modInfoFile"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    exit 1
}
if (-not (Test-Path $dllFile)) {
    $msg = "Error: thebasics.dll not found at $dllFile"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    exit 1
}
if (-not (Test-Path $pdbFile)) {
    $msg = "Error: thebasics.pdb not found at $pdbFile"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    exit 1
}
if (-not (Test-Path $assetsDir)) {
    $msg = "Warning: assets directory not found at $assetsDir"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
}

# Create the mod zip file
try {
    # Build list of items to include (only include assets if it exists)
    $itemsToZip = @($modInfoFile, $dllFile, $pdbFile)
    if (Test-Path $assetsDir) {
        $itemsToZip += $assetsDir
    }
    
    Compress-Archive -Force -Path $itemsToZip -DestinationPath $zipFile
    $msg = "Successfully created mod package at $zipFile"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
} catch {
    $msg = "Error creating zip file: $($_.Exception.Message)"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    exit 1
}

# Copy to local mods directories
$localModsDirectories = @(
    (Join-Path $env:APPDATA "VintagestoryData/Mods"),
    "D:\Games\VSProfiles\Profile2\Mods"
)

$msg = "Deploying mod to $($localModsDirectories.Count) local directories..."
Write-Host $msg
"[$timestamp] $msg" | Out-File -FilePath $logFile -Append

foreach ($localModsDir in $localModsDirectories) {
    $localModFile = Join-Path $localModsDir (Split-Path $zipFile -Leaf)
    
    try {
        if (-not (Test-Path $localModsDir)) {
            New-Item -ItemType Directory -Path $localModsDir -Force | Out-Null
            $msg = "Created directory: $localModsDir"
            Write-Host $msg
            "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        }
        
        # Remove old versions of the mod
        $oldVersions = Get-ChildItem -Path $localModsDir -Filter "thebasics*.zip" -ErrorAction SilentlyContinue
        foreach ($oldVersion in $oldVersions) {
            Remove-Item -Path $oldVersion.FullName -Force
            $msg = "Removed old version: $($oldVersion.Name)"
            Write-Host $msg
            "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        }
        
        Copy-Item -Path $zipFile -Destination $localModFile -Force
        $msg = "Successfully copied mod to local mods directory at $localModFile"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    } catch {
        $msg = "Error copying to local mods directory $localModsDir : $($_.Exception.Message)"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
        # Continue with other directories instead of exiting
    }
}

# Load environment variables from .env file in mod folder
$msg = "Looking for .env file at: $envPath"
Write-Host $msg
"[$timestamp] $msg" | Out-File -FilePath $logFile -Append

if (Test-Path $envPath) {
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

    # Only attempt SFTP upload if we have the required environment variables
    if ($env:SFTP_HOST -and $env:SFTP_PORT -and $env:SFTP_USERNAME -and $env:SFTP_PASSWORD -and $env:SFTP_HOST_KEY_FINGERPRINT) {
        $msg = "Attempting SFTP upload with host: $($env:SFTP_HOST), port: $($env:SFTP_PORT), username: $($env:SFTP_USERNAME)"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

        try {
            # Load WinSCP .NET assembly
            $winScpPath = "C:\Program Files (x86)\WinSCP\WinSCPnet.dll"
            if (-not (Test-Path $winScpPath)) {
                $msg = "WinSCP assembly not found at $winScpPath - skipping SFTP upload"
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
                exit 0
            }

            Add-Type -Path $winScpPath

            # Enable WinSCP logging
            $winscp_log = Join-Path $solutionRoot "winscp.log"
            if (Test-Path $winscp_log) {
                Remove-Item $winscp_log -Force
            }

            # Set up session options with explicit protocol version
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
                # Test connection first
                $msg = "Testing SFTP connection..."
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

                $session.Open($sessionOptions)

                $msg = "SFTP connection successful!"
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

                # Try to list the root directory to verify permissions
                $msg = "Testing SFTP permissions by listing root directory..."
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

                $directoryInfo = $session.ListDirectory("/")
                
                $msg = "Successfully listed root directory. Found $($directoryInfo.Files.Count) files/directories."
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

                # Now attempt the file transfer
                $ftpDestinationFile = "/data/Mods/$(Split-Path $zipFile -Leaf)"
                $transferOptions = New-Object WinSCP.TransferOptions
                $transferOptions.TransferMode = [WinSCP.TransferMode]::Binary

                $msg = "Uploading $zipFile to $ftpDestinationFile..."
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append

                $transferResult = $session.PutFiles($zipFile, $ftpDestinationFile, $false, $transferOptions)
                $transferResult.Check()

                foreach ($transfer in $transferResult.Transfers) {
                    $msg = "Successfully uploaded mod to SFTP server at $($transfer.FileName)"
                    Write-Host $msg
                    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
                }
            } catch {
                $msg = "SFTP Error: $($_.Exception.Message)"
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
                
                # Continue the script without exiting on SFTP errors
            } finally {
                $session.Dispose()
                $msg = "SFTP session closed"
                Write-Host $msg
                "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
            }
        } catch {
            $msg = "WinSCP Error: $($_.Exception.Message)"
            Write-Host $msg
            "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
            # Continue the script without exiting on WinSCP errors
        }
    } else {
        $msg = "SFTP configuration incomplete - skipping SFTP upload"
        Write-Host $msg
        "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
    }
} else {
    $msg = "No .env file found - skipping SFTP upload"
    Write-Host $msg
    "[$timestamp] $msg" | Out-File -FilePath $logFile -Append
}

$msg = "Package script completed successfully"
Write-Host $msg
"[$timestamp] $msg" | Out-File -FilePath $logFile -Append