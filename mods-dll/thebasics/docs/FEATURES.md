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
- Whisper, normal, yell, and sign-language ranges. The three speech ranges accept exactly `-1` to deliver server-wide; any other negative value is rejected by config validation, and `SignLanguageRange` has no unlimited sentinel at all. An unlimited range cannot be combined with `RequireClearSoundPathForSpeech`, since the sound-path check needs a bounded range to raycast against.
- Two independent chat axes: range (`/whisper`, `/say`, `/yell`) and sticky override kind (`/me`, `/ooc`, `/gooc`). Whispered OOC and yelled emotes are both valid combinations.
- Sticky override modes toggle: running the same override command again returns you to speech, and running a different one replaces it. An explicit message prefix still wins for that single line.
- Recipient filtering by distance, sign-language line of sight, and chat mode.
- Sign-language line-of-sight checks use multiple target points and can deliver shortly after send if line of sight is acquired within the retry window.
- Sight treats foliage as see-through, so signing carries through a tree canopy and the speech bubble, nametag, typing indicator, and placed environmental bubbles all remain visible under one. They share a single filter deliberately: they previously disagreed, delivering a signed message whose bubble never appeared.
- The character-sheet look-up keeps a stricter filter, where foliage does block. Noticing that someone is standing there and reading their written description off them at close range are different things.
- Experimental, off by default: per-mode `RequireClearSoundPathForSpeech` gating, and wall muffling that converts sound-blocking geometry into extra effective distance. Sound and sight occlude differently: glass and water stop speech but not sight, and foliage stops neither.
- Automatic IC formatting with configurable verbs, punctuation, delimiters, nicknames, nickname colors, OOC styling, and optional global OOC.
- Question verbs: a message ending in `?` uses `ProximityChatModeQuestionVerbs` (default `asks`) instead of the mode's normal verbs.
- Distance obfuscation and distance-based font-size changes.
- RP text opt-out with `/rptext`.
- Emote-only mode with `/emotemode` or a bare `/me`.
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
- `ProximityChatModeQuestionVerbs`
- `ProximityChatModePunctuation`
- `RequireClearSoundPathForSpeech`
- `SpeechOcclusionWallPenaltyBlocks`
- `SightPassThroughBlockCodePatterns`
- `SightBlockingBlockCodePatterns`
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
- `UseNicknameInSpectatorOOC`
- `AllowSpectatorPlacedEnvironmentalMessages`
- `ProtectSpectatorRoleplayChat`
- `RPTTS_ModeGain`
- `RPTTS_ModeFalloff`
- `EnableChatter`
- `ChatterModeVolume`
- `ChatterModePitch`
- `ChatterSelfVolumeMultiplier`
- `MaxEnvironmentPlacementDistance`

Sight override entries use fully qualified `domain:path` block-code patterns with optional `*` wildcards. They are resolved to block IDs when config loads, so sight raycasts use constant-time lookups. Explicit blocking wins over pass-through, then the normal render-pass and material rules apply. Block-entity-owned selection shapes remain position-aware; an explicitly blocking owning block with no selection boxes falls back to blocking its full cell. Sound occlusion remains separate, and ordinary entities are not treated as sight blockers.

## Scene Markers

Features:

