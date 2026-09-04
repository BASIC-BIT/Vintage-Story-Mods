# AWS-Backed ModDB Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give release agents a safe, repeatable way to prepare and publish The BASICs releases on ModDB using an AWS-backed session, while keeping the password and bearer cookie outside model context and preserving immediate owner approval before every public save.

**Architecture:** `basic-infra` owns two Secrets Manager containers and two least-privilege IAM roles in `us-east-2`. `Vintage-Story-Mods` owns one pinned Node broker that retrieves secrets in-process, handles the ModDB HTTP protocol, performs human-assisted Chrome renewal on Windows, and exposes only structured non-secret results. One manual GitHub workflow calls the broker from protected `main`; the repository skill remains the human-facing conductor.

**Tech Stack:** Terraform and AWS provider 5.x, AWS Secrets Manager and IAM, Node.js 22 ESM, `@aws-sdk/client-secrets-manager` 3.1125.0, Playwright 1.62.1, `fflate` 0.8.3, `parse5` 8.0.1, `yaml` 2.9.0, Node's built-in test runner, PowerShell 7, GitHub Actions OIDC.

**Spec:** [`docs/superpowers/specs/2026-09-03-moddb-aws-auth-design.md`](../specs/2026-09-03-moddb-aws-auth-design.md)

## Global Constraints

- Work in isolated worktrees. The Vintage Story implementation branch starts from `codex/moddb-aws-auth-plan`; create a separate `basic-infra` worktree at `D:\bench\basic-infra-wt\moddb-aws-auth` from its protected default branch.
- Do not modify either protected primary checkout.
- Never put the email, password, session cookie, any credential-derived value, or reCAPTCHA data in source, fixtures, arguments, environment variables, Terraform input/state, logs, errors, captures, workflow outputs, artifacts, comments, or agent context.
- Chrome's process-owned disposable profile is the one temporary exception during renewal. Restrict it to the current user, disable capture features, and remove it on every exit path.
- Terraform owns secret containers and metadata only. It must not declare either secret value or any `aws_secretsmanager_secret_version`.
- Keep the approved shape: two secrets, two new roles, one Node package, one workflow, one narrow PowerShell adapter, no DynamoDB lock, no custom KMS key, no consumer registry, and no GitHub environment reviewer.
- Keep `.github/workflows/release.yml` unchanged.
- Secret-bearing GitHub runs use code from `refs/heads/main`. Other refs must be unable to assume the publisher role.
- `prepare` may stage one upload. `publish` requires the exact staged file ID and immediate owner confirmation in the current release conversation.
- If a publish attempt renews the session, stop with `approval-required`; a fresh owner confirmation is required before another publish call.
- Do not apply Terraform, initialize values, migrate/delete WinCred, change GitHub settings, use live login, stage a real file, push, open a PR, merge, or publish without the separate approvals in Task 10.
- Public release copy must not contain U+2014.
- Each command writes exactly one safe JSON result line to stdout. Tests must prove fixture credentials never reach stdout or stderr.

## Stable Broker Interface

```text
node tools/moddb-release/src/cli.mjs account set
node tools/moddb-release/src/cli.mjs session status
node tools/moddb-release/src/cli.mjs session renew --expected-account <moddb-username>
node tools/moddb-release/src/cli.mjs session import-wincred --expected-account <moddb-username>
node tools/moddb-release/src/cli.mjs session import-wincred --finalize-version <aws-version-id>
node tools/moddb-release/src/cli.mjs release prepare --mod-id <number> --expected-mod-identifier <id> --expected-version <semver> --zip <path> --changelog <path> --compatible-version <semver> --expected-sha256 <hex> [--expected-account <moddb-username>]
node tools/moddb-release/src/cli.mjs release publish --mod-id <number> --expected-mod-identifier <id> --expected-version <semver> --zip <path> --changelog <path> --compatible-version <semver> --expected-sha256 <hex> --expected-file-id <number> [--expected-account <moddb-username>]
```

`--compatible-version` is repeatable. `<moddb-username>` is the account name shown in the ModDB account menu, not a numeric ID; release commands default it to the stored session's validated account. The broker detects cloud execution from `GITHUB_ACTIONS=true`; interactive renewal is allowed only on Windows with TTY stdin and outside GitHub Actions. No flag overrides that decision.

```js
export const ExitCode = Object.freeze({
  ok: 0,
  failed: 1,
  renewalRequired: 2,
  approvalRequired: 3,
});

// Success
{ "ok": true, "status": "valid" | "renewed" | "prepared" | "published" | "imported" | "finalized", "data": {} }

// Expected stop
{ "ok": false, "status": "renewal-required" | "approval-required", "reason": "expired" | "authentication-failed" | "renewed-during-publish" }
```

Safe output may contain AWS version IDs, timestamps, ModDB file IDs, public identity/version, filenames, size, ZIP entry count, compatible versions, SHA-256, and public URLs. It must never contain credential fields, raw request/response bodies, or headers.

