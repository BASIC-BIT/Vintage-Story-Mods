# The BASICs Command Reference

Use `/thebasics help`, `/basic help`, or `/tb help` in game for a short command summary.
Use `/thebasics guide` or press `H` and choose The BASICs in the handbook for the in-game guide.

## Localization Note

New guide/help strings in non-English locale files intentionally use English fallback text until human translations are available.

## Player Commands

- `/me`, `/it`, `/envhere`, `/ooc`, `/gooc`, `/say`, `/yell`, `/whisper` - RP chat commands.
- `/nick`, `/clearnick`, `/nickcolor`, `/clearnickcolor` - Manage RP nickname identity when enabled.
- `/rptext`, `/emotemode`, `/chatter` - Per-player RP chat presentation toggles.
- `/chatprefs` - View or change local chat accessibility and color preferences.
- `/thebasics guide`, `/basic guide`, `/tb guide` - Link to the in-game Survival Handbook guide.
- `/charsheet` - Open your character sheet.
- `/look` - Inspect visible character details for a nearby player.
- `/bio` - Quick character sheet view alias.
- `/character` - Manage optional RP character slots when enabled.
- `/notes` - Open your private notes.
- `/notes about <player>` - Open private notes about another player.
- `/notes help` - Show private notes command syntax.
- `/tpa`, `/tpahere`, `/tpaccept`, `/tpdeny`, `/tpalist`, `/tpallow`, `/tpacancel` - Teleport request commands when enabled.
- `/playerstats` - View player stats when enabled.

## Staff Commands

- `/adminnotes <player>` - Open staff notes for a player.
- `/adminnotes <player> list|add|view|edit|delete|ledger` - Manage staff notes from chat commands.
- `/adminnotes help` - Show staff notes command syntax.
- `/adminviewcharsheet <player>` - View all fields on a player's character sheet.
- `/adminsetcharsheet <player> <field> <value>` - Set a player's character sheet field.
- `/adminclearcharsheet <player> [field]` - Clear one or all character sheet fields for a player.
- `/adminclearheadshot <player>` - Clear a player's character headshot.
- `/adminsetnick <player> <nickname>` - Set another player's RP nickname.
- `/adminsetnickcolor <player> <color>` - Set another player's nickname color.
- `/adminaddlang`, `/adminremovelang`, `/adminlistlang` - Manage player languages when the language system is enabled.

## Server Operator Commands

- `/thebasics config`, `/basic config`, `/tb config` - Open the admin config panel.
- `/thebasics config languages` - Open the language definition editor.
- `/thebasics config charsheetfields` - Open the character sheet field editor.
- `/thebasics reloadconfig` - Reload The BASICs config from disk.
- `/thebasics character list <player>` - List an online player's RP character slots.
- `/thebasics character select <player> <character>` - Force-select an online player's RP character slot.
