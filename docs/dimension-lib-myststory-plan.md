# DimensionLib and MystStory Plan

Date: 2026-05-29
Worktree: `D:\bench\vs\work\dimension-lib-exploration`
Branch: `feature/dimension-lib-exploration`

## Verdict

DimensionLib is a reasonable and useful first layer, but it should be a normal Vintage Story universal code mod, not a bare DLL. Other mods can compile against its public API and declare a mod dependency on `dimensionlib`.

The reusable center is not Mystcraft-style page grammar. The reusable center is dimension lifecycle and projection infrastructure:

- Allocate safe dimension planes and backing rectangular DimensionRegions.
- Create, load, fill, relight, send, protect, and unload dimension chunk columns.
- Move players/entities safely between normal world and alternate dimensions.
- Provide client visual profiles for fog, ambient color, clouds, and later sky overrides.
- Materialize a read-only or mutable block volume from a generator, schematic, or replay snapshot source.

MystStory should sit on top of that. The replay/world-diff work should also sit on top of that, but only at the projection/materialization layer.

## Vintage Story Constraints

Important engine observations from the decompiled 1.22.1 source:

- Dimension is encoded as vertical offset: `EntityPos.InternalY = y + Dimension * 32768` in `EntityPos.cs`.
- Chunks are dimension-offset by `dimension * 1024` in `WorldMap.ChunkIndex3D(...)`.
- `IWorldManagerAPI.CreateChunkColumnForDimension(cx, cz, dim)` creates empty chunk columns for a dimension.
- `IWorldManagerAPI.LoadChunkColumnForDimension(cx, cz, dim)` loads dimension-specific chunk columns.
- `IWorldManagerAPI.ForceSendChunkColumn(player, cx, cz, dimension)` force-sends loaded dimension chunks.
- `EntityPlayer.ChangeDimension(dim)` changes player dimension and updates entity chunk. `TeleportTo(...)` alone does not carry a dimension parameter.
- Dimension `1` is reserved for mini-dimensions. Dimension `2` is used by vanilla lore/alt-world code. Dimension `0` is the normal world.
- Save-game chunk index encoding appears to preserve 10 bits of dimension ID in `ChunkPos.ToChunkIndex(...)`, so use `3..1023` unless an experiment proves higher IDs are safe.
- Standard worldgen callbacks are not dimension-aware: `IChunkColumnGenerateRequest` exposes chunks/X/Z/params but no dimension.
- Runtime natural spawning skips players outside dimension `0` in `ServerSystemEntitySpawner`.
- Climate hooks receive `BlockPos`, so dimension-aware climate overrides are possible.
- Weather, calendar, map regions, and standard map metadata are mostly global or X/Z-based, not per dimension.

Implication: DimensionLib should not pretend Vintage Story has first-class independent worlds. It should provide controlled finite Dimensions backed by regions inside dimension planes.

## Core Model

### Dimension Plane

A loaded VS dimension ID, usually one of a small reserved set. Avoid one dimension ID per pocket world because practical save encoding and compatibility favor a limited dimension range.

Suggested initial reservation:

- `0`: vanilla normal world.
- `1`: vanilla mini-dimensions.
- `2`: vanilla alt-world/lore dimension.
- `3`: DimensionLib primary pocket/projection plane.
- `4..15`: reserved for future DimensionLib planes or explicit consuming mods.
- `16..1023`: opt-in advanced allocation after compatibility testing.

### Dimension

A user-facing bounded alternate space. It has a stable Dimension ID, owner mod ID, generation/profile metadata, protection policy, lifecycle state, and a backing rectangular chunk allocation.

This is the scalable abstraction. MystStory Ages, replay projections, Nether-like mods, dungeons, and test spaces can all be Dimensions without consuming one engine dimension ID each.

### DimensionRegion

A backing rectangular chunk-area allocation inside a DimensionPlane. Multiple DimensionLib Dimensions can share one DimensionPlane as long as their DimensionRegions do not overlap.

### Projection

A Dimension whose block data is materialized from an external source rather than owned as ordinary terrain. Replay/world-diff should use this.

Projection sources:

- Generator source: procedural terrain.
- Schematic source: static structure/world copy.
- Snapshot source: reconstruct blocks from a timeline state.
- Diff source: reconstruct blocks by applying deltas to a base snapshot.
- Hybrid source: generated terrain plus overlay/diff.