---

## Task 1: Define the AWS resources in `basic-infra`

**Files:**

- Modify: `D:\bench\basic-infra-wt\moddb-aws-auth\terraform\stacks\shared-secrets\main.tf`
- Modify: `D:\bench\basic-infra-wt\moddb-aws-auth\terraform\stacks\shared-secrets\github-actions.tf`
- Modify: `D:\bench\basic-infra-wt\moddb-aws-auth\terraform\stacks\shared-secrets\outputs.tf`
- Create: `D:\bench\basic-infra-wt\moddb-aws-auth\scripts\Test-ModDbSharedSecretsPlan.ps1`
- Create: `D:\bench\basic-infra-wt\moddb-aws-auth\docs\moddb-release-credentials.md`

### Step 1: Create the infrastructure worktree

- [ ] Fetch the protected default branch without altering `D:\bench\basic-infra`, record its exact commit, and create branch `codex/moddb-aws-auth` at the path above.
- [ ] Read that worktree's `AGENTS.md` and `AGENTS.local.md` before editing.

### Step 2: Write the failing plan-policy test

- [ ] `Test-ModDbSharedSecretsPlan.ps1` accepts `-PlanJsonPath`, parses `terraform show -json`, and uses resource addresses rather than text matching.
- [ ] Require exact secret names, no KMS override, no secret-version resources, exact role names, publisher read access only to the session secret, renewal reads to both secrets and writes only to the session, exact GitHub OIDC audience/subject, and no DynamoDB resource.

```powershell
$plan = Get-Content -LiteralPath $PlanJsonPath -Raw | ConvertFrom-Json -Depth 100
$resources = @($plan.planned_values.root_module.resources)
function Get-PlannedResource([string]$Address) {
    $match = @($resources | Where-Object address -eq $Address)
    if ($match.Count -ne 1) { throw "Expected one resource at $Address, found $($match.Count)." }
    $match[0]
}
$account = Get-PlannedResource "aws_secretsmanager_secret.moddb_account_login"
Assert-Equal $account.values.name "/basic/vintage-story/moddb/account-login"
```

- [ ] Run a backend-disabled plan and the test. Expected: failure because the ModDB resources do not exist.

```powershell
Set-Location D:\bench\basic-infra-wt\moddb-aws-auth\terraform\stacks\shared-secrets
terraform init -backend=false
terraform plan -refresh=false -out moddb-test.tfplan
terraform show -json moddb-test.tfplan | Set-Content moddb-test.tfplan.json -Encoding utf8
..\..\..\scripts\Test-ModDbSharedSecretsPlan.ps1 -PlanJsonPath .\moddb-test.tfplan.json
```

### Step 3: Add two value-less secret containers

- [ ] Add these resources to `main.tf`; do not add `kms_key_id` or a version resource.

```hcl
resource "aws_secretsmanager_secret" "moddb_account_login" {
  name                    = "/basic/vintage-story/moddb/account-login"
  description             = "Vintage Story account login used only for human-assisted ModDB renewal; value managed out of Terraform."
  recovery_window_in_days = 30
  tags = {
    CredentialType = "account-login"
    ManagedBy      = "basic-infra"
    Owner          = "BASIC"
    RotationMode   = "manual-human-assisted"
    Service        = "vintage-story-moddb"
  }
}

resource "aws_secretsmanager_secret" "moddb_session" {
  name                    = "/basic/vintage-story/moddb/session"
  description             = "Current ModDB web session and lifecycle metadata; value managed out of Terraform."
  recovery_window_in_days = 30
  tags = {
    CredentialType = "web-session"
    ManagedBy      = "basic-infra"
    Owner          = "BASIC"
    RotationMode   = "manual-human-assisted"
    Service        = "vintage-story-moddb"
  }
}
```

### Step 4: Add exactly two roles and policies

- [ ] Renewal trust contains same-account `sts:AssumeRole`. Publisher trust contains that statement plus GitHub OIDC with exact `aud` and `sub`.

```hcl
data "aws_iam_policy_document" "moddb_publisher_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]
    principals {
      type        = "AWS"
      identifiers = ["arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"]
    }
  }
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]
    principals {
      type        = "Federated"
      identifiers = [data.aws_iam_openid_connect_provider.github_actions.arn]
    }
    condition {
      test = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values = ["sts.amazonaws.com"]
    }
    condition {
      test = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values = ["repo:BASIC-BIT/Vintage-Story-Mods:ref:refs/heads/main"]
    }
  }
}
```

- [ ] Name roles `basic-vintage-story-moddb-renewal` and `basic-vintage-story-moddb-publisher`, cap sessions at 3600 seconds, and tag them.
- [ ] Publisher actions are only `DescribeSecret` and `GetSecretValue` on `moddb_session.arn`.
- [ ] Renewal has those read actions on both secret ARNs plus `ListSecretVersionIds`, `PutSecretValue`, and `UpdateSecretVersionStage` only on `moddb_session.arn`.

