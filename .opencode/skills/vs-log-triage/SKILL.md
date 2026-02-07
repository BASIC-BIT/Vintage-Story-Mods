---
name: vs-log-triage
description: Find and triage Vintage Story client/server logs (local and remote) and capture them for bug reports.
compatibility: opencode
metadata:
  audience: maintainers
  domain: debugging
---

## Local log locations (Windows defaults)
Vintage Story uses `GamePaths.DataPath` under `%APPDATA%\VintagestoryData` by default.

Common folders:
- Logs: `%APPDATA%\VintagestoryData\Logs`
- Mod configs: `%APPDATA%\VintagestoryData\ModConfig`
- Mods: `%APPDATA%\VintagestoryData\Mods`

Note: the game can override log path via `GamePaths.CustomLogPath` (program args).

## Remote server logs (Pterodactyl-style container)
This repo already includes an SFTP-based log fetch script:
- `mods-dll/thebasics/scripts/fetch-logs.ps1`

By convention in this project, remote paths are under `/data/Logs` and `/data/CrashReports`.

## Workflow: collecting logs for an issue
1. Reproduce the issue.
2. Collect logs:
   - Local: copy the relevant `Logs/*.txt` files.
   - Remote: run `fetch-logs.ps1` (downloads into `logs/<timestamp>/`).
3. Identify the first exception stacktrace (often the root cause).
4. Capture:
   - game version
   - mod version (`modinfo.json`)
   - branch name
   - exact repro steps

## Common multiplayer dev pitfall: "Assembly with same name is already loaded"
Symptom (client log):
- `[thebasics] Exception: Assembly with same name is already loaded`
- Followed by: `Server sends me channel name thebasics, but no client side mod registered it.`

Likely cause:
- The client process already loaded a different version/copy of the same assembly name.
- Often triggered by `Mods` vs `ModsByServer/<host-port>` copies, or reconnecting without restarting.

Quick fixes:
- Restart the client between mod version changes.
- Ensure only one copy of the mod is discoverable in mod search paths (prefer one location).

## Hygiene
- Do not commit logs.
- Scrub personally identifying info if logs are shared publicly.
