---
name: moddb-release-playwright
description: Prepare and publish Vintage Story ModDB releases, including owner-reviewed public changelog copy and Playwright browser upload when direct API upload is unavailable.
compatibility: opencode
metadata:
  audience: maintainers
  domain: release-ops
---

## Skill: moddb-release-playwright

## Purpose

Prepare owner-reviewed public release copy and automate Vintage Story ModDB publishing through browser actions when a direct upload API is not available.

Target site: `https://mods.vintagestory.at`

For GitHub or ModDB release-note drafting, review, or platform conversion, read [references/public-release-notes.md](references/public-release-notes.md) before writing copy.

## Inputs required

- `modId` (numeric mod id on ModDB)
- `zipPath` (absolute path to built mod zip)
- `changelogHtmlOrText`
- `compatibleVersions` (array of semver strings, e.g. `1.21.6`)

## Preconditions

- The zip already exists locally (build/package step completed).
- Operator can complete any interactive auth challenge (account login/2FA) if prompted.

## Workflow

1. Prepare one fact-checked canonical release body using `references/public-release-notes.md`, present it verbatim for owner approval, and derive platform formatting only after approval.
2. Open login page: `https://mods.vintagestory.at/login`.
3. Complete auth flow and wait until logged in.
4. Navigate to release page:
   - `https://mods.vintagestory.at/edit/release/?modid=<modId>`
5. Upload file using file input selector:
   - `input[name="newfile"]`
6. Wait for upload/parse completion:
   - no active upload progress
   - auto-detected mod id/version fields populated
7. Set compatible versions by toggling:
   - `input[name="cgvs[]"]`
8. Set the approved changelog text in:
   - `textarea[name="text"]`
9. Click save button (`Save`), then wait for navigation.
10. Verify success:
   - URL includes `assetid=` OR
   - release appears in mod files tab

## Safety checks

- Do not submit if mod id/version did not parse from uploaded file.
- Do not submit if no compatible versions selected for game mods.
- Capture screenshot and page URL before final submit.

## Output

Return:

- final release URL
- uploaded filename
- selected compatible versions
- success/failure and any UI error text

## Notes

- If this flow becomes brittle, prefer implementing upstream API endpoint support in `anegostudios/vsmoddb` issue `#18`.
