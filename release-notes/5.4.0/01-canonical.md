# The BASICs v5.4.0

This release rolls up all changes since `v5.3.0`.

## Highlights

- Overhead speech bubbles got a major upgrade:
  - Optional RP bubble override via `OverrideSpeechBubblesWithRpText`
  - VTML rendering support in bubbles (italics, tags, icons)
  - Better LOS behavior and bubble spacing
- Typing indicators were redesigned into stateful UX (chat open, composing, typing) with display-mode options.
- Full i18n support landed for user-facing strings via lang keys, plus Crowdin mapping support.
- OOC/GOOC behavior got critical polish:
  - `((...))` is now explicitly denied when `EnableGlobalOOC=false`
  - OOC player chat now uses the bubble VTML/styling path consistently
- Heritage language grants were reintroduced and expanded:
  - automatic grants by class, class traits, and extra traits
  - optional PlayerModelLib model/group-based grants
  - safer revoke behavior when `RemoveGrantedLanguagesOnChange=true`

## Chat and RP Improvements

- Speech/emote/environment bubble rendering quality improvements
- Better language-aware RP bubble formatting
- Stronger guardrails around language switching and language-disabled mode behavior
- Improved nametag and indicator behavior in RP chat contexts
- Heritage watcher flow now keeps language grants in sync on class/trait/model changes

## Admin and Reliability Improvements

- New configurable permission for `/rptext` toggle (`RPTextTogglePermission`)
- `/setdurability` parsing/behavior fixes
- Player stats clear command hardening for better error handling
- Additional config/load hardening and debug/perf instrumentation improvements

## Localization and Translation Workflow

- `en.json`-driven localization introduced for user-facing strings
- Crowdin config added and mapped to Vintage Story locale conventions
- Added non-English locale files for:
  - `de`
  - `es`
  - `fr`
  - `ru`
  - `zh-cn`

## Included Upstream PRs

- `#41` RP chat: overhead bubbles + typing indicator
- `#72` `System.Net.Http` security bump
- `#73` i18n lang-file support for user-facing strings
- `#74` `/rptext` permission configurability
- `#78` Crowdin language mapping
- `#80` OOC/GOOC behavior fixes
- `#83` non-English GOOC-disabled translations
- `#90` heritage language grants for class/trait/model bindings

## Known Follow-up

- `#82` Speech bubble wrapping edge case for long unbroken words
- `#91` heritage language class-change notification churn (net-delta UX follow-up)
