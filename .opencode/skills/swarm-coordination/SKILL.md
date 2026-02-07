---
name: swarm-coordination
description: Patterns for using parallel subagents, lightweight shared state, and OpenCode plugins without blowing up context.
compatibility: opencode
metadata:
  audience: maintainers
  domain: agentic
---

## Goal
Maximize throughput with parallel subagents while keeping the main context clean and durable.

## Constraints to remember
- Subagents have limited context unless you provide anchors.
- Tool outputs can bloat context; summarize and persist the important parts.
- Shared state must be durable across restarts (file-backed) and safe (no secrets).

## Recommended workflow
1. Main agent creates a short plan and assigns discovery tasks to subagents.
2. Each subagent returns:
    - file paths
    - key findings
    - recommended next steps
3. Main agent writes the durable outcome into stable files:
    - `AGENTS.md` for global repo guidance
    - `memory-bank/*.md` when explicitly requested
    - `.opencode/skills/*/SKILL.md` for reusable playbooks
4. Use `todowrite` for session-level tracking.

## Asynchronous "meta" agents (session auditors, reviewers)

Patterns that add compute without derailing the main thread:

- Session auditor: periodically reviews the last N minutes of chat and extracts action items, decisions, and new heuristics.
  - Output should be a small patch to durable storage (skill/doc/AGENTS) or a todo list update.
- Doc coalescer: takes raw notes from auditors/researchers and files them into the right "cabinet" (AGENTS vs skill vs docs).
- Reviewer: cold-context review of a change-set with a checklist (correctness, safety, durability, log noise, backwards compat).
- Recycler: validates review feedback, decides what is actionable, applies changes, and optionally asks the reviewer to re-check.

## Reviewer -> recycler loop (single-layer delegation)

Even without recursive subagents, you can get most of the value with a single delegation layer:

1) Main agent makes changes and commits small chunks.
2) Launch 1 reviewer subagent on the diff/paths.
3) Main agent (recycler role) applies the accepted feedback.
4) Optional: re-run the reviewer on the updated diff.

This keeps the main thread moving while still getting cold-context critique.

Implementation detail:

- Use `git worktree` to isolate these agents so they can make clean commits without interfering with the main worktree.
- Keep their scope narrow (docs-only, tooling-only, etc.).

## "Ralph" / Loop drivers (long-running agents)

The "Ralph Wiggum" pattern (popularized as "Ralph is a bash loop") is: repeatedly re-run an agent on the same objective until an objective stop condition is reached.

How to use it safely:

- Always set a hard iteration cap and/or budget cap.
- Use objective completion criteria (build succeeds, tests pass), not the agent’s self-assessment.
- Prefer fresh-context iterations with progress persisted in files + git history.
- Treat destructive operations as gated steps (env flags + confirm=true), even inside loops.

In this repo, the recommended "stop condition" is usually a deterministic build/package step + a human-in-the-loop check (using the `question` tool) for in-game behavior.

## Manual parallelism (multiple terminals)
If you can open multiple OpenCode terminals, you can run parallel "main" sessions manually:
- Give each session a scoped objective and a shared output location (a file path).
- Use durable files (skills/docs) for any workflow you expect to repeat.
- Converge by summarizing into one commit/PR plan.

## Anchors to give subagents
Always include:
- Target directories
- Keywords to search
- Expected outputs (bullet list)
- Hard constraints ("don’t edit code", "no secrets", "must be compatible with VS 1.20")

## Shared state options
Start simple:
- File-backed JSON in the repo (ignored if needed), e.g. `logs/agent-state.json`.

When needed, graduate to infrastructure:
- Redis (or SQLite) as a coordination store.

## Worktrees for parallelism
For higher isolation, run parallel efforts in separate `git worktree` checkouts.
See: `.opencode/skills/git-worktrees/SKILL.md`.

## OpenCode plugin hooks for coordination
OpenCode plugins can listen to events such as:
- `todo.updated`
- `session.status`
- `session.compacted`

You can use these to:
- mirror todo state to a file/db
- emit status notifications
- inject extra compaction context

## Safety
- Never store credentials in shared state.
- Treat server restarts/deploys as destructive; gate behind explicit flags.
