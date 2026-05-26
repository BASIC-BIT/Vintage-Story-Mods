# The Basics

*As seen on Saltpoint RP, Fair Travels, and various other servers, finally released on ModDB!*

**ModDB: https://mods.vintagestory.at/thebasics**

## Features

The maintained feature inventory lives in [`docs/FEATURES.md`](docs/FEATURES.md). The release smoke-test matrix lives in [`docs/RELEASE_SMOKE_TEST.md`](docs/RELEASE_SMOKE_TEST.md).

High-level systems:

 * RP proximity chat with configurable talking ranges, nicknames, language support, emotes, environment messages, OOC, global OOC, placed environmental bubbles, RPTTS dispatch, and optional character chatter sounds.
 * Client-side RP UX: VTML overhead bubbles, bubble line-of-sight gating, typing indicators, nametag behavior, chat tab persistence, and placed environmental bubble rendering.
 * Server save notifications.
 * Sleep notifications.
 * Player stat tracking for deaths, player kills, NPC kills, block breaks, and distance travelled.
 * Permission-gated chat history search for staff, with GUI search, text command backup, export, and retention/purge controls.
 * TPA (teleport request) system with optional temporal gear cost, cooldowns, timeouts, and privileges.
 * Admin repair utility for setting item durability.

All features can be toggled.  If you want more granularity in any feature toggles, feel free to suggest it.

## RP Proximity Chat System

The RP Proximity Chat System is a feature-rich chat system designed for role-playing servers. It includes configurable talking ranges, nicknames, automatic message formatting, and more.

### Features
- **Proximity Chat**: Adds a new chat tab "Proximity", that can only be heard by nearby players
- **Nicknames**: Players can set and clear their own nicknames
- **Dynamic talking ranges**: Four different chat ranges (whisper, "normal", yelling, and signing)
- **Emote and environment messages**: Add additional flavor to roleplay
- **Language System**: Custom configurable language system; players can only fully understand languages they know
- **Placed Environment Messages**: Aim at a block and use `!!message` or `/envhere message` to place an environmental bubble in the world
- **Character Chatter**: Seraph voice/instrument chatter when players speak, with per-player opt-out

### Commands

- **/nick [nickname]**: Set your nickname.
- **/clearnick**: Clear your nickname.
- **/me [emote]**: Send a proximity emote message.
- **/it [envMessage]**: Send a proximity environment message.
- **/yell [message]**: Yell a message or set chat mode to yelling.
- **/say [message]**: Say a message or set chat mode to normal.
- **/whisper [message]**: Whisper a message or set chat mode to whispering.
- **/emotemode [on/off]**: Turn emote-only mode on or off.
- **/rptext [on/off]**: Turn RP text on or off for your messages.
- **/ooc [message]**: Send local OOC.
- **/gooc [message]**: Send global OOC when enabled.
- **/chatter [on/off]**: Toggle whether you hear character chatter sounds.
- **/envhere [message]**: Place an environmental message at the targeted block.

### Configuration

The system is highly configurable through the mod's configuration file. You can adjust the talking ranges, enable or disable features, and customize message formatting.

Version 5.5.0 makes the default generated config more feature-forward for RP servers: proximity chat opens by default, global OOC is enabled, server save completion announcements are enabled, chatter sounds are enabled, and LOS-gated nametags/sign language are enabled. Existing explicit config values are respected, so review `ModConfig/the_basics.json` after upgrading if you want quieter or more conservative defaults.

TPA is also enabled and usable by default in 5.5.0 via `TpaRequestPrivilege=chat`, while `TpaRequireTemporalGear=true` keeps teleport requests from becoming free fast travel.

Nametags are also configurable (in `ModConfig/the_basics.json`):
- `ShowNicknameInNametag`: show the RP nickname above heads
- `ShowPlayerNameInNametag`: show the Vintage Story account name above heads
- `HideNametagUnlessTargeting`: only show nametags when the player is targeted
- `NametagRenderRange`: how far away nametags render

RP speech formatting can be relaxed for servers that prefer player-authored casing and punctuation:
- `ProximityChatPresentationMode`: controls chat/bubble presentation. Allowed values are `StandardRoleplay`, `SimpleSpeech`, `PlainProximity`, and `Prose`.
  `Prose` treats unquoted text as narration and only quoted segments as spoken language, including language color and chatter sounds.
