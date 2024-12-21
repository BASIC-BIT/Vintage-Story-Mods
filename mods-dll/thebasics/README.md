# The Basics

*As seen on Saltpoint RP, Fair Travels, and various other servers, finally released on ModDB!*

**ModDB: https://mods.vintagestory.at/thebasics**

## Features

 * RP Proximity chat system, with configurable talking ranges, nicknames, automatic message formatting, and more!
 * Server save notification
 * Player stat tracking, currently including deaths, player kills, and mob kills
 * TPA (Teleport request) system, configurable with cooldowns and privileges

All features can be toggled.  If you want more granularity in any feature toggles, feel free to suggest it.

## RP Proximity Chat System

The RP Proximity Chat System is a feature-rich chat system designed for role-playing servers. It includes configurable talking ranges, nicknames, automatic message formatting, and more.

### Features
- **Proximity Chat**: Adds a new chat tab "Proximity", that can only be heard by nearby players
- **Nicknames**: Players can set and clear their own nicknames
- **Dynamic talking ranges**: Four different chat ranges (whisper, "normal", yelling, and signing)
- **Emote and environment messages**: Add additional flavor to roleplay
- **Language System**: Custom configurable language system, players can only speak and understand languages they know

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
- **/hands [message]**: Sign a message or set chat mode to sign language.

### Configuration

The system is highly configurable through the mod's configuration file. You can adjust the talking ranges, enable or disable features, and customize message formatting.

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
Language prefixes are used to set your current speaking language.  This can either be set as your "defaul"

The language system can be completely disabled if desired via the config flag `EnableLanguageSystem`


Players can add and remove languages via:
- `/addlang [language]`
- `/remlang [language]`

Admins can set another players languages via:
- `/addlangadmin [player] [language]`
- `/remlangadmin [player] [language]`

The permission required to use the commands can be configured via the config values `ChangeOwnLanguagePermission` and `ChangeOtherLanguagePermission`.



## Contributions

Feel free to submit PRs, or feature requests! To submit a request or issue, you can use Issues, submit a comment on ModDB, or send me a PM on discord.
