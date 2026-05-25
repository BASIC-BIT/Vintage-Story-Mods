variable "aws_region" {
  description = "AWS region for the Terraform state backend resources."
  type        = string
  default     = "us-east-2"
}

variable "project_name" {
  description = "Project tag for backend resources."
  type        = string
  default     = "thebasics"
}

variable "environment" {
  description = "Environment tag for backend resources."
  type        = string
  default     = "prod"
}

variable "state_bucket_name" {
  description = "Globally unique S3 bucket name for Terraform state."
  type        = string

  validation {
    condition = (
      length(var.state_bucket_name) >= 3 &&
      length(var.state_bucket_name) <= 63 &&
      can(regex("^[a-z0-9][a-z0-9.-]*[a-z0-9]$", var.state_bucket_name)) &&
      !can(regex("[_.]", var.state_bucket_name))
    )
    error_message = "state_bucket_name must be a globally unique S3 bucket name: 3-63 chars, lowercase letters/numbers/hyphens only, starting and ending with a letter or number. Example: basicbit-thebasics-tfstate-prod."
  }
}

variable "kms_alias_name" {
  description = "KMS alias name without the alias/ prefix."
  type        = string
  default     = "thebasics-analytics-terraform-state"
}

variable "tags" {
  description = "Additional tags to apply to backend resources."
  type        = map(string)
  default     = {}
}

variable "github_repository" {
  description = "GitHub repository allowed to assume the Terraform backend role. Format: owner/repo."
  type        = string
  default     = "BASIC-BIT/Vintage-Story-Mods"
}

variable "github_environment" {
  description = "GitHub Actions environment allowed to assume the Terraform backend role."
  type        = string
  default     = "production"
}

variable "github_oidc_provider_arn" {
  description = "Existing GitHub Actions OIDC provider ARN. Leave empty to create one in this AWS account."
  type        = string
  default     = ""
}
