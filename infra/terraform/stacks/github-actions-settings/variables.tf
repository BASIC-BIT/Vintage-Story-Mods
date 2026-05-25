variable "github_owner" {
  description = "GitHub repository owner or organization."
  type        = string
  default     = "BASIC-BIT"
}

variable "repository" {
  description = "Repository name."
  type        = string
  default     = "Vintage-Story-Mods"
}

variable "environment" {
  description = "GitHub Actions environment for infrastructure deployment."
  type        = string
  default     = "production"
}

variable "aws_region" {
  description = "AWS region used when assuming the Terraform backend role."
  type        = string
  default     = "us-east-2"
}

variable "tf_state_region" {
  description = "AWS region containing the Terraform state S3 bucket."
  type        = string
  default     = "us-east-2"
}

variable "tf_state_bucket" {
  description = "S3 bucket that stores Terraform state."
  type        = string
}

variable "tf_state_kms_key_arn" {
  description = "KMS key ARN that encrypts Terraform state."
  type        = string
}

variable "aws_terraform_role_arn" {
  description = "AWS role ARN assumed by GitHub Actions for Terraform state access."
  type        = string
}

variable "cloudflare_account_id" {
  description = "Cloudflare account ID that owns the analytics relay Worker."
  type        = string
  default     = ""
}

variable "cloudflare_workers_dev_subdomain" {
  description = "Cloudflare account workers.dev subdomain, without the workers.dev suffix."
  type        = string
  default     = "basic-bit-1001"
}

variable "posthog_host" {
  description = "PostHog ingestion host."
  type        = string
  default     = "https://us.i.posthog.com"
}

variable "production_reviewer_user_ids" {
  description = "GitHub numeric user IDs required to approve production environment deployments. Defaults to the BASIC-BIT owner account."
  type        = set(number)
  default     = [2165323]
}
