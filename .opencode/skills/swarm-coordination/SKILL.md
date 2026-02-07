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
