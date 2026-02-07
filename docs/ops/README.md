# Ops Playbooks

These playbooks document how we operate and ship `thebasics` safely.

Guiding principles:
- Prefer repeatable, checklisted workflows over tribal knowledge.
- Treat deployment and restarts as destructive; gate them and keep rollback steps handy.
- Never commit credentials. Use environment variables, secrets, or interactive login.

Entry points:
- GitHub release workflow: `RELEASE.md`
- VS DLL dependency workflow: `BUILD.md`
- Build output expectations: `BUILD_STRATEGY.md`

Project scripts:
- Build + package: `mods-dll/thebasics/scripts/build-and-package.ps1`
- Package only: `mods-dll/thebasics/scripts/package.ps1`
- Fetch server logs: `mods-dll/thebasics/scripts/fetch-logs.ps1`

Playbooks:
- GitHub release workflow: `docs/ops/release-github.md`
- ModDB upload checklist: `docs/ops/moddb-upload.md`
- SFTP deploy (opt-in): `docs/ops/deploy-sftp.md`
- Fetch logs (remote): `docs/ops/fetch-logs.md`
