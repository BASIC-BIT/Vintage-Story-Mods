# Visual setup wizard QA

Worktree: `.codex-worktrees/visual-config-wizard`, branch `codex/visual-config-wizard`.
Scope: chat, teleportation, notifications, invitation, restart guidance, native Pip preview and capture.
Game: 1.22.7. Source and package SHA-256 are recorded beside the staged build in ignored `.tmp`.
Retire this packet after the first release completes these cards and moves remaining findings to tracked work.

## Staged checkpoint, October 6

- Code commit: `b9e4d8c1d0a46d32ce6466f52ee8946899488408`, pushed to the feature branch.
- Package: `thebasics_5_9_1.zip`, SHA-256 `cf4f92afa75f7de38741d3e640094fb44fcf43cf2dacb96725f79217e145d5a0`. Server readback and both profile packages match.
- Source identity: `1f11444f065be8492eed2b3086a1a8b713b506ea5d99d6a596fb5e41239429bc`, identical in the canonical build and 26 final CPU captures.
- Verification: full suite 1251 passed with no skips; one later native-identity check passed separately. Python report checks 15 passed, relay checks 28 passed, workflow trust checks 2 passed. Canonical packaging has zero errors and 132 analyzer warnings.
- Server restarted and reached RunGame with all BASICs systems loaded and no exceptions. Existing flywheel duplicate warnings and a bare container-path error remain outside this change.
- Profile2 connected at 16:35 local. Agent Control is loaded but disabled; the owner must press Ctrl+Alt+F8 before native capture. No native appearance, video, PostHog ingestion, or manual card is complete.
- Launch discovery: Vintage Story forwards `-c` to an existing instance through a global URI pipe. An immediate second launch reached the first client before input initialization and caused vanilla `walkforward` startup failure. Launch Profile2 without `-c`, wait for the menu's shaders and controls, then run the connection command to forward to that initialized window. A second profile must also start without `-c` and connect from its own menu.
- WinSCP deployment requires Windows PowerShell for the installed .NET Framework assembly; using PowerShell Core fails before transfer. The successful transfer used the supported runtime and verified remote bytes before restart.

## Preparation and evidence

- Existing QA mod, config, startup log, and both client packages are backed up in `.tmp/wizard-qa-backup-20261006-160450`.
- Agent Control requires the owner to press Ctrl+Alt+F8 in Profile2 after connecting. Its optional operations then open capture-only wizard sessions; they cannot save settings or acknowledge the invitation.
- CPU renders cover 13 named scenes at two scales. They explicitly omit Pip. Native captures, analytics readback, and the observations below are separate evidence.
- Live collector health at 20:11 UTC on October 6 reports contract revision 8. It rejects the new wizard events until revision 9 is deployed through the existing Terraform production workflow. Ordinary supported events still flow; a producer success message is not proof that a wizard event reached PostHog.
- No card is complete until the owner reports the expected observation. No merge or release is authorized by this packet.

## Batch 1: current configuration, no restart

1. **Invitation and return command** (P1)
   - Config: current server settings; administrator has not acknowledged setup.
   - Do: connect, finish any character/consent dialog, then choose Not now or press Escape on the setup invitation. Enter `/basic setup` afterward.
   - Expect: one chat message, `You can configure The BASICs later with /basic setup.` The command opens the wizard with current settings. A declined invitation does not return after reconnecting.
   - Watch for: invitation covering character creation, duplicate reminders, or disconnect being treated as a decline.

2. **Native guide and repeated close** (P1)
   - Config: any; capture-only fixtures are sufficient.
   - Do: inspect Pip on Chat and Teleportation, change pages, close and reopen several times. Repeat at GUI scales 1 and 1.25. Compare the live player's clothes and inventory before and after.
   - Expect: Pip has the selected skin and pastoral outfit, plays a greeting and page animations, stays clipped inside the panel, and does not change the live player. Cancel on the dirty-close confirmation preserves the draft.
   - Watch for: invisible guide, stretched mesh, clipped controls, stalled animation, duplicated dialogs, inventory changes, or exceptions after closing during a mesh upload.

3. **Draft, review, and restart instructions** (P0)
   - Config: current General channel choice; note its original value.
   - Do: toggle Use General as proximity chat, review, save, then choose Later. Reopen setup and change it back to its original value, review and save again.
   - Expect: the first review names the exact changed setting and restart requirement. Success appears only after the server acknowledges it. Dedicated-host instructions explain restarting through the host and reconnecting; Later returns to the game. Reverting to the startup value clears that pending restart.
   - Watch for: a shutdown button presented as restart, lost edits, unrelated values changing, or success before acknowledgement.

4. **Travel options and conditional explanations** (P1)
   - Config: draft only; no save is necessary.
   - Do: disable homes, keep spawn enabled, and inspect Spawn's gear choice. In player requests, toggle the gear, cooldown and timeout controls Off and On. Close and discard the draft.
   - Expect: spawn's shared gear setting remains available. The examples describe only enabled charges and timing; labels distinguish real time from game time. Hidden tools preserve their settings.
   - Watch for: an unavailable spawn cost, examples claiming disabled costs or timing, or changes from merely visiting a page.

5. **Notification and sleep examples** (P1)
   - Config: draft only.
   - Do: try Off, Chat and Popup separately for save started and finished. Change the sleep percentage between 50, 0 and 100. Close and discard.
   - Expect: independent examples follow each choice. The four-person scene shows the 50% reminder with two sleepers. At 0 and 100 it explains that no reminder triggers. Saving itself and night skipping are unchanged.
   - Watch for: linked start/finish choices, misleading endpoint examples, or custom text being included in analytics.

## Batch 2: apply a reviewed configuration and reconnect

6. **General proximity delivery** (P0)
   - Config: save `UseGeneralChannelAsProximityChat=true`; keep RP features and global OOC enabled. Restart the disposable server and reconnect both profiles.
   - Do: use General to speak near the other player, then beyond the configured hearing range. Send global OOC with `(( hello ))` at a distance.
   - Expect: General replaces the separate Proximity tab, ordinary speech follows proximity delivery, and global OOC reaches the distant player. Restore the original configuration and restart after this batch.
   - Watch for: duplicate tabs, lost OOC, differences between the diagram and delivery, or the existing `/rptext off` player opt-out being mistaken for universal filtering.