```hcl
data "aws_iam_policy_document" "moddb_publisher_secret" {
  statement {
    actions   = ["secretsmanager:DescribeSecret", "secretsmanager:GetSecretValue"]
    resources = [aws_secretsmanager_secret.moddb_session.arn]
  }
}
```

### Step 5: Add outputs, documentation, and pass checks

- [ ] Output both secret names/ARNs and both role ARNs. These identifiers are not secret.
- [ ] Document the source-of-truth rule, JSON schemas, role boundaries, local assumed-role profiles, masked bootstrap, two-phase WinCred migration, renewal, cost, OIDC trust, and API-token retirement in `docs/moddb-release-credentials.md`.
- [ ] Run `terraform fmt -recursive`, `terraform validate`, a fresh plan, and `Test-ModDbSharedSecretsPlan.ps1`. Expected: two secrets, two roles, their policies, and zero secret values/versions.
- [ ] Inspect `git diff --check` and search the diff for credential values, secret-version resources, DynamoDB, and unexpected principals.
- [ ] Commit:

```powershell
git add terraform/stacks/shared-secrets/main.tf terraform/stacks/shared-secrets/github-actions.tf terraform/stacks/shared-secrets/outputs.tf scripts/Test-ModDbSharedSecretsPlan.ps1 docs/moddb-release-credentials.md
git commit -m "feat: define ModDB release credential infrastructure"
```

---

## Task 2: Scaffold the pinned broker and safe contracts

**Files:**

- Create: `tools/moddb-release/package.json`
- Create: `tools/moddb-release/package-lock.json`
- Create: `tools/moddb-release/src/config.mjs`
- Create: `tools/moddb-release/src/contracts.mjs`
- Create: `tools/moddb-release/src/session-schema.mjs`
- Create: `tools/moddb-release/test/contracts.test.mjs`
- Create: `tools/moddb-release/test/session-schema.test.mjs`

### Step 1: Write failing tests

- [ ] Require the four exit codes, one-line JSON serialization, recursive forbidden-key rejection, strict schema version 1 parsing, field-name-only schema errors, and earlier-known-expiry selection.

```js
test("safeResult rejects credential fields", () => {
  assert.throws(
    () => safeResult("valid", { cookieValue: "fixture-cookie-never-print" }),
    /forbidden result field: cookieValue/,
  );
});

test("effective deadline uses the earlier known expiry", () => {
  const session = validSession({
    observedCookieExpiresAt: "2026-09-10T00:00:00.000Z",
    modDbValidUntilEstimate: "2026-09-17T00:00:00.000Z",
  });
  assert.equal(getEffectiveExpiry(session).toISOString(), "2026-09-10T00:00:00.000Z");
});
```

- [ ] Run the two tests. Expected: module-not-found failures.

### Step 2: Pin the package

- [ ] Create a private Node 22 ESM package with exact versions and commit its lockfile.

```json
{
  "name": "@basic-bit/moddb-release",
  "version": "0.1.0",
  "private": true,
  "type": "module",
  "engines": { "node": ">=22.0.0" },
  "scripts": { "test": "node --test --test-concurrency=1 test/*.test.mjs" },
  "dependencies": {
    "@aws-sdk/client-secrets-manager": "3.1125.0",
    "fflate": "0.8.3",
    "parse5": "8.0.1",
    "playwright": "1.62.1"
  },
  "devDependencies": { "yaml": "2.9.0" }
}
```

- [ ] Run `npm install --package-lock-only --ignore-scripts` in `tools/moddb-release`.

### Step 3: Implement constants, envelopes, and schemas

- [ ] `config.mjs` exports exact non-secret constants:

```js
export const AWS_REGION = "us-east-2";
export const ACCOUNT_SECRET_ID = "/basic/vintage-story/moddb/account-login";
export const SESSION_SECRET_ID = "/basic/vintage-story/moddb/session";
export const SESSION_COOKIE_NAME = "vs_websessionkey";
export const WINCRED_TARGET = "TheBasics.ModDb.Session";
export const ACCOUNT_ORIGIN = "https://account.vintagestory.at";
export const MODDB_ORIGIN = "https://mods.vintagestory.at";
export const MODDB_SESSION_DAYS = 14;
```

- [ ] `contracts.mjs` exports `ExitCode`, `safeResult`, `safeFailure`, `writeResult`, and `classifyError`. Serialize explicit safe fields only; never serialize arbitrary errors or SDK/HTTP objects.
- [ ] `session-schema.mjs` exports `parseAccountLogin`, `parseSession`, `getEffectiveExpiry`, `isExpired`, and `buildSessionCandidate`. The candidate includes cookie name/value, captured/validated times, nullable observed expiry, exact 14-day ModDB estimate, and validated account.
- [ ] Run `npm ci --ignore-scripts` and `npm test`. Expected: all tests pass without downloading a browser.
- [ ] Commit `tools/moddb-release` as `feat: scaffold safe ModDB release broker`.

