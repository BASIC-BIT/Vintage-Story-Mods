# AWS-Backed Agent-Driven ModDB Authentication Design

Date: 2026-09-03
Status: Owner-approved design, pending written-spec review

## Context

The BASICs release workflow can stage and publish ModDB releases through authenticated web endpoints, but its current `vs_websessionkey` is held in Windows Credential Manager on one workstation. The session expires, cannot support cloud agents or other machines, and requires a human reCAPTCHA step to renew.

The desired system keeps release judgment and orchestration agent-driven while preventing the account password or session cookie from entering model context, command arguments, environment variables, application-authored files, logs, or workflow output. During renewal, the browser necessarily receives the credentials and may persist the cookie inside its process-owned disposable profile until cleanup.

Research supporting this design is recorded in `docs/research/2026-09-02-moddb-aws-authentication.md`. The current ModDB implementation stores one active session token per user, accepts that bearer token from any machine, and replaces it on a later successful login.

## Goals

- Store the Vintage Story account login and current ModDB session securely in AWS.
- Allow approved Windows machines to perform human-assisted reCAPTCHA renewal.
- Allow local agents and cloud-triggered GitHub Actions to prepare and publish ModDB releases without seeing raw credentials.
- Preserve an explicit, immediate owner confirmation before every public ModDB publication.
- Make AWS the single durable source of truth for the session.
- Keep the number of resources, roles, workflows, and moving parts small.
- Document the canonical sequence in the repository skill that release agents load.

## Non-Goals

- Solving, bypassing, reusing, or storing reCAPTCHA responses.
- Automatically rotating the Vintage Story account password.
- Supporting cross-account AWS access in the first version.
- Adding proactive session-expiry notifications.
- Adding a distributed lock, persistent desktop watcher, always-on Windows runner, or custom consumer registry.
- Exposing general ModDB account automation beyond release preparation, publication, validation, and renewal.
- Replacing the eventual need for an official scoped ModDB upload API.

## Approved Decisions

- AWS Secrets Manager is the durable credential store.
- Infrastructure belongs in `basic-infra`; release behavior belongs in `Vintage-Story-Mods`.
- Resources use the existing AWS account and `us-east-2`.
- Two JSON secrets and exactly two new purpose-specific roles are used.
- IAM roles and policies are the only consumer authorization mechanism.
- The AWS-managed Secrets Manager KMS key is sufficient.
- Renewal uses installed Chrome through Playwright in a visible disposable profile.
- The current Windows Credential Manager session is imported once, validated, and removed from durable Windows storage after successful AWS promotion.
- AWS remains the only durable cookie store after migration.
- Session updates use `AWSPENDING`, live validation, and conditional promotion to `AWSCURRENT`.
- One manually dispatched ModDB workflow supports separate `prepare` and `publish` operations.
- Secret-bearing workflow code always comes from protected `main`.
- No GitHub environment reviewer is added. The explicit second manual `publish` dispatch is the public-action gate.
- Cloud runs that need reCAPTCHA return `renewal-required`; an approved Windows invocation automatically opens renewal.
- If renewal interrupts a requested public publication, publication stops and requires fresh confirmation.
- A thin Node broker performs secret-bearing primitives; the agent remains the release conductor through the repository skill.

## Responsibility Boundaries

### Release agent

The agent:

1. Identifies and verifies the exact GitHub release artifact.
2. Prepares and reviews platform-appropriate release copy.
3. Checks non-secret session status through the broker.
4. Initiates renewal when the broker reports that an interactive Windows renewal is possible.
5. Calls release preparation with exact expected identity, version, compatibility, and artifact hash.
6. Presents the staged file ID, parsed identity, compatibility selection, changelog, and hash to the owner.
7. Obtains immediate confirmation before public publication.
8. Calls publication with the exact staged file ID only after confirmation.
9. Verifies the public ModDB listing and downloaded artifact hash.

The agent never receives the password or cookie.

### Privileged Node broker

The broker owns only operations that require secret access:

