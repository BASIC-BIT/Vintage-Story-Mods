output "state_bucket_name" {
  description = "S3 bucket name for The BASICs analytics Terraform state backend."
  value       = aws_s3_bucket.terraform_state.bucket
}

output "kms_key_arn" {
  description = "KMS key ARN used to encrypt backend resources."
  value       = aws_kms_key.terraform_state.arn
}

output "github_actions_role_arn" {
  description = "AWS role ARN for GitHub Actions to access the Terraform state backend."
  value       = aws_iam_role.github_actions_terraform_state.arn
}

output "github_oidc_provider_arn" {
  description = "GitHub Actions OIDC provider ARN used by the Terraform state role."
  value       = local.github_oidc_provider_arn
}
