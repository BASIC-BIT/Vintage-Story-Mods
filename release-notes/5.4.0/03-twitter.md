# The BASICs v5.4.0 - Twitter/X Drafts

## Option A: Single post

The BASICs v5.4.0 is live.

Major RP chat update: upgraded overhead bubbles, multi-state typing indicators, i18n lang keys + Crowdin mapping, and OOC/GOOC fixes.

Also includes configurable `/rptext` permission.

#VintageStory #GameDev

## Option B: 4-post thread

### Post 1

The BASICs v5.4.0 is live. Big one.

#VintageStory #GameDev

### Post 2

RP chat got major upgrades:
- Better overhead bubble rendering (including VTML path improvements)
- Better LOS/visual behavior
- Improved typing indicators (chat-open/composing/typing states)

### Post 3

Localization pipeline landed:
- i18n lang-key support for user-facing strings
- Crowdin mapping for VS locale conventions
- non-English locale files included (`de`, `es`, `fr`, `ru`, `zh-cn`)

### Post 4

Server/operator polish:
- configurable `/rptext` permission
- explicit deny feedback for `((...))` when GOOC is disabled
- OOC bubble path consistency improvements

Known follow-up tracked: long unbroken-word bubble wrapping edge case (`#82`).
