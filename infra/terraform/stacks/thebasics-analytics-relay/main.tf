locals {
  worker_module        = "analytics-relay.mjs"
  workers_dev_hostname = "${var.worker_name}.${var.workers_dev_subdomain}.workers.dev"
}

resource "cloudflare_worker" "analytics_relay" {
  account_id = var.cloudflare_account_id
  name       = var.worker_name

  observability = {
    enabled = true
  }

  subdomain = {
    enabled          = true
    previews_enabled = false
  }

  lifecycle {
    ignore_changes = [
      observability,
    ]
  }
}

resource "cloudflare_worker_version" "analytics_relay" {
  account_id         = var.cloudflare_account_id
  worker_id          = cloudflare_worker.analytics_relay.id
  compatibility_date = "2026-05-24"
  main_module        = local.worker_module

  modules = [
    {
      name         = local.worker_module
      content_type = "application/javascript+module"
      content_file = "${path.module}/worker/${local.worker_module}"
    },
  ]

  bindings = [
    {
      type = "plain_text"
      name = "POSTHOG_HOST"
      text = var.posthog_host
    },
    {
      type = "secret_text"
      name = "POSTHOG_PROJECT_TOKEN"
      text = var.posthog_project_token
    },
  ]
}

resource "cloudflare_workers_deployment" "analytics_relay" {
  account_id  = var.cloudflare_account_id
  script_name = cloudflare_worker.analytics_relay.name
  strategy    = "percentage"

  versions = [
    {
      percentage = 100
      version_id = cloudflare_worker_version.analytics_relay.id
    },
  ]
}

resource "cloudflare_workers_script_subdomain" "analytics_relay" {
  account_id       = var.cloudflare_account_id
  script_name      = cloudflare_worker.analytics_relay.name
  enabled          = true
  previews_enabled = false

  depends_on = [cloudflare_workers_deployment.analytics_relay]
}
