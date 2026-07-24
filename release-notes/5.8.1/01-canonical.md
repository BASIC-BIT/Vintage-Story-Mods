# The BASICs v5.8.1

v5.8.1 is a corrective release that makes the five new non-request teleport command families opt-in for server owners.

## Teleport command defaults

- `/home`, `/sethome`, `/delhome`, and `/homes` are no longer registered by default.
- `/spawn` and `/setspawn` are no longer registered by default.
- `/stuck` is no longer registered by default.
- `/top` is no longer registered by default.
- `/back` is no longer registered by default, and its global last-position recorder remains inactive until the command family is enabled.
- Server owners can enable only the families they want with `RegisterHomeCommands`, `RegisterSpawnCommands`, `RegisterStuckCommand`, `RegisterTopCommand`, and `RegisterBackCommand`, then restart the server.
- The opt-in privilege defaults are unchanged: player-facing teleport commands use `chat`, while `/setspawn` uses the administrative `commandplayer` privilege.

These new defaults apply when the settings are absent, including freshly generated configuration. Existing explicitly serialized `true` or `false` values are preserved.

## Language-understanding boundary

The BASICs still provides its base language behavior without a companion mod: known-versus-unknown language rendering, scrambling, and manual whole-language proficiency management through `/adminsetlangskill`.

Organic semantic exposure learning and concept-aware partial comprehension require the optional server-side Language Understanding companion mod (`thebasicslanguageunderstanding`). If that companion is not installed or available, the base language system continues to work, while semantic concept matching and automatic exposure learning remain inactive.

The BASICs has no hard runtime dependency on BASIC Config, Dimension Lib, Pocket Dimensions, or the Language Understanding companion.

## Installation

Replace the previous The BASICs ZIP with `thebasics_5_8_1.zip`, ensure only one The BASICs ZIP is present in the server's `Mods` directory, and restart the server.
