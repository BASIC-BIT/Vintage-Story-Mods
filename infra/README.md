# The BASICs Infrastructure

This directory contains public-facing infrastructure as code for services that belong directly to this Vintage Story mod project.

## Analytics Relay

`terraform/stacks/thebasics-analytics-relay` deploys a Cloudflare Worker at `https://thebasics-analytics-relay.basic-bit-1001.workers.dev/v1/events/batch`. The mod sends only opt-in, allowlisted server-install analytics to this endpoint. The Worker validates the schema and forwards accepted batches to PostHog with the real project token kept out of the mod.

## State Security

Terraform state can contain sensitive values, including Worker secret bindings. Do not use local state for the relay stack after initial experimentation.

Use the same secure remote-state pattern as `BASIC-BIT/basic-infra`:

1. Apply `terraform/bootstrap/thebasics-analytics-state` once to create an encrypted S3 state bucket and KMS key.
2. Copy `terraform/stacks/thebasics-analytics-relay/backend.hcl.example` to `backend.hcl` locally.
3. Fill in the bucket and KMS key ARN from the bootstrap outputs.
4. Run `terraform init -backend-config=backend.hcl` inside the relay stack before planning or applying.

Never commit `backend.hcl`, `terraform.tfvars`, Terraform state, plan files, Cloudflare API tokens, or PostHog tokens.

## CI/CD

`.github/workflows/infra-terraform.yml` validates Terraform and Worker syntax on pull requests and pushes that touch `infra/`. It also has a manual `workflow_dispatch` path that plans or applies the analytics relay stack from the GitHub `production` environment.

`terraform/stacks/github-actions-settings` can manage the GitHub `production` environment and non-secret environment variables with Terraform. It intentionally does not manage secret values by default because Terraform-managed plaintext secrets are stored in Terraform state as sensitive values.

The CI/CD deploy job expects these GitHub environment secrets in `production`:

- `CLOUDFLARE_API_TOKEN`: Cloudflare API token with permission to manage Workers for the account.
- `POSTHOG_PROJECT_TOKEN`: PostHog project token stored only in the Worker secret binding and encrypted Terraform state.

The CI/CD deploy job expects these GitHub environment variables in `production`; `terraform/stacks/github-actions-settings` manages them:

- `AWS_REGION`: AWS region for assuming the Terraform role, default `us-east-2`.
- `AWS_TERRAFORM_ROLE_ARN`: AWS role assumable by GitHub OIDC for reading and writing the encrypted S3 Terraform backend.
- `CLOUDFLARE_ACCOUNT_ID`: Cloudflare account ID.
- `CLOUDFLARE_WORKERS_DEV_SUBDOMAIN`: Cloudflare account workers.dev subdomain, default `basic-bit-1001`.
- `TF_STATE_BUCKET`: S3 bucket created by `terraform/bootstrap/thebasics-analytics-state`.
- `TF_STATE_KMS_KEY_ARN`: KMS key ARN created by the bootstrap stack.
- `TF_STATE_REGION`: S3 backend region, default `AWS_REGION` or `us-east-2`.
- `POSTHOG_HOST`: PostHog ingestion host, default `https://us.i.posthog.com`.

The backend bootstrap stack remains a one-time admin/local operation. It creates the encrypted S3/KMS state backend and an AWS role that GitHub Actions can assume through OIDC. Applying it from Actions would require either local state in CI or a pre-existing backend for the backend, so the safer path is to bootstrap the state bucket once, then let CI/CD own relay plans and applies.
