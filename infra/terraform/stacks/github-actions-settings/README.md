# GitHub Actions Settings

This stack manages GitHub Actions settings that are safe to keep in Terraform state:

- the `production` environment used by `.github/workflows/infra-terraform.yml`;
- a required deployment reviewer for production, with admin bypass disabled;
- non-secret environment variables: `AWS_REGION`, `TF_STATE_REGION`, `TF_STATE_BUCKET`, `TF_STATE_KMS_KEY_ARN`, `AWS_TERRAFORM_ROLE_ARN`, `CLOUDFLARE_ACCOUNT_ID`, `CLOUDFLARE_WORKERS_DEV_SUBDOMAIN`, and `POSTHOG_HOST`.

It intentionally does not manage secret values. Terraform can create GitHub Actions secrets, but plaintext secret inputs are stored in Terraform state as sensitive values. Because this repo is public-facing and the secrets bootstrap the deploy pipeline, prefer `gh secret set --env production ...` for secret values unless we later add an encrypted-value-only workflow.

## Apply

1. Bootstrap the S3/KMS backend from `infra/terraform/bootstrap/thebasics-analytics-state`.
2. Copy `backend.hcl.example` to `backend.hcl` and fill in the bootstrap outputs.
3. Copy `terraform.tfvars.example` to `terraform.tfvars` and fill in the bootstrap outputs plus the Cloudflare account ID.
4. Authenticate GitHub locally. The provider reads `GITHUB_TOKEN`; a fine-grained token needs repository administration and Actions variables/environment permissions.
5. Run `terraform init -backend-config=backend.hcl`.
6. Run `terraform plan` and `terraform apply`.
