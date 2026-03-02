$envFile = Get-Content "$PSScriptRoot\..\.env"
$token = ($envFile | Where-Object { $_ -match '^PTERO_TOKEN=' }) -replace '^PTERO_TOKEN=',''
$headers = @{
    Authorization = "Bearer $token"
    'Content-Type' = 'application/json'
}
Invoke-RestMethod -Uri 'https://pt.basicbit.net/api/client/servers/8982de16/power' -Method Post -Headers $headers -Body '{"signal":"restart"}'
Write-Host "Restart signal sent."
