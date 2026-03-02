$repoRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $repoRoot '.env'
Get-Content $envFile | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') {
        [System.Environment]::SetEnvironmentVariable($matches[1], $matches[2])
    }
}

$base = $env:PTERO_BASE_URL.TrimEnd('/')
$headers = @{
    'Authorization' = 'Bearer ' + $env:PTERO_TOKEN
    'Accept' = 'application/json'
}

$resp = Invoke-WebRequest -Uri "$base/api/client/servers/8982de16/files/contents?file=/data/ModConfig/the_basics.json" -Headers $headers -Method Get -UseBasicParsing
Write-Host "=== Content type ==="
Write-Host $resp.Headers['Content-Type']
Write-Host "=== First 500 chars ==="
Write-Host $resp.Content.Substring(0, [Math]::Min(500, $resp.Content.Length))
Write-Host "=== Content length ==="
Write-Host $resp.Content.Length
