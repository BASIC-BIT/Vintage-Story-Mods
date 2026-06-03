# DimensionLib Subsystems And Refactor Plan

DimensionLib reached the point where cavern visual iteration was blocked by architecture. Too many unrelated pieces were coupled inside `DimensionLibServerService`, `DimensionVisualSystem`, and built-in generators. This document defines the subsystem boundaries before more tuning happens.

The refactor goal is not to redesign public API yet. The goal is to split internal responsibilities so each experiment can change one module at a time with explicit variables, logs, and human feedback.

## Rules For The Refactor

- Preserve `IDimensionLibApi` until a separate API-design pass says otherwise.
- Do not make baked visual or generator tuning changes during pure refactor commits.
- Keep `/dlib` behavior stable while moving command handling out of core service classes.
- Every extracted module needs a short responsibility comment or doc section.
- After any visual/generator/light experiment, update `VISUAL_EXPERIMENT_LOG.md` and pause for human feedback.
- Use fresh dimensions after changing generated chunks, chunk lighting experiments, or generator shape.

## Current Subsystems

### Public API And Models

Current files:

- `src/Api/IDimensionLibApi.cs`
- `src/Api/DimensionSpec.cs`
- `src/Api/Dimension.cs`
- `src/Api/IBlockVolumeSource.cs`
- `src/Api/IChunkColumnWriter.cs`
- `src/Api/IDimensionGenerator.cs`
- `src/Api/IDimensionPolicyProvider.cs`
- Enum/result files under `src/Api/`

Responsibilities:

- Stable consuming-mod contract.
- Dimension claim/spec model.
- Block-volume source and chunk-writer seams.
- Generator and policy-provider extension points.

Refactor stance:

- Keep this layer boring and small.
- Do not add content-specific cavern concepts here until they become stable cross-mod features.

### Mod Entry Point And Public Facade

Current file:

- `src/Core/DimensionLibModSystem.cs`

Responsibilities today:

- Vintage Story lifecycle hooks.
- Server/client network channel registration.
- Public `IDimensionLibApi` forwarding.
- Client transfer/tuning packet handling.
- Delegates `/dlib` and `/dimensionlib` command registration to `Commands/DimensionLibCommandRegistrar`.

Current split:

- `DimensionLibModSystem`: lifecycle, API facade, network registration only.
- `Commands/DimensionLibCommandRegistrar`: `/dlib` command tree, aliases, parsing, and console/player command adaptation.

Remaining possible split:

- `Network/DimensionClientMessageHandlers`: client packet routing to visual/session systems if client packet handling grows.

Why:

- Command parsing should not obscure lifecycle/API behavior.
- Console QA helpers and visual tuning commands are experimental and should be easy to remove or gate later.

### Registry, Claims, And Persistence

Current files:

- `src/Services/DimensionLibServerService.cs`
- `src/Services/DimensionRegistry.cs`
- `src/Services/DimensionManifestService.cs`
- `src/Services/DimensionSpecValidator.cs`
- `src/Services/DimensionRegionAllocator.cs`
- `src/Services/PreparedDimensionTracker.cs`
- `src/Services/DimensionRegionStore.cs`
- `src/Services/DimensionRegionManifest.cs`

Responsibilities today:

- Register dimensions.
- Validate claims and overlap.
- Keep in-memory dimensions, orphaned flags, and prepared chunks.
- Load/save manifest.
- Allocate simple test regions.

Current split:

- `Services/DimensionRegistry`: authoritative in-memory dimensions and orphan state.
- `Services/DimensionSpecValidator`: spec normalization and validation.
- `Services/DimensionRegionAllocator`: prototype non-overlap allocation.
- `Services/DimensionManifestService`: manifest load/save and transient/orphan handling.
- `Services/PreparedDimensionTracker`: prepared-dimension and partial prepared chunk-key ownership.
- `Services/DimensionLibServerService`: public API orchestration and save trigger owner.

Why:

- Registration and persistence are product-core mechanics; visual experiments should not be able to accidentally change them.
- Prepared-chunk state needs a single owner before lazy generation/performance bugs can be reasoned about.

### Preparation, Materialization, And Chunk Transport

Current files:

- `src/Services/DimensionLibServerService.cs`
- `src/Services/DimensionChunkService.cs`
- `src/Services/ChunkColumnMaterializer.cs`
- `src/Generation/GeneratedDimensionWindowPreparer.cs`
- `src/Generation/GeneratedDimensionStreamer.cs`
- `src/Generation/BlockVolumeSourceValidator.cs`
- `src/Services/BlockAccessorChunkColumnWriter.cs`

Responsibilities today:

