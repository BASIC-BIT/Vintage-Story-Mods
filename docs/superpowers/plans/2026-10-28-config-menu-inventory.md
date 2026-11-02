# Legacy configuration migration inventory

Status: proposed destinations, audited October 28, 2026. No routes or settings were changed.

The current registry has **210 settings**. The new setup catalog owns **135 distinct setting keys**, leaving **75** registered settings without a new-menu page. Languages and character-sheet fields are separate structured collections and are not part of those counts. This is parity with the old menu, not a claim that every ModConfig property has a GUI.

Every missing key appears exactly once below. The proposed page IDs are implementation targets, not existing endpoints. Live means the registry classifies it as live; consuming code, optional integrations and command re-registration still need verification.

| Proposed page | Missing settings |
|---|---:|
| chat.language | 1 |
| chat.occlusion | 6 |
| chat.shortcuts | 18 |
| chat.tabs | 2 |
| chat.textappearance | 9 |
| chat.tts | 6 |
| chat.voices | 7 |
| chat.wording | 13 |
| notifications.deaths | 1 |
| roleplay.descriptions | 1 |
| server.diagnostics | 1 |
| server.integrations | 1 |
| server.retention | 1 |
| server.stats | 6 |
| teleport.homes | 1 |
| teleport.spawn | 1 |

## chat.language

| Exact key | Current group | Activation classification |
|---|---|---|
| `UseNicknameInGlobalOOC` | Chat/RP | Live (registry classification) |

## chat.occlusion

| Exact key | Current group | Activation classification |
|---|---|---|
| `RequireClearSoundPathForSpeech.Normal` | Chat/Occlusion | Live (registry classification) |
| `RequireClearSoundPathForSpeech.Whisper` | Chat/Occlusion | Live (registry classification) |
| `RequireClearSoundPathForSpeech.Yell` | Chat/Occlusion | Live (registry classification) |
| `SightBlockingBlockCodePatterns` | Chat/Occlusion | Live (registry classification) |
| `SightPassThroughBlockCodePatterns` | Chat/Occlusion | Live (registry classification) |
| `SpeechOcclusionWallPenaltyBlocks` | Chat/Occlusion | Live (registry classification) |

## chat.shortcuts

| Exact key | Current group | Activation classification |
|---|---|---|
| `ChatDelimiters.Bold.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Bold.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Emote.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Emote.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Environmental.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Environmental.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.GlobalOOC.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.GlobalOOC.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Italic.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Italic.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.OOC.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.OOC.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.PlacedEnvironmental.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.PlacedEnvironmental.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Quote.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.Quote.Start` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.SignLanguageQuote.End` | Chat/Delimiters | Live (registry classification) |
| `ChatDelimiters.SignLanguageQuote.Start` | Chat/Delimiters | Live (registry classification) |

## chat.tabs

| Exact key | Current group | Activation classification |
|---|---|---|
| `ProximityChatName` | Restart Required | Server restart |
| `ProximityChatTabPosition` | Chat tabs | Live (registry classification) |

## chat.textappearance

| Exact key | Current group | Activation classification |
|---|---|---|
| `ApplyColorsToNicknames` | Client UX | Live (registry classification) |
| `ApplyColorsToPlayerNames` | Client UX | Live (registry classification) |
| `BoldNicknames` | Client UX | Live (registry classification) |
| `EnableDistanceFontSizeSystem` | Chat/RP | Live (registry classification) |
| `IsolateChatMessageFormatting` | Client UX | Live (registry classification) |
| `ProximityChatClampFontSizes` | Chat/Font Sizes | Live (registry classification) |
| `ProximityChatDefaultFontSize.Normal` | Chat/Font Sizes | Live (registry classification) |
| `ProximityChatDefaultFontSize.Whisper` | Chat/Font Sizes | Live (registry classification) |
| `ProximityChatDefaultFontSize.Yell` | Chat/Font Sizes | Live (registry classification) |

## chat.tts

| Exact key | Current group | Activation classification |
|---|---|---|
| `RPTTS_ModeFalloff.Normal` | Chat/RPTTS Audio | Live (registry classification) |
| `RPTTS_ModeFalloff.Whisper` | Chat/RPTTS Audio | Live (registry classification) |
| `RPTTS_ModeFalloff.Yell` | Chat/RPTTS Audio | Live (registry classification) |
| `RPTTS_ModeGain.Normal` | Chat/RPTTS Audio | Live (registry classification) |
| `RPTTS_ModeGain.Whisper` | Chat/RPTTS Audio | Live (registry classification) |
| `RPTTS_ModeGain.Yell` | Chat/RPTTS Audio | Live (registry classification) |

## chat.voices

| Exact key | Current group | Activation classification |
|---|---|---|
| `ChatterModePitch.Normal` | Chat/Chatter Audio | Live (registry classification) |
| `ChatterModePitch.Whisper` | Chat/Chatter Audio | Live (registry classification) |
| `ChatterModePitch.Yell` | Chat/Chatter Audio | Live (registry classification) |
| `ChatterModeVolume.Normal` | Chat/Chatter Audio | Live (registry classification) |
| `ChatterModeVolume.Whisper` | Chat/Chatter Audio | Live (registry classification) |
| `ChatterModeVolume.Yell` | Chat/Chatter Audio | Live (registry classification) |
| `ChatterSelfVolumeMultiplier` | Chat/RP | Live (registry classification) |

## chat.wording

| Exact key | Current group | Activation classification |
|---|---|---|
| `AttributeFreeformMessagesToPlayerName` | Chat/RP | Live (registry classification) |
| `ProseNicknameToken` | Chat/RP | Live (registry classification) |
| `ProximityChatModeBabbleVerb` | Chat/RP | Live (registry classification) |
| `ProximityChatModePunctuation.Normal` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModePunctuation.Whisper` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModePunctuation.Yell` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModeQuestionVerbs.Normal` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModeQuestionVerbs.Whisper` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModeQuestionVerbs.Yell` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModeVerbs.Normal` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModeVerbs.Whisper` | Chat/RP Text | Live (registry classification) |
| `ProximityChatModeVerbs.Yell` | Chat/RP Text | Live (registry classification) |
| `RPTextTogglePermission` | Permissions | Live (registry classification) |

