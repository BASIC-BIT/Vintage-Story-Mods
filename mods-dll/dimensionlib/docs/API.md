# DimensionLib API Notes

DimensionLib is intended to be a shared dependency for mods that need bounded alternate spaces. It should feel boring to consume: register a dimension, prepare it, materialize blocks if needed, send it to a player, and teleport when appropriate.

## Dependency Boundary

Consuming mods should own their gameplay concepts. DimensionLib owns the mechanics that are awkward to get right repeatedly.

DimensionLib owns:

- Dimension registration backed by finite rectangles in dimension planes.
- Chunk column create/load/relight/force-send operations.
- Materialization from caller-provided block sources.
- Generator profile registration and generated-dimension preparation.
- Safe location-based transfer into, out of, and between dimensions.
- Owner-registered coordinate mappings between prepared dimensions.
- Dimension metadata needed for protection, visual/environment profiles, and future persistence.

Consuming mods own:

- MystStory books, pages, symbols, instability, and progression.
- Replay timestamps, snapshot/diff storage, retention, and event overlays.
- Economy balance, loot rules, structures, and mobs.
- Whether a dimension is gameplay, preview, staff-only, temporary, or persistent.

## Core Flow

1. Resolve `IDimensionLibApi` after DimensionLib has loaded.
2. Create a `DimensionSpec` with a stable `DimensionId` and `OwnerModId`.
3. Register the spec with `RegisterDimension(spec)`. Normal specs are automatically assigned sparse backing space.
4. Call `PrepareDimension(...)` with an `IBlockVolumeSource`, or set `GeneratorId` and call `PrepareGeneratedDimension(...)`.
5. Call `CaptureLocation(...)` only if the consumer intentionally needs an explicit source point for a later transfer.
6. Call `TeleportToDimension(...)` when the player should enter.
7. Call `TeleportToLocation(...)` when the player should move to a captured source point, linked endpoint, or another explicit destination.
8. Register a `DimensionMappingSpec` when two dimensions should behave like mapped versions of one cohesive space.
9. Call `TeleportAcrossMapping(...)` from a consumer-owned item, block, hotkey, portal, or command when the player should move through that mapped relationship.
10. `ReturnPlayer(...)` remains a transitional prototype helper for simple debug flows. New product code should prefer explicit locations, mappings, and consumer-owned links.

`mods-dll/dimensionpockets/src/PocketDimensionModSystem.cs` is the current integration consumer and releasable product mod. It uses only public API calls, including `RegisterPolicyProvider`, `RegisterDimension`, `PrepareDimension`, `TeleportToDimension`, `TeleportToLocation`, `ReleaseDimension`, and dimension lookup helpers.

## Future Location API Direction

The long-term abstraction is location/link, not return. DimensionLib now exposes a small first explicit location primitive for player flows, and future work should extend that toward persistent named references and non-player endpoints.

Candidate concepts:

- A captured location: dimension plane, coordinates, yaw/pitch/roll, optional visible dimension id, and optional display name.
- A named location reference: an id that a consumer can persist for a Waystone, item, machine, or portal endpoint.
- A link: source location reference, destination location reference, owner mod id, permissions, and optional transfer traits.
- A transfer operation: teleport a player to a location, sync visuals for the destination, and optionally remember the source location for that actor or item.

Avoid broad API names that imply player-only behavior if the primitive could later support Waystones, pocket teleporter items, mechanical power transfer, item/fluid links, quantum chest endpoints, or generated-world machine anchors.

## Dimension Mappings

`DimensionMappingSpec` describes how local coordinates in one registered DimensionLib dimension map into another registered DimensionLib dimension. This is core infrastructure for paired spaces such as present/past rooms, floor stacks backed by separate dimensions, compressed-travel spaces, and other cases where multiple dimensions should feel spatially related.

Mappings are intentionally lore-neutral. DimensionLib does not create portal blocks, hotkeys, ability items, room content, recipes, sounds, or UI. Consumers own those triggers and call `TeleportAcrossMapping(...)` when their product rules say a transfer should happen.

The first mapping transform is scale plus offset:

