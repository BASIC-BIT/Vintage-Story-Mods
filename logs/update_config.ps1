param(
    [hashtable]$Changes = @{}
)

$repoRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $repoRoot '.env'
if (Test-Path $envFile) {
    Get-Content $envFile | ForEach-Object {
        if ($_ -match '^([^=]+)=(.*)$') {
            [System.Environment]::SetEnvironmentVariable($matches[1], $matches[2])
        }
    }
} else {
    Write-Host "No .env found at $envFile"
    exit 1
}

$base = $env:PTERO_BASE_URL.TrimEnd('/')
$headers = @{
    'Authorization' = 'Bearer ' + $env:PTERO_TOKEN
    'Accept' = 'application/json'
}

# Read current config as raw text (Pterodactyl returns raw file content)
$configText = Invoke-WebRequest -Uri "$base/api/client/servers/8982de16/files/contents?file=/data/ModConfig/the_basics.json" -Headers $headers -Method Get -UseBasicParsing
$config = $configText.Content | ConvertFrom-Json

# Apply changes
foreach ($key in $Changes.Keys) {
    $val = $Changes[$key]
    if ($config.PSObject.Properties.Name -contains $key) {
        $config.$key = $val
        Write-Host "  $key = $val"
    } else {
        Write-Host "  WARNING: key '$key' not found in config, skipping"
    }
}

# Write back
$newJson = $config | ConvertTo-Json -Depth 10
$writeHeaders = @{
    'Authorization' = 'Bearer ' + $env:PTERO_TOKEN
    'Content-Type' = 'application/json'
}
Invoke-RestMethod -Uri "$base/api/client/servers/8982de16/files/write?file=/data/ModConfig/the_basics.json" -Headers $writeHeaders -Method Post -Body $newJson
Write-Host "Config updated."