---

## Task 3: Add conflict-safe Secrets Manager access

**Files:**

- Create: `tools/moddb-release/src/secret-store.mjs`
- Create: `tools/moddb-release/test/secret-store.test.mjs`
- Create: `tools/moddb-release/test/support/fake-secrets-manager.mjs`

### Step 1: Write failing command-recording tests

- [ ] Cover account/session `AWSCURRENT` reads, returned VersionId separation, `AWSPENDING` writes with caller UUID, identical idempotent retry, conditional promotion, first bootstrap, safe conflict mapping, and capability separation between existing administrator, renewal, and publisher credentials. This creates three in-process capability objects, not a third IAM role.

```js
test("promotion compares against observed current", async () => {
  const client = new FakeSecretsManagerClient();
  const store = createRenewalStore(client);
  await store.promoteSession({ candidateVersionId: "candidate-v2", originalCurrentVersionId: "current-v1" });
  assert.deepEqual(client.lastInput("UpdateSecretVersionStageCommand"), {
    SecretId: SESSION_SECRET_ID,
    VersionStage: "AWSCURRENT",
    MoveToVersionId: "candidate-v2",
    RemoveFromVersionId: "current-v1",
  });
});
```

- [ ] Run the test. Expected: module-not-found failure.

### Step 2: Implement capability-shaped stores

- [ ] `createAccountAdminStore(client)` exposes only `putAccountLogin()` and is used with the existing administrator identity.
- [ ] `createPublisherStore(client)` exposes only `readCurrentSession()`.
- [ ] `createRenewalStore(client)` exposes `readAccountLogin()`, `readCurrentSession()`, `putPendingSession()`, and `promoteSession()`.
- [ ] Pending writes use `VersionStages: ["AWSPENDING"]`; promotion names both candidate and originally observed current version. Omit removal only on first bootstrap.
- [ ] Parse `SecretString` immediately. Reject binary, missing, or malformed data with safe typed errors. Never attach AWS command input/output, metadata, secret JSON, or underlying messages.
- [ ] Run `npm test` and commit as `feat: add conflict-safe ModDB session storage`.

---

## Task 4: Port artifact inspection and the exact ModDB protocol

**Files:**

- Create: `tools/moddb-release/src/artifact.mjs`
- Create: `tools/moddb-release/src/moddb-client.mjs`
- Create: `tools/moddb-release/test/artifact.test.mjs`
- Create: `tools/moddb-release/test/moddb-client.test.mjs`
- Create: `tools/moddb-release/test/support/fake-moddb.mjs`

### Step 1: Write failing artifact tests

- [ ] Test exactly one root `modinfo.json`, rejection of absent/duplicate/nested-only metadata, nonblank `modid`/`version`, SHA-256, byte size, entry count, expected-hash mismatch, and a 256 MiB compressed-size limit before archive parsing.
- [ ] Generate ZIP fixtures in memory with `fflate`; do not commit release ZIPs.

```js
test("reads exact identity and evidence", async () => {
  const zip = zipSync({
    "modinfo.json": strToU8('{"modid":"thebasics","version":"5.9.1"}'),
  });
  const file = await writeFixture(zip);
  const evidence = await inspectArtifact(file);
  assert.deepEqual(
    [evidence.modIdentifier, evidence.version, evidence.entryCount],
    ["thebasics", "5.9.1", 1],
  );
});
```

### Step 2: Write failing fake-ModDB tests

- [ ] Use a local HTTP fake and real `fetch` calls to cover:
  - cookie transmission only to the configured ModDB origin;
  - manual redirects and cross-origin rejection;
  - unauthenticated release form detection;
  - HTML-decoded hidden `at` token;
  - multipart upload fields `upload=1`, `assettypeid=2`, `assetid=0`, numeric `modId`, and one ZIP file to `/edit-uploadfile`;
  - exact `status=ok`, `modparse=ok`, mod identifier/version, and positive file ID;
  - prepare blocked by any preexisting staged upload;
  - post-upload proof of exactly one staged file matching the returned file ID;
  - publish proof of the owner-approved staged file ID;
  - repeated `cgvs[]` fields and nonempty compatibility;
  - 302/303 same-origin `assetid=<number>` success redirect;
  - public identity, compatibility, and downloaded SHA-256 verification;
  - indeterminate save checked against public state before retry.

```js
const fields = new URLSearchParams();
fields.append("at", actionToken);
fields.append("save", "1");
fields.append("assetid", "0");
fields.append("modid", String(modId));
fields.append("numsaved", "0");
fields.append("saveandback", "0");
fields.append("text", changelogHtml);
for (const version of compatibleVersions) fields.append("cgvs[]", version);
```

- [ ] Run both new test files. Expected: module-not-found failures.

### Step 3: Implement artifact evidence

