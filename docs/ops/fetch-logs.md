# Fetch Logs (Remote)

We fetch server logs via SFTP using WinSCP.

Script:
- `mods-dll/thebasics/scripts/fetch-logs.ps1`

Prereqs:
- WinSCP installed (for `WinSCPnet.dll`)
- `mods-dll/thebasics/.env` exists and contains SFTP variables (see `.env.example`)

Optional:
- Set `THEBASICS_ENV_PATH` to use a different env file (e.g. `.env.test`).

Usage:
- Default:
  - `mods-dll/thebasics/scripts/fetch-logs.ps1`
- Optional parameters:
  - `-LogType server|debug|crash|mod|all`
  - `-Days <n>`

Output:
- Downloads into `logs/<timestamp>/` under the repo root.

Hygiene:
- Do not commit logs.
