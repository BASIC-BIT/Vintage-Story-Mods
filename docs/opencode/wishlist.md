# Wishlist (Agentic Tooling)

This file is a lightweight backlog of "meta" improvements we may want to dogfood over time.
Keep it short. Promote items into skills/tools/playbooks when they become real work.

## Near-term
- Playwright exploratory pass for Vintage Story ModDB pages (read comments, verify releases).
- VS wiki discovery workflow (search + fetch + durable notes).
- Pterodactyl test-server loop:
  - deploy zip
  - restart server
  - tail logs
- Deterministic log triage patterns for large files (tail, grep, last-N matches).
- Session auditor automation: post-hoc extraction of missed action items + filing into durable storage.

## Medium-term
- Observability for swarm sessions:
  - status + ownership + "locks" for active work items
  - worktree registry
- Context/compaction ergonomics:
  - a pre-compaction "nudge" hook (plugin)
  - optional session metrics snapshot

- "Report, then continue" loop driver for long-running tasks (Ralph-style), with objective stop conditions and explicit wait points.
  - Implemented first cut as `.opencode/plugins/auto-continue.ts` (opt-in env vars)

- Agent observability TUI (multi-session tabs + attention requests/demands).
  - Notes: `docs/opencode/agent-observability-tui.md`

## Long-term
- Obsidian notes integration (directory indexing + similarity search) for long-horizon memory.
- Cross-project "agentic starter kit" repo (portable skills/tools/playbooks).
- CAPA-style loop for recurring bug classes:
  - record root cause
  - add guardrail (tooling/checklist/skill)
  - reduce recurrence
