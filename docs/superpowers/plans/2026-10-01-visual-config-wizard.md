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
- [ ] Implement the clipped native renderer from the inspected engine seams; retain only appearance/inventory behaviors and deep-copy mutable properties.
- [ ] Add a test for owned fixture validation and a native probe for two GUI scales, named animation times, player-state invariance, and close-before-upload cleanup.
- [ ] Queue capture at Done; own/dispose raw bitmaps, apply vertical flip/crop without relying on the engine's leaking transformed screenshot allocation.
- [ ] Capture the real production panel and inspect PNGs before claiming native rendering works.

## Task 2: Wizard catalog, protocol, and safe draft

**Files:** new `AdminConfig/SetupWizardCatalog.cs`, `ChatUiSystem/SetupWizardDraft.cs`, request/result models, focused workflow tests.

**Interfaces:** catalog pages expose stable ID/title/description/setting keys; all keys resolve through `ConfigAdminSettingRegistry`. Draft exposes canonical values, Get/Set, changed-key/original-value save patches, and per-key response merge. New protobuf messages carry correlated request ID and server-issued run ID.

- [ ] Write a focused failing check for changed-key patches, concurrent edits, acknowledgement merging, duplicate/unknown keys, and timeout recovery.
- [ ] Implement the minimum catalog and draft model. Preserve disabled/hidden settings until explicitly edited.
- [ ] Add finite request/result kinds and bounded payload validation; no client actor UID.
- [ ] Run the focused tests and record red/green results.

## Task 3: Guided dialog, invitation, and server integration

**Files:** new native wizard/invitation dialogs and client controller; modify existing ChatUiSystem and RPProximityChatSystem registration/hooks; narrowly update shared persistence if required.

**Interfaces:** controller receives existing safe channel, opens server-issued state, and tracks requests. Dialog takes a draft, status, pending-restart labels, dedicated/integrated host flag, save/track/close callbacks, and explicit layout-only preview mode.

- [ ] Add meaningful checks for authorization, readiness ordering, dismissal once per admin, Escape/close versus disconnect, and privilege revocation.
- [ ] Add `/basic setup`; gate automatic invitation on ready + Playing and defer behind interactive dialogs.
- [ ] Integrate delta validation/conflicts and persist-before-mutation with a failure regression test.
- [ ] Capture restart-shaped startup values; compare current saved values for pending keys, including no-op saves/reopen/revert.
- [ ] Add correlated save handling and final restart instructions with Later; no shutdown action labeled restart.
- [ ] Build and render the production chat pages using GuiPreview; clearly mark layout-only omission where the native panel is absent.

## Task 4: Teleport and notification branches

**Files:** catalog/dialog pages and focused mapping tests; use existing registry definitions.

- [ ] Expose the six BASICs travel tools and show each selected tool's existing costs, timing, and privileges without overwriting hidden values.
- [ ] Add independent save-start/save-finish Off/Chat/Popup mapping and sleep enable/percentage/wording.
- [ ] Test mapping against real config fields, including unchanged hidden values and sleep endpoint behavior.
- [ ] Render default, edited, error, review, and pending-restart scenes at two scales; request creative feedback on the next storyboard.

## Task 5: Journey and screenshot evidence

**Files:** existing AnalyticsService/relay/test contract, named GuiPreview scenes, screenshot comparison/report script/workflow.

- [ ] Add server-issued run IDs, ordered finite step/action/choice properties, consent-compliant attribution, and saved/live/pending outcomes.
- [ ] Update producer/Worker contract revision and allowlists together; run existing relay contract tests plus the new journey cases.
- [ ] Add before/after/diff manifests and reuse the sticky-comment marker pattern for added/changed/removed/missing/stale captures.
- [ ] Generate short native clips from named frames and adapt the existing Gemini reviewer rubric/sampling. Verify provider access before making review claims.

## Task 6: Final validation and handoff

- [ ] Run focused tests, then one complete suite and canonical build-and-package with output restricted to local staging until QA staging.
- [ ] Review each task and one whole-branch diff; fix substantive findings and rerun affected checks.
- [ ] Stage the authorized test build, collect real-client captures, and keep human gameplay observation separate from model/image results.
- [ ] Record exact package/source/game versions and remaining human QA cards. No merge-ready or release claim without the repository's required evidence.

## Execution ledger

- Worktree: `C:/Users/steve/.codex/worktrees/visual-config-wizard/Vintage-Story-Mods`, base `cf8f1d5`.
- Baseline: 1148 passed, 6 skipped, 0 failed. Managed-worktree obj writes required sandbox escalation; no provider/server operation was performed.
- Guide fixture: installed-native validation and focused xUnit test passed, 1/1.
- Ruling: proceed autonomously under the owner's explicit continuation instruction; preserve creative checkpoints and human QA observations.
- Ruling: portable restart is instructions plus Later, because the engine exposes shutdown but no relaunch capability.
- Pre-flight: native preview signature is shared by dialog and probe; draft/message signatures are established before server/client integration. Root owns existing integration files; workers own separate new files.
