# Visual setup wizard QA

Worktree: `.codex-worktrees/visual-config-wizard`, branch `codex/visual-config-wizard`.
Scope: chat, teleportation, notifications, invitation, restart guidance, native Pip preview and capture, integrated config search and proximity tab placement.
Game: 1.22.7. Source and package SHA-256 are recorded beside the staged build in ignored `.tmp`.
Retire this packet after the first release completes these cards and moves remaining findings to tracked work.

## Empty preview input, October 7

- Removed the `Example chat` filler from the shared mock chat input. The native input starts empty and remains disabled, with its separator and background intact.
- All 12 affected chat preview checks pass with no skips, including an empty-input assertion. Canonical build: zero errors, 151 warnings. Source identity `29ac1ff7fafb71df6a03d1d40004e987afdddca619e4e2a37393adfe754ee684`; package SHA-256 `89e3d870be2b211f9a3d8777102a830cf0df89ba5cef9a1b1e04fbeff30b3379`. QA server readback and both profile packages match. The server reached RunGame at 05:30:23 UTC with no startup exceptions and the existing bare container-path error.
- Fresh native capture `.tmp/wizard-native-empty-input/wizard-chat-default.png` reports this source stamp, native-client coverage, 1908x1260, and GUI scale 1.125. Model inspection confirms the blank input. Profile2 is left on Chat; client logs contain no errors or exceptions. Earlier full suites and capture sets below are separate evidence; owner QA cards remain pending.

## Contextual preview revision, October 7

- The closed presentation dropdown contains its English title and smaller description in the same 58-pixel row as the open choices. Both rows center the entire text block. The drag hint and separate description label are removed.
- Chat basics, language, and tab settings show only the mock chat window. Hearing ranges and obfuscation show only their diagram. Teleportation shows its diagram and explanation. Pip greets and idles on Topics, retaining drag and release momentum there.
- Save options are Off / Chat / Chat notification. The existing notification flags send beige text (`#CCe0cfbb`) to General, rather than a separate popup. Their mock history follows the selected mode and escaped custom wording. Sleep previews cycle zero through three sleepers out of four, retaining the ordinary chat reminder after a threshold crossing until the loop resets; 0% and 100% stay silent.
- Full suite: 1316 passing tests, no skips. A subsequent 12-test chat run includes every teleport page and the hub. Native widget checks cover matching selected/open row layout, clipping, keyboard/focus behavior, save mode callbacks and text edits, contextual element presence, and sleep thresholds at 0/25/50/75/100%.
- Canonical build: zero errors, 151 warnings. Source identity `4b42c0f59883ffd3d5b1f3ca3f68d38960e4c2506c8ca7aed3e6da6489404450`; package SHA-256 `9a71f93ba4ffda4590b3a632bf36f7b97bee5cffc97c132248cb7b09006d5c3f`. QA server readback and both client profile zips match. The server reached RunGame at 05:12:53 UTC with no startup exceptions; the existing flywheel duplicate warning and bare container-path error remain.
- CPU report `.tmp/wizard-ci-contextual-report/report.json` records 32 additions with valid evidence at scales 1 and 1.25. The named set adds Topics and Sleep fixtures. These images omit native character rendering; they are separate from client captures. All 16 mocked capture receipts passed their protocol checks.
- All 16 fresh native scenes in `.tmp/wizard-native-contextual-set` report native-client coverage and this source stamp, at 1908x1260 and GUI scale 1.125. Model inspection confirms the closed/open described dropdown, chat-only pages, range-only diagram, save notification styling, travel layout, and Topics character. Client logs contain no errors or exceptions.
- Additional native captures in `.tmp/wizard-native-contextual-wave` and `.tmp/wizard-native-contextual-sleep` show the raised greeting at 1.5 seconds and the 50% reminder at two sleepers at 4.5 seconds. The final `.tmp/wizard-native-contextual-chat` capture leaves Profile2 on the live Chat page. Capture completion resumes animation. These are sampled poses and the tested resume path, rather than owner acceptance of gesture feel or smoothness.
- Owner observations and the manual cards remain pending. No merge or release is authorized by the automated checks.

## Chat window and interaction revision, October 6

