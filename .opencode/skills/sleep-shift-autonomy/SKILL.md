---
name: sleep-shift-autonomy
description: Run an overnight autonomous issue sweep while the operator is offline, producing ready-to-merge PRs without performing merges.
compatibility: opencode
metadata:
  audience: maintainers
  domain: workflow
---

## Goal

Keep agent output productive during operator off-hours by selecting reliable, high-impact issues and shipping clean PRs.

## Scope

- Pick one or more issues that are impactful but low-risk to implement autonomously.
- Prefer changes with deterministic validation (build, lint, static checks, logs) over changes requiring subjective visual judgment.

## Workflow

1. Triage open issues by impact x reliability.
2. Create a dedicated worktree + branch from the base branch.
3. Implement fixes in focused commits.
4. Run local validation (build/tests/log checks as applicable).
5. Push branch and open PR with clear summary, risks, and verification.
6. Watch CI/checks and recycle on bot/human feedback until green.
7. Confirm mergeability and leave merge for operator approval.

## Guardrails

- Do not merge.
- Respect approval gates for contributor-facing GitHub actions.
- If manual QA is required, provide a concrete QA card plan and leave completion to operator approval.
- Keep PR scope tight; avoid mixing unrelated polish.

## Overnight output standard

- PR is open, green, mergeable, and has no unresolved review feedback.
- Include a short "morning handoff" note: what changed, why, how verified, and any remaining manual QA cards.
