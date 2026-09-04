---
name: moddb-release-playwright
description: Conduct Vintage Story ModDB releases through the AWS-backed broker (session status, prepare, owner-confirmed publish, human-assisted Playwright renewal) and draft owner-reviewed public release copy.
compatibility: opencode
metadata:
  audience: maintainers
  domain: release-ops
---

## Skill: moddb-release-playwright

## Purpose

Conduct a ModDB release for The BASICs. The agent is the release conductor; the broker at `tools/moddb-release/src/cli.mjs` is the only code that touches credentials. AWS Secrets Manager is the canonical session store; nothing else durable holds the cookie.

Target site: `https://mods.vintagestory.at`

For GitHub or ModDB release-note drafting, review, or platform conversion, read [references/public-release-notes.md](references/public-release-notes.md) before writing copy.

## Boundaries

- Never ask for, accept, echo, or store a password, cookie, or reCAPTCHA response in chat, arguments, environment variables, files, or logs. The broker reads secrets in-process and prints only non-secret JSON.
- Each of these is a separate authorization and none implies another:
  - GitHub release creation (`Create Release` workflow, unchanged)
  - ModDB `release prepare` (stages one upload, nothing public)
  - ModDB `release publish` (public save; requires immediate owner confirmation in the current conversation)
  - Infrastructure apply in `basic-infra` (secret containers, IAM roles)
  - Credential migration (`account set`, `session import-wincred`)
- Playwright appears only inside `session renew` on an approved Windows machine, in a visible disposable Chrome profile. Do not script the ModDB website from this skill.

## Broker

```text
node tools/moddb-release/src/cli.mjs session status
node tools/moddb-release/src/cli.mjs release prepare --mod-id <number> --expected-mod-identifier <id> --expected-version <semver> --zip <path> --changelog <path> --compatible-version <semver> --expected-sha256 <hex> [--expected-account <moddb-username>]
node tools/moddb-release/src/cli.mjs release publish --mod-id <number> --expected-mod-identifier <id> --expected-version <semver> --zip <path> --changelog <path> --compatible-version <semver> --expected-sha256 <hex> --expected-file-id <number> [--expected-account <moddb-username>]
```

`--compatible-version` is repeatable. `<moddb-username>` is the account name shown in the ModDB account menu (not a numeric ID); release commands default it to the account the stored session was validated for. Run from the repository root with an AWS identity that can assume the publisher role (local) or renewal role (Windows renewal).

One JSON line on stdout per command:

| Exit | `ok` | `status` | Meaning |
| --- | --- | --- | --- |
| 0 | true | `valid`, `renewed`, `prepared`, `published`, `imported`, `finalized` | Proceed |
| 1 | false | (error) | Failed; read the field-name-only diagnostic, do not retry blindly |
| 2 | false | `renewal-required` | Session expired or failed live auth and this environment cannot renew (`reason`: `expired` or `authentication-failed`) |
| 3 | false | `approval-required` | `reason: renewed-during-publish`; publication did not happen |

The broker decides renewal eligibility itself (Windows, TTY stdin, not `GITHUB_ACTIONS`). No flag overrides it.

## Release sequence

1. Verify the exact GitHub tag and release asset: mod identifier, version, SHA-256, size, ZIP entry count, compatible Vintage Story versions, and owner-approved changelog copy (per the reference above; zero U+2014, zero `[AGENT]`).
2. Run `session status`. Do not request raw credentials.
3. If the broker reports renewal is possible (approved Windows run), let it open visible Chrome with the login form already filled. Ask the user only to complete reCAPTCHA and submit the form; the broker never clicks the login button. Do not read, describe, or capture the browser session.
4. In cloud (`GITHUB_ACTIONS`), a `renewal-required` result means stop and tell the owner a Windows renewal is needed.
5. Run or dispatch `release prepare`. Present the exact staged evidence: staged file ID, parsed identity and version, compatibility selection, changelog, SHA-256.
6. Obtain immediate owner confirmation for the public save. Confirmation is per staged file ID; a new prepare needs a new confirmation.
7. Run or dispatch `release publish` with `--expected-file-id` set to the confirmed staged file ID. Publish re-reads the staged file list immediately before the public save and refuses anything but that one file; a concurrent prepare in that window is the operator's responsibility, so run one release at a time.
8. If the result is `approval-required` with `renewed-during-publish`, stop. Re-present the staged evidence and obtain fresh confirmation before calling publish again.
9. Verify the public ModDB page and the downloaded artifact hash against GitHub before reporting success.

## Cloud path

The `ModDB Release` workflow (`.github/workflows/moddb-release.yml`, manual dispatch only, code from protected `main`) runs the same broker with `operation=prepare` or `operation=publish`. `prepare` and `publish` are separate dispatches; `publish` takes the expected staged file ID.

```powershell
gh workflow run "ModDB Release" --ref main -f operation=prepare <release identity inputs>
gh workflow run "ModDB Release" --ref main -f operation=publish <release identity inputs> <expected staged file id>
```

Cloud never renews. On an expired session it returns `renewal-required` and stops; complete a Windows renewal, then dispatch again. The owner-authorized `publish` dispatch is the public gate; there is no GitHub environment reviewer.

## Maintainer only: credentials

These are not part of a release conversation. Run them on an approved Windows machine with the renewal role.

### Set the account login

```text
node tools/moddb-release/src/cli.mjs account set
```

Masked prompts for email and password. Nothing is passed as an argument.

### One-time Windows Credential Manager import

```text
node tools/moddb-release/src/cli.mjs session import-wincred --expected-account <moddb-username>
node tools/moddb-release/src/cli.mjs session import-wincred --finalize-version <aws-version-id>
```

Phase one reads `TheBasics.ModDb.Session` through the checked-in Windows adapter, validates the account live, writes it as `AWSPENDING` only once validated, promotes it conditionally to `AWSCURRENT`, and reports `imported` with the promoted AWS version ID. The Windows Credential Manager entry stays in place. Phase two (`--finalize-version`) requires that exact version to still be the live-valid `AWSCURRENT`, then deletes the Windows Credential Manager entry and reports `finalized`. Run phase two only after another approved consumer has run `session status` successfully against AWS. The Windows deletion is not recoverable; AWS is canonical from then on.

### Ordinary renewal

```text
node tools/moddb-release/src/cli.mjs session renew --expected-account <moddb-username>
```

Opens visible Chrome; the human completes reCAPTCHA; the broker captures the cookie, validates it live, stages it as `AWSPENDING` only once validated, and promotes it conditionally. A candidate that fails validation is never written to AWS (the first version of an empty secret would become `AWSCURRENT` regardless of stage). A promotion conflict fails closed; run it again.

## Output

Report, from broker JSON only:

- staged file ID and public release URL
- parsed identity, version, compatible versions
- SHA-256 match between GitHub asset and public ModDB download
- final status and any non-secret diagnostic

## Retirement

This password-and-session bridge is interim. When `anegostudios/vsmoddb` issue `#18` ships a scoped, revocable upload token, move the publisher to it, drop the password secret and `session renew`, and update this skill.
