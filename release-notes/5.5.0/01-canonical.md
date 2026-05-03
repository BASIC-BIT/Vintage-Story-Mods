# The BASICs v5.5.0

This release is the Vintage Story `1.22.1` / `.NET 10` compatibility and release-smoke update for The BASICs.

## Highlights

- Updated The BASICs for Vintage Story `1.22.1` and the `.NET 10` runtime used by current stable servers.
- Added and smoke-tested character chatter, so speech can play the speaker's seraph voice/instrument sounds.
- TPA is now usable by default for normal players, while still requiring a temporal gear by default.
- Added raycast-placed environmental bubbles with `!!message` and `/envhere`.
- Tightened language, sign-language, nametag, and bubble visibility behavior for RP use.
- Added maintainer-facing feature inventory and release smoke-test docs.

## Defaults-On Config Shift

The generated/default config is now more feature-forward for RP servers. Existing explicit values in `ModConfig/the_basics.json` are respected, but new configs and missing keys now default to:

- `ProximityChatAsDefault=true` so the Proximity tab is selected by default.
- `EnableGlobalOOC=true` so `((...))` global OOC works out of the box.
- `SendServerSaveFinishedAnnouncement=true` so players get save-complete feedback by default.
- `EnableChatter=true` so speech chatter is showcased by default.
- `TpaRequestPrivilege=chat` and `TpaRequireTemporalGear=true` so TPA works out of the box but still costs a temporal gear.
- `RequireLineOfSightForSignLanguage=true` so sign language cannot be read through walls by default.
- `NametagRequiresLineOfSight=true` so nametags are not visible through walls by default.
- `DisableRpOverheadBubbles=false` so RP overhead/world bubbles are enabled by default, with an explicit opt-out.
- `TpaRequestPrivilege=chat` so TPA is actually usable by normal players when enabled, while temporal gear still provides friction.

Server operators should review `ModConfig/the_basics.json` after upgrading if they prefer quieter or more conservative defaults.

## Smoke-QA Fixes

- Fixed chatter playback on Vintage Story `1.22.1` by supporting the current player `talkUtil` path.
- Added `ChatterSelfVolumeMultiplier` so speakers hear their own chatter more quietly than listeners.
- Kept `/chatter off` as a receive-only opt-out, so players can mute chatter they hear without changing what others hear from them.
- Reapplied nametag range/visibility attributes for online players on startup and join, reducing stale client nametag state.
- Added `NametagRequiresLineOfSight` to gate client-side nametag rendering by LOS.
- Added `RequireLineOfSightForSignLanguage` to control sign-language delivery through walls.
- Kept typing indicators line-of-sight gated to avoid leaking player presence through walls.
- Added `DisableRpOverheadBubbles` as the inverse replacement for deprecated `OverrideSpeechBubblesWithRpText`; disabling it falls back to vanilla speech bubbles.
- Replaced confusing `AllowTpaPrivilegeByDefault` with explicit `TpaRequestPrivilege`; old configs migrate `true` to `chat` and `false` to `tpa`.
- Confirmed TPA's default posture: enabled, available to normal players, and still protected from free fast-travel spam by `TpaRequireTemporalGear=true`.
- Moved default proximity chat name, babble verb, and sign verb text into lang keys while preserving config overrides.
- Rejected unknown `:prefix message` language syntax instead of letting it pass through as normal speech.
- Improved over-limit admin language grants so admins can bypass the player language cap while receiving a warning.
- Improved default-language removal copy and valid-language list formatting.

## Compatibility And Tooling

- The BASICs package/build path now targets the current Vintage Story runtime and deployment layout.
- Local packaging now deploys to all local `D:\Games\VSProfiles\Profile*\Mods` folders by default when that profile directory exists.
- Added `docs/FEATURES.md` as a maintained feature/config inventory.
- Added `docs/RELEASE_SMOKE_TEST.md` as the broad compatibility smoke-test checklist.

## New Or Changed Config Keys

- `EnableChatter`
- `ChatterModeVolume`
- `ChatterModePitch`
- `ChatterSelfVolumeMultiplier`
- `MaxEnvironmentPlacementDistance`
- `RequireLineOfSightForSignLanguage`
- `NametagRequiresLineOfSight`
- `DisableRpOverheadBubbles`
- `TpaRequestPrivilege`

## Known Follow-up

- In-game admin config panel and live config reload are tracked separately in GitHub issue `#125`.
- Speech bubble wrapping for long unbroken words remains tracked separately in GitHub issue `#82`.
