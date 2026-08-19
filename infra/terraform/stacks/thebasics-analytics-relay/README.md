# The BASICs Analytics Relay

This stack deploys the BASIC-owned intake endpoint used by The BASICs server-install analytics.

## Contract

- Endpoint: `https://thebasics-analytics-relay.basic-bit-1001.workers.dev/v1/events/batch`
- Client: The BASICs mod server process only, after root-admin opt-in.
- Accepted event schema: allowlisted event names and per-event property keys in `worker/analytics-relay.mjs`.
- Forwarding target: PostHog `/batch/` with `$process_person_profile=false` and `$geoip_disable=true`.

The Worker rejects unknown event names, unknown properties, oversized batches, and malformed server install IDs. It does not accept chat text, command arguments, player names, player IDs, IPs, world names, seeds, coordinates, or raw config.

Current producers send the exact bounded `online_player_count` as a server-level numeric metric so PostHog can normalize activity by concurrent population. The relay continues to accept the legacy `online_player_count_bucket` property from already-released mod versions.

Closed values and semantic analytics labels use explicit server-side registries. This includes `feature_name`, `action`, `command_name`, `result`, `area`, `operation`, and `severity`, because string shape alone cannot distinguish a legitimate label from a player name or identifier. The contract suite derives literals from known C# analytics seams, covers dynamic labels with explicit fixtures, and fails CI when a producer emits a value that the relay does not recognize. This keeps the registries synchronized without weakening the privacy boundary.

Pull requests that change Worker behavior must also increase `CONTRACT_REVISION`. Release builds independently verify that the deployed relay revision satisfies the mod's `RequiredRelayContractRevision` before publishing.

## Batch behavior

- Fully valid batches are forwarded and return `204 No Content`.
- Mixed batches forward valid events, drop invalid events, and return `202 Accepted` with aggregate accepted/rejected counts and rejection reasons.
- Batches with no valid events return `400 Bad Request` and are not forwarded.
- PostHog connection failures and upstream rejections return `502 Bad Gateway`.

Every handled batch produces one structured Worker log with aggregate counts, rejection reasons, upstream status, and processing duration. Logs never include request bodies, event properties, server install IDs, player pseudonyms, or IP addresses.

The relay owns both PostHog control properties. Clients cannot submit or override them. Disabling GeoIP prevents PostHog from treating the Cloudflare Worker egress location as server geography.

## Operational monitoring

Terraform keeps custom Workers Logs enabled, persisted, and sampled at 100% for this low-volume relay. Automatic invocation logs stay disabled so request metadata is not retained with the sanitized batch summaries. In Cloudflare, open **Workers & Pages**, select `thebasics-analytics-relay`, then open **Observability**. Query custom logs containing `analytics_batch_processed` and monitor:

- `outcome = upstream_failed` or an `upstream_status` of 500 or greater;
- non-zero `rejected_event_count`, grouped by the bounded `rejection_reasons` values;
- sudden changes in `accepted_event_count` and `duration_ms`.

PostHog cannot reveal rejected batches or upstream delivery failures because those events never arrive there. Treat Workers Logs as the delivery-health source and the PostHog telemetry-volume insight as the accepted-event source.

Run the contract suite locally with:

```powershell
node --test infra/terraform/stacks/thebasics-analytics-relay/worker/analytics-relay.test.mjs
```

## Deploy

1. Bootstrap the encrypted S3 backend from `infra/terraform/bootstrap/thebasics-analytics-state` if it does not already exist.
2. Copy `backend.hcl.example` to `backend.hcl` and fill in the S3 bucket and KMS key ARN from bootstrap outputs.
3. Copy `terraform.tfvars.example` to `terraform.tfvars` and fill in the Cloudflare account ID and PostHog project token.
4. Export `CLOUDFLARE_API_TOKEN` with permissions to manage Workers for the account.
5. Run `terraform init -backend-config=backend.hcl`, then `terraform plan`, then `terraform apply`.

`terraform.tfvars` and `backend.hcl` are intentionally ignored because the stack state and secret bindings are sensitive.

## CI/CD Deploy

Use the `Terraform Infra` workflow from GitHub Actions after the bootstrap backend exists.

Required production environment secrets are documented in `infra/README.md`. The workflow:

- validates Terraform formatting, Terraform configuration, and Worker JavaScript syntax on PRs and pushes;
- runs manual relay plans with `workflow_dispatch` action `plan`;
- runs manual relay applies with `workflow_dispatch` action `apply`, guarded to `main` and the `production` environment.
