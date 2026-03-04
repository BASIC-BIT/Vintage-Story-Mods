# ModDB Release Automation Plan

## What we found

- Upstream feature request exists: `https://github.com/anegostudios/vsmoddb/issues/18`
  - Title: `[Feature request] API endpoint for CD to upload new versions of the mod`
  - Status: Open
  - Maintainer comment indicates they would accept a PR.
- In this repo, tracking issue exists: #84 (`Track ModDB release automation (API + Playwright paths)`).

## Recommended strategy

1. **Primary path (best long-term):** contribute upload endpoint support to `anegostudios/vsmoddb`.
2. **Interim path (usable now):** Playwright-based UI automation for release upload.

Run both in parallel if possible: immediate relief via Playwright, durable CI via API.

---

## Upstream API implementation notes

### Existing clues in upstream codebase

- Route/docs stub already exists in README:
  - `/api/v2/mods/{modid}/releases/new` marked `auth` + `at`, currently not implemented
- Authenticated API routing file:
  - `lib/api/authenticated/mods.php`
- Existing release creation logic already available:
  - `lib/edit-release.php` -> `createNewRelease(...)`
- Existing file upload validation/parsing pipeline:
  - `lib/fileupload.php` -> `processFileUpload(...)`

### Suggested endpoint contract

- Method: `PUT /api/v2/mods/{modid}/releases/new`
- Auth: existing session auth + action token (`at`)
- Content type: `multipart/form-data` (recommended over base64 for large zips)
- Required form fields:
  - `file`: release zip
  - `text`: changelog HTML/text
  - `cgvs[]`: compatible game versions (semver strings)
  - `at`: action token

### Suggested implementation steps

1. In `lib/api/authenticated/mods.php`, add handling for `releases/new` under the `mods/{modid}/releases/*` branch.
2. Validate:
   - method
   - action token
   - user ban state
   - permission to edit target mod (`canEditAsset`)
3. Upload/inspect release file using existing `processFileUpload(...)` pipeline to preserve current limits and modinfo parsing.
4. Validate parsed mod identifier/version collision rules and compatible versions (same rules as web form path).
5. Call `createNewRelease(...)`.
6. Return JSON response with release metadata and URL.

### Expected benefit

- Enables first-class CI/CD upload from GitHub Actions without browser automation.

---

## Interim Playwright automation (current website flow)

### Known flow from upstream templates/code

- Login route: `/login` (redirects through account service)
- Add release route: `/edit/release/?modid=<id>`
- Release form:
  - file input: `input[name="newfile"]`
  - changelog: `textarea[name="text"]`
  - compatible versions: `input[name="cgvs[]"]`
  - save buttons trigger JS `submitForm(...)`

### Playwright checklist

1. Navigate to `https://mods.vintagestory.at/login` and complete auth.
2. Open mod release page (`/edit/release/?modid=<id>`).
3. Upload zip via `input[name="newfile"]`.
4. Wait for upload and mod parse to complete (mod id/version fields auto-populate).
5. Set compatible versions (`cgvs[]`) as desired.
6. Fill changelog (`textarea[name="text"]`).
7. Click save and verify success redirect (asset id in URL or release appears in files tab).

### Caveats

- Authentication currently depends on account session flow (cookies/redirect), so some manual handoff may be needed.
- UI selector changes can break automation; keep selectors minimal and data-attribute based where possible.

---

## New workspace bootstrap (for upstream contribution)

Use a separate workspace outside this mod repo.

1. Clone upstream repository:
   - `git clone https://github.com/anegostudios/vsmoddb.git`
2. Follow upstream local setup (`README.md`):
   - add hosts entry for `mods.vintagestory.stage`
   - run docker compose from upstream `docker/`
   - configure `lib/config.php`
3. Create feature branch for API endpoint.
4. Implement endpoint in `lib/api/authenticated/mods.php` using existing release/file helpers.
5. Test endpoint locally with multipart uploads.
6. Open PR against upstream and reference issue `#18`.