- Chat now previews a complete miniature native chat window, with General/Proximity tabs, clipped history, scrollbar, and a disabled example input. It owns no live chat history and sends no chat. General replacement removes the mock Proximity tab; local OOC follows the nickname choice; the default-tab page starts without a remembered player tab.
- Presentation choices use English names and smaller descriptions in expanded rows. Topic tabs and an inset footer organize navigation. Ordinary pages omit restart suffixes; review identifies `After restart`, and the acknowledged-save screen retains the host-specific instructions.
- Pip supports horizontal drag, release momentum with bounded exponential decay, and gesture cancellation when changing pages, closing, suspending, or disposing. Native wave and idle animation remain live; named captures reset orientation and pin a pose, then resume live animation after capture.
- Full suite: 1310 tests passed, no skips. After the default-tab correction, all 33 affected checks passed; after the final copy/tooltip correction, all 8 chat checks passed. Tests cover native menu row clicks and keyboard/focus behavior at scales 1 and 1.25, input isolation, preview clipping and nickname/default-tab choices, momentum/cancellation, and actual installed native joint movement beyond 5.5 seconds after capture resumes.
- Canonical build: zero errors, 152 warnings. Source identity `0c4a680a4fc2a5c00101d343e0b9ea16dde6348456779ecc43dca5b1bd7abeb1`; package SHA-256 `22ec5adba8e55af618dee1eea7884717498ccf36045deecb8af48db830152b38`. QA server readback and both profile zips match.
- CPU report `.tmp/wizard-ci-chat-window-report/report.json` records 28 additions with valid evidence at scales 1 and 1.25, including the new expanded `wizard-chat-presentation` fixture. CPU images omit native Pip. Earlier native images below do not certify this revision.
- The staged server reached RunGame at 01:01:43 UTC on October 7 (21:01:43 EDT on October 6). Startup has no exceptions; the existing bare container-path error remains. All 14 fresh native scenes in `.tmp/wizard-native-chat-window-set` report native-client coverage, the staged source stamp, 1908x1260, and the restored GUI scale 1.125. Native inspection confirms the full chat window, expanded English title/description rows, guide clipping, review, and restart pages. The client log has no errors or exceptions. The capture runner also completed all 14 mocked scene receipts.
- An additional raised-wave capture at 1.5 seconds is in `.tmp/wizard-native-chat-window-live`. It shows Pip's arm moving from the 0.75-second pose without leaving the panel. Its completed capture receipt runs `ResumePreview`, so Profile2 is left on the live Chat page with drag enabled. This proves the captured native poses and the resume path, not owner acceptance of gesture feel or real-time animation smoothness.

## Combined search checkpoint, October 6

- Integrated source commits `0c5aa0a`, `53e86fc`, and handoff `424982a` into the wizard branch. Preserve `CharacterSheetAutoOpenOnCharacterScreen` at protobuf field 158; the new `ProximityChatTabPosition` uses 159. Search navigation and wizard hooks coexist.
- Combined suite: 1290 tests passed with no skips. A subsequent permission-denial fix passed all 62 affected checks, including two native GUI regressions. Failed language/field open result packets reopen the main editor, preserve its draft/query, and clear pending navigation.
- Canonical build: zero errors, 141 warnings. Package SHA-256 `59a59e0bbd1a6e5646502825007d0155bbac6cdfa7ae573a0287f525704fb699`, source identity `3ab79de9d2d8fb83bff5e4151a5edd05a47fed6f3a5a5a938195407c65bbadc4`. Server readback and Profile2/Profile3 match. The QA server reached RunGame at 23:59:10 UTC, with no startup exceptions and the existing unrelated container-path error/flywheel warning.
- Combined CPU report `.tmp/wizard-ci-combined-search-report/report.json` has valid evidence for 26 wizard additions at scales 1 and 1.25. Search CPU previews and focus/scroll/session checks are in `.tmp/combined-search-tests` and `.tmp/combined-search-denial-tests`. The wizard comparison collector does not currently include search scenes.
- Fresh native pilot and all 13 scenes are in `.tmp/wizard-native-combined-pilot` and `.tmp/wizard-native-combined-set`, at 1908x1260 and GUI scale 1.125. Every manifest reports native-client coverage and the combined source stamp. Model inspection confirms the rendered character, chat/tab examples, and dedicated restart instructions.
- Native scale probes in `.tmp/wizard-native-combined-scale-1` and `.tmp/wizard-native-combined-scale-1.25` cover Chat, Teleportation, and Notifications at their actual manifest scales. The 1.25 Chat poses at 0.75 and 1.5 seconds show the arm moving from lowered to raised, within the guide panel; the later pose is in `.tmp/wizard-native-combined-wave-1.5`. These are named poses, not real-time performance measurements. Profile2's original `floatSettings.guiScale=1.125` has been restored. Raw-preserving setting edits and verified backups are recorded in `.tmp/wizard-profile2-scale-backup-*/scale-receipt.json`.
- Search journey and five pending owner cards: [2026-10-06-config-search-qa.md](2026-10-06-config-search-qa.md). No in-game search roundtrip or human observation has been recorded for this combined source.

## Earlier native motion checkpoint, October 6

