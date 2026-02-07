---
name: pterodactyl-ops
description: Operate a Vintage Story server hosted on Pterodactyl (panel concepts, API-oriented workflows, and safe automation boundaries).
compatibility: opencode
metadata:
  audience: maintainers
  domain: server-ops
---

## What Pterodactyl is
Pterodactyl is a game server management panel. It typically provides:
- A web UI to start/stop/restart servers
- Console access and log viewing
- File manager for `/data/...` style container mounts
- An HTTP API for automation (tokens required)

References:
- Project docs: https://pterodactyl.io/
- API reference (community): https://dashflo.net/docs/api/pterodactyl/v1/

## Safe automation posture
- Treat restarts, file uploads, and config changes as "destructive" operations.
- Default to read-only operations (fetch logs, list files) until explicitly authorized.

## Inputs required to automate (never store in repo)
To use the API, you will need from the maintainer:
- Panel base URL
- API token (scoped minimally)
- Server identifier

Token type matters:
- `ptlc_...` is a **Client API** key (works with `/api/client/...`).
- `ptla_...` is an **Application API** key (does **not** work with `/api/client/...`).

This repo's `ptero_*` tools use the Client API, so you want a `ptlc_...` key.

Practical setup options:
- Put these in system environment variables, or
- Put them in a local `.env` in the repo root (gitignored).

Store secrets as environment variables or in the panel, never in git.

## Existing workflows in this repo
- Remote log download via SFTP:
  - `mods-dll/thebasics/scripts/fetch-logs.ps1`
- Mod zip upload via SFTP (opt-in):
  - `mods-dll/thebasics/scripts/package.ps1`

## Recommended next step
If Pterodactyl API access is desired, add a small script that can:
- Get server status (read-only)
- Fetch recent console output (read-only)
- Restart server (explicit flag required)

This repo includes repo-local `ptero_*` OpenCode tools under `.opencode/tools/ptero.ts`.

Keep it opt-in and require an environment variable guard.
