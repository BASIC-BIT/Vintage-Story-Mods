# SFTP Deploy (Opt-In)

This repo supports uploading a built zip to the server via SFTP.

Safety posture:
- Upload is disabled by default.
- Enable it only when you explicitly intend to deploy.

Prereqs:
- WinSCP installed (for `WinSCPnet.dll`)
- A local `.env` in `mods-dll/thebasics/.env` (see `.env.example`)

Optional:
- Use `THEBASICS_ENV_PATH` to pick a specific env file (e.g. `.env.test`).

Deploy:
1. Build + package:
   - `mods-dll/thebasics/scripts/build-and-package.ps1`
2. Enable SFTP upload for this session:
   - Set `THEBASICS_ENABLE_SFTP_UPLOAD=1`
3. Run packaging (or re-run build-and-package).

Notes:
- Destination path is `/data/Mods/<zip>`.

Rollback:
- Re-upload the previous known-good zip.
