# Standalone GUI preview implementation

Scope accepted by the owner: extend PR245 with a standalone renderer that executes production GUI code without starting the game. Research: docs/research/2026-09-08-standalone-gui-renderer.md.

Test seams (accepted through the research implementation request): complete production dialogs and fixture inputs -> PNG plus manifest; graphics API operations -> independently expected pixels; current/baseline PNG -> comparison result. Use real controls and painters, strict unknown-call failures, controlled input/time/assets. No silent partial rendering.

1. Implement CPU textures, rectangle rasterization, transform/clip/blend/filter behavior and PNG output with conformance tests.
2. Implement strict game API environment, real assets/fonts/Cairo upload capture, real GuiComposer construction and dialog event/render lifecycle.
3. Integrate real LanguageConfigDialog and admin settings fixtures, dice presentation, state driving, output manifests and image comparison. Produce and inspect complete images including dropdown/focus/hover.
4. Document executable workflow and limits; full tests/package, independent review, commit/push same PR. Keep no-game preview evidence distinct from live-world fidelity calibration.

No game launch or live deployment. Original live dice QA remains outstanding. New commits restart the PR review window; do not merge.
