# DimensionLib

DimensionLib is an experimental shared code mod for finite dimensions backed by Vintage Story dimension planes.

The goal is to give other mods a stable, boring API for alternate spaces without each mod rediscovering dimension chunk creation, relighting, force-send behavior, teleport ordering, and projection boundaries.

## Current Scope

- Reserve dimension plane `3` for the first prototype plane.
- Register finite dimensions with owner metadata and automatic sparse backing allocation.
- Create, relight, and force-send backing chunk columns.
- Register generator profiles and prepare generated dimensions.
- Materialize blocks from an `IBlockVolumeSource` into a dimension.
- Move a player into a dimension or to an explicit captured location.
- Apply basic client ambience, fog, sky cover, minimum scene light, and experimental generated light floors while inside generated dimensions.

## Mod Author API

Reference `dimensionlib.dll` at compile time and declare a dependency on mod id `dimensionlib`. At runtime, resolve the API from `api.ObjectCache["dimensionlib:api"]` or by getting `DimensionLibModSystem` from the mod loader.

The main contract is `IDimensionLibApi`:

- `RegisterDimension(DimensionSpec spec)` claims a finite dimension. Normal specs are automatically assigned far-apart sparse backing chunks.
- `PrepareDimension(dimensionId, source, player)` creates columns, optionally materializes blocks from an `IBlockVolumeSource`, relights, and can force-send the result.
- `RegisterGenerator(generator)` registers an `IDimensionGenerator` that can create an `IBlockVolumeSource` for a dimension.
- `PrepareGeneratedDimension(dimensionId, player)` prepares a dimension from its `GeneratorId`.
- `TeleportToDimension(player, dimensionId, options)` moves a player into a prepared dimension and can record a return point.
- `CaptureLocation(player)` captures the player's current engine position and optional visible DimensionLib id.
- `TeleportToLocation(player, location)` moves a player to an explicit captured location and syncs DimensionLib visuals when applicable.
- `ReturnPlayer(player)` returns the player to the last recorded origin and remains a transitional helper for simple debug flows.
- `ForceSendDimension(dimensionId, player)` sends the dimension's chunk columns without teleporting.
- `RegisterPolicyProvider(ownerModId, provider)` lets an owner mod enforce richer `OwnerOnly` entry/use/mutation rules.
- `ReleaseDimension(dimensionId, mode)` marks a dimension orphaned, forgets the manifest record, or clears blocks and forgets it.

Projection consumers such as replay systems should implement `IBlockVolumeSource` and keep their own storage/query logic. DimensionLib asks the source to fill a local chunk column through an `IChunkColumnWriter`; it does not own replay timestamps, diffs, retention, or event overlays.

DimensionLib persists the dimension manifest to `ModData/dimensionlib/regions.json`. It persists claims and policy metadata, not generated terrain recipes, replay timelines, or owner-mod state.

The current protection layer blocks normal player block placement, breaking, and block use in read-only or inaccessible dimensions through public server events. Root users bypass DimensionLib's generic checks, while owner policy providers can still veto block use or mutation for product invariants. Non-player world mutations are not fully sandboxed yet.

See `docs/API.md` for the contributor-facing API boundary, current limits, and projection guidance. See `docs/PRODUCT_DIRECTION.md` before promoting gameplay-facing features into DimensionLib. See `docs/SUBSYSTEMS_AND_REFACTOR.md` before changing DimensionLib internals or nether visual/generator behavior.

The repo also includes `mods-dll/dimensionpockets`, the Pocket Dimensions consumer mod. It uses only the public `IDimensionLibApi` surface to create, prepare, enter, inspect, capture explicit return locations for, and release pocket dimensions. Treat it as the first copyable integration example before promoting new convenience helpers into DimensionLib core.

Example registration:

