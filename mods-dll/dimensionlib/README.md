# DimensionLib

DimensionLib is an experimental shared code mod for finite dimensions backed by Vintage Story dimension planes.

The goal is to give other mods a stable, boring API for alternate spaces without each mod rediscovering dimension chunk creation, relighting, force-send behavior, teleport ordering, and projection boundaries.

## Current Scope

- Reserve dimension plane `3` for the first prototype plane.
- Register finite dimensions with owner metadata and automatic sparse backing allocation.
- Register owner-defined mappings between dimensions for paired spaces, scaled travel, and cohesive multi-dimension structures.
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
- `RegisterMapping(spec)` registers a source/target dimension coordinate mapping owned by a consumer mod.
- `ResolveMappedLocation(location, mappingId, options)` resolves where a mapping would send a location without teleporting; consumers use this for validation, previews, and UI.
- `ResolveLocalPosition(location)` converts backing coordinates into effective local coordinates inside a registered DimensionLib dimension.
- `TeleportAcrossMapping(player, mappingId, options)` maps the player's local position to the paired dimension, applies an optional destination-local offset, validates the target, and transfers the player.
- `ReturnPlayer(player)` returns the player to the last recorded origin and remains a transitional helper for simple debug flows.
- `RegisterPolicyProvider(ownerModId, provider)` lets an owner mod enforce richer `OwnerOnly` entry/use/mutation rules.
- `ReleaseDimension(dimensionId, mode)` marks a dimension orphaned, forgets the manifest record, or clears blocks and forgets it.

Projection consumers such as replay systems should implement `IBlockVolumeSource` and keep their own storage/query logic. DimensionLib asks the source to fill a local chunk column through an `IChunkColumnWriter`; it does not own replay timestamps, diffs, retention, or event overlays.

DimensionLib persists the dimension manifest to `ModData/dimensionlib/regions.json`. It persists dimension claims, policy metadata, and non-transient mappings between non-transient dimensions. It does not persist generated terrain recipes, replay timelines, or owner-mod state.

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

New dimensions are assigned sparse backing coordinates automatically. Consumers should preserve the returned `Dimension` or ask DimensionLib for it again instead of choosing backing chunks directly.

Example mapped transfer:

```csharp
dimensionLib.RegisterMapping(new DimensionMappingSpec
{
    MappingId = "mymod:tower-present-past",
    OwnerModId = "mymod",
    SourceDimensionId = "mymod:tower-present",
    TargetDimensionId = "mymod:tower-past",
    Bidirectional = true,
    Transform = DimensionMappingTransform.Identity(),
});

dimensionLib.TeleportAcrossMapping(player, "mymod:tower-present-past", new DimensionMappingTeleportOptions
{
    OffsetX = 1,
});
```

Mapping transforms operate on local coordinates inside the source dimension. A transform scale such as `ScaleX = 0.125` and `ScaleZ = 0.125` can express Nether-style compressed travel, while the per-call offset is applied in destination-local coordinates after the transform.

Use `ResolveMappedLocation(...)` before transfer when consumer-owned rules need to inspect the target first, such as requiring a matching elevator block, validating two-block headroom, or building a bounded preview. Use `ResolveLocalPosition(...)` for client HUDs and product UI that should show effective coordinates instead of sparse backing coordinates.

Mappings are durable by default and are saved with the dimension manifest when both endpoint dimensions are durable. Set `DimensionMappingSpec.IsTransient = true` for QA, temporary, or per-session mappings that the owner mod will recreate as needed.

## Debug Commands

Debug commands require `root` privilege:

- `/dlib generators`
- `/dlib prepare <dimensionId>`
- `/dlib enter <dimensionId>`
- `/dlib enter-player <playerName> <dimensionId>`
- `/dlib tp <dimensionId|overworld> [x y z]`
- `/dlib tp-player <playerName> <dimensionId|overworld> [x y z]`
- `/dlib exit`
- `/dlib list`
- `/dlib inspect`
- `/dlib validate [dimensionId]`
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
8. For disposable dimensions only, run `/dlib release <dimensionId> orphan confirm` and confirm `/dlib list` marks it orphaned until the owner mod re-registers it.