### Visual Profile

A client-side profile applied while the local player is in a registered Dimension.

Initial scope:

- Fog color/density.
- Ambient color.
- Cloud density/brightness.
- Weather attenuation or override hooks.
- Temporary live tuning commands for fog, sky cover, ambient color, scene brightness, and client-only light lift.

Later scope:

- Sky texture/sun/moon/celestial rendering.
- Per-dimension soundscape/music.
- Per-dimension particle ambience.
- Dimension editor GUI: a live in-game visual/world editor with sliders for profile values, immediate preview, preset save/load, and eventual terrain/generator parameter editing.

Current nether-cavern visual hypotheses:

- Fog density and flat fog may be too high for a sealed cavern and may hide surface detail before the player can evaluate block palette or terrain shape.
- Fog color/weight may be too red, turning terrain into silhouettes even if actual brightness is adequate.
- The sky cover may need to behave like background ambience rather than a heavy red fill; color, alpha/blend, and render order should be tested independently.
- Client-only light lift can raise blacks but cannot recreate texture information lost in chunk lighting, so it should remain a debug/tuning variable rather than the primary solution.
- Live tuning on 2026-05-30 showed that clearing fog/redness improves the background, but terrain surfaces remain mostly black; neutral post-process lift and strong ambient/scene-brightness modifiers also do not recover enough surface detail. This points at chunk lightmap/terrain shader inputs rather than fog alone.
- Next experiment: non-block ambient light floor written directly into chunk light data for generated cavern air cells. This avoids generated light blocks entirely, but it still affects light-level data and should be modeled as an explicit Dimension ambience rule if kept.
- 2026-05-30 result: lightmap floor level `8` plus thin warm visuals produced readable nether terrain. Baked visual defaults: sky `(0.16, 0.018, 0.004, alpha 0.62)`, fog color `(0.22, 0.045, 0.014)` at weight `0.05`, fog density `0.00025` at weight `0.02`, ambient `(0.70, 0.24, 0.10)` at weight `0.35`.
- Generated light blocks are not an acceptable default solution. They should only be revisited if DimensionLib can model them as true ambient world features: dynamically fitted to rooms, non-interactable, non-colliding, non-dropping, and isolated from gameplay/mod side effects.

## Proposed API Boundaries

### DimensionLib Owns

- `DimensionRegistry`: persistent registry of Dimensions, backing regions, and claims.
- `DimensionAllocator`: assigns backing regions and optionally dimension planes.
- `DimensionLifecycle`: create/load/unload/clear/relight/send chunk columns.
- `DimensionTeleporter`: safe player/entity transfer with collision checks and fallback exits.
- `DimensionMaterializer`: fills blocks from a generator/projection source.
- `DimensionProtection`: read-only, mutable, owner-only, admin-only, or public policies.
- `DimensionVisualSystem`: client profile application on DimensionLib dimension changes.
- `IDimensionGenerator`: custom terrain/features/ores/entity placement pipeline.
- `IBlockVolumeSource`: abstract source used by replay/diff and schematic previews.
- `IDimensionLibApi`: public service interface retrieved by dependent mods.

### DimensionLib Should Not Own

- MystStory page grammar, books, progression, research, or instability content.
- Replay timeline storage, diff format, or incident-replay UI.
- Specific Nether/Mystcraft/Thaumcraft clone content.
- Broad compatibility hacks for every mod. It should expose explicit integration points first.

### Contributor API Principles

- Make the common path obvious: register a dimension, prepare it, materialize optional blocks, send chunks, teleport, return.
- Use stable namespaced IDs (`myststory:age-123`, `thebasics:replay-preview-session`) so consumers can reason about ownership and persistence.
- Return structured `DimensionLibResult` values for expected failures instead of making consuming mods catch ordinary validation exceptions.
- Keep fuzzy/gameplay interpretation in consuming mods. DimensionLib should only validate explicit dimension specs, dimension-plane choices, bounds, materialization calls, and transfer requests.
- Prefer small interfaces that expose mechanics, not policy: `IBlockVolumeSource` answers block materialization; replay decides what timestamp means and MystStory decides what pages mean.
- Document current limits near the API. A small honest API is better than a broad surface that implies more protection or block-entity fidelity than exists.
- Preserve future compatibility by treating dimension metadata (`Kind`, `AccessPolicy`, `Mutability`, `IsTransient`) as stable policy concepts that can gain deeper enforcement over time.

