---
name: vintage-story-ci-dependencies
description: Update Vintage Story CI dependency versions, upload matching game DLLs to vs-build-dependencies, and verify GitHub Actions builds against the current stable game version.
compatibility: opencode
metadata:
  audience: maintainers
  domain: ci-release-ops
---

## Goal

Keep CI building against the intended Vintage Story version by aligning three things:

- the official latest stable Vintage Story version
- the local Vintage Story install used as the DLL source
- the `VS_VERSION` values in GitHub Actions workflows

## When To Use

Use this workflow when:

- GitHub Actions fails while downloading `vs-build-dependencies`.
- CI needs to move to a new Vintage Story version.
- A maintainer asks whether CI should target `1.x.y`.
- `scripts\upload-vs-dependencies.ps1` or workflow `VS_VERSION` values are changed.

## Safety Rules

- Check `git status --short` before making changes.
- Treat `BASIC-BIT/vs-build-dependencies` as a separate repository with contributor-facing impact.
- Get explicit maintainer approval before pushing new dependency DLLs to `vs-build-dependencies`.
- Get explicit maintainer approval before pushing changes to `Vintage-Story-Mods`.
- Commit only the CI/dependency-version files for this workflow. Do not include unrelated dirty feature work.
- Do not print tokens, `.env` values, or secret-bearing clone URLs.

## Version Detection

Check the official latest stable Windows server version:

```powershell
$metadata = Invoke-RestMethod -Uri "https://api.vintagestory.at/stable.json"
$latest = $metadata.PSObject.Properties |
  Where-Object { $_.Value.windowsserver -and $_.Value.windowsserver.latest -eq 1 } |
  Sort-Object { [version]$_.Name } -Descending |
  Select-Object -First 1
$latest.Name
```

Check the local game install version:

```powershell
& "D:\Games\Vintagestory\Vintagestory.exe" --version
```

If the local install is behind the official latest stable, stop and ask the maintainer whether to update the local install first or target the installed version temporarily.

## Dependency Repo Check

List available dependency versions:

```powershell
gh api repos/BASIC-BIT/vs-build-dependencies/contents --jq '.[].name'
```

Check a specific version directory:

```powershell
gh api repos/BASIC-BIT/vs-build-dependencies/contents/1.22.2 --jq '.[].name'
```

Expected contents for a valid dependency version:

- `core`
- `mods`
- `lib`
- `README.md`
- `version-info.json`

## Upload Dependencies

Only run this after explicit maintainer approval.

Use the local install as the source of the DLLs:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\upload-vs-dependencies.ps1" -VsInstallPath "D:\Games\Vintagestory" -VsVersion "1.22.2"
```

The upload script will:

- validate required DLLs under the local Vintage Story install
- clone `BASIC-BIT/vs-build-dependencies`
- create the version directory with `core`, `mods`, and `lib`
- commit the new DLL set
- push to `main` in the dependency repository
- remove its temporary work directory

After upload, verify the version is visible through the GitHub API before changing CI.

## Update CI Version

Update all workflow `VS_VERSION` values together:

- `.github/workflows/build.yml`
- `.github/workflows/codeql.yml`
- `.github/workflows/release.yml`

Update the upload script default so future runs use the same version:

- `scripts/upload-vs-dependencies.ps1`

Do not update only one workflow. A partial update leaves release, build, and CodeQL using different game DLLs.

## Local Verification

Use the repo-local .NET 10 SDK when available. The system `dotnet` may be .NET 9 and fail on `net10.0`.

```powershell
$env:VINTAGE_STORY = "D:\Games\Vintagestory"
& "D:\bench\vs\.dotnet\dotnet.exe" build "mods-dll\thebasics\thebasics.csproj" --configuration Release /p:GITHUB_ACTIONS=true
```

If the local .NET 10 SDK path is unavailable, use `actions/setup-dotnet` behavior as the reference and install or locate a .NET 10 SDK before concluding the build is broken.

## Commit And Push

Before committing, inspect only the intended files:

```powershell
git diff -- .github/workflows/build.yml .github/workflows/codeql.yml .github/workflows/release.yml scripts/upload-vs-dependencies.ps1
```

Commit only those files:

```powershell
git add -- .github/workflows/build.yml .github/workflows/codeql.yml .github/workflows/release.yml scripts/upload-vs-dependencies.ps1
git commit -m "Update CI to Vintage Story 1.22.2 dependencies"
```

Push only after explicit maintainer approval:

```powershell
git push origin main
```

If branch protection is bypassed, report that explicitly in the summary.

## GitHub Actions Verification

List new runs for the pushed SHA:

```powershell
gh run list --branch main --limit 5 --json databaseId,workflowName,status,conclusion,headSha,url,createdAt
```

Watch the relevant runs:

```powershell
gh run watch <run-id> --exit-status
```

At minimum, verify:

- CodeQL downloads the requested dependency version.
- CodeQL builds The BASICs and completes analysis.
- Build workflow downloads or restores the requested dependency version.
- Build workflow gets past dependency verification, restore, build, formatting, and tests.

## Failure Triage

If a run still fails at dependency download, check:

- workflow `VS_VERSION` matches the uploaded dependency folder name exactly
- `VS_DEPS_TOKEN` can access `BASIC-BIT/vs-build-dependencies`
- the dependency repo has `core`, `mods`, and `lib` under that version
- the dependency cache key is for the intended version

If a run gets past dependency download and fails later, classify it as a separate CI gate. Examples:

- formatting failure: code formatting issue
- test failure: product or test issue
- coverage threshold failure: coverage gate issue
- complexity failure: lizard complexity gate issue
- CodeQL warnings after a successful analysis: non-blocking unless the job conclusion is failure

Report clearly whether the original dependency problem is fixed before discussing later failures.

## Summary Template

Use a concise final summary:

```text
Updated CI to build against Vintage Story <version>.

- Official latest stable: <version>
- Local install: <path> reports <version>
- Uploaded dependencies: BASIC-BIT/vs-build-dependencies@<sha>
- Updated workflows/scripts: <commit sha>
- Local build: <result>
- GitHub Actions: CodeQL <result>, Build <result>

Remaining failures, if any, are <related/unrelated> to dependency setup: <short reason>.
```