## notifications.deaths

| Exact key | Current group | Activation classification |
|---|---|---|
| `EnableNearbyDeathMessagesInProximityChat` | Server Notifications | Live (registry classification) |

## roleplay.descriptions

| Exact key | Current group | Activation classification |
|---|---|---|
| `MaxEnvironmentPlacementDistance` | Chat/RP | Live (registry classification) |

## server.diagnostics

| Exact key | Current group | Activation classification |
|---|---|---|
| `DebugMode` | Diagnostics | Live (registry classification) |

## server.integrations

| Exact key | Current group | Activation classification |
|---|---|---|
| `EnableTh3EssentialsDiscordRelay` | Integrations | Live (registry classification) |

## server.retention

| Exact key | Current group | Activation classification |
|---|---|---|
| `ChatHistoryFlushIntervalMilliseconds` | Chat History | Server restart |

## server.stats

| Exact key | Current group | Activation classification |
|---|---|---|
| `PlayerStatDistanceTravelledTimer` | Restart Required | Server restart |
| `PlayerStatToggles.BlockBreaks` | Player Stats/Toggles | Live (registry classification) |
| `PlayerStatToggles.Deaths` | Player Stats/Toggles | Live (registry classification) |
| `PlayerStatToggles.DistanceTravelled` | Player Stats/Toggles | Live (registry classification) |
| `PlayerStatToggles.NpcKills` | Player Stats/Toggles | Live (registry classification) |
| `PlayerStatToggles.PlayerKills` | Player Stats/Toggles | Live (registry classification) |

## teleport.homes

| Exact key | Current group | Activation classification |
|---|---|---|
| `SetHomeCommandPrivilege` | Permissions | Live (registry classification) |

## teleport.spawn

| Exact key | Current group | Activation classification |
|---|---|---|
| `SetSpawnCommandPrivilege` | Permissions | Live (registry classification) |

## Audit method

Read ConfigAdminSettingRegistry.Settings and SetupWizardCatalog.Pages from the locally compiled current source, then take the set difference by exact setting key. Mode, delimiter and player-stat entries are expanded by the real registry rather than estimated from source strings. No runtime configuration values were read for this comparison.

Recompute before implementation. The coverage test in Task 1 of the linked implementation plan must fail when new registry entries lack an assigned destination or an explicit, reviewed deferral.

Source: mods-dll/thebasics/src/ModSystems/AdminConfig/ConfigAdminSettingRegistry.cs and SetupWizardCatalog.cs.
