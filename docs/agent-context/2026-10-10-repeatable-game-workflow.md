# Repeatable game workflow handoff

Objective: retain the automatic game setup and visual QA workflow in Git so it can be recovered after temporary outputs are removed. Branch: `codex/visual-config-wizard`. Retire this packet after the workflow is merged; retain its skill, scripts and pinned recovery patch.

Entry point: [.opencode/skills/vintage-story-agent-control/SKILL.md](../../.opencode/skills/vintage-story-agent-control/SKILL.md), with a Codex wrapper and a [command runbook](../../.opencode/skills/vintage-story-agent-control/references/client-workflow.md).

The tracked scripts cover pinned helper reconstruction, Profile2 provisioning and guarded restoration, exact-owned client stop/menu-first launch/connection, local-only BASICs packaging with a build receipt, backed-up QA staging and optional restart, GUI-scale probes, native screenshots and sampled-pose clips. The existing PR capture comparison and optional video-review adapter remain linked from the workflow. Temporary directories contain outputs only; `.tmp/` is now explicitly ignored.

Validation on October 10:

- Profile scripts: 57 behavioral checks pass, including rollback, restoration, exact process ownership, stopped-profile operation and helper filename collisions. Independent forward testing exercised the skill and corrected missing prerequisites, CLI paths, two-scale ordering and restoration instructions.
- Visual scripts: 40 assertions pass, including BOM/raw-byte preservation, conflicting-edit protection, identity consistency and real FFmpeg/FFprobe fixture encoding.
- QA staging: 9 isolated cases pass under Windows PowerShell, including backup-before-mutation, hash/source/server identity, readback corruption, credential redaction and fresh restart evidence.
- Local-only packaging passes under PowerShell 7 and Windows PowerShell. The canonical stamped package builds with zero errors. Receipt: `.tmp/workflow-canonical-build-20261010.json`; log: `.tmp/workflow-canonical-build-20261010.log`.
- Actual clean helper reconstruction passes all 23 native helper tests, packages and publishes the CLI. Receipt and logs: `.tmp/workflow-reconstruction-final-20261010/`. An initial probe exposed nested Git discovery silently skipping the patch; reconstruction now initializes an isolated Git context and verifies the startup/cancellation acceptance records.
- Source/Codex skill validators, agent-tooling validation and whitespace checks pass. The existing Windows build workflow runs the fixture suites; no hosted result is recorded yet.

No installed game profile, running game process, live QA server or provider was modified during this promotion. Human wizard cards and analytics rollout remain pending in [the wizard QA packet](2026-10-06-visual-config-wizard-qa.md). The automated launcher is verified for game 1.22.7. A failed QA staging run may leave partial writes; use its journal, backup index and generated recovery mapping before retrying.
