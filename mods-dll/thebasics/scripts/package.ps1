Compress-Archive -Force -Path $PSScriptRoot/../modinfo.json,$PSScriptRoot/../../../output/net7.0/thebasics.dll,$PSScriptRoot/../../../output/net7.0/thebasics.pdb,$PSScriptRoot/../assets -DestinationPath $PSScriptRoot/../thebasics.zip

# Load WinSCP .NET assembly
Add-Type -Path "C:\Program Files (x86)\WinSCP\WinSCPnet.dll"

# Load environment variables from .env file
$envPath = Join-Path $PSScriptRoot "../.env"
if (Test-Path $envPath) {
    Get-Content $envPath | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            Set-Item -Path "Env:$($matches[1])" -Value $matches[2]
        }
    }
} else {
    Write-Host "Error: .env file not found. Please create one with SFTP credentials."
    exit 1
}

# Set up session options
$sessionOptions = New-Object WinSCP.SessionOptions -Property @{
    Protocol = [WinSCP.Protocol]::Sftp
    HostName = $env:SFTP_HOST
    PortNumber = [int]$env:SFTP_PORT
    UserName = $env:SFTP_USERNAME
    Password = $env:SFTP_PASSWORD
    GiveUpSecurityAndAcceptAnySshHostKey = $true
}

$session = New-Object WinSCP.Session

$sourceFile = (Resolve-Path "$PSScriptRoot\..\thebasics.zip").Path
$ftpDestinationFile = "/data/Mods/thebasics.zip"

try {
    $session.Open($sessionOptions)

    $transferOptions = New-Object WinSCP.TransferOptions
    $transferOptions.TransferMode = [WinSCP.TransferMode]::Binary

    $transferResult = $session.PutFiles($sourceFile, $ftpDestinationFile, $false, $transferOptions)

    $transferResult.Check()

    foreach ($transfer in $transferResult.Transfers) {
        Write-Host "Upload of $($transfer.FileName) succeeded"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    $session.Dispose()
}

$destinationFile = "C:\Users\steve\AppData\Roaming\VintagestoryData\Mods\thebasics.zip"

# Copy the file from source to destination, overwriting if it already exists
Copy-Item -Path $sourceFile -Destination $destinationFile -Force