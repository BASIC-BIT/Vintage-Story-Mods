# The BASICs v5.9.1

## Fixes nametag line-of-sight freezes caused by entity searches

Servers using `NametagRequiresLineOfSight=true` could cause severe client hitching or freezes, especially at long nametag ranges. Line-of-sight checks now skip an entity search that could not affect the result while preserving Vintage Story's block-selection geometry.

Stone still blocks a nametag, while glass and foliage do not. The configured range boundary for other players, and the eye, torso, and feet visibility checks, are unchanged.

`HideNametagUnlessTargeting=true` now applies only to other players. Your own nametag continues to follow vanilla behavior: hidden in first person and visible in F5 third person.

## Spectators stay invisible until they deliberately communicate

Active spectators no longer reveal their position through speech bubbles, stale bubble textures, typing indicators, or positional seraph chatter. With the new protection enabled, plain speech, signing, and name-led emotes are refused while the player is invisible.

Spectators can still deliberately use local OOC, global OOC, nameless environmental narration, and placed environmental text. Local OOC uses the account name by default so an invisible speaker is identified clearly.

The spectator protections are enabled automatically for existing and new configurations. Server owners can adjust them live without a restart:

- `ProtectSpectatorRoleplayChat=false` allows embodied RP messages again. Spectator-attached visual and positional cues remain suppressed.
- `UseNicknameInSpectatorOOC=true` uses the RP nickname for spectator local OOC.
- `AllowSpectatorPlacedEnvironmentalMessages=false` blocks spectator use of `!!` and `/envhere`.

## Vintage Story 1.22.7

This release builds and is tested against Vintage Story 1.22.7.