- `account set`
- `session import-wincred`
- `session status`
- `session renew`
- `release prepare`
- `release publish`

It returns only non-secret metadata such as status, timestamps, AWS version IDs, ModDB file IDs, artifact hashes, compatible versions, and release URLs.

The broker uses the AWS SDK, Playwright, and the ModDB HTTP client in one pinned Node application. Raw credentials remain inside the broker and browser boundary. Generic AWS CLI secret output is not an application interface.

The one-time Windows Credential Manager migration uses a narrow Windows adapter because Node has no built-in Credential Manager API. The broker launches that checked-in adapter with redirected, non-terminal streams, captures the cookie without forwarding child output, imports it immediately, and discards the in-memory value. This exception avoids a native Node dependency while keeping the value out of arguments, environment variables, terminal output, and files.

### GitHub Actions

One new `ModDB Release` workflow invokes the same broker from trusted `main`. It supports `workflow_dispatch` with `operation=prepare` or `operation=publish`.

The workflow does not replace the existing `Create Release` workflow. GitHub publication and ModDB publication remain separate operations.

## AWS Resources

### Account-login secret

Name:

`/basic/vintage-story/moddb/account-login`

Shape:

```json
{
  "schemaVersion": 1,
  "email": "account email",
  "password": "account password"
}
```

The renewal role can read this secret. Existing administrator access initializes or replaces it through the broker's masked `account set` prompt. Terraform manages only the secret container and metadata, never the value.

### Session secret

Name:

`/basic/vintage-story/moddb/session`

Shape:

```json
{
  "schemaVersion": 1,
  "cookieName": "vs_websessionkey",
  "cookieValue": "bearer value",
  "capturedAt": "ISO-8601 timestamp",
  "observedCookieExpiresAt": "ISO-8601 timestamp or null",
  "modDbValidUntilEstimate": "ISO-8601 timestamp",
  "validatedAt": "ISO-8601 timestamp",
  "validatedAccount": "expected ModDB account identifier"
}
```

Publisher and renewal roles can read `AWSCURRENT`. Only the renewal role can create and promote versions. Consumers use the earlier known expiry deadline and still perform a harmless live authenticated check because upstream can invalidate a token early.

### IAM roles

`moddb-renewal`:

- Read `AWSCURRENT` from both ModDB secrets.
- Read session version metadata.
- Add a new session version as `AWSPENDING`.
- Promote a validated pending version conditionally.
- No IAM, resource-policy, or unrelated secret permissions.

`moddb-publisher`:

- Read only `AWSCURRENT` from the session secret.
- No account-login access.
- No secret-write access.
- Assumable by the approved GitHub OIDC workflow and appropriate local AWS identities according to existing IAM conventions.

There is no separate credential-admin role. Existing administrator access performs bootstrap.

### Region and encryption

Both secrets and roles are managed from `basic-infra` in `us-east-2`, matching newer shared BASIC-owned operator and agent infrastructure. Same-account access uses identity policies and the AWS-managed Secrets Manager KMS key. No resource policy, customer-managed KMS key, or cross-region replication is added.

Expected Secrets Manager storage cost is approximately $0.80 per month plus negligible API request charges.

## Human-Assisted Renewal Flow

1. An approved Windows operator authenticates through the existing AWS CLI or SDK identity flow and assumes the renewal role.
2. The broker reads the current session metadata.
3. If `AWSCURRENT` is still within its known deadline, the broker performs a harmless authenticated ModDB read. A successful expected-account response ends renewal without logging in again.
4. If renewal is required, the broker reads the account-login secret into process memory.
5. The broker creates a process-owned temporary directory and launches installed Chrome through Playwright in a headed, non-persistent context.
6. The broker verifies the exact Vintage Story account origin and TLS before filling email and password.
7. The human completes reCAPTCHA. The broker does not inspect, solve, store, or reuse its response.
8. The broker submits promptly, captures only the resulting `vs_websessionkey` and relevant expiry metadata, and verifies expected origins.
9. The broker creates a unique Secrets Manager version with only `AWSPENDING` attached.
10. The broker completes the ModDB login bridge and performs a harmless authenticated read that proves the expected ModDB account.
11. The broker conditionally moves `AWSCURRENT` from the originally observed version to the validated candidate. AWS moves the former current version to `AWSPREVIOUS`.
12. The broker validates `AWSCURRENT` once more, closes Chrome, removes the temporary profile, clears mutable secret buffers where the runtime permits, and reports non-secret status.