## Replay/World-Diff Integration

The replay system should not need to know about MystStory, and DimensionLib should not need to know how replay stores snapshots.

Clean seam:

```csharp
public interface IBlockVolumeSource
{
    string SourceId { get; }
    BlockVolumeBounds Bounds { get; }
    void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token);
}
```

Replay supplies an implementation backed by its timeline state. DimensionLib materializes that source into a projection Dimension, force-sends it, and optionally marks it read-only.

Guidance for the replay agent:

- Keep snapshot/diff storage independent from DimensionLib.
- Add a projection adapter that can answer block data for a local chunk column at a requested timestamp.
- Do not require replay snapshots to be full copies. DimensionLib should accept either full snapshots or reconstructed diff-applied views.
- Use a read-only projection policy by default so players cannot mutate historical views.
- If historical entities are needed, model them separately from block chunk materialization.

Current incident-replay branch facts from `D:\bench\vs\work\incident-replay`:

- Branch/status: `feature/incident-replay`, behind `origin/main` by 1 commit, with uncommitted incident replay work under `mods-dll/thebasics/src/ModSystems/IncidentReplay/` and tests under `mods-dll/thebasics.Tests/ModSystems/IncidentReplay/`.
- Storage root: per-savegame `ModData/thebasics/incident-replay/<savegame>/incident-replay.db`, plus compressed world-state blobs under `world-state/`.
- Schema: current replay store schema is `5`; world snapshot schema is `1`.
- Evidence stream: `events` table stores timestamped replay events with position, dimension, yaw/pitch, vitals, marker color, and computed 3D chunk coordinates.
- World-state stream: `world_block_diffs`, `world_chunk_snapshots`, and `world_chunk_coverage` tables store actor/admin block diffs, compressed chunk snapshots, and coverage metadata.
- Snapshot format: `IncidentReplayWorldChunkSnapshot.Blocks` serializes non-air `IncidentReplayWorldBlockState` records to JSON, compresses with gzip, hashes with SHA-256, and stores as external `*.json.gz` blobs.
- Diff format: `IncidentReplayWorldBlockDiff` stores timestamp, dimension, absolute block XYZ, 3D chunk XYZ, category, actor/correlation metadata, before/after block codes, and before/after block-entity blob/hash fields.
- Reconstruction API: `IncidentReplayStore.ReconstructWorldChunkState(dimension, chunkX, chunkY, chunkZ, timestampMs)` loads the nearest snapshot, then applies forward or reverse ordered diffs to return a dictionary of `IncidentReplayWorldBlockState` keyed by dimension/block coordinates.
- Current preview: `/ireplay inspect [secondsAgo] [radius]` is visual-only. It creates an `IMiniDimension`, starts from current terrain, applies captured diffs in reverse, populates the mini-dimension, and sends `IncidentReplayInspectionPreviewMessage` to the staff client. Staff are not teleported and live world state is not modified.
- Config boundary: `EnableIncidentReplay=false` by default; world-state capture is separately gated by `EnableIncidentReplayWorldStateCapture=false`. Actor world changes default true once world-state capture is enabled; natural changes default false.
- Test coverage: store tests cover event query filtering, chunk-coordinate backfill, world-state table/index creation, diff bound filtering, compressed snapshot storage, and forward/backward chunk reconstruction.
- Build status: replay branch not built here; the machine now has .NET SDK `10.0.300`, and the DimensionLib scaffold builds successfully against `net10.0`.

DimensionLib integration seam implied by replay:

- Replay can implement `IBlockVolumeSource` by adapting `ReconstructWorldChunkState(...)` for each requested 3D chunk section or projected column.
- DimensionLib should accept block states keyed by absolute source coordinates and map them into local Dimension coordinates during materialization.
- Projection should remain read-only by default and must not reuse replay storage, retention, or query policy.
- Replay actors, paths, event dots, and action logs should remain a separate overlay/ghost stream layered onto the projected blocks.

## MystStory Direction

MystStory should consume DimensionLib as a content mod.

Core concepts:

