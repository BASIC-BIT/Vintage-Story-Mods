The BASICs v5.4.0

This is a major quality and UX update for RP chat and localization.

Highlights

- Overhead speech bubbles received a major update:
  - Optional RP bubble override (`OverrideSpeechBubblesWithRpText`)
  - VTML rendering support path improvements (formatting/icons)
  - Better line-of-sight behavior and visual spacing
- Typing indicators now support multiple states (chat open, composing, typing) with display-mode options.
- Full localization groundwork is now in place:
  - user-facing strings moved to lang keys
  - Crowdin mapping for Vintage Story locale naming
  - non-English locale files included (`de`, `es`, `fr`, `ru`, `zh-cn`)

Behavior and moderation fixes

- Global OOC delimiter behavior (`((...))`) now gives explicit deny feedback when GOOC is disabled.
- OOC bubble rendering/styling path is now consistent.
- `/rptext` permission is now configurable (`RPTextTogglePermission`) for server operators.

Additional reliability improvements

- `/setdurability` command parsing and behavior fixes
- better hardening around player stats clear commands and config-related edge cases

Version

- Updated from v5.3.0 to v5.4.0

Known follow-up

- Long no-space word bubble wrapping edge case is tracked separately (GitHub issue #82)

Thanks to everyone who tested, reported issues, and helped verify fixes.
