# The BASICs Feature Inventory

This is the maintainer-facing inventory for release QA and compatibility review. Keep it in sync when adding commands, config keys, network packets, rendering patches, or behavior that needs manual verification.

## Core Systems

The mod currently loads these server-side systems:

- `TPA.TpaSystem`
- `SleepNotifier.SleepNotifierSystem`
- `SaveNotifications.SaveNotificationsSystem`
- `Repair.RepairModSystem`
- `ProximityChat.RPProximityChatSystem`
- `PlayerStats.PlayerStatSystem`

It also loads the client-side `ChatUiSystem` for config sync, chat UI patches, speech bubbles, typing indicators, placed environmental bubbles, RPTTS dispatch, and chatter playback.

## Chat History Search

Features:

- Optional server-side chat history capture controlled by `EnableChatHistory`.
- Captures The BASICs RP chat with canonical sender text, source/kind metadata, recipient counts, language/mode metadata, and sender/placed positions when available.
- Optional capture of non-BASICs player chat through Vintage Story's `PlayerChat` event when `ChatHistoryCaptureNonBasicChat=true`.
- JSONL storage under server mod data, with corrupt-line recovery during reads.
- Permission-gated GUI search opened with `/chatlog` or `/chathistory`.
- Text command backup for recent/search/player/view, plus export and purge management.
- Retention can keep forever, cap by age, cap by total entry count, or purge manually.

Commands:

- `/chatlog` or `/chathistory`: open the GUI search surface for player callers.
- `/chatlog recent [count]`
- `/chatlog search <text>`
- `/chatlog player <player> [count]`
- `/chatlog view <id>`
- `/chatlog export [all|search <text>|player <player>|recent [count]]`
- `/chatlog purge retention confirm`
- `/chatlog purge before <date> confirm`
- `/chatlog purge all confirm`

Primary config areas:

- `EnableChatHistory`
- `ChatHistoryCaptureNonBasicChat`
- `ChatHistoryPermission`
- `ChatHistoryManagePermission`
- `ChatHistoryRetentionDays`
- `ChatHistoryMaxEntries`
- `ChatHistorySearchMaxResults`
- `ChatHistoryFlushIntervalMilliseconds`

## Default Configuration Philosophy

Version 5.5.0 shifts the generated/default config toward showcasing RP-server features out of the box. Existing explicit values in `ModConfig/the_basics.json` are still respected, but new configs and missing keys now default to these feature-forward behaviors:

- `ProximityChatAsDefault=true`
- `EnableGlobalOOC=true`
- `SendServerSaveFinishedAnnouncement=true`
- `EnableChatter=true`
- `TpaRequestPrivilege=chat`
- `TpaRequireTemporalGear=true`
- `RequireLineOfSightForSignLanguage=true`
- `NametagRequiresLineOfSight=true`
- `OverheadChatBubbleMode=RpText`

## In-Game Admin Config

Features:

- Server-authoritative admin config panel opened with `/basic config`, `/thebasics config`, or `/tb config`.
- Server-side validation and persistence to `ModConfig/the_basics.json`.
- Shared server config object so live-safe changes update existing systems without replacing stale references.
- Client config resync after successful saves or disk reloads.
- New-setting discovery through persisted `ReviewedConfigSettingKeys`.
- Live/restart labeling in the panel; startup-shaped settings can be edited but are reported as restart-required.

Admin commands:

- `/basic config`, `/thebasics config`, `/tb config`
- `/basic reloadconfig`, `/thebasics reloadconfig`, `/tb reloadconfig`

Live-applied setting groups currently include chatter, typing indicators, nametag display/range/style, map player visibility, overhead bubble mode, selected TPA timeout/cooldown behavior, save notifications, sleep notifications, command privilege settings, per-mode proximity/chat/audio dictionaries, chat delimiters, player-stat toggles, and debug mode. Restart-required settings include startup-shaped command registration, chat group setup, language system enablement, and player stats enablement.

