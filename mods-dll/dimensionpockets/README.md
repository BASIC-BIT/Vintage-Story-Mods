# Pocket Dimensions

Pocket Dimensions is a playable admin utility built on DimensionLib and a concrete integration example for `IDimensionLibApi`.

Command privileges are configurable in `ModConfig/pocket_dimensions.json`. Defaults require `root`:

- `/pocket create <name> [sizeChunks] [spawnY]` registers and prepares a persistent pocket dimension with a full indestructible matte grid floor, a protected center-adjacent return pedestal, a generated central Pocket Elevator, sparse DimensionLib allocation, and explicit dark-void visual settings.
- `/pocket enter <name>` idempotently ensures managed pocket infrastructure, captures your current location, and teleports you in. The return pedestal and `/pocket exit` return you to that captured command-entry point.
- `/pocket exit` returns from the current pocket. Return resolution prefers the active ingress Waystone, then the captured command-entry point, then the single linked Waystone fallback when no player-specific return exists.
- `/pocket list` lists pockets owned by this mod.
- `/pocket layers` lists known pocket layer stacks and their layer indexes.
- `/pocket inspect [name]` shows the DimensionLib dimension at your current position, or a named pocket from console/admin chat, including the expected return pedestal position and actual block code.
- `/pocket bind <name>` binds the placed Pocket Waystone you are looking at to an existing pocket. Right-clicking that external Waystone enters the bound pocket.
- `/pocket unbind` clears the binding from the placed Pocket Waystone you are looking at.
- `/pocket release <name> confirm` marks the pocket orphaned through DimensionLib.

Config defaults:

- `CreatePrivilege`: `root`
- `EnterPrivilege`: `root`
- `ExitPrivilege`: `root`
- `UseWaystonePrivilege`: `root`
- `UseElevatorPrivilege`: `root`
- `UsePocketBlocksPrivilege`: `root`
- `MutatePocketBlocksPrivilege`: `root`
- `BindPrivilege`: `root`
- `ReleasePrivilege`: `root`
- `DefaultSizeChunks`: `3`
- `MaxSizeChunks`: `16`
- `DefaultSpawnY`: `0`, meaning use half the map height
- `ElevatorLandingMode`: `RequireElevatorBlock`; valid values are `RequireElevatorBlock`, `ClearHeadroomOnly`, and `AutoPlaceElevatorIfMissing`

The main implementation is `src/PocketDimensionModSystem.cs`.

It uses only public DimensionLib API calls:

- `RegisterPolicyProvider`
- `RegisterDimension`
- `PrepareDimension`
- `TeleportToDimension`
- `TeleportToLocation`
- `RegisterMapping`
- `ResolveMappedLocation`
- `ResolveLocalPosition`
- `ReleaseDimension`
- `Dimensions`, `GetDimension`, `GetDimensionAt`, and `IsDimensionPrepared`

Keep DimensionLib integration direct here. If a helper seems broadly useful, prove it in this product mod before promoting it into DimensionLib core.

Pocket floor and generated return pedestal blocks are protected by the mod's `IDimensionPolicyProvider`: players with `MutatePocketBlocksPrivilege` can build inside mutable pockets, but `pocketdimensions:pocketfloor` and `pocketdimensions:pocketreturnpedestal` cannot be broken through normal player block-breaking hooks. The pocket floor is also non-replaceable so ordinary placement cannot overwrite the generated floor.

Externally placed `pocketdimensions:pocketwaystone` blocks are craftable, breakable, and bindable. Bind one with `/pocket bind <name>` while looking at it, then right-click it to enter that pocket. Breaking a bound external Waystone drops the block and clears the binding data.

Right-clicking a bound external Waystone requires `UseWaystonePrivilege`, records that endpoint as the player's active ingress for the destination pocket, then uses DimensionLib's explicit transfer API with DimensionLib return recording disabled. The center-adjacent Pocket Return Pedestal resolves the active ingress endpoint back to the linked external Waystone position and calls `TeleportToLocation(...)`.

Waystone links, active ingress choices, and command-entry return locations are persisted to `ModData/pocketdimensions/waystone-links.json`. The store contains endpoint links plus the minimal `player -> pocket -> endpoint` and `player -> pocket -> location` active-trip state needed for return pedestal recovery across restarts. A successful pedestal or `/pocket exit` return clears the player-specific return state for that pocket.

Pocket layers are also persisted in `waystone-links.json`. Existing pockets become layer stacks when entered, inspected, or used by the HUD. Press `PageUp` or `PageDown` while standing on a Pocket Elevator to move to the adjacent layer. If that layer does not exist and the player has `CreatePrivilege`, the client asks for confirmation before creating the layer, registering a durable DimensionLib mapping, generating the central elevator, and continuing the transfer. New layers use signed indexes such as `Layer 0`, `Layer +1`, and `Layer -1`.

Elevators validate the mapped destination before transfer. By default the target layer must have a Pocket Elevator at the mapped landing and two clear blocks above it. Newly created layers auto-place the matching landing elevator so the confirmed creation flow can complete. The `ClearHeadroomOnly` and `AutoPlaceElevatorIfMissing` modes relax that rule for servers that prefer more seamless movement.

The client HUD shows the active pocket name, layer index, and effective local coordinates while inside a Pocket Dimensions layer. It uses DimensionLib's local-coordinate helper instead of showing sparse backing coordinates.

The Waystone model is a neutral JSON prop with generated stone, trim, and accent textures. See `docs/WAYSTONE_PROP_GUIDE.md` for design direction, model authoring rules, and the verification checklist.

Current crafting recipe:

```text
_N_
PGP
_N_
```

Where `N` is any metal nails and strips, `P` is any polished rock, and `G` is a temporal gear.