- `DescriptiveBook`: owns a stable Age ID, page list, author metadata, seed, and link target.
- `LinkingBook`: stores a return point or arbitrary target.
- `Page`: item carrying a symbol definition ID.
- `SymbolDefinition`: data-driven definition of a world characteristic.
- `ModifierPage`: modifies the next compatible core page or current grammar context.
- `CorePage`: contributes a required or optional world component.
- `AgeProfile`: collapsed result of resolving pages, random fill-ins, constraints, and instability.

Recommended symbol categories:

- Terrain: islands, caverns, flatlands, ridges, floating shelves.
- Surface: rock/soil palette, water/fluid palette, strata, vegetation density.
- Climate: temperature/rainfall profile, wind, season bias.
- Sky/visual: fog color, cloud density, ambient color, brightness.
- Celestial/time: day locked, night locked, accelerated time. This needs later client work.
- Structures: ruins, obelisks, towers, dungeons, portals.
- Resources: ore profile, rare deposits, hostile abundance. Be conservative for server economy.
- Rules: PvP, spawn behavior, decay/instability, read-only, no-return hazard.

Mechanic inspiration from Mystcraft/RFTools Dimensions:

- Mystcraft has symbol pages that register logic into an `AgeDirector`; missing critical controllers are filled randomly and instability is added.
- Mystcraft separates pages from collapsed symbol lists and stores per-Age data with seed, pages, symbols, instability, spawn, visited/dead flags.
- RFTools Dimensions uses compact descriptors made of typed dimlets, then randomizes missing categories like terrain, features, biome controller, and time.
- Both imply a good Vintage Story design: data-driven typed symbols, deterministic collapse, explicit defaults, and instability/cost for missing or conflicting choices.

License note: use these mods for design inspiration, not code copying. Mystcraft Legacy is LGPL-3.0 and RFTools Dimensions is MIT, but we should still implement original C# systems around VS APIs.

## Compatibility Strategy

Prefer these integration points before Harmony patches:

- Public API service and events for Dimension creation, materialization, visual profiles, and teleport lifecycle.
- Explicit Dimension and backing-region ownership metadata.
- Claims/protection integration through VS land claim APIs or DimensionLib's own block-access policy.
- Client `PlayerDimensionChanged` and Dimension detection hooks for visuals.
- Custom spawner API for nonzero dimensions because vanilla natural spawning ignores them.
- Optional adapters for popular mods after compatibility research.

Avoid:

- Patching vanilla worldgen to pretend `IChunkColumnGenerateRequest` has a dimension.
- One dimension ID per user-created Age.
- Mutating global weather/calendar state for one player in a dimension.
- Letting replay and MystStory share storage formats just because both use DimensionLib.

Likely necessary patches later:

- Block mutation guard for read-only projection dimensions if accessor-level events are insufficient.
- Client sky/celestial overrides beyond ambient/fog.
- Entity spawn/load behavior for dimension-specific custom mobs.

## First Milestones

### M0: Spike

Implement a temporary debug command in a `dimensionlib` code mod:

- Allocate a 3x3 chunk debug Dimension in dimension plane `3`.
- Create columns, fill with a simple generated platform/terrain, relight, and force-send.
- Switch the player to dimension `3` at a safe spawn.
- Switch back to the recorded return position.
- Apply a basic client ambient/fog profile while inside the Dimension.
- Validate server restart persistence.

Current scaffold state:

