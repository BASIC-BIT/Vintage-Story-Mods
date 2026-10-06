# Visual configuration wizard implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Deliver guided setup for chat, teleportation, and notifications, with native visual previews, safe draft saving, a dismissible invitation, restart guidance, and reviewable capture evidence.

**Architecture:** Reuse the config registry and validated server save path. Add a small wizard catalog/draft model and native dialog, plus an isolated native guide renderer. Extend existing Agent Control and GuiPreview seams instead of building another controller or rendering framework.

**Tech Stack:** C#/.NET 10, Vintage Story 1.22.7, existing Cairo/SkiaSharp/protobuf/xUnit, existing PostHog Worker and GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-10-01-visual-config-wizard-design.md`

## Global constraints

- First release covers chat, teleportation, and notifications; rich language editing follows later.
- Root authorization remains server-side at open, acknowledge, track, and save.
- No dependency additions, gameplay reinterpretation, hosting credentials, automatic restart, merge, or release.
- Save only changed keys; check expected originals and persist before shared mutation.
- Standalone layout renders cannot certify the native actor or animation.
- Keep existing ProtoMember numbers; append new fields or use new message types.
- Public prose contains no em dash.

## Review focus

- A second administrator changes one of the submitted keys: conflict retains local edits, unrelated changes merge.
- Disk write fails: no runtime mutation, broadcast, success event, or success screen.
- A delayed response follows timeout or a second open: it cannot overwrite or reopen a newer dialog.
- Invitation arrives while character creation or consent is open: wait; Escape leaves exactly one reopen message.
- Preview closes before async mesh upload: no stale upload, atlas allocation leak, or player/world mutation.

## Task 1: Native guide and capture probe

**Files:** new `assets/thebasics/config/setup-guide.json`, `src/ModSystems/ChatUiSystem/SetupGuidePreview.cs`, focused guide tests, and a bounded client-only QA/capture adapter.

**Interfaces:** `SetupGuidePreview(ICoreClientAPI api)`, `Render(float dt, ElementBounds bounds)`, `SetAnimation(string code, double timeSeconds)`, `Dispose()`. It owns an unregistered native actor and its resources. UI callers never own world/player entities.

- [x] Validate Pip's 11 native skin options, five garment codes/slots, and idle/wave/nod/cheer animation codes.
- [x] Add the fixture and run its focused asset test.
- [x] Implement the clipped native renderer from the inspected engine seams; retain only appearance/inventory behaviors and deep-copy mutable properties.
- [ ] Add a test for owned fixture validation and a native probe for two GUI scales, named animation times, player-state invariance, and close-before-upload cleanup.
- [x] Queue capture at Done; own/dispose raw bitmaps, apply a vertical flip to the full viewport without relying on the engine's leaking transformed screenshot allocation.
- [ ] Capture the real production panel and inspect PNGs before claiming native rendering works.

## Task 2: Wizard catalog, protocol, and safe draft

**Files:** new `AdminConfig/SetupWizardCatalog.cs`, `ChatUiSystem/SetupWizardDraft.cs`, request/result models, focused workflow tests.

**Interfaces:** catalog pages expose stable ID/title/description/setting keys; all keys resolve through `ConfigAdminSettingRegistry`. Draft exposes canonical values, Get/Set, changed-key/original-value save patches, and per-key response merge. New protobuf messages carry correlated request ID and server-issued run ID.

- [x] Write a focused failing check for changed-key patches, concurrent edits, acknowledgement merging, duplicate/unknown keys, and timeout recovery.
- [x] Implement the minimum catalog and draft model. Preserve disabled/hidden settings until explicitly edited.
- [x] Add finite request/result kinds and bounded payload validation; no client actor UID.
- [x] Run the focused tests and record red/green results.

## Task 3: Guided dialog, invitation, and server integration

**Files:** new native wizard/invitation dialogs and client controller; modify existing ChatUiSystem and RPProximityChatSystem registration/hooks; narrowly update shared persistence if required.

**Interfaces:** controller receives existing safe channel, opens server-issued state, and tracks requests. Dialog takes a draft, status, pending-restart labels, dedicated/integrated host flag, save/track/close callbacks, and explicit layout-only preview mode.

- [x] Add meaningful checks for authorization, readiness ordering, dismissal once per admin, Escape/close versus disconnect, and privilege revocation.
- [x] Add `/basic setup`; gate automatic invitation on ready + Playing and defer behind interactive dialogs.
- [x] Integrate delta validation/conflicts and persist-before-mutation with a failure regression test.
- [x] Capture restart-shaped startup values; compare current saved values for pending keys, including no-op saves/reopen/revert.
- [x] Add correlated save handling and final restart instructions with Later; no shutdown action labeled restart.
- [x] Build and render the production chat pages using GuiPreview; clearly mark layout-only omission where the native panel is absent.

## Task 4: Teleport and notification branches

**Files:** catalog/dialog pages and focused mapping tests; use existing registry definitions.

