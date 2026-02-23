param()
& "$PSScriptRoot\update_config.ps1" -Changes @{
    EnableLanguageSystem = $false
    OverrideSpeechBubblesWithRpText = $true
    TypingIndicatorDisplayMode = 1
    DebugMode = $false
}