- [ ] `inspectArtifact(zipPath, expected)` resolves the literal path, enforces size before reading, computes hash/size, parses with `fflate`, requires one root `modinfo.json`, and compares expected identity/version/hash before network access.
- [ ] Return internal `{ fileName, zipPath, modIdentifier, version, sha256, byteSize, entryCount }`; strip `zipPath` from public results.

### Step 4: Implement the HTTP client

- [ ] `createModDbClient({ origin, cookieValue, fetchImpl })` keeps the cookie in closure scope and exposes:

```js
{
  validateAccount(expectedAccount),
  prepareRelease({ modId, artifact, expectedModIdentifier, expectedVersion }),
  publishRelease({ modId, artifact, expectedModIdentifier, expectedVersion, expectedFileId, changelogHtml, compatibleVersions }),
  verifyPublishedArtifact({ releaseUrl, expectedModIdentifier, expectedVersion, expectedSha256, compatibleVersions }),
}
```

- [ ] Parse HTML with `parse5`, centralize exact-origin URL resolution, and use `redirect: "manual"` for authenticated requests.
- [ ] Never log headers or bodies. Errors may include status codes, expected public identity, and same-origin URLs only.
- [ ] Keep prepare and publish separate. Prepare only uploads/stages; publish revalidates exact staged state before public save.
- [ ] Run `npm test` and commit as `feat: add verified ModDB release protocol`.

---

## Task 5: Add the narrow Windows Credential Manager migration

**Files:**

- Create: `tools/moddb-release/scripts/wincred-session.ps1`
- Create: `tools/moddb-release/src/wincred.mjs`
- Create: `tools/moddb-release/test/wincred.test.mjs`

### Step 1: Write failing Windows-only tests

- [ ] Skip on non-Windows. Write a unique fixture through WinCred, then prove read capture uses redirected pipes without parent output, delete targets exactly one credential, absent delete is idempotent, and child failure returns a fixed safe error.
- [ ] Capture stdout/stderr while using fixture values `fixture-password-never-print` and `fixture-cookie-never-print`; assert neither appears in output, exceptions, or temporary files.

### Step 2: Implement the PowerShell adapter

- [ ] Accept only `-Operation Read|Delete` and `-Target`. Use `CredReadW`, `CredDeleteW`, and `CredFree`; write no host/progress output.
- [ ] For read, emit only UTF-8 credential bytes on stdout. Clear copied buffers and free native memory in `finally`.
- [ ] For delete, emit no stdout and treat Win32 error 1168 as success.

### Step 3: Implement the Node parent boundary

- [ ] Spawn without a shell, with hidden window and `stdio: ["ignore", "pipe", "pipe"]`:

```js
spawn("pwsh.exe", [
  "-NoLogo", "-NoProfile", "-NonInteractive", "-File", adapterPath,
  "-Operation", operation, "-Target", WINCRED_TARGET,
], { shell: false, windowsHide: true, stdio: ["ignore", "pipe", "pipe"] });
```

- [ ] Never forward child streams. Enforce a 10-second timeout and 4 KiB stdout cap. Convert failures to fixed `WINCRED_READ_FAILED` or `WINCRED_DELETE_FAILED` errors.
- [ ] The target is the only credential-related argument. The value remains in the private redirected stream and process memory.
- [ ] Run `npm test` on Windows and commit as `feat: add one-time WinCred session migration`.

---

## Task 6: Implement human-assisted Chrome renewal

**Files:**

- Create: `tools/moddb-release/src/browser-renewal.mjs`
- Create: `tools/moddb-release/src/session-service.mjs`
- Create: `tools/moddb-release/test/browser-renewal.test.mjs`
- Create: `tools/moddb-release/test/session-service.test.mjs`
- Create: `tools/moddb-release/test/support/fake-account-server.mjs`

### Step 1: Write failing decision tests

- [ ] Cover these exact branches:
  - unexpired metadata plus successful live check returns `valid` without reading the account secret;
  - expired metadata or early auth failure in cloud returns `renewal-required` without password read/browser launch;
  - the same states on interactive Windows enter renewal;
  - wrong account, cancellation, timeout, browser crash, origin mismatch, bridge failure, candidate-validation failure, or promotion conflict leaves `AWSCURRENT` unchanged;
  - success writes `AWSPENDING`, validates, promotes conditionally, rereads `AWSCURRENT`, and validates again;
  - a publish caller gets `approval-required` after renewal and never resumes publication.

```js
test("cloud expiry never reads login", async () => {
  const secrets = fakeRenewalStore({ expired: true });
  const result = await ensureSession({
    runtime: { interactiveWindows: false },
    purpose: "prepare",
    secrets,
  });
  assert.equal(result.status, "renewal-required");
  assert.equal(secrets.calls.readAccountLogin, 0);
});
```

### Step 2: Write the local Playwright test