The admin panel exposes fixed-shape complex settings as validated flattened rows. This covers per-mode distance, obfuscation, font-size, verb, punctuation, RPTTS, chatter dictionaries, chat delimiter start/end values, player-stat toggles, and comma-separated font-size clamps. Variable-length/nested collections use dedicated editors where available: `/thebasics config languages` for `Languages` and `/thebasics config charsheetfields` for `CharacterSheetFields`. Prefer these editors over direct JSON edits because they validate field keys, bindings, options, and persisted character-sheet compatibility.

## RP Proximity Chat

Features:

- Dedicated proximity chat group, or optional General-chat replacement via `UseGeneralChannelAsProximityChat`.
- Whisper, normal, yell, and sign-language ranges.
- Recipient filtering by distance, sign-language line of sight, and chat mode.
- Sign-language line-of-sight checks use multiple target points and can deliver shortly after send if line of sight is acquired within the retry window.
- Automatic IC formatting with configurable verbs, punctuation, delimiters, nicknames, nickname colors, OOC styling, and optional global OOC.
- Distance obfuscation and distance-based font-size changes.
- RP text opt-out with `/rptext`.
- Emote-only mode with `/emotemode`.
- Local OOC and optional global OOC.
- Environmental messages and raycast-placed environmental messages.
- Optional RPTTS bridge for speech text.
- Character chatter sounds using the speaker's seraph voice instrument, with per-player opt-out.

Player-facing commands:

- `/nick`, `/nickname`, `/setnick`
- `/clearnick`
- `/nickcolor`, `/nicknamecolor`, `/nickcol`
- `/clearnickcolor`
- `/me`, `/m`
- `/it`, `/do`
- `/envhere`, `/dohere`, `/ithere`
- `/emotemode`
- `/rptext`
- `/oocToggle`
- `/ooc`
- `/gooc` when `EnableGlobalOOC=true`
- `/chatter`
- `/yell`, `/y`
- `/say`, `/s`, `/normal`
- `/whisper`, `/w`

Admin commands:

- `/adminsetnickname`, `/adminsetnick`, `/adminnick`, `/adminnickname`
- `/adminsetnicknamecolor`, `/adminsetnickcolor`, `/adminsetnickcol`

Shortcut delimiters:

- `*message` for emotes.
- `!message` for environmental messages.
- `!!message` for raycast-placed environmental messages.
- `(message)` for local OOC.
- `((message))` for global OOC when enabled.
- `"quoted text"` inside emotes for spoken segments.

Primary config areas:

- `ReviewedConfigSettingKeys`
- `DisableRPChat`
- `ProximityChatName`
- `UseGeneralChannelAsProximityChat`
- `ProximityChatAsDefault`
- `PreserveDefaultChatChoice`
- `PreventProximityChannelSwitching`
- `ProximityChatModeDistances`
- `ProximityChatModeObfuscationRanges`
- `EnableDistanceObfuscationSystem`
- `EnableDistanceFontSizeSystem`
- `ProximityChatDefaultFontSize`
- `ProximityChatClampFontSizes`
- `ProximityChatModeVerbs`
- `ProximityChatModePunctuation`
- `ProximityChatName` defaults to the stable persisted group name `Proximity` when unset.
- `ProximityChatModeBabbleVerb` is a legacy/custom override; default babble text uses lang key `thebasics:chat-babble-verb`.
- `ChatDelimiters`
- `EnableGlobalOOC`
- `AllowOOCToggle`
- `OOCTogglePermission`
- `OOCColor`
- `GlobalOOCColor`
- `UseNicknameInOOC`
- `UseNicknameInGlobalOOC`
- `RPTTS_ModeGain`
- `RPTTS_ModeFalloff`
- `EnableChatter`
- `ChatterModeVolume`
- `ChatterModePitch`
- `ChatterSelfVolumeMultiplier`
- `MaxEnvironmentPlacementDistance`

## Languages And Heritage Grants

Features:

