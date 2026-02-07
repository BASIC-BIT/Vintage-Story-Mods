# Agentic Vision (Durable Notes)

This document distills operator guidance for building high-throughput, non-sloppy agentic workflows around Vintage Story modding.

## North Star
- Operator provides high-level direction; agent executes with research and parallelism.
- Output must be boring, reliable, and maintainable (tens of thousands of users).
- Avoid "AI slop": prefer evidence, repo patterns, and small safe increments.

## Key constraints
- Context is finite. Use:
  - parallel subagents for discovery
  - stable files (skills/playbooks/docs) for durable knowledge
  - concise summaries in the main thread
- Treat the operator as an active tool/input source.
  - Use explicit questions at decision points.

## Swarm model
- Parallel subagents increase throughput but need strong anchoring:
  - where to look
  - keywords
  - expected outputs
  - safety constraints
- Prefer a "fan-out then converge" pattern:
  1) subagents discover
  2) main agent integrates
  3) durable docs/skills updated

## Worktrees + versioning
- Multiple Vintage Story versions may need simultaneous support.
- Use compatibility branches and `git worktree` to keep multiple checkouts active.
- Avoid cross-version hacks; prefer cherry-picks between branches.

## Coordination state + observability
Start simple, then scale:
- File-backed state (repo-ignored) for what’s running and why.
- Graduate to Redis when it becomes valuable:
  - worktree registry
  - task status
  - lightweight locks ("agent owns X")
  - cleanup hooks on agent exit

## Tooling roadmap (progressive disclosure)
Prefer the lowest-cost mechanism that works:
1. Skills (`.opencode/skills/*`) and playbooks (`docs/ops/*`).
2. OpenCode plugins for hooks/automation glue.
3. MCP servers for structured tools (logs, server ops, ModDB, etc.).

## Product idea: Vintage Story Dev MCP
We can dogfood and potentially open-source a "Vintage Story Dev MCP":
- read-only MVP: local logs + profile discovery + mod/version introspection
- opt-in destructive tools: deploy zip, restart server (Pterodactyl API)
- strict secrets hygiene + approval gating
