# Visual settings human QA run

The owner authorized starting human QA on October 28. No human card has passed yet. Use the [numbered QA cards](2026-10-28-visual-settings-human-qa.md) to report observations; automated checks and agent captures do not establish human acceptance.

## Preparation

- Profile2 control helper rebuilt from the pinned recovery source, with 23 tests passing. Its previous package and configuration have rollback copies.
- Panel reads work; panel connector writes return `writes_disabled`. The documented repository Client API and SFTP route is being used.
- Panel backups are unavailable because the server has a zero-backup limit. A stopped-world local snapshot contains 84 files, including the active save, configuration, mod data and player data. File sizes were verified and local SHA256 values recorded. No restore drill has been performed.
- Reviewed BASICs 5.9.1 package staged on the disposable server and Profile2/Profile3, with verified matching hashes. Other profile mods were preserved.
- Server reached a fresh RunGame phase. BASICs systems registered. A duplicate FlywheelPower warning and bare `/home/container` error also appear in the pre-staging baseline; they remain unresolved.
- Profile2 connected using Vintage Story 1.22.7. Agent Control reported protocol 1.0 and discovered the BASICs setup operations. Sending `/basic setup` completed; the server responded “Opening The BASICs setup.” No configuration was saved.
- Profile3 has the reviewed package but has not been launched for this run. Its normal-player role and permission checks remain pending. Begin with the Profile2 draft checks; prepare Profile3 before card 1's access checks and the later two-player cards.

## Human observations

All cards are pending. Batch A is draft-only: do not save changes. The first requested observation is card 2, navigation and unsaved-close behavior. Cards 2-11 are available in the linked plan for the owner to work through in order or report individually.

### Feedback and fixes during Batch A

The owner reported overlapping palette and underlying-setting tooltips, with a screenshot. A standalone regression reproduced the collision at scales 1 and 1.25. Background tooltips now stop rendering while a color picker, privilege suggestion list or dropdown is open in the settings dialogs. The popup's own tooltips remain available. Shared protection covers setup, advanced settings, languages, scene markers and the character-sheet dialogs.

The owner also requested polish for language starting-speaker rules. The section is now named Starting speakers, puts Everyone starts with it above a two-column rule form, and explains that any matching rule grants the language. Rules and labels are disabled when everyone already knows the language; hover help explains how to enable editing. Existing IDs are preserved when switching modes. Compact renders at 1280x720 and scales 1 and 1.25 fit.

These source changes passed focused standalone checks. They have not been packaged or installed into the running Profile2 session. Retest the palette overlap and starting-speaker form after refreshing the QA build; no human pass is inferred from automated results.

Local evidence: `.tmp/qa-tooltip-language-final.log`, `.tmp/qa-popup-all-hosts.log`. Standalone images: `C:/Users/steve/.codex/visualizations/2026/10/02/01a0fa70-8ca5-7321-ac1f-07fe824f5be2/qa-tooltip-language-20261028/`.

### Continuous language editor

The owner approved removing the extra Details / Starting speakers tabs. Language details and Starting speakers now occupy one scrolling form. The title, language selector, chat previews and validation messages remain fixed. Keyboard focus reveals off-screen fields; returning from another category preserves scroll position, while selecting another language starts at the top. Default-language rule disabling still preserves IDs.

All 64 focused checks passed, covering language editing, compact layout, scroll retention, keyboard access, palettes and tooltip suppression. Evidence: `.tmp/language-continuous-final.log`. Standalone captures at 1280x720, GUI scales 1 and 1.25: `C:/Users/steve/.codex/visualizations/2026/10/02/01a0fa70-8ca5-7321-ac1f-07fe824f5be2/language-continuous-20261028/`.

As requested, Profile2 has not been refreshed. These standalone previews precede the in-game retest.

## Evidence appendix

- Local run directory: `.tmp/human-qa-20261028-1793206009381/`
- Snapshot receipt: `backup-receipt.json`; snapshot: `active-world-backup/`
- Helper receipt and rollback: `profile/profile-receipt.json`
- Package staging and rollback: `stage/stage-receipt.json`, `stage/backup-index.json`, `stage/RESTORE.md`
- Reviewed package SHA256: `820ef471706a6996abcae994e3968eef4eaa1361a118e0fafe357170bb30dbc1`
- Source tree stamp: `1e9692b6c344ffa68521bcbfb0c1cffb9bfbb586e52e45fe0e74376a51ed5a69`

World snapshots and baseline files stay local. Do not commit them or include private player data in findings.
