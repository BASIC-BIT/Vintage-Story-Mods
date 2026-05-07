# Issue 125 Config UI Dev Tracker

## Current Status

The branch has a first implementation of the admin config foundation:

- Admin-only commands: `/thebasics config`, `/tb config`, `/thebasics reloadconfig`, `/tb reloadconfig`.
- Server-authoritative save/reload packets on the existing `thebasics` network channel.
- Shared mutable server `ModConfig` instance via `BaseBasicModSystem`.
- Server-side setting registry and validation for an initial allowlist.
- Client `GuiJsonDialog` panel with save, reload, close, and mark-reviewed actions.
- The panel shows one setting group at a time via a group dropdown because Vintage Story's `GuiJsonDialog` is autosized and does not scroll.
- Persisted `ReviewedConfigSettingKeys` using `ProtoMember(100)`.
- Live refresh hooks for TPA timeout, save notifications, sleep notifications, nametag refresh, and typing-indicator clearing.
- Most scalar `ModConfig` values are now present in the admin registry; complex dictionaries/lists remain intentionally unsupported pending custom UI/validation.
- Command privilege settings now live-refresh on existing command instances for TPA, language, nickname color, OOC toggle, RP text toggle, and stat-clear commands.
- Admin config UI strings have lang keys in every supported language file, with English fallback text for non-English files.
- Docs/smoke checklist updated for the new admin config panel.
- Build passes with local SDK: `D:\bench\vs\.dotnet\dotnet.exe build mods-dll\thebasics\thebasics.csproj --configuration Release /p:GITHUB_ACTIONS=true`.
- Package verification passes: `mods-dll\thebasics\scripts\build-and-package.ps1` creates `mods-dll\thebasics\thebasics_5_5_0.zip`.
- Test server package deploy/boot verification passed for `thebasics_5_5_0.zip` with all six The BASICs systems loaded and no duplicate mod warning.
- QA profiles are prepped but not relaunched: Profile2/Profile3 local mod zips match package hash `BDCE1746B2846E60A4559D2304809B6108336652FCD96D2293987328C915C2D0`.
- Profile2/Profile3 `clientsettings.json` and `.bak.json` now contain required default movement/chat key mappings after the empty `keyMapping` block caused a pre-mod-load vanilla crash.

## Not Dev-Complete Yet

This is not complete for “all config values live-reload.” It is currently a safe live-subset implementation.

Remaining work before calling the feature dev-complete:

- Document unsupported complex config keys in the release notes/smoke plan if the product decision is to ship a scalar-settings UI first.
- Confirm in-game that the group-filtered `GuiJsonDialog` fits target client resolutions.
- Translate admin config UI strings for non-English language files; English fallback keys are present in every supported language file.
- Decide and implement the final policy for startup-shaped settings: live-gate/refactor them or keep them restart-required.
- Add `PlayerStatSystem` lifecycle refresh if player-stat settings become live-editable.
- Run in-game manual QA.

## Client QA Prep

- Do not launch clients until explicitly approved in the active conversation.
- Profile3 previously crashed before mods loaded because `keyMapping` was `{}` and `SystemPlayerControl` could not read `walkforward`; the follow-on `GuiScreenDisconnected` crash is vanilla disconnected-screen handling.
- Profile2 and Profile3 settings were backed up before repair using `*.qa-keymap-backup-20260506-045330` copies.
- Profile3 settings were also backed up before the mod-path correction using `*.qa-backup-20260506-044339` copies.
- Profile3 `modPaths` now points to `D:\Games\VSProfiles\Profile3\Mods` instead of Profile2's mod directory.
- Required key mappings verified present: `walkforward`, `walkbackward`, `walkleft`, `walkright`, `sneak`, `sprint`, `jump`, `sitdown`, `ctrl`, `shift`.

## Live-Reload Gap Inventory

Settings/features that are still restart-required or only partially covered:

- `DisableRPChat`: commands and transformer behavior are startup-shaped unless commands always register and gate in handlers.
- `DisableNicknames`: nickname commands are startup-shaped unless commands always register and gate in handlers.
- `AllowPlayerTpa`: TPA commands and join recovery are startup-shaped unless commands always register and gate in handlers.
- `EnableLanguageSystem`: language commands and heritage grant hooks need lifecycle/reconciliation work.
- `PlayerStatSystem`: player stat commands/events/tick need explicit subscribe/unsubscribe refresh before live toggle.
- Command privilege settings: TPA, language, nickname color, OOC toggle, RP text toggle, and stat-clear privileges now refresh live on existing command instances.
- Proximity group/default chat settings: chat group setup and client chat-tab behavior need reconciliation/rejoin handling.
- Dictionaries/lists: languages, delimiters, mode distances, mode verbs, player stat toggles, etc. need custom UI/validation/reconciliation.

## Current Safe Live Subset

Implemented or intended live-safe settings in the current registry include:

- Chatter toggle.
- Typing indicator toggle/display/range/timeout.
- Nametag content/range/targeting/line-of-sight behavior.
- Overhead bubble mode and minimum bubble display time.
- TPA temporal gear/cooldown/timeout settings, including timeout listener refresh.
- Save notification toggles and notification mode, including subscription refresh.
- Sleep notification toggle/threshold, including tick listener refresh.
- Debug mode.
- Simple scalar chat/RP presentation, OOC styling, chatter volume, notification text, and command privilege settings.

