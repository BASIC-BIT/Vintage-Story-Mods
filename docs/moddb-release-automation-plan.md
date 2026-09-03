# ModDB Release Automation

Status as of 2026-09-03: the AWS-backed design below is implemented on branch `codex/moddb-aws-auth-plan` in this repository and on a matching `basic-infra` branch. Nothing is merged, no Terraform is applied, no secret is initialized, and the Windows Credential Manager session has not been migrated. Those steps each need their own owner approval (see "Not yet done").

Design: `docs/superpowers/specs/2026-09-03-moddb-aws-auth-design.md`. Research: `docs/research/2026-09-02-moddb-aws-authentication.md`.

## Architecture

### AWS (owned by `basic-infra`, `us-east-2`)

- Secrets Manager `/basic/vintage-story/moddb/account-login`: email and password. Readable only by the renewal role. Value set through the broker's masked prompt, never through Terraform.
- Secrets Manager `/basic/vintage-story/moddb/session`: `vs_websessionkey` plus capture, expiry-estimate, and validation metadata. Renewal writes `AWSPENDING`, validates live, then promotes to `AWSCURRENT` conditionally on the version it originally observed. No lock table.
- IAM `moddb-renewal`: read both secrets, stage and promote session versions.
- IAM `moddb-publisher`: read `AWSCURRENT` of the session secret only. Assumable by local operator identities and by GitHub OIDC restricted to `repo:BASIC-BIT/Vintage-Story-Mods:ref:refs/heads/main`.
- AWS-managed KMS key, no resource policies, no replication.

### Broker (owned by this repository, `tools/moddb-release`)

One pinned Node 22 ESM package. It is the only code that holds credentials in memory. Commands: `account set`, `session status`, `session renew`, `session import-wincred`, `release prepare`, `release publish`. Each prints one non-secret JSON line; exit codes 0 ok, 1 failed, 2 `renewal-required`, 3 `approval-required`. Grammar and statuses are in the implementation plan's "Stable Broker Interface" section and in the skill.

`session renew` is the only place Playwright runs: installed Chrome, headed, non-persistent disposable profile, tracing and capture disabled. The human completes reCAPTCHA; the broker captures the cookie, completes the ModDB login bridge, validates the expected account (`--expected-account <moddb-username>`, the name shown in the ModDB account menu), and promotes it. Renewal is allowed only on Windows with a TTY and outside GitHub Actions.

`session import-wincred` is the one-time migration from `TheBasics.ModDb.Session` in Windows Credential Manager, via a narrow checked-in PowerShell adapter over a private process stream, in two phases (import as `AWSPENDING`, then `--finalize-version`).

### GitHub Actions

`ModDB Release` (`.github/workflows/moddb-release.yml`): `workflow_dispatch` only, `operation=prepare` or `operation=publish`, checks out the broker from protected `main`, assumes `moddb-publisher` through OIDC. Cloud runs cannot renew; an expired session returns `renewal-required` and stops. The separate manual `publish` dispatch is the public gate. `Create Release` (`release.yml`) is unchanged and remains a separate authorization.

### Skill

`.opencode/skills/moddb-release-playwright/SKILL.md` is the canonical agent runbook (nine-step sequence, authorization boundaries, maintainer commands). `.codex/skills/moddb-release-playwright/SKILL.md` is a thin pointer. `scripts/check-agent-tooling.ps1` asserts the skill names every broker command and the confirmation and renewal rules.

## Guarantees

- Password and cookie never enter agent context, arguments, environment, files, logs, workflow output, or artifacts.
- Every public ModDB save requires immediate owner confirmation of the exact staged file ID in the current conversation. A renewal during publish stops publication and requires fresh confirmation.
- The broker verifies parsed mod identity and version, a single staged file, and the public download hash against the GitHub asset before reporting success. Publish re-reads the staged file list immediately before the public save; a concurrent prepare in that window is the operator's responsibility (one release at a time).

## Not yet done

Each item is a separate approval, in order:

1. Merge the `basic-infra` branch and apply Terraform (empty containers and roles only).
2. Merge this repository's branch.
3. `account set` on an approved Windows machine.
4. `session import-wincred` both phases, then verify `session status` from a second approved consumer.
5. Remove the Windows Credential Manager entry.
6. One live `release prepare` against a real artifact, then one owner-confirmed `release publish`.

## Remaining official-API work

The bridge above exists because ModDB has no supported upload API. The durable fix is upstream:

- `anegostudios/vsmoddb` issue `#18` (open; maintainer said a PR would be accepted). The README already stubs `/api/v2/mods/{modid}/releases/new` as `auth` + `at`, unimplemented.
- Implementation sketch: handle `releases/new` in `lib/api/authenticated/mods.php`, reuse `processFileUpload(...)` from `lib/fileupload.php` and `createNewRelease(...)` from `lib/edit-release.php`, accept `multipart/form-data` with `file`, `text`, `cgvs[]`, `at`, return release metadata and URL.
- Repository tracking issue #84 stays open until the upstream endpoint ships or is declined.

When a scoped, revocable upload token exists, retire the password secret, the renewal browser flow, and the session secret; point the publisher role and the skill at the token.
