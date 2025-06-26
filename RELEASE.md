# Release Process

This document explains how to create releases using the automated GitHub Actions release workflow.

## How to Create a Release

1. **Navigate to Actions**: Go to the repository's Actions tab on GitHub
2. **Select Release Workflow**: Click on "Create Release" in the workflow list
3. **Run Workflow**: Click "Run workflow" and fill in the inputs:
   - **New Version**: Enter the semantic version (e.g., `5.1.1`, `6.0.0-rc.1`)
   - **Pre-release**: Check if this is a pre-release version (optional)
   - **Release Notes**: Add custom release notes or leave empty for auto-generated notes (optional)

## What the Workflow Does

The release workflow automatically:

1. **Validates** the version format (semantic versioning)
2. **Updates** version numbers in:
   - `mods-dll/thebasics/modinfo.json`
   - `mods-dll/thebasics/Properties/AssemblyInfo.cs`
3. **Commits** the version changes to the master branch
4. **Builds** the project using the existing build system
5. **Packages** the mod into a versioned zip file (`thebasics-vX.X.X.zip`)
6. **Creates** a Git tag (e.g., `V5.1.1`)
7. **Publishes** a GitHub release with the packaged mod as an attachment

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
- Access to the `master` branch
- `VS_DEPS_TOKEN` secret (for build dependencies)
- Write permissions for the repository

## Troubleshooting

If the workflow fails:
1. Check the version format is correct
2. Ensure the `master` branch is up to date
3. Verify the `VS_DEPS_TOKEN` secret is set
4. Check the build logs for specific errors

## Manual Process (if needed)

If you need to create a release manually:
1. Update version numbers in `modinfo.json` and `AssemblyInfo.cs`
2. Commit and push changes
3. Run the build process locally
4. Create a GitHub release manually
5. Upload the packaged mod file 