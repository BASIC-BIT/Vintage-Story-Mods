[h2]The BASICs v5.8.1[/h2]

v5.8.1 is a corrective release that makes the five new non-request teleport command families opt-in for server owners.

[h3]Teleport command defaults[/h3]

[list]
[*][b]/home, /sethome, /delhome, and /homes[/b] are no longer registered by default.
[*][b]/spawn and /setspawn[/b] are no longer registered by default.
[*][b]/stuck[/b] is no longer registered by default.
[*][b]/top[/b] is no longer registered by default.
[*][b]/back[/b] is no longer registered by default, and its global last-position recorder remains inactive until the command family is enabled.
[*]Server owners can enable only the families they want with [b]RegisterHomeCommands[/b], [b]RegisterSpawnCommands[/b], [b]RegisterStuckCommand[/b], [b]RegisterTopCommand[/b], and [b]RegisterBackCommand[/b], then restart the server.
[*]The opt-in privilege defaults are unchanged: player-facing teleport commands use [b]chat[/b], while [b]/setspawn[/b] uses the administrative [b]commandplayer[/b] privilege.
[/list]

[h3]One-time v5.8.0 safety migration[/h3]

The first v5.8.1 load adds a command-registration defaults marker and switches all five families off for configurations that do not already contain that marker. This includes configurations where v5.8.0 wrote its previous [b]true[/b] defaults to disk.

Because a persisted v5.8.0 [b]true[/b] value does not reveal whether it was an intentional administrator choice or merely the old default, v5.8.1 takes the safer course and resets it to [b]false[/b]. Administrators who intentionally opted in on v5.8.0 must re-enable the desired families once and restart. Explicit [b]false[/b] values remain false, and choices made after the v5.8.1 migration are preserved.

[h3]Language-understanding boundary[/h3]

The BASICs still provides its base language behavior without a companion mod: known-versus-unknown language rendering, scrambling, and manual whole-language proficiency management through [b]/adminsetlangskill[/b].

Organic semantic exposure learning and concept-aware partial comprehension require the optional server-side Language Understanding companion mod ([b]thebasicslanguageunderstanding[/b]). If that companion is not installed or available, the base language system continues to work, while semantic concept matching and automatic exposure learning remain inactive.

The BASICs has no hard runtime dependency on BASIC Config, Dimension Lib, Pocket Dimensions, or the Language Understanding companion.

[h3]Installation[/h3]

Replace the previous The BASICs ZIP with [b]thebasics_5_8_1.zip[/b], ensure only one The BASICs ZIP is present in the server's Mods directory, and restart the server.
