param()
& "$PSScriptRoot\update_config.ps1" -Changes @{
    EnableLanguageSystem = $true
    OverrideSpeechBubblesWithRpText = $true
    TypingIndicatorDisplayMode = 2
    DebugMode = $false
    NametagRenderRange = 30
    TypingIndicatorMaxRange = 35
    SendServerSaveAnnouncement = $true
    SendServerSaveFinishedAnnouncement = $true
    ServerSaveAnnouncementAsNotification = $true
}