- `ScaleX`, `ScaleY`, and `ScaleZ` map source-local coordinates into target-local coordinates. Identity mapping uses `1, 1, 1`.
- `OffsetX`, `OffsetY`, and `OffsetZ` on `DimensionMappingTransform` are persistent mapping offsets applied as part of the registered mapping.
- `DimensionMappingTeleportOptions.OffsetX/Y/Z` are per-call destination-local offsets applied after the mapping transform. Use these for portals or paired controls that should place the player beside the mathematically equivalent point.
- Bidirectional mappings use the inverse transform when the player starts in the target dimension.

`TeleportAcrossMapping(...)` resolves whether the player is in the mapping source or, for bidirectional mappings, the mapping target. It then validates that the destination is inside the mapped dimension, checks target access/prepared state, optionally requires a collision-free destination, and delegates to the normal location transfer path so chunk visibility and visuals stay synchronized.

Mappings are owner-registered runtime metadata. DimensionLib does not persist them in the region manifest; consumer mods should persist their own site/link definitions and register mappings after their dimensions are registered.

## Dimension IDs

Use stable namespaced IDs such as `myststory:age-0000123` or `thebasics:replay-preview-<session>`. DimensionLib treats duplicate registration with the same bounds as idempotent, but rejects duplicate IDs with different bounds or owners.

## Persistence

DimensionLib persists the dimension registry to `ModData/dimensionlib/regions.json`. It does not persist generated source definitions or replay timelines.

Persisted dimension metadata includes:

- Dimension id and owner mod id.
- Dimension plane id and backing chunk rectangle.
- Spawn Y and derived spawn X/Z.
- Generator id, explicit visual settings, and seed.
- Access policy, mutability, and transient flag.
- Orphaned state.

Transient dimensions are marked orphaned when loaded from disk. The owning mod reactivates them by registering the same dimension id and bounds during startup. Orphaned dimensions are retained in the manifest so admins can inspect or explicitly release them instead of DimensionLib silently forgetting chunks that may still exist on disk.

Release modes:

- `MarkOrphaned`: keep the manifest record, but block normal access and require owner re-registration.
- `ForgetOnly`: remove the manifest record without clearing dimension chunks.
- `ClearBlocksAndForget`: clear the dimension's blocks, relight it, and remove the manifest record.

## Region Allocation

`RegisterDimension(spec)` intentionally places normal new dimensions on a sparse grid with large gaps between backing chunk rectangles. It does not attempt dense fallback packing; if the sparse scan fails, the API returns `no-free-region` rather than silently placing unrelated dimensions next door.

New dimensions are assigned sparse backing coordinates automatically. `DimensionSpec.ChunkX` and `ChunkZ` are backing coordinates for persisted/idempotent re-registration, not a placement API for new consumer dimensions.

This spacing is a core DimensionLib behavior because adjacent backing rectangles can bleed visually and can involve online players in neighboring dimensions. Chunk unloading and cleanup are separate lifecycle concerns and should not be relied on to hide nearby dimensions.

## Protection

DimensionLib currently enforces the first layer of protection through server hooks:

- `CanPlaceOrBreakBlock` denies non-root player block changes in read-only, admin-only, owner-only-without-provider, or orphaned dimensions.
- `CanUseBlock` denies non-root block use in read-only dimensions and dimensions the player cannot enter.
- `BreakBlock` sets `EnumHandling.PreventDefault` as a fallback if a break reaches the break hook.
- `TeleportToDimension(...)` checks access policy before moving the player.

Root users bypass DimensionLib's generic entry/read-only checks and owner-provider checks. Owner policy providers can enforce product invariants such as unbreakable pocket floors for normal players. DimensionLib materialization also writes through the server block accessor and is not blocked by player interaction hooks.

Owner mods can register an `IDimensionPolicyProvider` for richer rules. DimensionLib calls the provider for `OwnerOnly` entry decisions and for extra mutable-dimension block-use/mutation checks. The provider does not override core read-only or orphaned-dimension protection for normal players.

## Visual Environment