- `ProseNicknameToken`: in Prose mode, this standalone token is replaced with the sender's formatted RP nickname. Default: `@`. Set to an empty string to disable.
- `AttributeFreeformMessagesToPlayerName`: when true, Prose and environmental messages are prefixed with the account name in brackets, for example `[PlayerName]`. Default: `false`.
- `NormalizeProximityChatText`: when true, automatically capitalizes and punctuates RP speech/emotes/environmental messages. When false, typed casing and punctuation are preserved.
- `OverheadChatBubbleMode`: controls overhead bubbles. Allowed values are `RpText`, `Vanilla`, and `Off`.

### Example Usage

To set a nickname: `/nick MyNickname`

To send an emote message: `/me waves towards the stranger.`

To send an environment message: `/it The wind howls through the trees.` or `!The wind howls through the trees.`

To send a local OOC message: `(I'll BRB 2 minutes)`

#### Languages
Languages are defined via the config:
```json
{
  "Languages": [
    {
      "Name": "Common",
      "Prefix": "c",
      "Description": "The universal language",
      "Syllables": ["al", "er", "at", "th", "it", "ha", "er", "es", "s", "le", "ed", "ve"],
      "Color": "#92C4E1",
      "Default": true
    },
    {
      "Name": "Tradeband",
      "Prefix": "tr",
      "Description": "A common language for ease of trade across regions",
      "Syllables": ["feng", "tar", "kin", "ga", "shin", "ji"],
      "Color": "#D4A96A",
      "Default": false
    }
  ]
}
```
Language prefixes are used to set your current speaking language for future messages, or to speak a single message in that language.

The language system can be completely disabled if desired via the config flag `EnableLanguageSystem`


Players can add and remove languages via:
- `/addlang [language]`
- `/remlang [language]`
- `/listlang`

Admins can set another players languages via:
- `/adminaddlang [player] [language]`
- `/adminremovelang [player] [language]`
- `/adminlistlang [player]`

The permission required to use the commands can be configured via the config values `ChangeOwnLanguagePermission` and `ChangeOtherLanguagePermission`.

## Typing Indicator

The BASICs can optionally show a small "Typing..." indicator above a player's head.

Notes:
- It is rendered client-side, but controlled by server config.
- It shows above other players (you won't see it above yourself).

Configuration keys (in `ModConfig/the_basics.json`):
- `EnableTypingIndicator`: master toggle
- `TypingIndicatorMaxRange`: max range (blocks) to see the indicator
- `TypingIndicatorTimeoutSeconds`: how long after the last input change the indicator stays on
- `TypingIndicatorTextOverride`: override the displayed text (otherwise uses lang key `thebasics:typingindicator-typing-text`)
- `TypingIndicatorDisplayMode`: show icon only, text only, or both
- `DebugMode`: enables verbose debug logging/diagnostics (recommended off unless troubleshooting)

Notes:
- The indicator uses multiple states (chat open / composing / actively typing) for a unified UX.



## Overhead Speech Bubble Override

Vintage Story shows a short chat bubble above player heads for nearby chat.

The BASICs overrides RP speech bubbles while RP chat is enabled so the bubble can reflect processed RP text rather than raw typed text.

Applies to:

- speech
- emotes (`/me` or `*...`)
- environmental messages (`/it` or `!...`)
- placed environmental messages (`/envhere` or `!!...`)

Clients render VTML in overhead bubbles, including italics, font tags, icons, and subtle kind-specific styling.

Notes:

- Vanilla overhead bubbles render plain text (they do not parse VTML).
- `OverrideSpeechBubblesWithRpText` is deprecated and ignored.
- `DisableRpOverheadBubbles` is deprecated; use `OverheadChatBubbleMode=Vanilla` to fall back to vanilla speech bubbles or `OverheadChatBubbleMode=Off` to suppress overhead chat bubbles entirely.



## Contributions

Feel free to submit PRs, or feature requests! To submit a request or issue, you can use Issues, submit a comment on ModDB, or send me a PM on discord.
