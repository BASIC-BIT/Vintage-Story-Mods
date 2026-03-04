---
name: moddb-release-playwright
description: Automate ModDB release uploads using Playwright browser flow when direct API upload is unavailable.
compatibility: opencode
metadata:
  audience: maintainers
  domain: release-ops
---

## Skill: moddb-release-playwright

## Purpose

Automate Vintage Story ModDB release publishing through browser actions when a direct upload API is not available.

Target site: `https://mods.vintagestory.at`

## Inputs required

- `modId` (numeric mod id on ModDB)
- `zipPath` (absolute path to built mod zip)
- `changelogHtmlOrText`
- `compatibleVersions` (array of semver strings, e.g. `1.21.6`)

## Preconditions

- The zip already exists locally (build/package step completed).
- Operator can complete any interactive auth challenge (account login/2FA) if prompted.

## Workflow

1. Open login page: `https://mods.vintagestory.at/login`.
2. Complete auth flow and wait until logged in.
3. Navigate to release page:
   - `https://mods.vintagestory.at/edit/release/?modid=<modId>`
4. Upload file using file input selector:
   - `input[name="newfile"]`
5. Wait for upload/parse completion:
   - no active upload progress
   - auto-detected mod id/version fields populated
6. Set compatible versions by toggling:
   - `input[name="cgvs[]"]`
7. Set changelog text in:
   - `textarea[name="text"]`
8. Click save button (`Save`), then wait for navigation.
9. Verify success:
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
