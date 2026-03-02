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
    'Content-Type' = 'application/json'
}
$resp = Invoke-RestMethod -Uri "$base/api/client/servers/8982de16/files/contents?file=/data/ModConfig/the_basics.json" -Headers $headers -Method Get
$resp
