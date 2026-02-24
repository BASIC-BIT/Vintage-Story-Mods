# The BASICs v5.4.0 is out

Big release this round.

## What is new

- Overhead RP bubbles got a major upgrade (VTML support, better LOS behavior, better spacing)
- Typing indicators now support chat-open/composing/typing states with display mode options
- Full i18n foundation landed (lang keys + Crowdin mapping + non-English locale files)
- OOC/GOOC polish:
  - `((...))` now gives explicit deny feedback when global OOC is disabled
  - OOC chat now follows the proper bubble styling/render path

## Admin/server quality upgrades

- New configurable permission for `/rptext` (`RPTextTogglePermission`)
- `/setdurability` reliability fixes
- Better safety/error handling in stats clear commands and config-loading paths

## Version

- Updated from `v5.3.0` to `v5.4.0`

## Known follow-up

- Tracking one bubble-wrapping edge case for long no-space words: `#82`

Thanks everyone for the feedback and testing help.
