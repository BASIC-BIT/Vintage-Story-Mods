# Scene marker analytics implementation plan

> **For agentic workers:** Use superpowers:subagent-driven-development to implement and review this plan.

**Goal:** Measure all approved Scene Marker usage actions through the existing consent-controlled analytics pipeline.

**Architecture:** Server gameplay hooks emit accepted actions. Clients report successful reader opens and debounced rendered bubbles through a bounded, server-validated observation channel. The existing relay validates the new bounded contract before forwarding events.

**Tech Stack:** C#, Vintage Story 1.22.6, .NET 10, xUnit, Cloudflare Worker JavaScript, Node test runner, PostHog.

**Spec:** `docs/agent-context/2026-09-14-scene-marker-analytics.md`

## Global constraints

- No marker text, player identifiers, coordinates, or marker identifiers in exported events.
- Preserve existing consent behavior and existing gameplay.
- Client observations use SafeClientNetworkChannel and are never queued while disconnected.
- Relay revision 6 must be deployed before distributing the mod. Deployment and publication are outside implementation approval.

## Task 1: Producer and interaction validation

Files: `mods-dll/thebasics/src/ModSystems/SceneDescriptions/`, `Analytics/AnalyticsService.cs`, `Analytics/RelayAnalyticsSink.cs`, and scene tests under `mods-dll/thebasics.Tests/ModSystems/SceneDescriptions/`.

- [x] Write and run failing tests for accepted saves, denied saves, actual read-state transitions, reuse, opt-out, client dwell/cooldown, server distance validation, and bounded state.
- [x] Add successful action hooks and safe observation networking. Use `AnalyticsService.TrackFeatureUsed("scene_markers", action, properties: properties)` without `actorPlayerUid`.
- [x] Add `enable_scene_markers` to config snapshots and raise `RequiredRelayContractRevision` to 6.
- [x] Run `dotnet test mods-dll/thebasics.Tests/thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true` with `VINTAGE_STORY=D:/Games/Vintagestory`.

## Task 2: Relay contract

Files: `infra/terraform/stacks/thebasics-analytics-relay/worker/analytics-relay.mjs` and `analytics-relay.test.mjs`.

- [x] Add failing tests that submit every action with its scene properties through `validatePayload(payloadForEvent("feature used", properties))`, asserting no rejections and preserved bounded values.
- [x] Add rejection tests for marker text/coordinates, invalid enum values, wrong boolean types, scene properties on other features, and scene pseudonyms.
- [x] Extend property/action/feature allowlists and event-specific sets, enforce the scene-only contract, raise `CONTRACT_REVISION` to 6.
- [x] Run `node --test infra/terraform/stacks/thebasics-analytics-relay/worker/analytics-relay.test.mjs`, including source-derived producer compatibility tests.

## Task 3: Reporting, integration and review

Files: `docs/analytics/scene-markers.md`, query definitions adjacent to that document, and the QA handoff.

- [x] Record exact installation-based metric definitions and ready-to-run queries for weekly adoption, action volume, display modes, and consecutive-week usage. Include opt-in bias and partial-week limitations.
- [x] Run the full C# suite and Worker suite together after both implementations land.
- [x] Build/package using the repository script with an isolated local destination and no SFTP credentials.
- [x] Review the complete diff for gameplay regressions, telemetry failure isolation, client spam, consent changes, and contract drift. Address findings and rerun affected checks.
- [x] Prepare simple numbered manual QA cards and rollout order; leave deployment, merge and release pending owner approval.

## Verification outcome

September 14, 2026: 865 C# tests and 20 relay tests passed. Build/package completed with zero compiler warnings or errors and no uploads. Independent review findings were fixed; held-reader cooldown was explicitly narrowed to per viewer across held items and documented. Reporting query definitions remain unpublished and require live validation after instrumented events arrive. No manual QA, merge, Worker deployment or release was performed.

The local archive retains the source branch's 5.9.1 metadata and is only a packaging validation artifact. It must be versioned for the next prerelease before QA distribution. SHA256: 2BE95399F4F947B2F4A7E1CB7ED0BB04C489E7AAF56F144BC9B3965BA3BB7659.
