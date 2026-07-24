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

### One-time v5.8.0 safety migration

The first v5.8.1 load adds a command-registration defaults marker and switches all five families off for configurations that do not already contain that marker. This includes configurations where v5.8.0 wrote its previous `true` defaults to disk.

Because a persisted v5.8.0 `true` value does not reveal whether it was an intentional administrator choice or merely the old default, v5.8.1 takes the safer course and resets it to `false`. Administrators who intentionally opted in on v5.8.0 must re-enable the desired families once and restart. Explicit `false` values remain false, and choices made after the v5.8.1 migration are preserved.

## Language-understanding boundary

The BASICs still provides its base language behavior without a companion mod: known-versus-unknown language rendering, scrambling, and manual whole-language proficiency management through `/adminsetlangskill`.

Organic semantic exposure learning and concept-aware partial comprehension require the optional server-side Language Understanding companion mod (`thebasicslanguageunderstanding`). If that companion is not installed or available, the base language system continues to work, while semantic concept matching and automatic exposure learning remain inactive.

The BASICs has no hard runtime dependency on BASIC Config, Dimension Lib, Pocket Dimensions, or the Language Understanding companion.

## Installation

Replace the previous The BASICs ZIP with `thebasics_5_8_1.zip`, ensure only one The BASICs ZIP is present in the server's `Mods` directory, and restart the server.
