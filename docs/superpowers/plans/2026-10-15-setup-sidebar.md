# Native setup sidebar implementation plan

> **For agentic workers:** Use subagent-driven development for the sidebar component and focused checks; root integrates the existing dialog. Each task has a separate file owner.

**Goal:** Ship the approved expandable sidebar in the native setup wizard, preserving drafts, previews and the existing save/restart flow.

**Architecture:** A dedicated native GUI element owns sidebar drawing, branch layout, scrolling, hit testing and keyboard focus. `SetupWizardDialog` owns destination changes, remembered category pages, editor layout and footer actions. Existing preview/capture systems continue to open catalog page IDs.

**Tech stack:** C#, Vintage Story 1.22.7 native GUI/Cairo, existing GuiPreview and xUnit harness, retained Agent Control workflow.

**Spec:** [Approved sidebar design](../specs/2026-10-15-setup-sidebar-design.md).

## Constraints and review focus

- No configuration/protocol or gameplay behavior changes. Retain privilege cursor routing and pending-save behavior.
- All eight category headers remain visible; only the current branch's subpage list scrolls. All 40 destinations remain listed.
- Native callbacks finish before a navigation change replaces their composer. Restore sidebar keyboard focus after its own navigation.
- A changed text/number field updates draft indicators without replacing the focused field.
- Exercise long branches, short branches, restoring a scrolled page, keyboard selection, shared setting keys, unsaved close, errors and finish pages.
- Fit 1600x1000 and 1280x720 at scales 1 and 1.25. Do not use automated screenshots as human acceptance.

## Task 1: Native sidebar element

Files: create `src/ModSystems/ChatUiSystem/GuiElementSetupSidebar.cs`; share the existing topic-card icon painter in `GuiElementSetupTopicCard.cs`; add focused sidebar tests.

Interface: constructor `(ICoreClientAPI api, ElementBounds bounds, Action<string> navigate)`. `SetState(string pageId, string topicId, IReadOnlyCollection<string> changedKeys)` refreshes selection, dirty indicators and branch geometry. Destinations are `hub`, catalog page IDs, and `topic:<id>` for category headers. Internal destination bounds/layout access may support GUI integration checks.

- [x] Write GUI checks for mouse/keyboard destinations, inner scrolling with fixed category headers, selected-page reveal and short-branch compaction.
- [x] Implement native Cairo row rendering, pointer/focus states, clipping, scroll thumb/wheel/drag and disposal. Reuse catalog IDs and existing icons.
- [x] Run focused sidebar checks at both scales once root releases the shared build slot.

## Task 2: Dialog integration and navigation regressions

Files: `SetupWizardDialog.cs`, existing `SetupWizardChatPreviewTests.cs`, and focused privilege/dialog tests if geometry changes require them.

- [x] Replace top tabs and per-page footer navigation with the sidebar. Keep remembered category pages and queue navigation until the current input dispatch completes.
- [x] Place the existing editor in a separate content region, adjust available control/card widths for narrower clients, and keep the footer outside that region.
- [x] Keep All settings, dirty count, Close and Review/Save together. Move review pagination into the review content. Preserve finish/restart and pending-save handling.
- [x] Refresh sidebar dirty state in place after edits. Keep all catalog subpages discoverable and preserve current dependent field visibility.
- [x] Replace tab/dropdown-specific assertions with actual sidebar mouse/keyboard navigation, remembered-page, draft and viewport checks. Preserve unrelated preview/cursor assertions.

## Task 3: Review, package and native QA handoff

Files: capture fixtures/tests as needed, `mods-dll/thebasics/docs/FEATURES.md`, existing wizard QA packet. Record the prototype's approved verdict on its throwaway branch.

- [x] Run focused GUI checks, then the full implementation suite and all standalone wizard captures at both scales. Inspect hub, Chat, long Teleportation, Map, Review and restart samples.
- [x] Obtain an independent source/visual review and fix valid findings before final packaging.
- [x] Build with the canonical local-only package script and record source/package hashes. This is a client GUI change, so refresh only verified Profile2; preserve all other mods and leave Profile3/server untouched.
- [ ] Capture source-verified native samples through Agent Control, inspect logs and images, reopen ordinary setup, and disable automation. Deferred while Profile3 is running. The retained launcher blocks concurrent-client connection forwarding; Profile3 remains untouched.
- [x] Record exact evidence and remaining human cards, commit/push the scoped implementation branch, and present the native result. No merge, release or baseline approval.

## Verification and handoff

The full suite passed 1,427 cases. After the final card-text fit correction, all 25 targeted checks passed. All 104 standalone scene/viewport cases rendered; four hub renders were regenerated after the text correction. Independent source and visual review has no outstanding findings. Profile2 is staged with the compiled-source-verified final package, and all 11 other mod hashes are unchanged. Native capture and owner observations remain pending. See the current section of [the QA packet](../../agent-context/2026-10-06-visual-config-wizard-qa.md) for receipts, source identity and the launch blocker.
