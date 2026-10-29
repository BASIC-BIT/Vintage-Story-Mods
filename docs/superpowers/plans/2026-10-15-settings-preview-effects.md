# Settings preview effects implementation plan

> **For agentic workers:** Implement the speech, travel and native-artwork tasks independently; root owns dialog integration. Keep file ownership separate and run builds sequentially.

**Goal:** Demonstrate the approved settings' effects in standalone previews and present the renders, keeping game launches paused.

**Architecture:** Small preview painters use deterministic sample state and the existing draft. The dialog composes their scene with its native chat window. Scene markers and dice reuse their existing native artwork.

**Tech stack:** Existing C#, Cairo, Vintage Story GUI composer and GuiPreview harness. No dependencies, configuration keys or network messages added.

**Spec:** [Approved preview audit](../../agent-context/2026-10-15-settings-preview-audit.md), approved in chat for speech/signing, teleportation and scene markers, with optional related refinements.

## Scope and checks

- [x] Speech: listener distance and wall scene; actual obscured chat, language scrambling, signing delivery boundaries. Preserve unlimited speech and Prose's clear actions.
- [x] Travel: two platforms and one traveler, relevant destinations, command enablement, warmup, gear and cancellation effects. Respect real units and zero-delay behavior.
- [x] Native artwork: marker icon and description with enabled/disabled states; actual dice result bubble when practical. Keep permission strings out of illustrations.
- [x] Integrate scene/chat geometry, deterministic time and draft refresh without touching real players, chat, saves or clients.
- [x] Add fixed-time capture pairs and meaningful state/boundary tests. Update affected fixture consumers and previous picture assertions.
- [x] Run focused and full checks, inspect standalone renders at wide and compact sizes, obtain independent behavior and visual review, and fix findings.
- [x] Present the source-linked render gallery with remaining limitations. No game launches or live QA in this pass.

## File ownership

- Speech worker: new `SetupWizardSpeechPreview.cs` and its focused tests.
- Travel worker: new `SetupWizardTravelPreview.cs` and its focused tests.
- Artwork worker: `SetupWizardFeaturePreview.cs` and focused marker/dice tests.
- Fixture worker: `SetupWizardCaptureScenes.cs`, fixture tests and capture-list consumers.
- Root: `SetupWizardDialog.cs`, existing preview integration assertions, final renders and review handoff.

## Review focus

Check exact range boundaries and distance metrics, timing after edits, invalid or zero settings, disabled dependencies, native texture disposal, escaped custom text, and compact scene/chat fit. The standalone renderer establishes sampled appearance; in-game motion remains unverified while launches are paused.

## Verification results

Implemented the approved previews and the small dice/sleep refinements. All 1,486 tests passed with no skips; the capture workflow self-test passed 72 assertions. The agent tooling check passed. Generated 144 source-stamped renders and reviewed the setting comparisons at wide and compact sizes.

The [render gallery](C:/Users/steve/.codex/visualizations/2026/10/02/01a0fa70-8ca5-7321-ac1f-07fe824f5be2/settings-preview-effects-20261015/gallery.md) includes the comparisons and verification log. VS launches, live motion checks, deployment and human QA remain deferred. Language examples use shipped definitions; audible speech uses an open-ground sample.

Source tree identity: 89d07c77a8c4d7dacaf5d4423d2d96dc53d1c81e9d7fe6bf846dcc22628d2a13.