The broker disables tracing, screenshots, video, HAR capture, verbose HTTP logging, and browser-profile persistence throughout authentication.

## Expiry Behavior

The session secret stores both the observed browser-cookie expiry and a conservative ModDB estimate based on the login bridge's current 14-day validity behavior. The earlier known deadline is treated as expired. A live authenticated check remains authoritative.

- Interactive Windows `prepare`: renew automatically, then resume preparation.
- Interactive Windows `publish`: renew automatically, then stop and require fresh publication approval.
- Cloud `prepare` or `publish`: return structured `renewal-required` and stop without mutation beyond harmless validation.
- No proactive warning or scheduled renewal is added.

## Concurrency Without a Lock

No DynamoDB resource or distributed lock is used. Renewal instead relies on Secrets Manager version IDs and conditional stage movement:

1. Each renewer records the original `AWSCURRENT` version ID.
2. Each candidate receives a unique idempotency token and `AWSPENDING` only.
3. Promotion names both the candidate version and the original version from which `AWSCURRENT` must be removed.
4. If another renewal changed `AWSCURRENT`, promotion fails closed.
5. A losing or indeterminate attempt never prints credentials and reports that renewal must be retried.

This does not prevent two humans from briefly racing the external login bridge. The accepted tradeoff is that simultaneous human CAPTCHA renewals are rare. Post-promotion live validation detects an invalid winner, and normal release operations stop rather than attempting an unsafe rollback.

## Initial Migration

1. Deploy the two empty secret containers and IAM roles from `basic-infra` without secret values in Terraform state.
2. Run `account set` on an approved Windows machine and enter the email and password through masked prompts.
3. Run `session import-wincred`; the broker's narrow Windows adapter reads `TheBasics.ModDb.Session` through the Windows API and returns it only over a private redirected process stream.
4. Write the cookie as `AWSPENDING`, validate the expected ModDB account, and promote it to `AWSCURRENT`.
5. Verify another approved consumer can perform a harmless authenticated read using AWS.
6. Remove the durable Windows Credential Manager value only after successful promotion and cross-consumer verification.
7. Use the normal human-assisted renewal flow for all later session replacement.

## Agent-Facing Release Flow

The canonical instructions live in `.opencode/skills/moddb-release-playwright/SKILL.md`; its Codex wrapper remains a thin pointer to that source skill.

### Prepare

1. Agent verifies the GitHub tag, release asset, mod identity, version, compatible Vintage Story version, release copy, size, entry count, and SHA-256.
2. Agent invokes local `release prepare` or dispatches the workflow with `operation=prepare`.
3. Broker requests `AWSCURRENT`, checks expiry, validates authentication, and handles the environment-specific renewal behavior.
4. Broker uploads the exact ZIP, requires ModDB to parse the exact expected mod identifier and version, and verifies there is exactly one staged file.
5. Broker returns the staged file ID and non-secret verification data.

### Publish

1. Agent presents the exact staged file ID, parsed identity, version, intended compatibility, approved changelog, and SHA-256.
2. Agent obtains immediate owner confirmation.
3. Agent invokes local `release publish` or dispatches the same workflow with `operation=publish`, including the expected staged file ID.
4. Broker revalidates the single staged file and session. If renewal occurs, it stops before publication and requires fresh approval.
5. Broker submits the public save, verifies the returned release identity and selected compatibility, downloads the public asset, and compares its hash with GitHub.
6. Agent reports the public ModDB URL and verification result.

## GitHub Workflow Security