## Verification Still Needed

Automated/local:

- Package build: `mods-dll\thebasics\scripts\build-and-package.ps1` passes.
- Confirm generated zip includes new code and no duplicate mod issues after server boot.

Manual QA:

- Non-admin denied from opening/saving config.
- Admin can open, edit, save, close, and reopen config panel.
- `the_basics.json` persists changed values and `ReviewedConfigSettingKeys`.
- Live toggle `EnableTypingIndicator` clears stale indicators and re-enables without restart.
- Live nametag changes refresh online players.
- Live save/sleep notification changes affect subsequent save/sleep events.
- Restart-required setting save reports restart-required clearly.
- UI remains usable on the target client resolution.

## Manual QA Cards

Run these after explicit approval to relaunch clients. Use Profile2 as the admin-capable player unless server permissions indicate otherwise, and Profile3 as the non-admin player.

Card 1: Client Boot And Mod Load

- Start both QA profiles after closing any existing Vintage Story instances.
- Expected: both clients reach the server or main menu without the `walkforward`/`GuiScreenDisconnected` crash.
- Expected: client logs show base game mods plus `thebasics@5.5.0` once connected.
- Failure modes: empty loaded-mod list, duplicate The BASICs DLL/load error, missing-key exception, or disconnected-screen crash.

Card 2: Non-Admin Access Denial

- On the non-admin client, run `/tb config` and `/thebasics config`.
- Expected: config panel does not open and the player receives a denial/error message.
- Failure modes: panel opens, save/reload controls are usable, or no feedback is shown.

Card 3: Admin Panel Open And Group Usability

- On the admin client, run `/tb config`.
- Change the group dropdown through several groups.
- Expected: each group redraws with readable labels, status text, and Save/Reload/Mark Reviewed/Close controls visible at target resolution.
- Failure modes: clipped dialog, inaccessible buttons, missing group entries, or exceptions in the client log.

Card 4: Live Debug Save And Persistence

- In the admin panel, change `DebugMode`, save, close, and reopen the panel.
- Fetch or inspect `/data/ModConfig/the_basics.json` after save.
- Expected: save succeeds, reopened panel shows the changed value, and the JSON file contains the persisted value.
- Failure modes: save rejected for a valid value, UI reverts unexpectedly, or file value does not persist.

Card 5: Reviewed Settings State

- Open the panel as admin and click Mark Reviewed.
- Fetch or inspect `/data/ModConfig/the_basics.json`.
- Expected: new/needs-review indicators clear after refresh and `ReviewedConfigSettingKeys` persists setting keys.
- Failure modes: indicators remain for reviewed keys, JSON does not persist reviewed state, or save result reports failure.

Card 6: Restart-Required Messaging

- Change a clearly restart-required scalar setting such as a startup-shaped enable/disable toggle exposed in the panel.
- Save the setting.
- Expected: save succeeds and result/status text clearly says restart is required for at least one changed setting.
- Failure modes: setting is presented as fully live when it is not, or no restart-required warning is visible.

Card 7: Live Permission Refresh

- Change a command privilege setting for an existing command, such as TPA request privilege, to a stricter privilege.
- Without restarting, test the command from a player without the new privilege.
- Restore the original privilege and retest.
- Expected: permission behavior changes without restart for existing command instances.
- Failure modes: old privilege remains active until restart, command disappears, or unrelated command permissions change.

Card 8: Typing Indicator Live Toggle

- With both clients near each other, type in chat enough to show the typing indicator.
- Disable `EnableTypingIndicator` from the admin panel and save.
- Re-enable it and repeat typing.
- Expected: disabling clears stale indicators and prevents new ones; re-enabling allows indicators again without restart.
- Failure modes: stale indicator remains indefinitely, indicators still appear while disabled, or re-enable requires restart.

Card 9: Nametag Live Refresh

- Change a live nametag display/range setting from the admin panel and save.
- Observe the other client without reconnecting.
- Expected: visible nametag behavior updates for online players without server restart.
- Failure modes: no visible update until reconnect/restart, or nametags disappear unexpectedly.

Card 10: Character Sheet Regression From Merged Main

- Open character sheet UI/hotkey from main's new feature.
- Save a basic field if available.
- Expected: character sheet still opens/saves after admin config merge; no network packet mismatch/disconnect.

## Manual QA Results - 2026-05-06

- Card 1 passed: both clients booted/loaded after profile keymap repair.
- Card 2 passed: admin/non-admin access behavior verified.
- Card 3 passed: config group dropdown usability verified.
- Card 4 passed: `EnableGlobalOOC=false` persisted in `/data/ModConfig/the_basics.json` after turning GOOC off.
- Card 5 passed: Mark Reviewed persisted `ReviewedConfigSettingKeys`.
- Card 6 passed: restart-required save warning appeared, but the restart/live distinction being tooltip-only is a UX concern.
- Card 7 passed: live privilege/setting refresh verified using the babble verb setting.
- Card 8 passed: typing indicator live toggle verified.
- Card 9 passed: nametag live refresh verified.
- Card 10 passed: character sheet regression check passed after merging main.

## Retirement Condition

Retire this tracker when issue #125 has either:

- A complete all-config live reload implementation, package verification, and manual QA approval, or
- A deliberate product decision that the shipped feature is an admin config panel with a documented safe live subset and restart-required remainder.