- Config-defined languages with name, prefix, description, syllables, color, default flag, and hidden flag.
- Built-in pseudo-languages for babble and sign language.
- Prefix-based speaking language selection, including default-language selection.
- Unknown spoken language scrambling using configured syllables while preserving words that match the listener's account name or nickname.
- Unknown sign language rendering as deterministic gesture-symbol text while preserving listener name words.
- Language grants from character class, class traits, extra traits, PlayerModelLib model, and PlayerModelLib model group.
- Optional removal of auto-granted languages when the source class, trait, or model changes.

Commands:

- `/addlang`, `/addlanguage`
- `/removelang`, `/removelanguage`, `/remlang`, `/remlanguage`
- `/listlang`, `/listlanguage`, `/listlanguages`
- `/adminaddlang`, `/adminaddlanguage`
- `/adminremovelang`, `/adminremovelanguage`
- `/adminlistlang`, `/adminlistlanguage`

Primary config areas:

- `EnableLanguageSystem`
- `ChangeOwnLanguagePermission`
- `ChangeOtherLanguagePermission`
- `MaxLanguagesPerPlayer`
- `SignLanguageRange`
- `RequireLineOfSightForSignLanguage`
- `Languages`
- `RemoveGrantedLanguagesOnChange`

## Nicknames And Nametags

Features:

- RP nicknames separate from Vintage Story account names.
- Nickname color support.
- Admin nickname and nickname-color assignment.
- Configurable nickname length limits and change permissions.
- Configurable nametag content and range.
- Optional custom nametag background and border colors.
- Optional player-selected nametag background and border colors, falling back to server defaults.
- Optional hide-unless-targeting nametags.
- Multi-point line-of-sight gating for nametags when LOS is required.

Primary config areas:

- `DisableNicknames`
- `ProximityChatAllowPlayersToChangeNicknames`
- `ProximityChatAllowPlayersToChangeNicknameColors`
- `ChangeNicknameColorPermission`
- `BoldNicknames`
- `ApplyColorsToNicknames`
- `ApplyColorsToPlayerNames`
- `ShowNicknameInNametag`
- `ShowPlayerNameInNametag`
- `HideNametagUnlessTargeting`
- `NametagRenderRange`
- `NametagRequiresLineOfSight`
- `NametagBackgroundColor`
- `NametagBorderColor`
- `AllowPlayersToChangeNametagColors`
- `ChangeNametagColorPermission`
- `MinNicknameLength`
- `MaxNicknameLength`

## Map Player Visibility

Features:

- Optional The BASICs management of vanilla player map marker world config.
- Applies to both minimap and full world map because both use Vintage Story's player map marker tracking.
- Can hide other players completely with `MapHideOtherPlayers=true`.
- Can limit visible player marker range with `MapPlayerRenderDistance`; `-1` means unlimited.
- Forces vanilla `mapShowGroupPlayers=false` while managed, because The BASICs Proximity chat is a player group and same-group map visibility can otherwise reveal everyone.

Primary config areas:

- `ManageMapPlayerVisibility`
- `MapHideOtherPlayers`
- `MapPlayerRenderDistance`

## Client UI And Rendering

Features:

- Server config sync to clients after local player join.
- Safe client network send wrapper for early-join timing.
- Proximity chat tab persistence and default tab behavior.
- Prevention of unwanted chat tab auto-switching while in the proximity tab.
- Configurable overhead chat bubble modes: RP VTML text, vanilla plain text, or off.
- VTML-capable overhead speech bubbles when `OverheadChatBubbleMode=RpText`.
- Bubble styling for speech, emote, OOC, and environmental messages.
- Bubble scaling for yell and whisper.
- Multi-point line-of-sight gating for RP speech bubbles.
- Raycast-placed environmental bubbles at world positions.
- Typing indicators above other players with chat-open, composing, and actively-typing states.
- Typing indicator range and multi-point line-of-sight gating.
- Typing indicator display modes: icon, text, or both.
- Client-side nametag range/target sync.
- Debug/perf logging when `DebugMode=true`.

Primary config areas:

