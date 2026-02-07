---
name: github-ops
description: Repeatable GitHub workflows for this repo (issues, branches, commits, PRs, and public-facing communication).
compatibility: opencode
metadata:
  audience: maintainers
  domain: github
---

## Principles
- Public-facing actions (PRs, issue comments, releases, pushes) should be deliberate.
- Prefer small, reviewable PRs.
- Never leak secrets (logs, tokens, `.env`).

## Local workflow (typical)
1. Create or switch to a topic branch.
2. Implement changes.
3. Build locally.
4. Commit in logical chunks.
5. Push and open a PR.
6. Ensure CI is green.

## Commit guidelines
- Keep commits focused.
- Message should explain the why.
- Avoid committing generated binaries and logs.

## PR guidelines
Include:
- What changed (1-3 bullets)
- Why it changed
- How to test (manual steps)
- Risk notes / rollout plan (config flags, defaults)

## Issue triage
When a report comes in:
- Request: game version, mod version, repro steps, logs.
- Categorize: crash, regression, feature request.
- Link to related issues.

## Safety gates
If a tool/action affects the public (push, PR create, issue comment), ask the maintainer for a quick approval of the exact text/command.
