# The BASICs Analytics Relay

This stack deploys the BASIC-owned intake endpoint used by The BASICs server-install analytics.

## Contract

- Endpoint: `https://thebasics-analytics-relay.basic-bit-1001.workers.dev/v1/events/batch`
- Client: The BASICs mod server process only, after root-admin opt-in.
- Accepted event schema: allowlisted event names and property keys in `worker/analytics-relay.mjs`.
- Forwarding target: PostHog `/batch/` with `$process_person_profile=false`.

The Worker rejects unknown event names, unknown properties, oversized batches, and malformed server install IDs. It does not accept chat text, command arguments, player names, player IDs, IPs, world names, seeds, coordinates, or raw config.

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
