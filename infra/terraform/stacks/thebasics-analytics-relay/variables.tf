variable "cloudflare_account_id" {
  description = "Cloudflare account ID that owns the Worker."
  type        = string
}

variable "workers_dev_subdomain" {
  description = "Cloudflare account workers.dev subdomain, without the workers.dev suffix."
  type        = string
  default     = "basic-bit-1001"
}

variable "worker_name" {
  description = "Cloudflare Worker service name."
  type        = string
  default     = "thebasics-analytics-relay"
}

variable "posthog_host" {
  description = "PostHog ingestion host."
  type        = string
  default     = "https://us.i.posthog.com"
}

variable "posthog_project_token" {
  description = "PostHog project token used only by the relay Worker. Stored in encrypted remote Terraform state."
  type        = string
  sensitive   = true
}