- Create/load/relight/clear chunk columns.
- Materialize blocks from `IBlockVolumeSource`.
- Validate source bounds.
- Prepare generated chunk windows near players.
- Force-send prepared chunk columns to clients.

Current split:

- `Services/DimensionLibServerService`: orchestration for explicit preparation and lazy-generation tick scheduling.
- `Services/DimensionChunkService`: chunk create/load/send/relight/clear mechanics.
- `Services/ChunkColumnMaterializer`: `IBlockVolumeSource` -> `IChunkColumnWriter` loop.
- `Generation/BlockVolumeSourceValidator`: source bounds validation.
- `Generation/GeneratedDimensionWindowPreparer`: bounded generated-window preparation near players.
- `Generation/GeneratedDimensionStreamer`: lazy generated-column tick loop around online players.

Remaining possible split:

- `Transfer/DimensionJoinResyncService`: player-join resend/resync behavior.

Why:

- The 9x9 freeze and earlier full-height light-write freeze need isolated measurement around generation, relight, force-send, and client receipt.
- Materialization should not know about transfer policy, visual settings, or access control.

### Access Control And Protection

Current file:

- `src/Protection/PolicyProviderRegistry.cs`
- `src/Protection/DimensionAccessService.cs`
- `src/Protection/BlockInteractionProtectionAdapter.cs`
- `src/Services/DimensionLibServerService.cs`

Responsibilities today:

- `CanPlaceOrBreakBlock`, `CanUseBlock`, and `BreakBlock` event hooks.
- Access-policy checks.
- Owner policy-provider dispatch.

Current split:

- `Protection/DimensionAccessService`: enter/send eligibility.
- `Protection/BlockInteractionProtectionAdapter`: block use/mutation event adapter.
- `Protection/PolicyProviderRegistry`: owner-mod policy provider lookup.
- `Services/DimensionLibServerService`: event subscription lifecycle and public policy-provider registration.

Why:

- Protection is product-critical and should not be mixed with visual/generator experiments.

### Player Transfer And Session State

Current files:

- `src/Services/DimensionLibServerService.cs`
- `src/Transfer/DimensionTransferService.cs`
- `src/Transfer/ReturnPositionStore.cs`
- `src/Effects/TemporalStabilityGuard.cs`
- `src/Network/DimensionTransferMessage.cs`

Responsibilities today:

- Record return positions.
- Move entities between dimensions.
- Sync transfer and visual settings state to clients.
- Re-sync prepared dimensions on player join.
- Suppress server-side temporal stability loss.

Current split:

- `Transfer/DimensionTransferService`: teleport/move/sync packet orchestration.
- `Transfer/ReturnPositionStore`: in-memory and player moddata return positions.
- `Effects/TemporalStabilityGuard`: explicit opt-in suppression of temporal effects.
- `Services/DimensionLibServerService`: public teleport/return orchestration and player-join resend/resync behavior.

Remaining possible split:

- `Transfer/DimensionJoinResyncService`: player-join resend/resync behavior.

Why:

- Client visuals depend on transfer messages today, so transfer bugs can masquerade as visual bugs.
- Return-position behavior is reusable and should not live beside content-specific test code.

### Generator Registry And Streaming

Current files:

- `src/Generation/BuiltInBlockSource.cs`
- `src/Generation/Noise/ValueNoise2D.cs`
- `src/Generation/DimensionGeneratorRegistry.cs`
- `src/Generation/GeneratedDimensionWindowPreparer.cs`
- `src/Generation/GeneratedDimensionStreamer.cs`

Responsibilities today:

- Generator registration types.
- Shared value-noise helpers.
- Public generator registry and generic generated-dimension streaming for consumer-registered dimensions.
- Standard-overworld source-window preparation for consumers that deliberately use `DimensionGeneratorIds.StandardOverworldWindow`.

Remaining target split:

- Keep content-specific generators in consumer/demo mods, such as `mods-dll/dimensioncavern`.

Why:

- Cavern generator experiments changed too many variables per pass: floor shape, ceiling shape, lava, columns, sky exposure, spawn plateau, and lighting all moved together.
- Generator shape needs its own experiment series independent of client atmosphere.

### Client Visual Environment

Current file:

- `src/Services/DimensionVisualSystem.cs`

Responsibilities today:

- Active visual settings tracking.
- Ambient modifier creation.
- Opaque sky/background renderer.
- Minimum-scene-light overlay.
- Vanilla cave-fog suppression.
- Temporal visual suppression.
- Debug tuning key store and validation for live experiments.

Current split:

