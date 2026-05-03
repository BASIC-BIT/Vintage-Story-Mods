The BASICs v5.5.0

This is the Vintage Story 1.22.1 / .NET 10 compatibility and release-smoke update for The BASICs.

Highlights

- Updated The BASICs for Vintage Story 1.22.1 and the .NET 10 runtime used by current stable servers.
- Character chatter has been added and smoke-tested, so speech can play the speaker's seraph voice/instrument sounds.
- TPA is now usable by default for normal players, while still requiring a temporal gear by default.
- Placed environmental bubbles are available with `!!message` and `/envhere`.
- Language, sign-language, nametag, and bubble visibility behavior received smoke-QA hardening.
- Maintainer-facing feature inventory and release smoke-test docs were added.

Defaults-on config shift

The generated/default config is now more feature-forward for RP servers. Existing explicit values in `ModConfig/the_basics.json` are respected, but new configs and missing keys now default to:

- `ProximityChatAsDefault=true`
- `EnableGlobalOOC=true`
- `SendServerSaveFinishedAnnouncement=true`
- `EnableChatter=true`
- `TpaRequestPrivilege=chat` and `TpaRequireTemporalGear=true`
- `RequireLineOfSightForSignLanguage=true`
- `NametagRequiresLineOfSight=true`
- `DisableRpOverheadBubbles=false`
- `TpaRequestPrivilege=chat`

Server operators should review `ModConfig/the_basics.json` after upgrading if they prefer quieter or more conservative defaults.

Smoke-QA fixes

- Fixed chatter playback on Vintage Story 1.22.1 by supporting the current player `talkUtil` path.
- Added `ChatterSelfVolumeMultiplier` so speakers hear their own chatter more quietly than listeners.
- Kept `/chatter off` as a receive-only opt-out.
- Reapplied nametag range/visibility attributes for online players on startup and join.
- Added LOS gating for nametags and sign-language delivery.
- Kept typing indicators LOS-gated to avoid leaking player presence through walls.
- Added `DisableRpOverheadBubbles` as the inverse replacement for deprecated `OverrideSpeechBubblesWithRpText`; disabling it falls back to vanilla speech bubbles.
- Replaced confusing `AllowTpaPrivilegeByDefault` with explicit `TpaRequestPrivilege`; old configs migrate `true` to `chat` and `false` to `tpa`.
- Confirmed TPA's default posture: enabled, available to normal players, and still protected from free fast-travel spam by `TpaRequireTemporalGear=true`.
- Moved default proximity chat name, babble verb, and sign verb text into lang keys while preserving config overrides.
- Rejected unknown `:prefix message` language syntax instead of letting it pass through as normal speech.
- Improved over-limit admin language grants, default-language removal copy, and valid-language list formatting.

Compatibility and tooling

- The BASICs package/build path now targets the current Vintage Story runtime and deployment layout.
- Local packaging now deploys to all local `D:\Games\VSProfiles\Profile*\Mods` folders by default when that profile directory exists.
- Added `docs/FEATURES.md` and `docs/RELEASE_SMOKE_TEST.md` for release readiness.

Known follow-up

- In-game admin config panel and live config reload are tracked separately in GitHub issue #125.
- Speech bubble wrapping for long unbroken words remains tracked separately in GitHub issue #82.
