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
$config = $resp.Content | ConvertFrom-Json

Write-Host "=== Property names ==="
$config.PSObject.Properties.Name | Sort-Object

Write-Host "`n=== Checking specific keys ==="
Write-Host "Has OverrideSpeechBubblesWithRpText: $($config.PSObject.Properties.Name -contains 'OverrideSpeechBubblesWithRpText')"
Write-Host "Has overrideSpeechBubblesWithRpText: $($config.PSObject.Properties.Name -contains 'overrideSpeechBubblesWithRpText')"
Write-Host "Has DebugMode: $($config.PSObject.Properties.Name -contains 'DebugMode')"
Write-Host "Has debugMode: $($config.PSObject.Properties.Name -contains 'debugMode')"
