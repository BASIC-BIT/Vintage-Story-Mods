---
name: change-review
description: Cold-context review checklist for thebasics + ops/tooling changes
compatibility: opencode
metadata:
  audience: maintainers
  domain: quality
---

## Purpose

Provide a repeatable, low-drama review loop using a fresh context agent.

## When To Use

- Before releases
- After a risky change (networking, config contracts, UI rendering)
- After adding new ops tooling (Pterodactyl, scripts)

## Inputs

- `git status`
- `git diff`
- `git log -n <N>`

If needed, the reviewer can request a specific file read.

## Checklist

Client safety:

- No crash-prone code paths for cosmetic features
- Exceptions swallowed only where appropriate; logs are low-volume

Backwards compatibility:

- No renumbering/reuse of `[ProtoMember(n)]`
- New config fields use next sequential ID

Networking:

- Client->server sends use safe patterns; sends on state change (not every frame)
- Messages are resilient to missing/unknown fields

Ops/tooling safety:

- Destructive actions gated (env var + `confirm=true`)
- Tokens/secrets never written to repo
- Scripts handle common file naming variants (`.log` vs `.txt`)

Docs durability:

- New env vars added to `.env.example`
- New workflows captured in the correct place (AGENTS vs skill vs docs)

## Reviewer Output Format

- 3-8 bullets max
- Each bullet includes a file path and a concrete suggestion
- Separate "must fix" vs "nice to have"