- Durable roleplay descriptions attached to a small physical marker, rather than literal writing rendered on a sign face.
- A shapeless two-loose-stone recipe with no ink requirement. In inventory, in hand, and as a dropped item the marker shows its exclamation glyph instead of the stone base.
- Ground and wall placement using the same familiar placement behavior as signs.
- Plain right-click opens the complete description in a book-style read-only reader, headed by the marker's title icon and its title in bold, in every display mode. Shift-right-click opens the editor, which requires normal land-claim build permissions and standing within 8 blocks. Save & Close sits at the bottom right of the editor, and closing the editor with unsaved changes asks for confirmation before discarding them. The top-middle inspector shows the title as the block name with a short preview under it, and Shift-right-click reads a written marker while it is held.
- Six-symbol indicator picker with a live preview: exclamation, question, information, solid dot, hollow ring, and diamond, drawn as camera-facing billboards. Colors are Yellow, White, Blue, Green, and Red. An `Other icons...` button beside the Symbol label opens the same catalog the title icon uses, so the billboard can be any catalog icon tinted with the marker's color; picking one of the six tiles clears it again, and an icon that cannot be drawn falls back to the chosen symbol rather than to nothing.
- One fixed translucent indicator style (68 percent opacity before distance fade). There is no effect or hologram picker; markers saved under the retired styles display in the current style without content or ownership changes.
- Indicator size is adjustable from 25 to 300 percent (default 100) independently of bubble size, height offset runs from -2 to 4 blocks, and a per-marker Idle bobbing switch (default on) moves the symbol 0.05 blocks on a four-second cycle. The symbol grows about 10 percent while targeted; the floating bubble stays still through both animations.
- Indicator distance is saved per marker (1-1024 blocks, default 24) with an Unlimited option bounded by loaded terrain. Indicators fade through the outer quarter of the chosen distance and are obscured by walls.
- Three display modes for the floating bubble: When targeted (default), Always nearby (its own text distance, default 8 blocks, with its own Unlimited option, fading independently of the indicator), or On interaction (no floating bubble at all; reading and the inspector still work). The bubble is drawn screen-aligned above the indicator rather than as a billboard in the world, so it stays upright and never intersects a block; when a wall blocks the line of sight to the marker the bubble is hidden entirely instead of being clipped. The indicator itself is unchanged.
- Bubble size scales the text and its panel together from 40 to 350 percent (default 100) at constant letter density, so a longer description grows the panel instead of shrinking the letters. Floating body text is a bounded preview (first 12 lines, 600 characters); the reader keeps the full text.
- Optional title icon drawn to the left of a non-empty title, chosen from a searchable, paginated catalog of built-in game icons, registered mod icons, and loaded SVG textures, plus None to clear it. A reserved icon column keeps wrapping from moving the icon above the title; untitled bubbles carry no icon.
- Show description in bubble is off by default, so the bubble shows the title only. Turning it on adds the description to the bubble.
- Title (80 characters, single line), body (4096 characters), display mode, appearance, creator, and lock state persist in the world and on the dropped item when the marker is broken and placed again. All text is VTML-escaped in the bubble, reader, inspector, and item tooltip.
- Fresh crafted markers inherit the player's last saved appearance on that server with empty content; picked-up markers keep their own settings.
- Read marks are per player. The reader has a Mark as read / Mark as unread button; a marker you have read shows no floating bubble in any display mode and draws its indicator at half opacity, bobbing at half height and half speed, while plain right-click still opens the full text. Marking read needs no build permission. Saving an edit, picking the marker up and placing it again, and the editor's Clear read button (confirmed, disabled while locked, same permissions as Save & Close) all issue a fresh stamp that makes the marker unread again for everyone. Your marks are stored per player on the server and sent to your client on join.
- Creator lock via the lock glyph at the top right of the editor: only the creator can arm it, and Save & Close applies the lock; Cancel discards a pending lock. No padlock item is involved.
- A locked marker opens read-only for everyone, including its creator - every field and Save & Close are disabled until it is unlocked. The creator, or a server admin with the `controlserver` privilege, clicks the same glyph to unlock and reopen the editor editable. Land-claim build access is still required for both, and while a marker is locked nobody can break or pick it up - not its creator, not an admin - until it is unlocked in the editor.
- Creator and lock metadata survive world reload and pickup/replacement. Editing an unlocked marker does not transfer ownership. Locked markers resist explosions. Edits and unlocks are written to the server audit log.
- `EnableSceneMarkers` (default `true`) gates the whole feature server-side. With it set to `false`, existing markers stay in the world as plain blocks, but placement, editing, floating bubbles, indicators, and the crafting recipe are all disabled. The setting is read at startup, so changing it requires a server restart.

Scene markers are independent world content. They borrow visual conventions from cast environmental messages, but do not use the RP chat pipeline or the local/global OOC chat toggles.

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
- Optional hide-unless-targeting nametags for other players; the local player's own nametag still
  follows vanilla first-person/third-person camera behavior.
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
