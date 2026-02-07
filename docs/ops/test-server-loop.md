# Test Server Dev Loop (Pterodactyl)

Target server (example): `15.235.75.126:30000`

Goal: make multiplayer iteration deterministic:
- build → deploy → restart → verify loaded version → repro → grep logs

## Preconditions
- You have a dedicated test server.
- You have OpenCode `ptero_*` tools configured via env vars (do not commit secrets).

Required env vars (provide via a local `.env` in the repo root or system env vars):
- `PTERO_BASE_URL`
- `PTERO_TOKEN`
- `PTERO_SERVER_ID`

To enable destructive actions:
- `PTERO_ALLOW_FILES=1` (uploads)
- `PTERO_ALLOW_POWER=1` (restart)

## Loop
1. Build + package locally:
   - `mods-dll/thebasics/scripts/build-and-package.ps1`
2. Upload the mod zip to the server Mods folder:
   - Use `ptero_files_upload` to upload the desired `thebasics_*.zip` to `/Mods`.
3. Restart the server:
   - Use `ptero_power` with `signal=restart`.
4. Restart client(s) if needed:
   - If client logs show `Assembly with same name is already loaded`, fully restart the client before reconnecting.
5. Verify client load:
   - Use `vsdev_logs_grep_latest` on `client-main.log` for:
     - `Mods, sorted by dependency` (should include `thebasics`)
     - absence of `Assembly with same name is already loaded`

## Notes
- The server dictates required mod versions. If your local `thebasics` version does not match server, the client will disable it and may download another version into `ModsByServer/<host-port>`.
