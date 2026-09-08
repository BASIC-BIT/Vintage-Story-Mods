# Standalone GUI preview verification, 2026-09-08

Implemented in dice PR #245. Entrypoint: scripts/preview-gui.ps1; details: tools/GuiPreview/README.md.

- Complete installed Vintage Story 1.22.6 on Windows, no game process or server required.
- All 13 CLI scenarios rendered and representative settings/dropdown/dice images inspected.
- 847 The BASICs tests passed, including 28 new GUI tests. Full scene tests were explicitly enabled.
- Real focus-state change produced a nonzero pixel diff; known-pixel tests cover compositor behavior.
- A fresh standalone output directory rendered an admin settings scene with only declared runtime dependencies.
- Canonical build-and-package passed with output directed to ignored local staging, no profile/server deployment.
- Independent standards review found no hard violations or actionable defects. Spec review findings for manifest completeness and production regression proof were fixed and verified; broader edge-tooltip and selection coverage is explicitly future work.
- Output includes PNGs and provenance/command/control-bounds manifests. Baselines remain explicit reviewed inputs, never automatically accepted.

This evidence does not establish pixel identity to the GPU renderer or complete existing manual dice QA. Live calibration, multiplayer coexistence and actual Discord delivery remain unverified. New commits require a fresh exact-head CI/review window before merge-ready can be claimed.
