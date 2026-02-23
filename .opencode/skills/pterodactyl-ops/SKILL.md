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

Tool-specific safety defaults in this repo:

- `ptero_files_upload` and `ptero_files_write` are restricted to `data/Mods` and `data/ModConfig`.
- `ptero_files_read` refuses obvious secret paths (`.env`, `.pem`, `.key`, `.pfx`).

## Inputs required to automate (never store in repo)
To use the API, you will need from the maintainer:
- Panel base URL
- API token (scoped minimally)
- Server identifier

Token type matters:
- `ptlc_...` is a **Client API** key (works with `/api/client/...`).
- `ptla_...` is an **Application API** key (does **not** work with `/api/client/...`).

This repo ships two tool families:

- `ptero_*`: Client API (use `PTERO_TOKEN=ptlc_...`)
- `ptero_app_*`: Application API (use `PTERO_TOKEN_APPLICATION=ptla_...`)

Practical setup options:
- Put these in system environment variables, or
- Put them in a local `.env` in the repo root (gitignored).

Store secrets as environment variables or in the panel, never in git.

## Naming gotchas

- `PTERO_SERVER_ID` is the client *server identifier* (short string) used by `/api/client/...`.
- Application API endpoints use a numeric `id`.

## Application API tooling (admin)

Read-only exploration:

- Use `ptero_app_get` to query application endpoints safely (GET-only).
  - Examples: `path="servers"`, `path="servers/1"`

By default, `ptero_app_get` is restricted to a safe allowlist (servers/nodes/locations/nests).
To allow arbitrary reads, set `PTERO_APP_ALLOW_ALL_READ=1`.

Destructive operations:

- Use `ptero_app_request` for POST/PATCH/PUT/DELETE.
- Guardrails:
  - Requires `PTERO_APP_ALLOW_WRITE=1`
  - Requires `confirm=true`

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
