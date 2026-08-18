# The BASICs Analytics Relay

This stack deploys the BASIC-owned intake endpoint used by The BASICs server-install analytics.

## Contract

- Endpoint: `https://thebasics-analytics-relay.basic-bit-1001.workers.dev/v1/events/batch`
- Client: The BASICs mod server process only, after root-admin opt-in.
- Accepted event schema: allowlisted event names and per-event property keys in `worker/analytics-relay.mjs`.
- Forwarding target: PostHog `/batch/` with `$process_person_profile=false`.

The Worker rejects unknown event names, unknown properties, oversized batches, and malformed server install IDs. It does not accept chat text, command arguments, player names, player IDs, IPs, world names, seeds, coordinates, or raw config.

Closed values and semantic analytics labels use explicit server-side registries. This includes `feature_name`, `action`, `command_name`, `result`, `area`, `operation`, and `severity`, because string shape alone cannot distinguish a legitimate label from a player name or identifier. The contract suite derives literals from known C# analytics seams, covers dynamic labels with explicit fixtures, and fails CI when a producer emits a value that the relay does not recognize. This keeps the registries synchronized without weakening the privacy boundary.

## Batch behavior

- Fully valid batches are forwarded and return `204 No Content`.
- Mixed batches forward valid events, drop invalid events, and return `202 Accepted` with aggregate accepted/rejected counts and rejection reasons.
- Batches with no valid events return `400 Bad Request` and are not forwarded.
- PostHog connection failures and upstream rejections return `502 Bad Gateway`.

Every handled batch produces one structured Worker log with aggregate counts, rejection reasons, upstream status, and processing duration. Logs never include request bodies, event properties, server install IDs, player pseudonyms, or IP addresses.

Run the contract suite locally with:

```powershell
node --test infra/terraform/stacks/thebasics-analytics-relay/worker/analytics-relay.test.mjs
```

## Deploy

1. Bootstrap the encrypted S3 backend from `infra/terraform/bootstrap/thebasics-analytics-state` if it does not already exist.
2. Copy `backend.hcl.example` to `backend.hcl` and fill in the S3 bucket and KMS key ARN from bootstrap outputs.
3. Copy `terraform.tfvars.example` to `terraform.tfvars` and fill in the Cloudflare account ID and PostHog project token.
4. Export `CLOUDFLARE_API_TOKEN` with permissions to manage Workers for the account.
5. Run `terraform init -backend-config=backend.hcl`, then `terraform plan`, then `terraform apply`.

`terraform.tfvars` and `backend.hcl` are intentionally ignored because the stack state and secret bindings are sensitive.

## CI/CD Deploy

Use the `Terraform Infra` workflow from GitHub Actions after the bootstrap backend exists.

Required production environment secrets are documented in `infra/README.md`. The workflow:

- validates Terraform formatting, Terraform configuration, and Worker JavaScript syntax on PRs and pushes;
- runs manual relay plans with `workflow_dispatch` action `plan`;
- runs manual relay applies with `workflow_dispatch` action `apply`, guarded to `main` and the `production` environment.
