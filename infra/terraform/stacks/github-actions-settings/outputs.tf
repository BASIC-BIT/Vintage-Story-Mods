output "environment" {
  description = "Managed GitHub Actions environment."
  value       = github_repository_environment.production.environment
}

output "managed_variables" {
  description = "Environment variables managed by this stack."
  value = compact([
    github_actions_environment_variable.aws_terraform_role_arn.variable_name,
    github_actions_environment_variable.aws_region.variable_name,
    github_actions_environment_variable.tf_state_region.variable_name,
    github_actions_environment_variable.tf_state_bucket.variable_name,
    github_actions_environment_variable.tf_state_kms_key_arn.variable_name,
    github_actions_environment_variable.posthog_host.variable_name,
    length(github_actions_environment_variable.cloudflare_workers_dev_subdomain) == 0 ? "" : github_actions_environment_variable.cloudflare_workers_dev_subdomain[0].variable_name,
  ])
}

output "optional_variables_to_fill" {
  description = "Optional variables not managed until values are provided."
  value = compact([
    var.cloudflare_account_id == "" ? "CLOUDFLARE_ACCOUNT_ID" : "",
  ])
}