- Added `mods-dll/dimensionlib` as a universal SDK-style code mod.
- Added root-only debug commands: `/dlib prepare-spike`, `/dlib enter-spike`, `/dlib exit-spike`, with `/dimensionlib` alias.
- Added a temporary 3x3 chunk Dimension in dimension plane `3` at chunk `0,0`, with a generated platform and spawn at Y `92`.
- Added a public API/model surface: `IDimensionLibApi`, `DimensionSpec`, `Dimension`, `DimensionLibResult`, `DimensionTeleportOptions`, `BlockVolumeBounds`, `IBlockVolumeSource`, and `IChunkColumnWriter`.
- Added Dimension registration with duplicate-ID and backing-region overlap validation.
- Added versioned JSON manifest persistence under `ModData/dimensionlib/regions.json`.
- Added transient-dimension orphan handling and release modes: `MarkOrphaned`, `ForgetOnly`, `ClearBlocksAndForget`.
- Added first-layer protection through `CanPlaceOrBreakBlock`, `CanUseBlock`, and `BreakBlock` hooks.
- Added owner policy provider registration via `IDimensionPolicyProvider`.
- Added prepared-dimension tracking, force-send, teleport, return, and dimension lookup by `BlockPos`.
- Added `/dlib list`, `/dlib inspect`, and `/dlib release <dimensionId> [orphan|forget|clear] confirm`.
- Added `mods-dll/dimensionlib/docs/API.md` as the first contributor-facing API note.
- Added a client ambient/fog modifier while the local player is in dimension `3`.
- Added generator/profile fields (`GeneratorId`, `VisualProfileId`, `Seed`) to `DimensionSpec` and persisted dimensions.
- Added `IDimensionGenerator` and built-in test generators for `overworld-opposite` and `nether-cavern`.
- Added `/dlib create-test <type> [dimensionId] [sizeChunks] [seed]`, `/dlib generators`, `/dlib prepare <dimensionId>`, `/dlib send <dimensionId>`, `/dlib enter <dimensionId>`, `/dlib exit`, and `/dlib validate [dimensionId]` as the first creation lab.
- Added generator source-bounds validation and spawn block sampling so generated dimensions can be checked before manual visual QA.
- Wired `dimensionlib` into `Vintage-Story-Mods.sln`.

Verification note: .NET SDK `10.0.300` was installed with winget after the initial build blocker. `dotnet build mods-dll\dimensionlib\dimensionlib.csproj --configuration Release` now succeeds and creates `mods-dll/dimensionlib/dimensionlib_0_1_0.zip`.

### M1: DimensionLib API

Replace the debug-only spike with stable services:

- Dimension registry and storage.
- Generator/materializer interfaces.
- Safe teleporter.
- Client visual profile system.
- Read-only policy hook.

### M2: Replay Projection Consumer

- Add a replay adapter implementing `IBlockVolumeSource`.
- Materialize a selected timestamp into a read-only projection Dimension.
- Let a player enter/exit the projection.
- Validate that changing replay storage does not require DimensionLib changes.

### M3: MystStory Minimal Age

- Add books/pages/items.
- Collapse page list into an `AgeProfile`.
- Generate one small Age Dimension using a few terrain/surface/visual symbols.
- Add return linking and instability placeholder.

### M4: Compatibility Pass

- Inventory popular ModDB mods.
- Classify risk: worldgen, block entities, claims, entities/mobs, UI, weather/ambience, chunk storage.
- Add adapter APIs first, patches only when a concrete mod requires them.

## Prompt For Replay/Diff Agent

Use this prompt with the other agent:

```text
Another agent is designing a shared DimensionLib for Vintage Story pocket dimensions and replay projections. Please dump the current state of your instant replay/world-diff work in a format they can consume.

Include:
1. Worktree path, branch, current git status summary, and latest commit/base.
2. Files changed and a one-sentence purpose for each important file.
3. Current storage model: where snapshots/diffs live, whether chunks store full snapshots or deltas, serialization format, versioning, and cleanup strategy.
4. Timeline reconstruction model: how to answer "what block data existed at timestamp T for chunk column X/Z?"
5. Public/internal APIs you already introduced or expect to introduce.
6. Assumptions about fake-world rendering, read-only previews, chunk loading, or teleporting.
7. Build/test status and known failures.
8. Open questions or places where DimensionLib could provide an interface instead of replay owning it.
9. Any constraints I must not break while designing DimensionLib.

Please do not rewrite architecture in response unless you already know it is wrong. The goal is a factual state dump for cross-agent integration.
```

## Open Questions

1. Should DimensionLib ship as a public dependency for other modders from day one, or stay private/internal until the M1 API stabilizes?
2. Should MystStory be lore-neutral and server-configurable by default, or lean into a strong authored fantasy/sci-fi framing?
3. Should user-created Ages be persistent by default, or should early versions treat them as disposable/generated spaces until stability and cleanup are proven?
4. For replay projections, is the target experience “walk around an old version of the world” or “view ghost/overlay diffs near the live world”? Those map to different first implementations.
5. How much server economy risk is acceptable for resource-rich Ages? This affects ore/resource symbol defaults and instability/cost balancing.
