# Ralph Loop Notes (Agent Run-To-Completion)

This doc captures a recent pattern sometimes called the "Ralph Wiggum loop" ("Ralph is a bash loop"): re-running an AI coding agent repeatedly on the same objective until an objective stop condition is met.

This is not about blind persistence. It is about building reliable control loops.

## Why it helps

- The agent can keep making incremental progress while the human is away.
- The real "memory" is the repo state (files + git history), not a massive context window.

## Guardrails

- Always set a hard cap: max iterations and/or budget.
- Always use objective completion checks:
  - `dotnet build` succeeds
  - packaging succeeds
  - (if available) tests pass
- Never allow destructive steps without explicit gating:
  - environment flags
  - `confirm=true`
- Avoid per-frame logs or spammy debug instrumentation.

## Recommended stop conditions for this repo

- Mechanical code changes: `dotnet build` + packaging outputs expected zip.
- In-game behavior changes: use a human-in-the-loop step.
  - Ask for exactly one concrete action + one expected observation.
  - Use the OpenCode `question` tool as the deliberate wait point.

## Where to persist learnings

- Ultra-durable: `AGENTS.md` (global heuristics)
- Durable playbooks: `.opencode/skills/*/SKILL.md`
- Reference docs: `docs/ops/*`, `docs/opencode/*`
- Sticky (session-only): `todowrite`

If a loop repeats, promote the workflow into a playbook.

## "Report, then continue" (preferred operator experience)

In interactive use, the agent should treat intermediate summaries as *reports* to the maintainer, not as a stopping point.

Suggested pattern:

- After each meaningful milestone (a commit landed, server restarted, logs confirm), emit a short report.
- If not blocked, immediately proceed to the next planned step.
- Only pause when you need the maintainer to do something in-game or in a panel.
  - Use the OpenCode `question` tool as the explicit wait point.

In other words: default to forward progress; use human input as a deliberate gate, not as a scheduling mechanism.

## OpenCode plugin: auto-continue stop hook

OpenCode plugins can call the SDK (`client.session.prompt(...)`) when `session.idle` fires. That means we can implement a true in-OpenCode loop driver.

This repo includes an opt-in project plugin:

- `.opencode/plugins/auto-continue.ts`

Enable it by setting environment variables (local only):

- `OPENCODE_AUTO_CONTINUE=1`
- `OPENCODE_AUTO_CONTINUE_MAX=25`
- `OPENCODE_AUTO_CONTINUE_PROMISE=<promise>COMPLETE</promise>`

Safety notes:

- Keep a hard iteration cap.
- Keep destructive operations gated (env flags + confirm=true).
- Prefer objective stop conditions; for in-game behavior, the loop should pause behind an explicit `question` gate.
