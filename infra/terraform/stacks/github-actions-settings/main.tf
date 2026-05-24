resource "github_repository_environment" "production" {
  repository          = var.repository
  environment         = var.environment
  can_admins_bypass   = false
  prevent_self_review = false

  dynamic "reviewers" {
    for_each = length(var.production_reviewer_user_ids) == 0 ? [] : [1]

    content {
      users = var.production_reviewer_user_ids
    }
  }
}

resource "github_actions_environment_variable" "aws_region" {
  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "AWS_REGION"
  value         = var.aws_region
}

resource "github_actions_environment_variable" "tf_state_region" {
  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "TF_STATE_REGION"
  value         = var.tf_state_region
}

resource "github_actions_environment_variable" "tf_state_bucket" {
  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "TF_STATE_BUCKET"
  value         = var.tf_state_bucket
}

resource "github_actions_environment_variable" "tf_state_kms_key_arn" {
  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "TF_STATE_KMS_KEY_ARN"
  value         = var.tf_state_kms_key_arn
}

resource "github_actions_environment_variable" "aws_terraform_role_arn" {
  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "AWS_TERRAFORM_ROLE_ARN"
  value         = var.aws_terraform_role_arn
}

resource "github_actions_environment_variable" "cloudflare_account_id" {
  count = var.cloudflare_account_id == "" ? 0 : 1

  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "CLOUDFLARE_ACCOUNT_ID"
  value         = var.cloudflare_account_id
}

resource "github_actions_environment_variable" "cloudflare_workers_dev_subdomain" {
  count = var.cloudflare_workers_dev_subdomain == "" ? 0 : 1

  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "CLOUDFLARE_WORKERS_DEV_SUBDOMAIN"
  value         = var.cloudflare_workers_dev_subdomain
}

resource "github_actions_environment_variable" "posthog_host" {
  repository    = var.repository
  environment   = github_repository_environment.production.environment
  variable_name = "POSTHOG_HOST"
  value         = var.posthog_host
}
