output "analytics_endpoint_url" {
  description = "Batch endpoint configured in The BASICs analytics config by default."
  value       = "https://${local.workers_dev_hostname}/v1/events/batch"
}

output "workers_dev_hostname" {
  description = "Public workers.dev hostname for the analytics relay Worker."
  value       = local.workers_dev_hostname
}

output "worker_name" {
  description = "Cloudflare Worker service name."
  value       = cloudflare_worker.analytics_relay.name
}
