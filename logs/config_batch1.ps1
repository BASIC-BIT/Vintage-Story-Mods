# Config Change 1: test plain bubbles + debug mode + icon-only typing indicator
# OverrideSpeechBubblesWithRpText=false (already default, set explicitly)
# DebugMode=true
# TypingIndicatorDisplayMode=0 (already default)
# EnableTypingIndicator=true (turned off by default regeneration)

$changes = @{
    OverrideSpeechBubblesWithRpText = $false
    DebugMode = $true
    TypingIndicatorDisplayMode = 0
    EnableTypingIndicator = $true
    TypingIndicatorMaxRange = 35
    NametagRenderRange = 30
    SendServerSaveFinishedAnnouncement = $true
}
& "$PSScriptRoot\update_config.ps1" -Changes $changes
