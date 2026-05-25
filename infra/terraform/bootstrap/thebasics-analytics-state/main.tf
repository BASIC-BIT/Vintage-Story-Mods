data "aws_caller_identity" "current" {}

data "tls_certificate" "github_actions" {
  count = var.github_oidc_provider_arn == "" ? 1 : 0

  url = "https://token.actions.githubusercontent.com"
}

locals {
  common_tags = merge(
    {
      Project     = var.project_name
      Environment = var.environment
      Stack       = "thebasics-analytics-state"
      ManagedBy   = "terraform"
    },
    var.tags,
  )
}

data "aws_iam_policy_document" "kms" {
  statement {
    sid = "EnableRootPermissions"

    actions   = ["kms:*"]
    resources = ["*"]

    principals {
      type        = "AWS"
      identifiers = ["arn:aws:iam::${data.aws_caller_identity.current.account_id}:root"]
    }
  }
}

resource "aws_kms_key" "terraform_state" {
  description         = "CMK for The BASICs analytics Terraform backend resources"
  enable_key_rotation = true
  policy              = data.aws_iam_policy_document.kms.json

  tags = local.common_tags

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_kms_alias" "terraform_state" {
  name          = "alias/${var.kms_alias_name}"
  target_key_id = aws_kms_key.terraform_state.key_id
}

resource "aws_s3_bucket" "terraform_state" {
  bucket = var.state_bucket_name
  tags   = local.common_tags

  lifecycle {
    prevent_destroy = true
  }
}

resource "aws_s3_bucket_versioning" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm     = "aws:kms"
      kms_master_key_id = aws_kms_key.terraform_state.arn
    }
  }
}

resource "aws_s3_bucket_public_access_block" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_ownership_controls" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    object_ownership = "BucketOwnerEnforced"
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "terraform_state" {
  bucket = aws_s3_bucket.terraform_state.id

  rule {
    id     = "state-retention"
    status = "Enabled"

    filter {}

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }

    noncurrent_version_expiration {
      noncurrent_days           = 90
      newer_noncurrent_versions = 20
    }
  }

  depends_on = [aws_s3_bucket_versioning.terraform_state]
}

resource "aws_s3_bucket_policy" "terraform_state_tls_only" {
  bucket = aws_s3_bucket.terraform_state.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid       = "DenyInsecureTransport"
        Effect    = "Deny"
        Principal = "*"
        Action    = "s3:*"
        Resource = [
          aws_s3_bucket.terraform_state.arn,
          "${aws_s3_bucket.terraform_state.arn}/*",
        ]
        Condition = {
          Bool = {
            "aws:SecureTransport" = "false"
          }
        }
      },
    ]
  })
}

resource "aws_iam_openid_connect_provider" "github_actions" {
  count = var.github_oidc_provider_arn == "" ? 1 : 0

  url             = "https://token.actions.githubusercontent.com"
  client_id_list  = ["sts.amazonaws.com"]
  thumbprint_list = [data.tls_certificate.github_actions[0].certificates[0].sha1_fingerprint]

  tags = local.common_tags
}

locals {
  github_oidc_provider_arn = var.github_oidc_provider_arn == "" ? aws_iam_openid_connect_provider.github_actions[0].arn : var.github_oidc_provider_arn
}

data "aws_iam_policy_document" "github_actions_assume_state_role" {
  statement {
    actions = ["sts:AssumeRoleWithWebIdentity"]

    principals {
      type        = "Federated"
      identifiers = [local.github_oidc_provider_arn]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:aud"
      values   = ["sts.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "token.actions.githubusercontent.com:sub"
      values   = ["repo:${var.github_repository}:environment:${var.github_environment}"]
    }
  }
}

resource "aws_iam_role" "github_actions_terraform_state" {
  name               = "${var.project_name}-${var.environment}-analytics-terraform-state"
  assume_role_policy = data.aws_iam_policy_document.github_actions_assume_state_role.json
  tags               = local.common_tags
}

data "aws_iam_policy_document" "github_actions_terraform_state" {
  statement {
    sid = "ListStateBucket"

    actions = [
      "s3:GetBucketLocation",
      "s3:ListBucket",
    ]

    resources = [aws_s3_bucket.terraform_state.arn]
  }

  statement {
    sid = "ReadWriteStateObjects"

    actions = [
      "s3:DeleteObject",
      "s3:GetObject",
      "s3:GetObjectVersion",
      "s3:PutObject",
    ]

    resources = ["${aws_s3_bucket.terraform_state.arn}/*"]
  }

  statement {
    sid = "UseStateKmsKey"

    actions = [
      "kms:Decrypt",
      "kms:DescribeKey",
      "kms:Encrypt",
      "kms:GenerateDataKey",
    ]

    resources = [aws_kms_key.terraform_state.arn]
  }
}

resource "aws_iam_role_policy" "github_actions_terraform_state" {
  name   = "terraform-state-access"
  role   = aws_iam_role.github_actions_terraform_state.id
  policy = data.aws_iam_policy_document.github_actions_terraform_state.json
}