- [ ] The fake account server supplies login fields, a human-completion control, submit, and a same-origin `vs_websessionkey` cookie. Test origins are injected into the module; the production CLI has no origin override.
- [ ] Run `npx playwright install chromium` for this test only.
- [ ] Prove credentials are filled only after expected-origin verification, the driver waits for human completion, captures only the named cookie, rejects unexpected origins, and removes its disposable profile after success/cancellation.
- [ ] Assert tracing, screenshots, video, HAR, downloads, and persistent production profile paths are absent.

### Step 3: Implement the browser boundary

- [ ] `renewInBrowser({ accountLogin, expectedAccount, browserConfig, onHumanActionRequired })` creates a random process-owned temp directory, launches installed Chrome headed, visits the exact account login URL, verifies origin before fill, waits up to 10 minutes for human reCAPTCHA completion, captures the named cookie/expiry, and removes the exact resolved temp directory in `finally`.

```js
const context = await chromium.launchPersistentContext(profileDir, {
  channel: "chrome",
  headless: false,
  acceptDownloads: false,
});
```

- [ ] Allow only required account, ModDB, and live reCAPTCHA origins while credentials are in memory. Never return browser objects, page content, cookie objects, or account-service errors.

### Step 4: Implement session orchestration

- [ ] `ensureSession({ purpose, expectedAccount, runtime, renewalStore, publisherStore, browserRenewal, modDbFactory, clock, uuid })` implements the approved environment matrix.
- [ ] Keep the raw cookie in an internal symbol-keyed result that `safeResult` rejects; only the in-process ModDB client receives it.
- [ ] Renewal order is: observe current version, login/capture, write candidate pending, bridge/validate candidate, conditional promote, reread current, validate again.
- [ ] Do not roll back to `AWSPREVIOUS` after a post-promotion failure because upstream supports only one live token.
- [ ] Run all tests, including bundled Chromium and installed Chrome locally when present. Commit as `feat: add human-assisted ModDB session renewal`.

---

## Task 7: Assemble the agent-facing CLI and two-phase migration

**Files:**

- Create: `tools/moddb-release/src/args.mjs`
- Create: `tools/moddb-release/src/commands.mjs`
- Create: `tools/moddb-release/src/cli.mjs`
- Create: `tools/moddb-release/test/args.test.mjs`
- Create: `tools/moddb-release/test/commands.test.mjs`
- Create: `tools/moddb-release/test/cli-output.test.mjs`

### Step 1: Write failing CLI tests

- [ ] Test exact command grammar, repeated compatibility flags, positive IDs, 64-hex SHA-256, literal paths, nonblank/no-U+2014 changelog, and unknown-option rejection.
- [ ] Assert no `--password`, `--cookie`, `--session`, `--secret-string`, `--account-origin`, `--moddb-origin`, or `--interactive` option exists.
- [ ] Cover command behavior:
  - `account set` requires TTY, reads email/password with no echo, and writes through the account-admin store under the existing administrator identity;
  - `session status` never renews;
  - `session renew` is interactive-Windows-only;
  - initial `session import-wincred` imports/validates and retains WinCred while returning the promoted version ID;
  - `session import-wincred --finalize-version <id>` requires that exact live-valid `AWSCURRENT`, then deletes WinCred;
  - prepare inspects artifact before acquiring session and returns exact stage evidence;
  - publish rechecks artifact, session, file ID, changelog, and compatibility;
  - renewal during publish exits 3 and never calls the save.
- [ ] Spawn the CLI with fakes and prove fixture credentials are absent from stdout, stderr, serialized errors, and temporary files.

### Step 2: Implement strict arguments and masked prompts

- [ ] Use a small explicit parser with per-command option allowlists and duplicate checks.
- [ ] `readMaskedLine` uses raw TTY mode, renders only prompt/newline, restores terminal state in `finally`, and clears mutable buffers where practical. `account set` asks email, password, and confirmation; mismatch stops before AWS access.

### Step 3: Compose commands

- [ ] Construct `SecretsManagerClient({ region: AWS_REGION })` from the default AWS SDK credential chain. Local AWS config assumes the appropriate role; GitHub supplies short-lived OIDC credentials.
- [ ] Read changelog from a literal path and validate it before network access.
- [ ] Build public results field by field rather than spreading internal objects.
- [ ] Treat broker invocation as a technical precondition, not owner approval. The skill/workflow owns the immediate public-action gate.
- [ ] Run `npm test` and `node tools/moddb-release/src/cli.mjs --help`; assert the chosen help contract and no stack trace. Commit as `feat: expose agent-safe ModDB release commands`.

---

## Task 8: Add the trusted-main manual workflow

**Files:**

- Create: `.github/workflows/moddb-release.yml`
- Create: `tools/moddb-release/test/workflow.test.mjs`

### Step 1: Write the failing workflow security test

