# Release (GitHub Actions)

We prefer cutting releases via the GitHub Actions "Create Release" workflow.

Files that drive versioning:
- `mods-dll/thebasics/modinfo.json`
- `mods-dll/thebasics/Properties/AssemblyInfo.cs`

Workflow:
1. Ensure `master` (or the target branch) is green and up to date.
2. In GitHub Actions, run the "Create Release" workflow.
3. Provide the new version (semver) and whether it is a prerelease.
4. Verify the workflow:
   - updates version files
   - builds and packages
   - creates a tag `Vx.y.z`
   - publishes a GitHub Release with the zip attached

Rollback:
- If a release is bad, immediately publish a new release that reverts the change, or re-upload a previously known-good zip (and document it).
