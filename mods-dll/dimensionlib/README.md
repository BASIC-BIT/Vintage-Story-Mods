# DimensionLib

DimensionLib is an experimental shared code mod for finite dimensions backed by Vintage Story dimension planes.

The goal is to give other mods a stable, boring API for alternate spaces without each mod rediscovering dimension chunk creation, relighting, force-send behavior, teleport ordering, and projection boundaries.

## Current Scope

- Reserve dimension plane `3` for the first prototype plane.
- Register finite dimensions with owner metadata and automatic sparse backing allocation.
- Create, relight, and force-send backing chunk columns.
- Register generator profiles and prepare generated dimensions.
- Materialize blocks from an `IBlockVolumeSource` into a dimension.
- Prototype `standard` overworld source windows for testing large overworld-like dimensions without owning vanilla generator code.
- Move a player into a dimension or to an explicit captured location.
- Apply basic client ambience, fog, sky cover, and minimum scene light while inside generated dimensions.

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

The current protection layer blocks normal player block placement, breaking, and block use in read-only or inaccessible dimensions through public server events. Root users bypass DimensionLib's generic and owner-provider checks; owner policy providers can veto block use or mutation for normal players to enforce product invariants. Non-player world mutations are not fully sandboxed yet.

See `docs/API.md` for the contributor-facing API boundary, current limits, and projection guidance. See `docs/PRODUCT_DIRECTION.md` before promoting gameplay-facing features into DimensionLib. See `docs/SUBSYSTEMS_AND_REFACTOR.md` before changing DimensionLib internals.

The repo also includes `mods-dll/dimensionpockets`, the Pocket Dimensions consumer mod, and `mods-dll/dimensioncavern`, a demo cavern generator. They use only the public `IDimensionLibApi` surface. Treat them as copyable integration examples before promoting new convenience helpers into DimensionLib core.

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
    VisualSettings = new DimensionVisualSettings
    {
        Fog = new DimensionFogVisualSettings
        {
            Color = new DimensionWeightedColor(new DimensionColor3(0.12f, 0.14f, 0.28f), 0.65f),
        },
    },
    AccessPolicy = DimensionAccessPolicy.OwnerOnly,
    Mutability = DimensionMutability.Mutable,
});
```

Set `Placement = DimensionPlacement.Explicit` and provide `ChunkX`/`ChunkZ` only for debug fixtures or mods with a deliberate backing-region layout.

## Debug Commands

Debug commands require `root` privilege:

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
- `/dlib visual status`
- `/dlib visual reset`
- `/dlib visual set <key> <value>`
- `/dlib release <dimensionId> [orphan|forget|clear] confirm`

The alias `/dimensionlib` is also registered. The `tp` commands use absolute engine coordinates. Omit `x y z` to use the target's default spawn (`overworld`) or DimensionLib dimension spawn.

## Smoke Test Plan

These steps require a server with DimensionLib loaded and a root/admin player:

1. With Pocket Dimensions loaded, run `/pocket create smoke 3` or use another consumer mod to register and prepare a disposable DimensionLib dimension.
2. Run `/dlib list` and confirm the dimension is registered, prepared, and not orphaned.
3. Run `/dlib enter <dimensionId>` and confirm the player arrives inside the consumer-created dimension.
4. Run `/dlib inspect` inside the dimension and confirm the reported bounds and policy match the consumer spec.
5. Run `/dlib validate` and confirm expected bounds, prepared chunk counts, and spawn block samples.
6. Run `/dlib tp overworld` and confirm the player can recover to the overworld spawn.
7. Run `/dlib tp <dimensionId>` without coordinates and confirm the player returns to the dimension spawn.
8. Run `/dlib visual status` and check the client log for the active visual state snapshot.
9. For disposable dimensions only, run `/dlib release <dimensionId> orphan confirm` and confirm `/dlib list` marks it orphaned until the owner mod re-registers it.