- [ ] Parse YAML with `yaml` and require:
  - only `workflow_dispatch`;
  - required choice `operation` containing only `prepare` and `publish`;
  - exact public inputs for tag, asset name, SHA-256, ModDB numeric ID, mod identifier, version, compatibility JSON, release notes, expected account, and optional/conditional file ID;
  - only `contents: read` and `id-token: write` job permissions;
  - job guard `github.ref == 'refs/heads/main'`;
  - protected-default-branch checkout with credentials not persisted;
  - pinned AWS action `ec61189d14ec14c8efccab744f656cffd0e33f37` assuming `arn:aws:iam::079358094174:role/basic-vintage-story-moddb-publisher` in `us-east-2`;
  - `npm ci --ignore-scripts` in the broker directory;
  - no environment, schedule, PR trigger, secret copy, cache, browser install, artifact upload, or continue-on-error;
  - publish file-ID validation before AWS credentials;
  - no broker output copied to GitHub outputs, env, summaries, or artifacts.
- [ ] Run the test. Expected: failure because the workflow is absent.

### Step 2: Implement exact inputs and trusted execution

- [ ] Inputs:
  - `operation`: `prepare|publish` choice;
  - `release_tag` and `asset_name`: exact existing GitHub release asset;
  - `expected_sha256`, `mod_id`, `expected_mod_identifier`, `expected_version`, `expected_account`;
  - `compatible_versions`: required JSON array;
  - `release_notes`: required multiline text;
  - `expected_file_id`: blank for prepare, required positive integer for publish.
- [ ] Guard the sole Ubuntu job to main. Checkout the repository default branch explicitly, disable persisted credentials, set up Node 22, and install with `npm ci --ignore-scripts`.
- [ ] Validate public inputs and download the exact release asset with `gh release download` before requesting AWS credentials.
- [ ] Use the pinned AWS action, exact role ARN, 15-minute session, and no static AWS secrets.
- [ ] Convert compatibility JSON into repeated arguments in PowerShell without `Invoke-Expression`. Write only public changelog text to runner temp.
- [ ] Let the broker's one safe JSON line remain in the job log; do not propagate it elsewhere.
- [ ] Run `npm test` and commit as `ci: add manual ModDB release workflow`.

---

## Task 9: Make the repository skill the canonical conductor

**Files:**

- Modify: `.opencode/skills/moddb-release-playwright/SKILL.md`
- Modify: `.codex/skills/moddb-release-playwright/SKILL.md`
- Modify: `docs/moddb-release-automation-plan.md`
- Modify: `docs/agentic/codex.md` only if its one-line listing changes
- Modify: `scripts/check-agent-tooling.ps1`

### Step 1: Extend the tooling check first

- [ ] Require the source skill to name all six broker commands, point to `tools/moddb-release/src/cli.mjs`, call AWS canonical, and include `immediate owner confirmation` plus `renewed-during-publish`.
- [ ] Require the thin Codex wrapper to point to the source skill without duplicating its runbook.
- [ ] Run `./scripts/check-agent-tooling.ps1`. Expected: failure against the old browser-first skill.

### Step 2: Rewrite the source skill

- [ ] Preserve `references/public-release-notes.md` and define this sequence:
  1. Verify exact GitHub tag/asset, ZIP identity, hash, size, entry count, compatible versions, and owner-approved copy.
  2. Call `session status` without requesting raw credentials.
  3. On an approved Windows run, allow the broker to open visible Chrome and ask the user only to complete reCAPTCHA.
  4. In cloud, report `renewal-required` and stop.
  5. Run/dispatch `release prepare` and present exact staged evidence.
  6. Obtain immediate owner confirmation.
  7. Run/dispatch `release publish` with the exact file ID.
  8. If status is `renewed-during-publish`, stop and obtain fresh confirmation.
  9. Verify public page and downloaded hash before success.
- [ ] Add maintainer-only sections for masked `account set`, two-phase WinCred import/finalize, and ordinary renewal. State that agents never ask for a password or cookie in chat.
- [ ] Keep GitHub release creation, ModDB preparation, public save, infrastructure apply, and credential migration as distinct authorization boundaries.
- [ ] Keep upstream API issue 18 as the retirement path.

### Step 3: Update the wrapper and existing automation document

- [ ] Change only wrapper description/translation notes needed to mention the AWS-backed broker and Playwright renewal.
- [ ] Rewrite `docs/moddb-release-automation-plan.md` as the current architecture and remaining official-API work. Remove manual cookie-paste and general browser-automation instructions.

### Step 4: Validate and commit

- [ ] Run:

```powershell
.\scripts\check-agent-tooling.ps1
Set-Location tools/moddb-release
npm test
Set-Location ..\..
git diff --check
```

- [ ] Expected: tooling and broker tests pass; no public-release example contains U+2014.
- [ ] Commit the actually changed files as `docs: teach agents the AWS-backed ModDB release flow`. Do not touch `docs/agentic/codex.md` if its listing is already correct.

---

## Task 10: Integrate, deploy, migrate, and prove behind explicit gates

**Files:** Review all files from Tasks 1 through 9. Create no credential-value files.

### Step 1: Finish local verification

