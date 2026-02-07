---
name: release-and-modb
description: Cut a safe release (GitHub Actions) and upload to ModDB with a checklist and rollback steps.
compatibility: opencode
metadata:
  audience: maintainers
  domain: release
---

## Scope
This repo releases `thebasics` primarily via GitHub Actions, then performs a manual ModDB upload.

## Version sources of truth
- `mods-dll/thebasics/modinfo.json` controls the mod version shown in-game.
- `mods-dll/thebasics/Properties/AssemblyInfo.cs` also carries version info for the DLL.

## Release workflow (preferred)
Use the GitHub Actions workflow "Create Release":
- Validates semantic version format
- Updates version files
- Builds + packages
- Creates tag `Vx.y.z`
- Publishes a GitHub Release with the packaged zip attached

Docs:
- `RELEASE.md`

## ModDB upload (manual)
ModDB has no upload API. Use a checklist:
1. Download the packaged zip from the GitHub Release.
2. Confirm the zip contains:
   - `modinfo.json`
   - `thebasics.dll`
   - `assets/` (when present)
3. Confirm the version in `modinfo.json` matches the intended ModDB version.
4. Upload the file to ModDB.
5. Paste release notes/changelog.
6. Verify the public page shows the new version.

## Rollback plan
- Keep the previous zip handy.
- If a bad release goes out:
  - Upload the previous known-good zip as the latest file.
  - Add a short ModDB note explaining the rollback.
  - Open a GitHub issue for the regression.

## Automation option (Playwright)
Use `playwright-exploration` skill:
- First explore ModDB UI safely and record anchors.
- Then create a deterministic upload playbook.
- Require human-authenticated session; do not store credentials.