- [x] Expose the six BASICs travel tools and show each selected tool's existing costs, timing, and privileges without overwriting hidden values.
- [x] Add independent save-start/save-finish Off/Chat/Popup mapping and sleep enable/percentage/wording.
- [x] Test mapping against real config fields, including unchanged hidden values and sleep endpoint behavior.
- [x] Render default, edited, error, review, and pending-restart scenes at two scales; request creative feedback on the next storyboard.

## Task 5: Journey and screenshot evidence

**Files:** existing AnalyticsService/relay/test contract, named GuiPreview scenes, screenshot comparison/report script/workflow.

- [x] Add server-issued run IDs, ordered finite step/action/choice properties, consent-compliant attribution, and saved/live/pending outcomes.
- [x] Update producer/Worker contract revision and allowlists together; run existing relay contract tests plus the new journey cases.
- [x] Add before/after/diff manifests and reuse the sticky-comment marker pattern for added/changed/removed/missing/stale captures.
- [ ] Generate short native clips from named frames and adapt the existing Gemini reviewer rubric/sampling. Verify provider access before making review claims.

## Task 6: Final validation and handoff

- [x] Run focused tests, then one complete suite and canonical build-and-package with output restricted to local staging until QA staging.
- [x] Review each task and one whole-branch diff; fix substantive findings and rerun affected checks.
- [ ] Stage the authorized test build, collect real-client captures, and keep human gameplay observation separate from model/image results.
- [ ] Record exact package/source/game versions and remaining human QA cards. No merge-ready or release claim without the repository's required evidence.

## Execution ledger

- Worktree: `D:/bench/vs/Vintage-Story-Mods/.codex-worktrees/visual-config-wizard`, branch `codex/visual-config-wizard`.
- Recovery: both native managed checkout paths disappeared between sessions. Git snapshot `f6018bce65cdeff98c9f1a6311accc37f8781198` preserved the 41-file implementation. The ordinary repo-local worktree now retains it, and current `origin/main` (`5324f19`) was merged without conflicts.
- Baseline: 1148 passed, 6 skipped, 0 failed. Managed-worktree obj writes required sandbox escalation; no provider/server operation was performed.
- Guide fixture: installed-native validation and focused xUnit test passed, 1/1.
- Ruling: proceed autonomously under the owner's explicit continuation instruction; preserve creative checkpoints and human QA observations.
- Ruling: portable restart is instructions plus Later, because the engine exposes shutdown but no relaunch capability.
- Pre-flight: native preview signature is shared by dialog and probe; draft/message signatures are established before server/client integration. Root owns existing integration files; workers own separate new files.
- October 6 validation: recovered code compiles after the missing server API import. A failing conflict-acknowledgement case demonstrated loss of an edit made during a pending save, then passed after the draft merge fix. The completed suite passes 1238/1238, with full GUI assets enabled and no skipped tests. Existing nullable warnings remain in test support.
- Native animation regression: 75 seconds of live rendering preserves one animator, while explicit capture time remains bounded to 60 seconds. Native game appearance and framebuffer capture still need runtime proof.
- CPU layout evidence: 13 production wizard scenes render at 1600x1000 and 1280x720 at 1.25 scale. The compact dialog fits the smaller viewport. These fixtures explicitly omit the 3D actor. Whisper, Normal, and Yell now each affect labeled hearing diagrams.
- Lifecycle checks cover capture-only server snapshots, reauthorization while retaining drafts, failed live application after successful persistence, and replay after a lost acknowledgement. QA snapshots do not acknowledge an invitation or emit normal setup journey events.
- Capture CI provisioning: official Windows server 1.22.7 binaries plus Linux client 1.22.7 assets render the production GUI without another runtime dependency. Archive SHA-256 pins are recorded in `scripts/collect-gui-captures.ps1`. GUI binary, asset, and font identities match the local installation. Source identity normalizes text line endings, and the renderer rejects a claimed hash that differs from its compiled source stamp.
- QA preflight: disposable server `8982de16` is running with `thebasics_5_9_1.zip`. Its current config and startup log are backed up in ignored `.tmp/wizard-qa-preflight`; no wizard package has been deployed yet. Agent Control is installed on Profile2 but starts disabled until the operator presses Ctrl+Alt+F8.

- Final October 6 check: 1251/1251 full-suite tests pass with no skips, plus the subsequently added native-identity regression passes separately. Report checks pass 15/15, relay checks 28/28, workflow trust checks 2/2, and agent tooling passes. The canonical package builds with zero errors and 132 analyzer warnings. Fresh final capture evidence has 26 added layout scenes and no missing or stale frames.
- Fresh review fixes cover transient dialog registration, owned Cancel confirmations, invitation visibility and distinct entry actions, spawn-only gear controls, conditional travel descriptions, capture host labels, and native assembly/effective patched asset provenance. Native asset identity states its runtime mutation and external-family omissions.
- Live telemetry boundary: deployed collector revision 8 rejects wizard events; revision 9 source and tests are ready, but production Terraform apply and PostHog ingestion have not been performed.