- `EnableTypingIndicator`
- `TypingIndicatorMaxRange`
- `TypingIndicatorTimeoutSeconds`
- `TypingIndicatorTextOverride`
- `TypingIndicatorDisplayMode`
- `OverheadChatBubbleMode`
- `DebugMode`
- `DisableRpOverheadBubbles` is deprecated and only used as a legacy fallback when `OverheadChatBubbleMode` is missing or empty.
- `OverrideSpeechBubblesWithRpText` is deprecated and ignored; use `OverheadChatBubbleMode=Vanilla` to fall back to vanilla plain-text bubbles or `OverheadChatBubbleMode=Off` to suppress overhead chat bubbles entirely.

## TPA

Features:

- `/tpa` request to teleport requester to target.
- `/tpahere` request to bring target to requester.
- Optional `tpa` privilege or default chat privilege.
- Optional temporal gear requirement.
- Gear consumption only after validation.
- Gear return on deny, timeout, clear, cancel, or rejoin-expired recovery.
- Optional cooldown in in-game hours.
- Optional timeout in real minutes.
- Multiple incoming request support.
- Request listing and cancel/clear flows.
- Teleport request and teleport particles.

Commands:

- `/tpa <player>`
- `/tpahere <player>`
- `/tpaccept [player]`
- `/tpdeny [player]`
- `/tpalist`
- `/tpallow <on|off>`
- `/cleartpa`
- `/tpacancel`

Primary config areas:

- `AllowPlayerTpa`
- `TpaRequestPrivilege`
- `TpaRequireTemporalGear`
- `TpaUseCooldown`
- `TpaCooldownInGameHours`
- `TpaUseTimeout`
- `TpaTimeoutMinutes`

## Player Stats

Tracked stats:

- Deaths.
- Player kills.
- NPC kills.
- Block breaks.
- Distance travelled.

Commands:

- `/playerstats [player]`
- `/pstats [player]`
- `/clearstats <player> [confirm]`
- `/clearstat <player> <statName> [confirm]`

Primary config areas:

- `PlayerStatSystem`
- `PlayerStatToggles`
- `PlayerStatClearPermission`
- `PlayerStatDistanceTravelledTimer`

## Save Notifications

Features:

- Announce server save start.
 - Announce save completion when enabled.
- Send as notification popups or chat lines.

Primary config areas:

- `SendServerSaveAnnouncement`
- `SendServerSaveFinishedAnnouncement`
- `ServerSaveAnnouncementAsNotification`
- `ServerSaveFinishedAsNotification`
- `TEXT_ServerSaveAnnouncement`
- `TEXT_ServerSaveFinished`

## Sleep Notifications

Features:

- Counts online players mounted on beds.
- Broadcasts when sleeping players cross `SleepNotificationThreshold`.
- Avoids spam while the count remains above the threshold.
- Does not notify for single-player or all-players-sleeping cases.

Primary config areas:

- `EnableSleepNotifications`
- `SleepNotificationThreshold`
- `TEXT_SleepNotification`

## Repair/Admin Utility

Command:

- `/setdurability <durability>` where durability is an absolute value (`250`) or percentage (`100%`).

Behavior:

- Requires root privilege.
- Requires an item in the active hotbar slot.
- Rejects blocks and non-durability items.
- Clamps over-max durability to the item's maximum.
- Rejects negative and invalid values with a clear error.
- Reports the result as current durability over maximum durability.

## Network Messages

Channel: `thebasics`.

Messages:

- `TheBasicsClientReadyMessage`: client to server, requests config after local join.
- `TheBasicsConfigMessage`: server to client, includes proximity group, config, and last selected group.
- `ChannelSelectedMessage`: client to server, persists selected chat tab.
- `ChatTypingStateMessage`: client to server and server to clients, synchronizes typing state.
- `ProximitySpeechMessage`: server to client, dispatches RPTTS text/gain/falloff.
- `ChatterSoundMessage`: server to clients, dispatches speaker entity, talk type, note count, volume, and pitch.
- `PlacedEnvironmentMessage`: server to clients, dispatches placed bubble position and text.

## Follow-Up Candidates

- Expand custom nested editors for any remaining variable-length config collections.
- Investigate a targeted decompiled Vintage Story API diff between the previous supported version and the current version before major compatibility releases.
