# Configuration retirement QA handoff, October 28

The sidebar editor has replacement controls, search and maintenance actions. This checkpoint prepares local evidence for owner review. It does not change the normal config routes or retire the old windows.

Read the [automated review](../qa/2026-10-28-config-retirement-automated-review.md) for current validation and artifact identity, then the [pending human QA cards](../qa/2026-10-28-config-retirement-human-qa.md). All human observations remain pending. Profile2 must remain unchanged until the owner reviews standalone previews and requests in-game continuation.

## Remaining scope and gates

- Observe direct commands, search return, keyboard/compact forms, mixed atomic saves, conflicts, permission revocation, disk reload and write failures.
- Verify actual voice and optional-provider output separately. An absent provider is unavailable evidence.
- Obtain approval before staging or the restart/reconnect activation batch, then obtain explicit human QA completion.
- Decide default routing only after acceptance. Decide legacy-window removal and supported recovery separately. Retain wire support until an explicit compatibility decision.

## Source and verification entry points

The catalog and registry are in `mods-dll/thebasics/src/ModSystems/AdminConfig`. Shared drafts, dialog, search, capture scenes and controller are in `mods-dll/thebasics/src/ModSystems/ChatUiSystem`. Commands and authoritative setup actions are in `mods-dll/thebasics/src/ModSystems/ProximityChat`.

Use the native regression command and canonical local build in the [implementation plan](../superpowers/plans/2026-10-28-config-menu-retirement-implementation.md). `scripts/preview-gui.ps1 -Wizard -Width 1280 -Height 720` captures both compact scales. The automated review links the exact run logs, package receipt and source-stamped capture index. Do not install a package merely because it builds. Before any later approved in-game test, verify the staged server ZIP and Profile2 ZIP both match the package receipt SHA256. A matching version string is insufficient because rebuilt 5.9.1 packages can differ. Server/client installation and hash verification remain pending.

Retire this packet when the human cards and activation observations are recorded, the default-route decision is implemented and verified, and legacy recovery/compatibility decisions have a durable owner. Until then, keep each acceptance gate pending.
