# The BASICs v5.5.0

This release updates The BASICs for Vintage Story `1.22.2` and makes the default setup easier for RP servers.

## Highlights

- Supports Vintage Story `1.22.2`.
- Adds character chatter for spoken RP messages.
- Makes TPA available to regular players by default, while still requiring a temporal gear by default.
- Adds placed environmental messages with `!!message` and `/envhere`.
- Improves RP chat defaults for proximity chat, global OOC, overhead speech bubbles, nametags, and typing indicators.
- Cleans up language and admin feedback so mistakes are clearer in chat.

## Server Defaults

The generated/default config is now more feature-forward for RP servers. Existing explicit values in `ModConfig/the_basics.json` are respected.

- Proximity chat opens by default.
- Global OOC works out of the box with `((...))`.
- Save-complete announcements are enabled by default.
- Character chatter is enabled by default.
- TPA is available to normal players by default, while still requiring a temporal gear by default.
- Nametags and typing indicators avoid leaking player presence through walls.
- RP-processed overhead speech bubbles are enabled by default, with a vanilla-bubble opt-out.

Server owners who prefer quieter or more conservative behavior should review `ModConfig/the_basics.json` after upgrading.

## Fixes And Polish

- Fixed character chatter on Vintage Story `1.22.2`.
- Speakers now hear their own chatter more quietly than nearby listeners.
- Kept `/chatter off` as a receive-only opt-out, so players can mute chatter they hear without changing what others hear from them.
- Reduced stale nametag state after startup and player joins.
- Kept typing indicators line-of-sight gated to avoid leaking player presence through walls.
- Added a clear opt-out for The BASICs RP overhead speech bubbles; disabling it falls back to vanilla speech bubbles.
- Replaced confusing TPA privilege behavior with a clearer setting. Old configs continue to work.
- Improved localization and customization for babble and sign-language text.
- Rejected unknown `:prefix message` language syntax instead of letting it pass through as normal speech.
- Improved over-limit admin language grants so admins can bypass the player language cap while receiving a warning.
- Improved default-language removal copy and valid-language list formatting.

## Known Follow-up

- In-game admin config panel and live config reload are tracked separately in GitHub issue `#125`.
- Speech bubble wrapping for long unbroken words remains tracked separately in GitHub issue `#82`.
