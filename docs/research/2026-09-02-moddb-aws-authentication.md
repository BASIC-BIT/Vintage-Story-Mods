# AWS-Backed ModDB Authentication

Date: 2026-09-02

## Conclusion

Use AWS Secrets Manager as the durable source of truth, with two secrets and three distinct access roles. Store the Vintage Story account email and password in one secret that only a human-assisted renewal role can read. Store `vs_websessionkey` and non-sensitive lifecycle metadata in a second secret that release publishers can read and the renewal role can replace.

Do not give an arbitrary cloud agent direct access to either secret. For Codex cloud, the practical secure path is a protected GitHub Actions workflow that assumes a narrow AWS role through OIDC, reads only the current session secret in-process, and performs a specific prepared release operation after the existing owner approval gate. OpenAI documents that encrypted Codex cloud environment secrets are available only to setup scripts and are removed before the agent phase, while ordinary environment variables remain available for the whole task. Persisting credentials from setup for the agent would put them in an environment variable or file and break the desired boundary. [OpenAI cloud environment documentation](https://developers.openai.com/codex/cloud/environments)

Human CAPTCHA renewal can run from any approved Windows machine using AWS IAM Identity Center. The renewal program reads the password in-process, opens a headed non-persistent browser context, waits for the human to complete reCAPTCHA, captures the resulting cookie in-process, validates it, and writes a new staged secret version. It must disable browser tracing, screenshots, video, HAR capture, and verbose HTTP logging during authentication.

## Current ModDB Authentication Facts

The public Vintage Story account page currently posts `email`, `password`, `loginredir`, and a reCAPTCHA response to `attemptlogin`. This was confirmed from the live page on 2026-09-02. The form loads Google's reCAPTCHA client and, when reached from ModDB, sets `loginredir=mods`. [Vintage Story account login](https://account.vintagestory.at/?loginredir=mods)

reCAPTCHA is therefore a deliberate interactive step, not a password storage problem. Google documents that a reCAPTCHA response token is single-use and expires after two minutes. The renewal program should fill the credentials, stop for the human challenge, and submit immediately after completion. It should not attempt to solve, reuse, or centrally store CAPTCHA tokens. [Google reCAPTCHA verification documentation](https://developers.google.com/recaptcha/docs/verify)

At current ModDB commit `1bd3f4f371cbfcc3bec6902ee9d2a637ccdd32f7`:

- ModDB reads the bearer token directly from the `vs_websessionkey` cookie and authenticates it by matching the token in its `users` table while `sessionValidUntil` is in the future. No IP address, browser identifier, or user agent participates in that lookup. [ModDB `lib/user.php`](https://github.com/anegostudios/vsmoddb/blob/1bd3f4f371cbfcc3bec6902ee9d2a637ccdd32f7/lib/user.php#L3-L21)
- The ModDB login bridge sends that cookie value to the Vintage Story account service's `/webprofile` endpoint. If accepted, it writes the token into the single `sessionToken` column for the user and sets ModDB's validity window to 14 days. [ModDB `login.php`](https://github.com/anegostudios/vsmoddb/blob/1bd3f4f371cbfcc3bec6902ee9d2a637ccdd32f7/login.php#L8-L52)
- A later successful ModDB login overwrites that one `sessionToken`, so previously stored ModDB sessions for the same account stop matching. ModDB logout clears the same database field. [ModDB `login.php`](https://github.com/anegostudios/vsmoddb/blob/1bd3f4f371cbfcc3bec6902ee9d2a637ccdd32f7/login.php#L40-L52), [ModDB `logout.php`](https://github.com/anegostudios/vsmoddb/blob/1bd3f4f371cbfcc3bec6902ee9d2a637ccdd32f7/logout.php#L1-L9)
- State-changing ModDB forms also require the per-user action token generated during login, so a publisher must first load the authenticated release form and use its current action token. [ModDB action-token validation](https://github.com/anegostudios/vsmoddb/blob/1bd3f4f371cbfcc3bec6902ee9d2a637ccdd32f7/lib/user.php#L331-L337)
- The official upload API request remains open. The issue itself proposes cookie or authorization-header authentication, but no supported release-upload endpoint exists yet. [ModDB issue 18](https://github.com/anegostudios/vsmoddb/issues/18)

These facts make `vs_websessionkey` a bearer credential. Any process that can read it can act as that Vintage Story account on ModDB for as long as ModDB accepts it. Machine sharing is technically possible, but the credential is still one shared session, not independent per-machine sessions.

## Secrets Manager Versus Parameter Store

Choose Secrets Manager. AWS describes Parameter Store as a configuration store and explicitly recommends Secrets Manager for passwords, API keys, tokens, automatic rotation, cross-account access, and fine-grained audit needs. Parameter Store `SecureString` is encrypted with KMS, but Parameter Store has no automatic credential rotation and its versioning is less suited to a validated pending-to-current promotion. [AWS Parameter Store documentation](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html)

The cost difference is small for this use case. Current AWS pricing is $0.40 per secret per month and $0.05 per 10,000 API calls. Two secrets therefore cost about $0.80 per month plus negligible calls. Standard Parameter Store has no additional service charge, while advanced parameters cost $0.05 each per month and API calls are billed. [Secrets Manager pricing](https://aws.amazon.com/secrets-manager/pricing/), [Systems Manager pricing](https://aws.amazon.com/systems-manager/pricing/)

Use two secrets even though one JSON object would save approximately $0.40 per month:

| Secret | Value shape | Readers | Writers |
| --- | --- | --- | --- |
| `/basic/vintage-story/moddb/account-login` | JSON containing `email` and `password` | Human renewal role only | Separate bootstrap or credential-admin role only |
| `/basic/vintage-story/moddb/session` | JSON containing `cookieValue`, `capturedAt`, `bridgeAttemptStartedAt`, observed cookie expiry when present, a conservative estimated ModDB deadline, and schema version | Renewal role and narrowly scoped publisher roles | Human renewal role only |

A single combined JSON secret cannot enforce field-level IAM. Any cloud publisher allowed to retrieve the cookie would also receive the password. Separate secrets preserve the meaningful security boundary.

Secrets Manager encrypts values at rest with KMS and transmits retrieved values over TLS. AWS recommends its managed `aws/secretsmanager` key for most same-account uses at no extra KMS-key charge. A customer-managed key is justified if access will cross AWS accounts or if a custom key policy is required. [AWS Secrets Manager best practices](https://docs.aws.amazon.com/secretsmanager/latest/userguide/best-practices.html)

Keep both secrets and the renewal lock in the repo's existing AWS home Region, currently `us-east-2`. Other machines and runners can call that Region directly. Cross-Region replication is unnecessary for this low-volume workflow and would add writer-ordering complexity.

## Identity and Least-Privilege Design

Create distinct permissions rather than a shared general-purpose AWS credential:

### Bootstrap or credential-admin role

- Creates and configures the secrets and lock table.
- Can set or replace the account password secret.
- Is not used by release or renewal automation.

### Human renewal role

- `secretsmanager:GetSecretValue` on the account-login secret, constrained to `secretsmanager:VersionStage=AWSCURRENT`.
- `secretsmanager:GetSecretValue` and `secretsmanager:DescribeSecret` on the session secret.
- `secretsmanager:PutSecretValue` and `secretsmanager:UpdateSecretVersionStage` on the session secret.
- Only the selected `dynamodb:GetItem`, `dynamodb:PutItem`, `dynamodb:UpdateItem`, and `dynamodb:DeleteItem` operations on the lock table, constrained with `dynamodb:LeadingKeys` to `moddb-session-renewal`. Do not grant `Scan`, `Query`, or generic table access. [DynamoDB fine-grained access control](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/specifying-conditions.html)
- No permission to change IAM, secret resource policies, or release artifacts.

### Publisher role

- `secretsmanager:GetSecretValue` on the session secret only, constrained to `secretsmanager:VersionStage=AWSCURRENT`; publisher code must explicitly request that stage.
- `secretsmanager:DescribeSecret` only if the program needs version metadata.
- No account-login secret access and no secret-write access.
- No DynamoDB renewal-lock access because publishing does not renew authentication.

AWS documents exact-secret identity policies for `GetSecretValue`; a customer-managed KMS key additionally requires `kms:Decrypt`. Use full secret ARNs, not broad prefixes, unless a narrowly bounded suffix wildcard is required because Secrets Manager appends characters to an ARN. [AWS identity-based policy examples](https://docs.aws.amazon.com/secretsmanager/latest/userguide/auth-and-access_iam-policies.html), [GetSecretValue API](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html)

AWS supports `secretsmanager:VersionStage` as a condition on `GetSecretValue`. Requiring an explicit `AWSCURRENT` request prevents publisher roles from selecting `AWSPENDING` or `AWSPREVIOUS`. [AWS Secrets Manager service authorization reference](https://docs.aws.amazon.com/service-authorization/latest/reference/list_secretsmanager.html)

For same-account access, identity policies are enough. Do not add a resource policy unless cross-account access is actually required. If cross-account access is later needed, use a customer-managed KMS key and coordinate the caller identity policy, secret resource policy, and KMS key policy. Any identity allowed to call `PutResourcePolicy` should be constrained with `secretsmanager:BlockPublicPolicy=true`. [AWS Secrets Manager best practices](https://docs.aws.amazon.com/secretsmanager/latest/userguide/best-practices.html#security-best-practices), [AWS resource-based policies](https://docs.aws.amazon.com/secretsmanager/latest/userguide/auth-and-access_resource-policies.html)

### Windows and other human machines

Use AWS CLI v2 IAM Identity Center profiles and `aws sso login`, which provide temporary AWS credentials and avoid machine-specific long-lived access keys. The renewal application should use the AWS SDK's standard profile credential provider rather than parsing CLI output. AWS recommends IAM Identity Center for workforce CLI access and documents automatic management of short-term credentials. [AWS CLI authentication choices](https://docs.aws.amazon.com/cli/latest/userguide/cli-chap-authentication.html), [IAM Identity Center CLI configuration](https://docs.aws.amazon.com/cli/latest/userguide/cli-configure-sso.html)

Grant the renewal permission set only to BASIC initially. Being able to authenticate to AWS generally must not imply permission to read the Vintage Story password.

### GitHub Actions

Use GitHub OIDC and a dedicated protected environment such as `moddb-release`, not long-lived AWS access keys. Constrain the AWS role trust policy to both:

- `token.actions.githubusercontent.com:aud = sts.amazonaws.com`
- `token.actions.githubusercontent.com:sub = repo:BASIC-BIT/Vintage-Story-Mods:environment:moddb-release`

The workflow needs `id-token: write` only to request an OIDC token and `contents: read` for checkout. GitHub documents that this avoids long-lived AWS credentials and recommends evaluating the `sub` claim in AWS trust policies. [GitHub OIDC for AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws)

This repository already uses the same `aud` plus exact environment-bound `sub` pattern in its Terraform bootstrap and assumes AWS roles from workflows. The new workflow should follow that convention and continue pinning third-party actions by commit. It should not expose the ModDB cookie through GitHub outputs, job environment variables, workflow artifacts, caches, or step summaries.

The final public ModDB save should remain behind the protected GitHub environment reviewer or equivalent owner confirmation. A cloud agent may prepare the artifact and release metadata, then request the narrow workflow. Secret storage does not itself authorize publication.

An environment-bound GitHub OIDC `sub` does not also name a branch. Configure deployment-branch or tag rules on `moddb-release` so only the protected default branch or an explicitly approved release-tag policy can use it, and ensure the secret-bearing job executes workflow and publisher code from that trusted ref. Environment approval alone must not allow a manually dispatched untrusted ref to run with the publisher role.

### Codex cloud and other agent runtimes

Codex cloud environment secrets cannot directly supply AWS credentials to agent-phase code because OpenAI removes those secrets after setup. Writing retrieved credentials or the ModDB cookie into `~/.aws`, the workspace, shell startup files, or a persistent environment variable would expose them to the agent and cached environment. OpenAI also warns that enabling agent internet access introduces prompt-injection and secret-exfiltration risk and recommends a minimal domain and HTTP-method allowlist. [Codex cloud environments](https://developers.openai.com/codex/cloud/environments), [Codex agent internet access](https://developers.openai.com/codex/cloud/agent-internet)

Therefore, use GitHub Actions as the execution broker for Codex cloud. The agent can produce a commit or a release request containing only non-secret inputs. A protected workflow on trusted repository code assumes the publisher role, retrieves the session, and performs the bounded operation.

For AWS-native agents or jobs, use the workload's attached IAM role. For a different external cloud-agent platform, decide only after identifying its workload identity. Prefer its OIDC federation if supported. IAM Roles Anywhere is the heavier fallback AWS documents for non-AWS workloads that can protect an X.509 private key. [AWS roles for non-AWS workloads](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_common-scenarios_non-aws.html)

## Human-Assisted Renewal Flow

The renewal program should use one orchestrator with no shell, file, or environment transport for either ModDB secret. Browser automation necessarily sends field values to a separate local browser process, so require a local-only automation transport and tear down that context immediately after capture:

1. Authenticate to AWS through an approved IAM Identity Center profile and assume the human renewal role.
2. Acquire the distributed renewal lock before opening the login page. Fail closed if another holder owns it.
3. Before starting a fresh login, inspect `AWSCURRENT` and `AWSPENDING`. If current validates, discard or ignore an abandoned pending candidate. If pending already validates on ModDB, promote it. If pending is still valid at the account service but has not completed the ModDB bridge, finish that bridge and validate it. Only perform a new password login when neither candidate is recoverable.
4. Read the current session secret's `AWSCURRENT` version ID, if it exists, and read the account-login secret with the AWS SDK. Keep both JSON values in orchestrator memory only.
5. Open exactly `https://account.vintagestory.at/` in a headed non-persistent browser context. Verify the final origin and TLS before filling.
6. Fill email and password through the browser automation API. Do not pass them on a command line, place them in process environment variables, or print them.
7. Pause for the human to complete reCAPTCHA, then submit promptly.
8. Capture only the `vs_websessionkey` value and necessary cookie metadata from the browser context. Reject redirects or cookies from an unexpected origin.
9. Immediately before the bridge, record `bridgeAttemptStartedAt` and calculate a conservative estimated ModDB deadline 14 days after it. Write that metadata, the observed browser-cookie expiry when available, and the candidate cookie as a new Secrets Manager version with a unique `ClientRequestToken` and explicit `VersionStages=["AWSPENDING"]`. Omitting `VersionStages` would move `AWSCURRENT` immediately. [PutSecretValue API](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_PutSecretValue.html)
10. Complete the ModDB login bridge and validate the candidate with a harmless authenticated read that proves the expected ModDB account. A redirect to login, a 401, or an unexpected identity is failure. The subsequent version-stage event records when validation and promotion succeeded without requiring another secret version.
11. For later renewals, promote the validated version by moving `AWSCURRENT` to its version ID while supplying the originally observed current version as `RemoveFromVersionId`. AWS fails the operation if the label is no longer attached to that version, providing an optimistic concurrency check. AWS automatically moves `AWSPREVIOUS` to the old current version. On the first successful bootstrap, when no `AWSCURRENT` exists, attach `AWSCURRENT` to the validated pending version without `RemoveFromVersionId` while still holding the renewal lock. [UpdateSecretVersionStage API](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_UpdateSecretVersionStage.html)
12. Close the browser context, zero mutable secret buffers where the runtime permits, release the lock in a `finally` path, and report only version IDs, timestamps, and validation status.

The process should never automatically change the Vintage Story password. If the owner changes it, the credential-admin flow should replace the account-login secret only after a successful human-assisted login test.

Secrets Manager's built-in unattended rotation is not appropriate for this third-party credential because the account login requires human reCAPTCHA and there is no supported ModDB token-minting API. The workflow is manual rotation with AWS version staging and audit, not automatic password rotation.

## Concurrency and Recovery

Secrets Manager staging labels protect the AWS write, but they do not serialize the external ModDB login. Two simultaneous logins can each change ModDB's one `sessionToken` before either process promotes its AWS version. Use a distributed lock whenever renewal is available from more than one machine.

A small DynamoDB lock table is suitable because AWS documents its lock client specifically for coordinating access to an external shared resource across multiple instances. It uses conditional writes, lease duration, and heartbeats. [AWS DynamoDB distributed locking](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/BestPractices_DistributedLocking.html)

An automatically expiring lease cannot fully fence ModDB because ModDB does not understand a fencing token. A paused old holder could resume after another machine takes over and overwrite ModDB before its AWS compare-and-swap fails. For this low-frequency human workflow, use a non-automatically-stealable lock record with a heartbeat for diagnosis, and require explicit recovery before takeover. The recovery operator first examines `AWSCURRENT`, `AWSPENDING`, and their live ModDB/account validity, then clears or transfers the lock conditionally. The renewer must also confirm ownership immediately before the ModDB bridge and abort on heartbeat loss. A future single AWS-side login broker could provide stronger serialization, but it is additional infrastructure.

The lock resource should be one key such as `moddb-session-renewal`. DynamoDB TTL may clean a separately marked safe-to-delete record, but it must not decide lock ownership because AWS says expired TTL items can remain for days. Ownership, release, and explicit recovery must use conditional writes. [DynamoDB TTL](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/TTL.html)

Use Secrets Manager versions as follows:

- Consumers always request `AWSCURRENT`, the default stage.
- The renewer writes an explicit `AWSPENDING` candidate while holding the lock.
- The renewer validates before promotion.
- Promotion includes both `MoveToVersionId` and the previously observed `RemoveFromVersionId`.
- Normal code never reads `AWSPREVIOUS`. It is limited recovery state, not the audit trail, and it is not a reliable ModDB rollback because ModDB stores only one active token. The value might remain valid at another Vintage Story account surface until upstream expiry, so lifecycle cleanup must treat it as sensitive even when ModDB no longer accepts it.
- A retry uses the same `ClientRequestToken` only for the identical candidate value. A new renewal attempt uses a new UUID. Secrets Manager documents this idempotency behavior and immutable secret versions. [PutSecretValue API](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_PutSecretValue.html)

If the process obtains a new account cookie but fails before it can store `AWSPENDING`, it must keep the browser context open and retry the AWS write. If it cannot recover, it should fail clearly without printing the cookie. On the next run, recovery of an existing pending candidate takes precedence over creating another login. This covers crashes after pending storage and after the ModDB bridge but before AWS promotion.

## Secret-Handling Requirements

Use an AWS SDK in the same orchestrator as the browser controller and ModDB HTTP client. Do not use `aws secretsmanager get-secret-value --query SecretString --output text`, command substitution, PowerShell pipeline output, or a GitHub output as the application interface. AWS warns that shells, history, and background utilities can capture command parameters. Secrets Manager excludes `SecretString` from CloudTrail, but custom logs must also exclude it. [AWS CLI exposure guidance](https://docs.aws.amazon.com/secretsmanager/latest/userguide/security_cli-exposure-risks.html), [GetSecretValue API](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html)

Hard requirements for code that handles either secret:

- No secret values in command arguments, environment variables, application-authored temporary files, persistent browser profiles, repository files, exceptions, logs, traces, screenshots, videos, HAR files, telemetry, workflow outputs, artifacts, caches, comments, or chat.
- No full HTTP request or response logging when `Cookie`, `Set-Cookie`, password fields, or AWS `SecretString` may be present.
- Redaction by field name before structured logging. Logging a hash or prefix of a bearer token is unnecessary and should be prohibited.
- Validate exact origins before filling or sending credentials. Allow network access only to required AWS endpoints, `account.vintagestory.at`, `mods.vintagestory.at`, and the reCAPTCHA domains actually required by the live form.
- Keep the browser context non-persistent. If the selected browser engine still writes an ephemeral profile internally, create it in a private, process-owned temporary directory, prevent tracing and backup/indexing, and securely clean it on normal and failure paths. This is risk reduction, not a guarantee against host-level process or disk forensics.
- Do not rely on masking as the transport mechanism. GitHub `add-mask` may be defense in depth, but the value should never be emitted in the first place.

Enable CloudTrail coverage and review or alert on `GetSecretValue` for the account-login secret, unexpected principals reading the session secret, session `PutSecretValue` or version-stage changes, and denied access. CloudTrail records Secrets Manager API calls while Secrets Manager excludes `SecretString` itself from the event. Persistent audit belongs in CloudTrail and version IDs, not in retained bearer values. [Secrets Manager CloudTrail logging](https://docs.aws.amazon.com/secretsmanager/latest/userguide/monitoring-cloudtrail.html), [GetSecretValue API](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_GetSecretValue.html), [PutSecretValue API](https://docs.aws.amazon.com/secretsmanager/latest/apireference/API_PutSecretValue.html)

## Is One Shared Web Session Sound?

Technically, yes for the current ModDB implementation. Its authentication query compares the supplied cookie value with a database token and expiry, with no machine binding. A publisher on another machine can therefore use the same raw cookie value.

Operationally, treat it as a short-lived shared bearer credential with important limitations:

- Every reader has the account's ModDB authority for the lifetime of the token.
- A new ModDB login overwrites the single server-side token and invalidates every consumer still holding the previous value.
- Logout invalidates the shared token for all consumers.
- Parallel publishers may share a token for reads, but release publication itself should be serialized and remain owner-approved.
- The 14-day value is ModDB's database validity window after its login bridge. The account service can impose its own independent lifetime or invalidate a token earlier, so consumers must validate `AWSCURRENT` before preparing a state-changing request and handle authentication failure as a renewal request.

For deterministic local policy, store both the observed browser-cookie expiry when available and `bridgeAttemptStartedAt + 14 days`. This estimate is conservative because the ModDB database deadline is set after the bridge begins. Treat the earlier known deadline, minus an operational safety margin, as renewal-required. Live validation still decides whether the session is usable because upstream can invalidate it earlier.

This is acceptable as an interim release mechanism if only the publisher role can read the cookie, renewal is serialized, every public save has an authorization gate, and all consumers fail closed. It is not equivalent to a scoped release token. The proper long-term design remains an official ModDB upload API with a revocable token limited to one mod and release creation.

## Repository Fit and Suggested Delivery Order

Read-only inspection found these existing conventions:

- AWS region defaults to `us-east-2`.
- GitHub Actions already assumes AWS roles through OIDC.
- The Terraform bootstrap already restricts trust with exact `aud` and environment-bound `sub` claims.
- The `production` GitHub environment already gates infrastructure deployment.
- Repository guidance keeps credential values out of Terraform state, committed files, and `AGENTS.local.md`.

If implementation is later approved:

1. Add a separate Terraform stack for the two Secrets Manager secret containers, the narrow roles and policies, and the DynamoDB renewal lock. Do not pass secret values through Terraform variables because they would enter Terraform state.
2. Add a small SDK-based credential bootstrap command. It reads the email and password from a no-echo console prompt, writes `SecretString` in-process, and never places either value in CLI arguments, environment variables, Terraform state, or temporary files.
3. Add the human renewal program with an in-process AWS SDK and headed non-persistent browser. Test with fake AWS and fake login services before touching live authentication.
4. Add a read-only session validation command.
5. Adapt the release publisher to retrieve only `AWSCURRENT` through the SDK.
6. Add a protected `moddb-release` GitHub environment and OIDC publisher role. Keep pull-request workflows and untrusted code unable to reach it.
7. Add a manually dispatched publish workflow with immutable artifact identity checks, prepared metadata checks, and an action-time public-save approval.
8. Update the repository ModDB release skill and automation plan only after the new path is proven end to end.
9. Continue pursuing the official scoped ModDB upload API and retire password/session automation when it exists.

## Decisions Needed Before Implementation

- Confirm that only BASIC may assume the renewal role initially.
- Confirm that cloud publishers may read only the session secret, never the password secret.
- Confirm that public ModDB Save remains a human-approved protected-environment action.
- Confirm that all access stays in the current AWS account and `us-east-2`; otherwise a customer-managed KMS key and cross-account resource policies are required.
- Choose whether renewal is available from multiple SSO-authorized machines immediately. If yes, deploy the DynamoDB lease in the first iteration.
- Confirm that the Vintage Story password is unique to that account. Central secret storage cannot contain the blast radius of password reuse.
- Decide whether to send a non-secret expiry or authentication-failure notification, or renew only on demand.

## Primary Sources

- [Vintage Story account login](https://account.vintagestory.at/?loginredir=mods)
- [ModDB source at the inspected commit](https://github.com/anegostudios/vsmoddb/tree/1bd3f4f371cbfcc3bec6902ee9d2a637ccdd32f7)
- [ModDB upload API issue 18](https://github.com/anegostudios/vsmoddb/issues/18)
- [Google reCAPTCHA verification](https://developers.google.com/recaptcha/docs/verify)
- [AWS Secrets Manager documentation](https://docs.aws.amazon.com/secretsmanager/latest/userguide/intro.html)
- [AWS Secrets Manager pricing](https://aws.amazon.com/secrets-manager/pricing/)
- [AWS Systems Manager Parameter Store](https://docs.aws.amazon.com/systems-manager/latest/userguide/systems-manager-parameter-store.html)
- [GitHub Actions OIDC for AWS](https://docs.github.com/en/actions/how-tos/secure-your-work/security-harden-deployments/oidc-in-aws)
- [OpenAI Codex cloud environments](https://developers.openai.com/codex/cloud/environments)
- [OpenAI Codex agent internet access](https://developers.openai.com/codex/cloud/agent-internet)

