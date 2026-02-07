# Agentic Vision (Durable Notes)

This document distills operator guidance for building high-throughput, non-sloppy agentic workflows around Vintage Story modding.

## North Star
- Operator provides high-level direction; agent executes with research and parallelism.
- Output must be boring, reliable, and maintainable (tens of thousands of users).
- Avoid "AI slop": prefer evidence, repo patterns, and small safe increments.

Extra heuristics:
- Dopamine matters: optimize for fast feedback loops (builds, logs, repro) without sacrificing safety.
- The system should feel simple even when the implementation is powerful.

## Key constraints
- Context is finite. Use:
  - parallel subagents for discovery
  - stable files (skills/playbooks/docs) for durable knowledge
  - concise summaries in the main thread
- Treat the operator as an active tool/input source.
  - Use explicit questions at decision points.

## Uncertainty is a feature
- Say when we *don't know* yet.
- When touching unfamiliar surfaces (new VS APIs, networking, release ops), do a research pass first:
  - search the repo and `../vs_source`
  - inspect upstream docs/mod references
  - then propose the smallest safe change
- Use the operator's judgment for:
  - UX expectations in-game
  - public-facing communication
  - risk tradeoffs and rollout defaults

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

Long-horizon idea:
- Adopt lightweight CAPA-style habits for recurring bug classes:
  - capture root cause
  - add a guardrail (test/checklist/skill/playbook)
  - ensure the class of bug is less likely to recur

## Tooling roadmap (progressive disclosure)
Prefer the lowest-cost mechanism that works:
1. Skills (`.opencode/skills/*`) and playbooks (`docs/ops/*`).
2. OpenCode plugins for hooks/automation glue.
3. MCP servers for structured tools (logs, server ops, ModDB, etc.).

Portability:
- Keep these concepts transferable across projects and agent tools.
- When possible, keep the core playbooks tool-agnostic and add thin adapters per agent platform.

## Product idea: Vintage Story Dev MCP
We can dogfood and potentially open-source a "Vintage Story Dev MCP":
- read-only MVP: local logs + profile discovery + mod/version introspection
- opt-in destructive tools: deploy zip, restart server (Pterodactyl API)
- strict secrets hygiene + approval gating