- Native motion checkpoint commit: `548bb84596fd86b26e9c763c58203799a65d4d2d`, pushed before search integration. The source identity below identifies this earlier evidence.
- Package: `thebasics_5_9_1.zip`, SHA-256 `7654294da2f5ead9c766b813578d90c7cbf1341d12ae446f95228b1f768b81fa`. Server readback and both profile packages match.
- Source identity: `e845a6c86dd96ad30cc3ce43e41e28d0ed83c706ba5669d421b9b3a5395363f3`, identical in the canonical build and 26 final CPU captures. Report: `.tmp/wizard-ci-native-motion-report/report.json`, 26 added scenes, valid evidence, no missing or stale frames.
- Verification: 1257 checks passed with no skips in the final motion suite; its new synthetic-joint test initially failed because the fixture omitted two required rotation axes, then passed separately after that fixture-only fix (1258 passing checks in total). The prior native camera-angle change also passed all 21 affected checks with no skips. Python report checks 15 passed, relay checks 28 passed, workflow trust checks 2 passed. Canonical packaging has zero errors and 132 analyzer warnings. Package after tests: test builds can invoke the mod's post-build packager and overwrite an earlier stamped zip.
- Server restarted and reached RunGame with all BASICs systems loaded and no exceptions. Existing flywheel duplicate warnings and a bare container-path error remain outside this change.
- Profile2 has a temporary Agent Control startup build. `EnableOnStartup=true` is enabled only in this profile, using pipe `vintage-story-agentcontrol-profile2`. It waits for player readiness and enables once; cancellation, disabling, RPC shutdown, and pipe failure cannot enable it again. All 23 bridge checks pass. Terminal requests cancel their extension tokens, including completed and failed requests, so queued captures cannot outlive their owner. PostHog ingestion and manual cards remain unverified; sampled video evidence is recorded below.
- Native recovery: PlayerModelLib renames the live player's skin behavior and suppresses vanilla clothing composition. Pip retains a private native skin descriptor and calls the original native compositor through the existing Harmony reverse-patch facility. The guide uses the character editor's native viewing angle. Constructor/composer cleanup preserves the original error, prevents a client crash, and retains a dirty draft if reopening fails.
- Motion recovery: named frames at 0.75 and 1.5 seconds exposed a frozen wave. The native animation engine decrements its iteration on a zero-time prime, which prevents this animation from easing in. The private animator now starts on a positive step and keys active animations by their native `Animation` field (`idle1` for the `idle` metadata). A real `ClientAnimator` regression checks time-zero identity, alias resolution, easing, and changing joint matrices.
- Earlier native evidence: `.tmp/wizard-native-motion-set` contains all 13 source-verified scenes at 1908x1260, GUI scale 1.125. Model inspection covers chat, ranges, languages, travel, notifications, and restart guidance. The later combined-source probes above add scales 1 and 1.25; owner observations remain pending.
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
   - Do: inspect Pip on Topics for at least ten seconds. Drag slowly, then flick and release, including outside the panel. Catch the moving preview with another drag. Change a setting, return to Topics, begin another drag, press Escape, and cancel discard. Change pages, close, and reopen several times. Repeat at GUI scales 1 and 1.25. Compare the live player's clothes and inventory before and after.
   - Expect: Pip has the selected skin and pastoral outfit, greets and returns to a moving idle, stays clipped inside the panel, and does not change the live player. Slow dragging turns Pip; a recent flick coasts smoothly to rest. A new drag catches the motion. Canceling discard preserves the draft and leaves no stuck gesture.
   - Watch for: invisible guide, stretched mesh, clipped controls, stalled animation, motion without a pressed button after cancellation, duplicated dialogs, inventory changes, or exceptions after closing during a mesh upload.

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
   - Do: try Off, Chat and Chat notification separately for save started and finished. Change their wording. Change the sleep percentage between 25, 50, 75, 0 and 100. Close and discard.
   - Expect: save pages show only General chat. Off leaves its history empty; Chat shows ordinary text; Chat notification shows beige text, with no separate popup. Edits update the preview. The sleep diagram cycles zero through three sleepers out of four; reminders appear at the chosen threshold and remain until the example resets. At 0 and 100 the history stays empty. Saving itself and night skipping are unchanged.
   - Watch for: linked start/finish choices, misleading endpoint examples, or custom text being included in analytics.

## Batch 2: apply a reviewed configuration and reconnect

6. **General proximity delivery** (P0)
   - Config: save `UseGeneralChannelAsProximityChat=true`; keep RP features and global OOC enabled. Restart the disposable server and reconnect both profiles.
   - Do: use General to speak near the other player, then beyond the configured hearing range. Send global OOC with `(( hello ))` at a distance.
   - Expect: General replaces the separate Proximity tab, ordinary speech follows proximity delivery, and global OOC reaches the distant player. Restore the original configuration and restart after this batch.
   - Watch for: duplicate tabs, lost OOC, differences between the diagram and delivery, or the existing `/rptext off` player opt-out being mistaken for universal filtering.

7. **Chat window and described presentation choices** (P1)
   - Config: draft only, no save needed.
   - Do: compare General and Proximity in the preview with General replacement Off, then enable replacement. Open the presentation selector and choose each option, using both mouse and keyboard. On Languages and OOC, switch the local OOC nickname choice; on Default chat tab, switch the initial tab. Review the draft, then discard it.
   - Expect: General shows ordinary global chat when separate channels are selected; Proximity shows RP examples. Replacement leaves one General tab showing nearby chat. Every expanded option and the closed selection have an English title and smaller description inside the dropdown. Chat basics, language, and tab pages show only the chat window; range and obfuscation pages show only the hearing diagram. All chat lines fit the history; the example input cannot send text. Nickname and initial-tab changes update the mock. Restart information appears in review and hover help, with explicit instructions after an acknowledged save.
   - Watch for: Pascal-case values, tooltips covering expanded descriptions, double-offset or clipped chat lines, inactive choices with misleading previews, or fake input changing live chat.
