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

## Medium-term
- Observability for swarm sessions:
  - status + ownership + "locks" for active work items
  - worktree registry
- Context/compaction ergonomics:
  - a pre-compaction "nudge" hook (plugin)
  - optional session metrics snapshot

## Long-term
- Obsidian notes integration (directory indexing + similarity search) for long-horizon memory.
- Cross-project "agentic starter kit" repo (portable skills/tools/playbooks).
- CAPA-style loop for recurring bug classes:
  - record root cause
  - add guardrail (tooling/checklist/skill)
  - reduce recurrence