- [ ] Vintage worktree: run `npm ci --ignore-scripts`, `npm test`, `./scripts/check-agent-tooling.ps1`, `git diff --check`, and a credential-pattern scan over the branch diff.
- [ ] Infrastructure worktree: run `terraform fmt -check -recursive`, `terraform validate`, a fresh remote-backed plan, `Test-ModDbSharedSecretsPlan.ps1`, `git diff --check`, and the same scan.
- [ ] Inspect both full diffs. Confirm `.github/workflows/release.yml` is byte-for-byte unchanged.
- [ ] Confirm both branches still start from the intended default-branch heads and record exact commits.

### Step 2: Request contributor-facing approval

- [ ] Present branch summaries, tests, Terraform resource counts, policy boundaries, limitations, and landing order.
- [ ] Obtain explicit owner approval before either push or PR.
- [ ] Land infrastructure first, then Vintage code. Obtain separate owner approval before each merge and satisfy repository merge-ready checks.

### Step 3: Request infrastructure-apply approval

- [ ] After the infrastructure PR merges, generate a fresh plan from merged `main` and present the exact two secrets, two roles, policies, and zero version resources.
- [ ] Obtain explicit owner approval before `terraform apply`.
- [ ] After apply, verify names, trust, and effective permissions using read-only AWS calls. Empty containers do not mean credentials are initialized.

### Step 4: Bootstrap the login secret

- [ ] Obtain explicit owner approval to initialize the account-login value.
- [ ] On approved Windows, authenticate an administrator AWS profile and run `account set`. The user enters email/password only into the no-echo terminal prompt.
- [ ] Verify only AWS version metadata and safe broker status. Never retrieve/print the value.

### Step 5: Migrate and remove WinCred

- [ ] Obtain explicit owner approval to migrate `TheBasics.ModDb.Session`.
- [ ] Through the renewal-role profile, run `session import-wincred --expected-account <owner-provided-moddb-username>` and record only promoted AWS version ID/status.
- [ ] Through an independently configured publisher-role profile or trusted-main workflow, prove `AWSCURRENT` authenticates as the expected account.
- [ ] Through renewal role, run `session import-wincred --finalize-version <recorded-version-id>`. It must revalidate exact AWS current state before deletion.
- [ ] Prove WinCred is absent and AWS-backed status remains valid. Report that the Windows deletion is not recoverable and AWS is now canonical.

### Step 6: Prove prepare without public save

- [ ] Obtain explicit owner approval before staging a real upload.
- [ ] Select one exact existing GitHub release artifact and approved changelog; independently verify identity, hash, size, and entries.
- [ ] Run local prepare or workflow operation `prepare` from main.
- [ ] Verify exactly one staged file with matching identity/version/file ID/hash. Do not save/publish.

### Step 7: Prove renewal when naturally required

- [ ] Do not force expiry or invalidate a working session for convenience.
- [ ] When renewal is naturally required, run an approved Windows command. Confirm installed Chrome opens visibly, the user alone completes reCAPTCHA, expected account validates, AWS current advances, and the disposable profile is removed.
- [ ] If this occurs during publish, prove status is `approval-required` and no public save occurred.

### Step 8: Preserve the actual public-action gate

- [ ] For a future release, present exact staged file ID, identity/version, compatibility, approved changelog, and SHA-256.
- [ ] Obtain immediate explicit owner confirmation in that release conversation.
- [ ] Publish once using the exact file ID. For an indeterminate response, verify public state before retry.
- [ ] Verify the public page and byte-for-byte download hash before claiming release completion.

### Step 9: Final implementation self-review

- [ ] Spec coverage: map every approved design decision and verification bullet to code, test, policy, workflow, or skill text.
- [ ] Unfinished-work scan: search changed implementation files for standard task markers, placeholder prose, `your-account`, and `example-role`; resolve every hit.
- [ ] Interface consistency: compare command grammar, exit codes, statuses, schemas, role/secret names, workflow inputs, and skill examples across repositories.
- [ ] Security: confirm no generic secret-read command, raw-secret result, secret environment variable, application-authored credential file, automatic post-renewal publish, or non-main GitHub trust.
- [ ] Simplicity: confirm two secrets, two roles, one Node package, one workflow, one adapter, no lock table, no environment reviewer, and no custom authorization registry.

## Completion Evidence

- [ ] Both isolated branches have clean, reviewable commits.
- [ ] Terraform formatting, validation, focused policy assertions, and a fresh plan pass.
- [ ] Full Node tests pass on Windows, including WinCred and fake-service Playwright tests.
- [ ] Workflow security tests and agent-tooling checks pass.
- [ ] Credential scans find no values or derived identifiers.
- [ ] Both PRs separately satisfy review and merge requirements.
- [ ] After separately approved deployment/migration, AWS `AWSCURRENT` validates from renewal and publisher paths and WinCred is absent.
- [ ] Prepare proves exact staged identity without public save.
- [ ] A public release is claimed only after immediate owner confirmation, exact-file-ID save, public-page verification, and matching download SHA-256.
