# Scene analytics producer handoff

Implemented in `.codex-worktrees/scene-marker-analytics`, September 14, 2026. No commits, pushes, deployments, or manual game QA performed.

## Changes

- `mods-dll/thebasics/src/ModSystems/SceneDescriptions/SceneAnalytics.cs`: bounded common properties, save classification, viewport/opacity test, bounded dwell and cooldown state.
- `SceneAnalyticsObserver.cs` in that directory: dedicated client/server channel, one-second permission heartbeat with a 2.5-second client lease, authoritative consent checks, held/placed reader validation, dimension and loaded-block checks, server-derived scene properties, local-only dedupe keys.
- `SceneDescriptionSystem.cs`: observer lifecycle.
- `SceneDescriptionBlock.cs`: accepted placements and written reused placements, successful reader TryOpen acknowledgements, confirmed player removal.
- `SceneDescriptionBlockEntity.cs`: accepted saves and actual read-state transitions. Existing gameplay permission/distance behavior preserved. Read telemetry separately requires matching dimension and eight-block proximity.
- `SceneMarkerIconRenderer.cs`: observation only after successful visible bubble rendering, with opacity at least 0.5 and center inside viewport. Existing line-of-sight and visibility gates run first.
- `mods-dll/thebasics/src/Utilities/Network/SafeClientNetworkChannel.cs`: non-queuing ephemeral send method. Failed/disconnected observations are discarded.
- `AnalyticsService.cs` adds `enable_scene_markers`; `RelayAnalyticsSink.cs` requires relay contract revision 6.

## Validation

Full C# test suite passes: 865 tests, 0 failures, 0 skipped, versus baseline 838. Command used `VINTAGE_STORY=D:/Games/Vintagestory` and `D:/bench/vs/.dotnet/dotnet.exe test mods-dll/thebasics.Tests/thebasics.Tests.csproj -c Release -p:SkipPostBuildPackage=true --no-restore --verbosity quiet`. Initial restore required approved network escalation. A red compile test for the missing non-queuing send method preceded its implementation; the first general helper test attempt was blocked by restore and did not produce a behavioral red result.

Added tests in `SceneAnalyticsTests.cs` cover property minimization, dwell gaps and cooldown, bounded state, save classification, non-queued disconnected observations, viewport/opacity, server bubble visibility, and reader range. Added server tests cover accepted/denied saves, consent-disabled saves, and duplicate read transitions. `SceneAnalyticsObserverTests.cs` checks client lease absence/expiry/revocation and duplicate opens, server unloaded/dimension/range rejection, per-viewer cooldown, revoked consent, and distinct held items intentionally sharing one throttle. Callback exceptions are isolated; moved events retain placement metadata; feature-disabled removals do not count.

## Limits and interpretation

- Reader/bubble events remain client observations, with server plausibility validation. They are not proof of attention or comprehension. Renderer execution and GUI networking still need manual game QA.
- Bubble validation follows configured distance and targeted visibility. It does not impose the reader's eight-block distance on AlwaysNearby bubbles, which support wider or unlimited visibility.
- Reader requests require a loaded marker, same dimension, and eight-block distance. Held reader checks the active held marker with written body, and dedupes all held readers for that player for 30 seconds rather than assigning item IDs.
- Local client/server dedupe state is capped at 4096 entries. Eviction can permit another observation before cooldown under extreme distinct-marker churn. State and consent clear on disposal/disabled heartbeat; no identifiers enter exported properties.
- A consent revocation immediately rejects all server events. Clients stop reporting on the next permission heartbeat, or lease expiration if heartbeats stop. Their observations cannot queue for later consent.
- Written reused placements include copied markers. There is no move linkage or exported marker identifier.
- Passive dwell is one second with gaps at most 250 ms; view cooldown is 60 seconds. Reader cooldown is 30 seconds.
- `scene_body_shown` describes the server's ShowBodyInBubble setting, not body content or a guarantee that body text was read.
