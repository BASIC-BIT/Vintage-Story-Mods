# Pocket Dimensions

Pocket Dimensions is a deliberately small consumer mod for DimensionLib. It is both a playable admin utility and the first integration example for `IDimensionLibApi`.

Command privileges are configurable in `ModConfig/pocket_dimensions.json`. Defaults require `root`:

- `/pocket create <name> [sizeChunks] [spawnY]` registers and prepares a persistent pocket dimension with a full indestructible matte grid floor, a protected center-adjacent return pedestal, sparse DimensionLib allocation, and explicit dark-void visual settings.
- `/pocket enter <name>` idempotently ensures managed pocket infrastructure and teleports you in. If the pocket has exactly one linked external Waystone, that endpoint is used as the command-entry return target.
- `/pocket exit` returns you through the active ingress Waystone for the current pocket, or through the single linked Waystone when there is no active ingress.
- `/pocket list` lists pockets owned by this mod.
- `/pocket inspect [name]` shows the DimensionLib dimension at your current position, or a named pocket from console/admin chat, including the expected return pedestal position and actual block code.
- `/pocket bind <name>` binds the placed Pocket Waystone you are looking at to an existing pocket. Right-clicking that external Waystone enters the bound pocket.
- `/pocket unbind` clears the binding from the placed Pocket Waystone you are looking at.
- `/pocket release <name> confirm` marks the pocket orphaned through DimensionLib.

Config defaults:

- `CreatePrivilege`: `root`
- `EnterPrivilege`: `root`
- `BindPrivilege`: `root`
- `ReleasePrivilege`: `root`
- `DefaultSizeChunks`: `3`
- `MaxSizeChunks`: `16`
- `DefaultSpawnY`: `0`, meaning use half the map height

The useful example file is `src/PocketDimensionModSystem.cs`.

It intentionally uses only public DimensionLib API calls:

- `RegisterPolicyProvider`
- `RegisterDimension`
- `PrepareDimension`
- `TeleportToDimension`
- `TeleportToLocation`
- `ReleaseDimension`
- `Dimensions`, `GetDimension`, `GetDimensionAt`, and `IsDimensionPrepared`

Keep this mod simple. If a helper seems broadly useful, prove it here first before promoting it into DimensionLib core.

Pocket floor and generated return pedestal blocks are protected by the mod's `IDimensionPolicyProvider`: players can build inside mutable pockets, but `pocketdimensions:pocketfloor` and `pocketdimensions:pocketreturnpedestal` cannot be broken through normal player block-breaking hooks.

Externally placed `pocketdimensions:pocketwaystone` blocks are craftable, breakable, and bindable. Bind one with `/pocket bind <name>` while looking at it, then right-click it to enter that pocket. Breaking a bound external Waystone drops the block and clears the binding data.

Right-clicking a bound external Waystone records that endpoint as the player's active ingress for the destination pocket, then uses DimensionLib's explicit transfer API with DimensionLib return recording disabled. The center-adjacent Pocket Return Pedestal resolves the active ingress endpoint back to the linked external Waystone position and calls `TeleportToLocation(...)`.

Waystone links and active ingress choices are persisted to `ModData/pocketdimensions/waystone-links.json`. The store contains endpoint links plus the minimal `player -> pocket -> endpoint` active-trip state needed for return pedestal recovery across restarts. A successful pedestal or `/pocket exit` return clears that active ingress entry.

The current Waystone model is a first-pass neutral JSON prop with generated stone, trim, and accent textures. See `docs/WAYSTONE_PROP_GUIDE.md` for design direction, model authoring rules, and the verification checklist.

Current crafting recipe:

```text
_N_
PGP
_N_
```

Where `N` is any metal nails and strips, `P` is any polished rock, and `G` is a temporal gear.
