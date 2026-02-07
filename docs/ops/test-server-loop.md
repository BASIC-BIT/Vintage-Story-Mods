# Test Server Dev Loop (Pterodactyl)

Target server (example): `15.235.75.126:30000`

Goal: make multiplayer iteration deterministic:
- build → deploy → restart → verify loaded version → repro → grep logs

## Preconditions
- You have a dedicated test server.
- You have OpenCode `ptero_*` tools configured via env vars (do not commit secrets).

## Version sanity check (do this first)
- Your client and server Vintage Story major/minor versions must match (e.g. VS 1.21.x).
- Your server container runtime must match that VS line:
  - VS 1.20.x typically runs on .NET 7 (Pterodactyl image like `dotnet_7`)
  - VS 1.21.x typically runs on .NET 8 (Pterodactyl image like `dotnet_8`)

If clients are on VS 1.21.x but the server is still on `dotnet_7`, upgrade the server before trying to deploy net8-built mods.

Required env vars (provide via a local `.env` in the repo root or system env vars):
- `PTERO_BASE_URL`
- `PTERO_TOKEN`
- `PTERO_SERVER_ID`

To enable destructive actions (ptero tools will refuse otherwise):
- `PTERO_ALLOW_FILES=1` (uploads)
- `PTERO_ALLOW_POWER=1` (restart)

## Agent next steps (breadcrumbs)
1. Confirm server id / connection:
   - `ptero_servers_list`
    - `ptero_status`
2. Find the mod folder:
   - `ptero_files_list` for `/`, then `data/Mods` (common for VS eggs with `--dataPath ./data`).
3. Deploy:
   - `ptero_files_upload` to `data/Mods` (confirm=true)
4. Restart:
   - `ptero_power signal=restart confirm=true`
5. Validate quickly:
   - Check server logs (API read or SFTP) for `thebasics` load.
   - Check client logs with `vsdev_logs_grep_latest`:
     - `Mods, sorted by dependency`
     - no `Assembly with same name is already loaded`

## Human action (do this last)
- Ensure the `.env` values include `PTERO_ALLOW_FILES=1` and `PTERO_ALLOW_POWER=1` when you want the agent to deploy/restart the test server.

## Notes
- The server dictates required mod versions. If your local `thebasics` version does not match server, the client will disable it and may download another version into `ModsByServer/<host-port>`.
- Typing indicator renders above other players (not yourself). Verify with 2 clients.