- `ClientVisuals/VisualSettingsMapper`: maps explicit settings into Vintage Story ambient/fog/sky/lift values.
- `ClientVisuals/AmbientModifierController`: ambient/fog/cloud/brightness modifier lifecycle.
- `ClientVisuals/ScreenColorOverlayRenderer`: shared opaque sky cover and post-composition lift renderer.
- `ClientVisuals/VanillaEffectSuppressor`: `blackfogincaves`, temporal instability, cloud policy.
- `ClientVisuals/VisualTuningState`: debug keys and validation.
- `Services/DimensionVisualSystem`: high-level coordinator only.

Why:

- We need to know whether “no fog” means ambient modifier not applied, fog shader not visible with current values, sky replacement dominating, or chunk lighting making fog irrelevant.
- Each renderer/effect must be toggled independently for experiments.

### QA And Experimentation

Current files:

- `docs/VISUAL_EXPERIMENT_LOG.md`
- External temp scripts under `C:\Users\steve\AppData\Local\Temp\opencode\`

Responsibilities today:

- Manual deploy/restart/capture loop.
- Screenshot capture.
- Human subjective feedback.

Target split:

- Keep temp scripts out of the product tree until stable.
- Add a repo script only after the variables and workflow stabilize.
- Use `VISUAL_EXPERIMENT_LOG.md` as the source of truth for visual attempts.

Why:

- Visual tuning is currently empirical. The log is required to prevent repeated untracked changes.

## Proposed Refactor Phases

### Phase 0: Freeze Visual Tuning

Status: active.

Work:

- Stop changing visual/generator defaults until the module split plan is reviewed.
- Keep current package only as a baseline, not as a claimed good result.
- Use the latest rejected attempt G as the control for future experiments.

Verification:

- Docs exist and link the pause gate.
- No new visual constants changed.

### Phase 1: Pure Documentation And Names

Work:

- Keep this subsystem map current.
- Add comments at future extraction seams if needed.
- Decide names for target services before moving code.

Verification:

- `dotnet build mods-dll\dimensionlib\dimensionlib.csproj --configuration Release`

### Phase 2: Server Service Decomposition Without Behavior Changes

Work order:

1. Extract `DimensionSpecValidator` and region overlap/allocation helpers. Done.
2. Extract prepared chunk key ownership. Done.
3. Extract `DimensionManifestService` around `DimensionRegionStore`. Done.
4. Extract `DimensionChunkService`, `ChunkColumnMaterializer`, and generated-window preparation. Done.
5. Remove baked light-floor debug tooling from DimensionLib core. Done.
6. Extract protection and policy-provider services. Done.
7. Extract transfer/return-position services. Done.
8. Extract generator registry and visual tuning broadcasting. Done.
9. Extract diagnostics and temporal-stability guard. Done.
10. Extract lazy generated-column streaming loop. Done.

Verification after each extraction:

- Build succeeds.
- Public `IDimensionLibApi` signatures unchanged.
- `/caverndemo create <fresh-id> 5 <seed>` still creates and teleports when the demo mod is loaded.
- Do not judge visuals during this phase except for obvious regressions.

### Phase 3: Client Visual Decomposition Without Behavior Changes

Work order:

1. Extract `VisualTuningState`. Done.
2. Replace the old named-profile registry with explicit `VisualSettingsMapper`. Done.
3. Extract `AmbientModifierController`. Done.
4. Extract shared screen-color overlay renderer. Done.
5. Extract `VanillaEffectSuppressor`. Done.

Verification after each extraction:

- Build succeeds.
- Client log shows `dimensionlib-skycover` shader loads.
- A transfer into a test dimension still activates the expected visual settings.

### Phase 4: Generator Decomposition Without Behavior Changes

Work order:

1. Split content-specific cavern generation out of DimensionLib into `mods-dll/dimensioncavern`. Done.
2. Extract shared noise helpers. Done.
3. Extract cavern generator parameters into a named config object. Done.
4. Add a generator-debug output path if needed so attempted shapes are documented before screenshot QA.

Verification:

- Build succeeds.
- Fresh dimensions with the same seed generate identical block layouts before and after extraction.

### Phase 5: Controlled Experiments

Work:

- Resume visual and generator experiments one variable at a time.
- First likely experiments after refactor:
  - Verify whether ambient fog is applied at all with an exaggerated debug fog-only profile.
  - Verify sky replacement independently from terrain lighting.
  - Verify generator enclosure independently from sky/fog.

Required pause:

- After each screenshot, ask for human subjective scoring before changing another variable.

## Immediate Open Questions

- Should `/dlib visual` tune one client only, all online clients, or require explicit target syntax?
- Should the Cavern Demo keep a custom spawn-light rule separate from broader cavern lighting?
- Should visual experiments get a minimal debug HUD/log line showing active profile and effective fog/light values?