```csharp
var dimensionLib = (IDimensionLibApi)api.ObjectCache["dimensionlib:api"];
var registered = dimensionLib.RegisterDimension(new DimensionSpec
{
    DimensionId = "mymod:first-pocket",
    OwnerModId = "mymod",
    DimensionPlaneId = dimensionLib.PrimaryDimensionPlaneId,
    ChunkSizeX = 3,
    ChunkSizeZ = 3,
    SpawnY = 90,
    GeneratorId = DimensionGeneratorIds.OverworldOpposite,
    VisualProfileId = DimensionVisualProfileIds.OppositeDay,
    MinimumSceneLight = 0.0f,
    Seed = 12345,
    Kind = DimensionKind.Pocket,
    AccessPolicy = DimensionAccessPolicy.OwnerOnly,
    Mutability = DimensionMutability.Mutable,
});
```

Set `Placement = DimensionPlacement.Explicit` and provide `ChunkX`/`ChunkZ` only for debug fixtures or mods with a deliberate backing-region layout.

## Debug Commands

Debug commands require `root` privilege:

- `/dlib prepare-spike`
- `/dlib enter-spike`
- `/dlib exit-spike`
- `/dlib create-test <overworld-opposite|nether-cavern> [dimensionId] [sizeChunks] [seed]`
- `/dlib generators`
- `/dlib prepare <dimensionId>`
- `/dlib send <dimensionId>`
- `/dlib enter <dimensionId>`
- `/dlib enter-player <playerName> <dimensionId>`
- `/dlib tp <dimensionId|overworld> [x y z]`
- `/dlib tp-player <playerName> <dimensionId|overworld> [x y z]`
- `/dlib exit`
- `/dlib list`
- `/dlib inspect`
- `/dlib validate [dimensionId]`
- `/dlib visual reset`
- `/dlib visual preset <clear|thin|default>`
- `/dlib visual set <key> <value>`
- `/dlib light-floor <dimensionId> <level>`
- `/dlib release <dimensionId> [orphan|forget|clear] confirm`

The alias `/dimensionlib` is also registered. The `tp` commands use absolute engine coordinates. Omit `x y z` to use the target's default spawn (`overworld`) or DimensionLib dimension spawn.

## Smoke Test Plan

These steps require a server with DimensionLib loaded and a root/admin player:

1. Run `/dlib prepare-spike`.
2. Run `/dlib list` and confirm `dimensionlib:debug-spike` is registered, prepared, and not orphaned.
3. Run `/dlib enter-spike` and confirm the player arrives on the generated platform in dimension plane `3`.
4. Run `/dlib inspect` inside the dimension and confirm the reported bounds and policy match the debug dimension.
5. Place and break a block as root to confirm admin interaction still works in the mutable debug dimension.
6. Run `/dlib exit-spike` and confirm the player returns to the recorded origin.
7. Run `/dlib release dimensionlib:debug-spike orphan confirm` and confirm `/dlib list` marks it orphaned until the mod re-registers it.
8. Run `/dlib create-test overworld-opposite`, then `/dlib enter dimensionlib:test-overworld-opposite`, and confirm rolling terrain with a darker opposite-day ambience.
9. Run `/dlib create-test nether-cavern`, then `/dlib enter dimensionlib:test-nether-cavern`, and confirm a generated cavern with floor and ceiling terrain, an opaque red sky cover, and readable low-light areas.
10. Run `/dlib validate` inside each generated dimension and confirm `spawnFeetBlockId=0`, `spawnHeadBlockId=0`, and a nonzero `spawnFloorBlockId`.
11. If nether readability is poor, use `/dlib visual preset clear`, `/dlib visual preset thin`, and exact-key `/dlib visual set <key> <value>` commands to tune fog, sky cover, ambient color, and client-only light lift live without regenerating terrain.
12. For sealed cavern terrain-lighting experiments, use `/dlib light-floor dimensionlib:test-nether-cavern <level>` to write a low blocklight floor into air cells and resend prepared chunks. The built-in nether-cavern profile also applies a synthetic sunlight floor in the cavern's vertical band during generation. These are not generated light blocks, but they do intentionally affect chunk light-level data.
13. From a dedicated-server console, use `/dlib create-test <type> ...` followed by `/dlib enter-player <playerName> <dimensionId>` for non-interactive QA clients.
