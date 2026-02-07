---
name: session-auditor
description: Post-hoc audit of a session to extract missed tasks and promote durable knowledge
compatibility: opencode
metadata:
  audience: maintainers
  domain: agentic
---

## Purpose

Increase time horizon and reliability by spending a small amount of extra compute to:

- extract decisions, action items, and heuristics that might have been missed in the moment
- promote them into the right persistence tier (AGENTS vs skills vs docs)
- keep the main agent thread focused on shipping

This is not meant to be perfect; it is meant to be repeatable.

## When To Use

- After a long interactive session
- After a feature lands and the verify loop is proven
- When the user gives multiple meta-instructions in a single message

## Inputs

The auditor should use objective artifacts (not just the chat):

- `git status`, `git diff`, `git log` (what actually changed)
- newly-added docs/skills (what got persisted)
- current todo list (sticky session state)

If needed, ask the user for ONE thing:

- a short paste of the last few key chat instructions (or a list of top 3 goals)

## Outputs

Produce small, reviewable changes:

- 1-3 patches to durable storage (AGENTS / skills / docs)
- optional `todowrite` updates (only for the current session)
- a short list of "open loops" that remain

Avoid mega-PRs. Prefer several small commits.

## Filing Heuristics (Where Does This Go?)

- `AGENTS.md`: global operating rules, safety boundaries, verify loop, storage tiers
- `.opencode/skills/*/SKILL.md`: repeatable playbooks and workflows
- `docs/ops/*`: operational runbooks (build/deploy/logs/servers)
- `docs/opencode/*`: OpenCode-specific notes, patterns, control loops
- `todowrite`: current-session sticky tracking; promote out once proven

## Suggested Checklist

- Did we add a new tool? Ensure its env vars are documented in `.env.example`.
- Did we learn a new failure mode? Add it to the appropriate skill/runbook.
- Did we invent a new rule? Put it in `AGENTS.md`.
- Did we do something repeatedly? Convert it into a skill playbook.
- Did we increase log noise? Gate it behind config and keep it low-volume.
