# Analytics And Observability Plan

## Product Context

The BASICs is a Vintage Story roleplay and server-management mod. The useful product questions are server-owner and community-fit questions:

- Which feature families are enabled on real servers: proximity chat, nicknames, languages, character sheets, headshots, notes, TPA, save/sleep notifications, typing indicators, nametag rendering, RP character slots, and chatter.
- Which commands and workflows are actually used after a feature is enabled.
- Which defaults cause server owners to opt out, turn features off, or repeatedly change settings.
- Which game versions, mod versions, and server-size buckets need compatibility attention.
- Which reliability or performance failures happen in the wild and cannot be reproduced locally.

The product stance is privacy-forward because the mod handles roleplay identity, chat behavior, notes, character bios, images, and admin workflows. Analytics should answer adoption and reliability questions without collecting roleplay content.

## Implemented MVP Shape

- `AnalyticsConfig` stores consent and transport settings in `ModConfig/the_basics_analytics.json`.
- Root admins can use `/basicsanalytics` or `/thebasicsanalytics` with `status`, `server`, `personalized`, `off`, and `prompt`.
- A client-side prompt is available over the `thebasicsanalytics` channel, with chat-command fallback.
- `AnalyticsService` records `command used`, `feature used`, and `config snapshot` events.
- `RelayAnalyticsSink` queues events, batches them, sends HTTPS only, drops failed batches, and adds version, server-session, and online-player-count bucket properties.
- `server` consent sends anonymous server statistics only.
- `personalized` consent sends anonymous server statistics plus pseudonymous player session start/end events.
- Consent policy version `2` is required for remote analytics because full statistics now include pseudonymous player session events.
- The Cloudflare Worker relay validates a strict allowlist before forwarding accepted events to PostHog with `$process_person_profile=false`.

## Consent Tiers

### Off

Remote analytics and remote feature flag requests are disabled. The sink is `NoopAnalyticsSink`, no remote batches are sent, and player sessions are not tracked.

### Anonymous Server Statistics

This maps to config value `server`.

Allowed data:

- Random `server_install_id` generated after opt-in.
- Random per-run `server_session_id`.
- Mod ID, mod version, and Vintage Story game version.
- Online player count bucket.
- Allowlisted event, command, feature, action, success, and result-code enums.
- Allowlisted boolean/enum config summaries and count buckets.

Disallowed data:

- Chat text, command arguments, names, nicknames, character names, bios, notes, images, coordinates, world seed, world name, server IP, server name, player UID, raw config, raw logs, and stack traces.

### Full Statistics

This maps to config value `personalized`.

Additional allowed data:

- `player session started` and `player session ended` events.
- Per-server pseudonymous player IDs created with HMAC-SHA256 from player UID and a random local salt.
- Bucketed session duration and bounded end reason.

The raw player UID and player names are not sent. Future expansion of full statistics requires a consent-version bump and public docs update.

## Cross-Network Verification Boundary

Cross-network verification should not be treated as generic PostHog analytics.

Recommended boundary:

- Keep PostHog for product analytics, adoption funnels, config summaries, and coarse reliability trends.
- Build verification as a separate BASIC-owned service or relay route with its own schema, retention, audit trail, and revocation semantics.
- Use full-statistics consent as a prerequisite, but add explicit verification enrollment so owners understand that their server is joining a network.
- Distinguish server identity from player identity. Server-owner de-anonymization does not automatically justify player-level de-anonymization.
- If player matching is required across servers, prefer relay-side tokenization or a purpose-built identity flow over shipping raw player identifiers into analytics events.
- Do not use PostHog person profiles as the source of truth for verification decisions.

Minimum viable verification path:

- Server owner opts into full statistics.
- Server owner enrolls the server with a declared public server profile.
- The mod receives a server verification token or signed enrollment response.
- Verification events use a separate event family and are not mixed into generic feature analytics.
- The server owner can revoke enrollment and rotate the server verification identity.

## Next Phases

### Phase 2: Dashboards And Event Hygiene

- Build dashboards for active installs, consent distribution, feature adoption, command usage, TPA result codes, chat-mode usage, upgrade lag, and relay health.
- Review command instrumentation for duplicate counting and missing failure paths.
- Add relay metrics around reject reasons and upstream failures.
- Add rate limits or local aggregation if real servers produce too much chat-related event volume.

### Phase 3: Reliability And Performance Telemetry

- Add allowlisted `error observed` events gated by remote analytics consent and `AllowErrorTelemetry`.
- Add optional aggregated `performance sample` events gated by `AllowPerformanceTelemetry`.
- Use stable error codes and metric buckets only.
- Keep stack traces local until there is a full-statistics-only policy, redaction layer, and retention plan.

### Phase 4: Verification Enrollment

- Define a separate verification enrollment contract.
- Add explicit server profile fields only for owners who choose to publish or verify them.
- Add server verification token rotation and revocation.
- Add player-level identity handling only for a named verification use case, not generic product analytics.
- Bump `CurrentConsentVersion` before collecting any newly sensitive field.

## Open Product Decisions

- Should the in-game labels become `Off`, `Anonymous`, and `Full` while config keeps `disabled`, `server`, and `personalized`?
- Should full statistics require an explicit server profile before anything beyond pseudonymous session events is sent?
- What is the minimum viable cross-network verification use case: server authenticity, player account linking, ban/safety signals, roleplay character continuity, or something else?
- What retention window is acceptable for anonymous server analytics, full statistics, and verification records?
- Should the root-admin prompt repeat every 24 hours while undecided, or only after install, upgrade, and consent-policy changes?
