# Release Process

This document explains how to create releases using the automated GitHub Actions release workflow.

## How to Create a Release

1. Save the exact owner-approved GitHub release body to a local UTF-8 Markdown file.
2. Run the workflow with GitHub CLI so the file's line breaks are preserved:

   ```powershell
   $notesPath = Resolve-Path ".\release-notes.md"
   gh workflow run release.yml --ref main `
     --raw-field new_version=5.1.1 `
     --raw-field prerelease=false `
     --raw-field persist_version_commit=false `
     --field "release_notes=@$notesPath"
   ```

3. Change `new_version` to the approved semantic version. Set `prerelease` when appropriate. Set `persist_version_commit=true` only when version changes should be pushed to the default branch and `RELEASE_PUSH_TOKEN` is configured.

The Actions-tab form uses a single-line string field and is not suitable for an exact multiline release body. The workflow rejects blank notes and notes containing U+2014 before changing or pushing version files.

## Public Release Notes

Before drafting, reviewing, or converting GitHub or ModDB release copy, follow [the repository release-note reference](.opencode/skills/moddb-release-playwright/references/public-release-notes.md).

- Prepare one canonical body and adapt only the platform formatting.
- Present each exact platform-ready body and its rendered preview for owner approval before posting or updating either public page.
- Never put an `[AGENT]` prefix or an em dash in public release-note copy.

## What the Workflow Does

The release workflow automatically:

1. **Validates** the version format (semantic versioning)
2. **Updates** version numbers in:
   - `mods-dll/thebasics/modinfo.json`
   - `mods-dll/thebasics/Properties/AssemblyInfo.cs`
3. **Creates** a local release source commit for versioned packaging and tagging
4. **Builds** the project using the existing build system
5. **Packages** the mod into a versioned zip file (`thebasics-vX.X.X.zip`)
6. **Creates** a Git tag (e.g., `V5.1.1`)
7. **Publishes** a GitHub release with the packaged mod as an attachment

By default, the workflow does not push version file changes back to the default branch. To persist those changes, enable **Persist Version Commit** and ensure `RELEASE_PUSH_TOKEN` is configured.

## Version Format

Use semantic versioning format:
- **Release**: `5.1.1`, `6.0.0`
- **Pre-release**: `6.0.0-rc.1`, `5.1.1-beta.2`

## Release Artifacts

Each release includes:
- **Source code** (automatically attached by GitHub)
- **Mod package**: `thebasics-vX.X.X.zip` (ready for installation)

## Prerequisites

The workflow requires:
- Access to the repository default branch
- `VS_DEPS_TOKEN` secret (for build dependencies)
- Write permissions for the repository
- `RELEASE_PUSH_TOKEN` secret only when **Persist Version Commit** is enabled

## Troubleshooting

If the workflow fails:
1. Check the version format is correct
2. Ensure the default branch is up to date
3. Verify the `VS_DEPS_TOKEN` secret is set
4. Check the build logs for specific errors

## Manual Process (if needed)

If you need to create a release manually:
1. Update version numbers in `modinfo.json` and `AssemblyInfo.cs`
2. Commit and push changes
3. Run the build process locally
4. Create a GitHub release manually
5. Upload the packaged mod file
