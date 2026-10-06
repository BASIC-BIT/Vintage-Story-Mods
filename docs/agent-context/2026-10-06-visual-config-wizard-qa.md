# Visual setup wizard QA

Worktree: `.codex-worktrees/visual-config-wizard`, branch `codex/visual-config-wizard`.
Scope: chat, teleportation, notifications, invitation, restart guidance, native Pip preview and capture.
Game: 1.22.7. Source and package SHA-256 are recorded beside the staged build in ignored `.tmp`.
Retire this packet after the first release completes these cards and moves remaining findings to tracked work.

## Staged checkpoint, October 6

- Recovery baseline commit: `8a5e99092b33400314ee5ba3fa1f40c10a2399ae`, pushed to the feature branch before this native checkpoint. The source identity below identifies the staged fixes.
- Package: `thebasics_5_9_1.zip`, SHA-256 `7654294da2f5ead9c766b813578d90c7cbf1341d12ae446f95228b1f768b81fa`. Server readback and both profile packages match.
- Source identity: `e845a6c86dd96ad30cc3ce43e41e28d0ed83c706ba5669d421b9b3a5395363f3`, identical in the canonical build and 26 final CPU captures. Report: `.tmp/wizard-ci-native-motion-report/report.json`, 26 added scenes, valid evidence, no missing or stale frames.
- Verification: 1257 checks passed with no skips in the final motion suite; its new synthetic-joint test initially failed because the fixture omitted two required rotation axes, then passed separately after that fixture-only fix (1258 passing checks in total). The prior native camera-angle change also passed all 21 affected checks with no skips. Python report checks 15 passed, relay checks 28 passed, workflow trust checks 2 passed. Canonical packaging has zero errors and 132 analyzer warnings. Package after tests: test builds can invoke the mod's post-build packager and overwrite an earlier stamped zip.
- Server restarted and reached RunGame with all BASICs systems loaded and no exceptions. Existing flywheel duplicate warnings and a bare container-path error remain outside this change.
- Profile2 now has a temporary Agent Control startup build. `EnableOnStartup=true` is enabled only in this profile, using pipe `vintage-story-agentcontrol-profile2`. It waits for player readiness and enables once; cancellation, disabling, RPC shutdown, and pipe failure cannot enable it again. All 23 bridge checks pass. Terminal requests cancel their extension tokens, including completed and failed requests, so queued captures cannot outlive their owner. Video, PostHog ingestion, and manual cards remain unverified.
- Native recovery: PlayerModelLib renames the live player's skin behavior and suppresses vanilla clothing composition. Pip retains a private native skin descriptor and calls the original native compositor through the existing Harmony reverse-patch facility. The guide uses the character editor's native viewing angle. Constructor/composer cleanup preserves the original error, prevents a client crash, and retains a dirty draft if reopening fails.
- Motion recovery: named frames at 0.75 and 1.5 seconds exposed a frozen wave. The native animation engine decrements its iteration on a zero-time prime, which prevents this animation from easing in. The private animator now starts on a positive step and keys active animations by their native `Animation` field (`idle1` for the `idle` metadata). A real `ClientAnimator` regression checks time-zero identity, alias resolution, easing, and changing joint matrices.
- Native evidence: `.tmp/wizard-native-motion-set` contains all 13 source-verified scenes at 1908x1260, GUI scale 1.125. Model inspection covers chat, ranges, languages, travel, notifications, and restart guidance. Native captures at scales 1 and 1.25 and owner observations remain pending.
- Video evidence: `.tmp/wizard-native-wave-clip/clip.json` records nine native poses at 0..2 seconds, sampled at 4 Hz and cropped to the wizard. The H.264 clip is 1038x730, nine frames, 2.25 seconds, SHA-256 `fc961c71b0ca7206a3e786c2ea6f31621c4184189d946d16f0b4e85c116008f7`. The authorized `gemini-3.1-pro-preview` call returned six PASS criteria in `wizard-chat-wave.review.md`. It establishes sampled appearance/motion/layout assessment, not real-time smoothness or human acceptance. Reproduction and rubric: [2026-10-06-wizard-video-rubric.md](2026-10-06-wizard-video-rubric.md).
- Player snapshots: before/after inventory aggregates and hotbar match with no truncated entries; position, pitch, ground state, and active slot match. Yaw changes by about 0.00666 radians, so the strict whole-player-state assertion fails. Retain `.tmp/wizard-native-player-check.json` as that limitation; do not mark the player-isolation human card complete.
- Launch discovery: Vintage Story forwards `-c` to an existing instance through a global URI pipe. An immediate second launch reached the first client before input initialization and caused vanilla `walkforward` startup failure. Launch Profile2 without `-c`, wait for the menu's shaders and controls, then run the connection command to forward to that initialized window. A second profile must also start without `-c` and connect from its own menu.
- WinSCP deployment requires Windows PowerShell for the installed .NET Framework assembly; using PowerShell Core fails before transfer. The successful transfer used the supported runtime and verified remote bytes before restart.

## Preparation and evidence

- Existing QA mod, config, startup log, and both client packages are backed up in `.tmp/wizard-qa-backup-20261006-160450`.
- Profile2 enables Agent Control automatically after player readiness. Its optional operations open capture-only wizard sessions; they cannot save settings or acknowledge the invitation. Ctrl+Alt+F9 still cancels, and Ctrl+Alt+F8 disables the session.
- Startup helper source is an isolated copy of `D:\bench\vs\work\s3-agentcontrol` at `ffab5508d10153f0dce3fe0073a94b9172d3a568`. The reproducible delta is [2026-10-06-profile2-startup.patch](2026-10-06-profile2-startup.patch), checked with `git apply --check --ignore-space-change`. Apply it in a disposable copy, then use that project's existing test and package commands. This temporary fork is not part of the BASICs release.
- Helper package SHA-256: `d6e46d05acc63aff308009b7b829a8ed8c5fa92117fa023543d6cb292bf982cc`. Original Profile2 helper zip and config are backed up in `.tmp/wizard-profile2-bridge-backup-20261006-230147`; the prior temporary helper is also backed up in `.tmp/wizard-profile2-bridge-backup-20261006-233158`. Staging receipt is `.tmp/wizard-profile2-bridge-stage.json`.
- Only Profile2's existing limits are raised to `MaxActionDurationMs=30000` and `MaxBatchDurationMs=60000`, because effective patched asset fingerprinting takes about 11 seconds on the render thread. Original limits are preserved in `.tmp/wizard-profile2-limits-receipt.json`'s backup. No release default changes. The capture must complete before its owning batch ends; a slow mesh load fails safely rather than writing later.
- Run the native capture batch with `./scripts/capture-native-wizard.ps1 -ExpectedSourceTreeHash (./scripts/gui-source-identity.ps1) -Vsctl D:\bench\vs\work\s3-agentcontrol\tools\vsctl\bin\Release\net10.0\publish\vsctl.exe -DataPath D:\Games\VSProfiles\Profile2`. Add `-Scenarios wizard-chat-default` for a pilot. Source stamps, fixed profile capture directories, and native coverage are validated before copying artifacts. Mock checks cover all 13 scenes and rejection of unknown scenes, wrong source hashes, and wrong profile directories.
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