- The workflow is manually dispatched only.
- Broker and workflow code are checked out explicitly from protected `main`.
- AWS OIDC trust restricts the subject to `repo:BASIC-BIT/Vintage-Story-Mods:ref:refs/heads/main` and the audience to `sts.amazonaws.com`.
- `prepare` and `publish` are separate dispatches of the same workflow.
- `publish` requires the expected staged file ID and exact release identity inputs.
- Workflow output contains only non-secret release metadata.
- The cookie is not stored in GitHub secrets, outputs, environment variables, artifacts, caches, or summaries.
- Third-party actions remain pinned according to repository policy.
- There is no GitHub environment reviewer. The owner-authorized manual `publish` dispatch is the action-time public gate.

## Secret-Handling Requirements

Password and cookie values must never appear in:

- Agent or chat context
- Command arguments
- Process environment variables
- Repository or application-authored temporary files. The required exception is Chrome's process-owned disposable profile during renewal; it is access-restricted and removed when the browser closes.
- Terraform variables or state
- Logs or exception messages
- HTTP request or response dumps
- Browser screenshots, traces, videos, or HAR files
- GitHub outputs, summaries, artifacts, caches, or comments
- Hashes, prefixes, or identifiers derived from the bearer value

The broker validates exact account and ModDB origins before filling or sending credentials. Network access during renewal is limited to required AWS, Vintage Story account, ModDB, and live reCAPTCHA endpoints. CloudTrail provides AWS API audit metadata without application-authored secret logging.

## Error Handling

- Missing or malformed secret schema: stop with a field-name-only diagnostic.
- Expired metadata or failed live authentication: follow environment-specific renewal behavior.
- Wrong account after login: do not promote the candidate.
- Missing, blank, or mismatched parsed ModDB metadata: do not publish.
- Multiple or changed staged file IDs: do not publish.
- AWS conditional promotion conflict: fail closed and require another renewal attempt.
- Indeterminate ModDB publication response: verify public state before any retry.
- Browser crash or cancellation: close the context, remove its profile on a best-effort retry path, leave `AWSCURRENT` unchanged, and report non-secret status. Profile deletion reduces residual risk but does not promise forensic erasure from the host filesystem.
- Public artifact hash mismatch: report release failure immediately and do not claim success.

## Verification Strategy

### Automated tests

- Secret schema parsing and validation.
- Expiry and live-validation decisions.
- Redaction and forbidden-output checks.
- Pending-version creation and conditional promotion.
- Concurrent-version conflict behavior.
- Windows Credential Manager import without stdout exposure.
- Browser credential filling against a local fake account page.
- Human-pause and cancellation behavior.
- Cookie capture, origin rejection, and temporary-profile cleanup.
- ModDB action-token, upload, staged-file, compatibility, publication, and public-download behavior against fake services.
- Terraform formatting, validation, and least-privilege policy assertions.
- GitHub workflow syntax, trusted-main checkout, manual operations, and OIDC subject assertions.

### Live verification

- Initialize the password secret through the masked bootstrap prompt.
- Import and validate the current Windows session without printing it.
- Validate session use from another approved consumer.
- Prepare a release or safe fixture without public publication.
- Complete one human-assisted renewal when naturally required or during an explicitly approved validation exercise.
- Preserve the existing immediate owner gate for any public release test.

## Repository Delivery

### `basic-infra`

- Terraform for two secret containers and two IAM roles in `us-east-2`.
- GitHub OIDC trust for the publisher role.
- Bootstrap and operator documentation that never contains secret values.

### `Vintage-Story-Mods`

- Pinned Node broker and tests.
- One manual `ModDB Release` workflow with `prepare` and `publish` operations.
- Updated ModDB release skill and automation plan.
- Migration command for the current Windows credential.

Infrastructure lands first. Application code can be tested against fakes before infrastructure exists, but live migration and end-to-end verification occur only after the AWS resources are deployed.

## Retirement

The password and session automation is an interim bridge. When ModDB provides a supported, revocable upload token scoped to The BASICs and release creation, migrate the publisher to that token, remove password access from release infrastructure, delete the session-renewal browser flow, and update the agent skill.