`DimensionSpec.VisualSettings` carries explicit ambience, fog, cloud, sky-cover, light-lift, and cave-fog suppression settings. There are no public named visual presets; consumers set the fields they want. `DimensionVisualSettings.Scene.MinimumLight` adds a per-dimension post-process light lift for sealed or non-solar spaces. DimensionLib renders this as a client-only ambient lift after final composition because Vintage Story's built-in `minlight` shader uniform is an input black point, not an output brightness floor. It does not create physical block light, affect mobs, or change the saved chunk lightmap.

See `RENDER_EFFECTS.md` for current research notes on sky replacement, vanilla cave-fog suppression, sealed-dimension lighting, and possible custom depth-aware fog passes.

Use `VisualSettings = null` for normal Vintage Story visuals, or `Scene.MinimumLight = 0` inside explicit settings to avoid client-side light lift. Prefer explicit visual tuning before generated light blocks. Generated light sources should only be considered if they are true ambient world features: dynamically fitted to the room, non-interactable, and isolated from gameplay/mod interactions.

Content-specific cavern visual hypotheses now live in the Cavern Dimension Demo mod. DimensionLib keeps only the reusable visual settings fields and transfer/apply mechanics.

- Fog and flat fog should remain conservative until sealed-dimension lighting is understood.
- Baked chunk-light floors are suspected workaround code and are not part of DimensionLib core.
- Client-only post-process light lift can raise blacks but cannot recover texture detail that chunk lighting rendered too dark.
- Generated block emitters are not acceptable unless they are non-interactive, room-fitted environmental features.

Demo content may set minimum scene light, sky cover, fog, ambient, and light lift values through `DimensionVisualSettings` without adding presets or domain-specific values to DimensionLib.

## Generator And Diagnostic Commands

DimensionLib does not ship built-in gameplay or lab generators. Consumers register `IDimensionGenerator` implementations, set `DimensionSpec.GeneratorId`, and then use DimensionLib's prepare/enter/validate mechanics. `DimensionGeneratorIds.StandardOverworldWindow` remains a narrow built-in source id for mods that deliberately need bounded vanilla-overworld source projection without owning vanilla generator code.

Root commands operate on dimensions that a consumer mod has already registered:

Useful validation commands:

- `/dlib generators`: list registered generator IDs.
- `/dlib prepare <dimensionId>`: prepare generated content without teleporting.
- `/dlib enter-player <playerName> <dimensionId>`: send an online player into a prepared or generated dimension from the server console.
- `/dlib tp <dimensionId|overworld> [x y z]`: root-only manual recovery teleport for the current player. Coordinates are absolute; omitted coordinates use the target spawn.
- `/dlib tp-player <playerName> <dimensionId|overworld> [x y z]`: server-console/admin recovery teleport for an online player.
- `/dlib validate [dimensionId]`: report metadata, generator/source bounds, prepared/orphaned state, and spawn block samples.

## Projection Sources

`IBlockVolumeSource` is the intended seam for replay, schematics, and generated previews. The source receives local chunk coordinates and an `IChunkColumnWriter`. It can reconstruct from any backing store as long as it writes block IDs into local dimension coordinates.

Replay-specific guidance:

- Reconstruct block state in the replay mod, not in DimensionLib.
- Keep actor ghosts, event markers, and action logs as overlay streams.
- Use `DimensionMutability.ReadOnly` for historical projections by default.
- Treat block-entity data as a separate milestone; the current writer only places block IDs.

## Current Limits

- Registry persistence is JSON-manifest based, not a database.
- Dimension mappings are runtime registrations owned by consumers, not manifest-persisted DimensionLib state.
- Read-only protection covers player break/place/use hooks, not every possible world mutation source.
- Materialization currently writes solid block IDs only.
- Visual settings are currently synced by DimensionLib transfers, not by a robust client-side dimension registry/resync path.
- Region allocation intentionally provides only sparse placement. It does not provide dense packing, named neighborhoods, or lifecycle cleanup policy yet.
- Calendar, season, and weather are still global VS systems; DimensionLib does not provide per-dimension calendars yet.
- Mapping transforms support scale and offset, not arbitrary rotation, curves, graph routing, or custom transform callbacks yet.

These limits are intentional. They keep the first API small while leaving clear extension points for persistence, protection, block entities, and per-dimension visuals.
