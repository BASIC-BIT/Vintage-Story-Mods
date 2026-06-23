# Pocket Dimensions

Pocket Dimensions is a playable admin utility built on DimensionLib and a concrete integration example for `IDimensionLibApi`.

Command privileges and capability modes are configurable in `ModConfig/pocket_dimensions.json` or in-game with `/pocket config`. The default split keeps creation, binding, configuration, and release staff-gated while allowing ordinary players to use bound Waystones and return pedestals:

- `/pocket create <name> [sizeChunks] [spawnY]` registers and prepares a persistent pocket dimension with a full indestructible matte grid floor, a protected center-adjacent return pedestal, a generated central Pocket Elevator, sparse DimensionLib allocation, and explicit dark-void visual settings.
- `/pocket enter <name>` idempotently ensures managed pocket infrastructure, captures your current location, and teleports you in. The return pedestal and `/pocket exit` return you to that captured command-entry point.
- `/pocket exit` returns from the current pocket. Return resolution prefers the active ingress Waystone, then the captured command-entry point, then the single linked Waystone fallback when no player-specific return exists.
- `/pocket list` lists pockets owned by this mod.
- `/pocket layers` lists known pocket layer stacks and their layer indexes.
- `/pocket inspect [name]` shows the DimensionLib dimension at your current position, or a named pocket from console/admin chat, including the expected return pedestal position and actual block code.
- `/pocket links` reports durable Waystone links and whether their pocket, block, and binding still line up.
- `/pocket config` opens the shared BasicConfig admin panel for Pocket Dimensions.
- `/pocket bind <name>` binds the placed Pocket Waystone you are looking at to an existing pocket. Right-clicking that external Waystone enters the bound pocket.
- `/pocket unbind` clears the binding from the placed Pocket Waystone you are looking at.
- `/pocket release <name> confirm` marks the pocket orphaned through DimensionLib.

Press `Ctrl+Shift+P` to open the Pocket Directory. The directory lists pockets visible to your current capability policy and lets you enter an available pocket without typing `/pocket enter`. Players allowed to create pockets get a **New Pocket** form with display name, slug, size chunks, and spawn Y fields. Connected spaces inside a pocket are summarized instead of listed as implementation-level layers; movement between them is handled in-world through Pocket Elevators, while admin commands still expose detailed layer diagnostics. Server permissions still decide whether the directory shows any spaces and whether creation, entry, Waystone, elevator, and block-use buttons are available.

Config defaults:

- `CreatePrivilege`: `root`
- `EnterPrivilege`: `root`
- `ExitPrivilege`: `chat`
- `UseWaystonePrivilege`: `chat`
- `UseElevatorPrivilege`: `root`
- `UsePocketBlocksPrivilege`: `root`
- `MutatePocketBlocksPrivilege`: `root`
- `BindPrivilege`: `root`
- `UnbindPrivilege`: `root`
- `ConfigPrivilege`: `root`
- `ReleasePrivilege`: `root`
- `PlaceWaystonePrivilege`: `chat`
- `BreakWaystonePrivilege`: `chat`
- `UseReturnPedestalPrivilege`: `chat`
- `CreatePocketCapabilityMode`: `Privilege`
- `CreateLayerCapabilityMode`: `Privilege`
- `EditLayerCapabilityMode`: `Privilege`
- `DirectoryVisibilityCapabilityMode`: `Privilege`
- `DirectoryTeleportCapabilityMode`: `Privilege`
- `UseWaystoneCapabilityMode`: `Public`
- `UseElevatorCapabilityMode`: `Privilege`
- `UsePocketBlocksCapabilityMode`: `Privilege`
- `MutatePocketBlocksCapabilityMode`: `Privilege`
- `DefaultSizeChunks`: `3`
- `MaxSizeChunks`: `16`
- `DefaultSpawnY`: `0`, meaning use half the map height
- `ElevatorLandingMode`: `RequireElevatorBlock`; valid values are `RequireElevatorBlock`, `ClearHeadroomOnly`, and `AutoPlaceElevatorIfMissing`
- `AllowWaystoneCrafting`: `true`, restart required when changed
- `TeleportDelaySeconds`: `1.0`
- `EnableTeleportSounds`: `true`
- `TeleportStartSound`: `sounds/effect/translocate-active`
- `TeleportCompleteSound`: `sounds/effect/translocate-breakdimension`
- `TeleportSoundVolume`: `0.7`
- `TeleportSoundRange`: `16`
- `PrepareChunksDuringTeleportDelay`: `true`

Capability mode values are `Privilege`, `OwnerOrPrivilege`, `OwnerMemberOrPrivilege`, `Public`, and `Disabled`. The configured privilege remains the staff/admin override in every mode. `Privilege` preserves the historical privilege-only behavior and is the default for all actions. `Public` allows any online player. `OwnerOrPrivilege` allows the recorded pocket owner. `OwnerMemberOrPrivilege` also allows player UIDs listed on the persisted stack metadata. `Disabled` disables non-privileged use of that action.

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

Externally placed `pocketdimensions:pocketwaystone` blocks are craftable, breakable, and bindable. Bind one with `/pocket bind <name>` while looking at it, then right-click it to enter that pocket. Bound external Waystones must be cleared with `/pocket unbind` before they can be broken; unbound Waystones can be broken by players with `BreakWaystonePrivilege`.

Right-clicking a bound external Waystone checks the configured Waystone capability mode, records that endpoint as the player's active ingress for the destination pocket, plays the configured activation sound, waits `TeleportDelaySeconds`, revalidates the Waystone binding, then uses DimensionLib's explicit transfer API with DimensionLib return recording disabled. The center-adjacent Pocket Return Pedestal resolves the active ingress endpoint back to the linked external Waystone position and calls `TeleportToLocation(...)` after the same delayed-transfer flow.

Waystone links, active ingress choices, and command-entry return locations are persisted to `ModData/pocketdimensions/waystone-links.json`. The store contains endpoint links plus the minimal `player -> pocket -> endpoint` and `player -> pocket -> location` active-trip state needed for return pedestal recovery across restarts. A successful pedestal or `/pocket exit` return clears the player-specific return state for that pocket. Unbinding a Waystone preserves active return locations for players already inside the linked pocket so they are not stranded by the cleared endpoint.

Pocket layers are also persisted in `waystone-links.json`, but the player-facing UI treats them as connected spaces inside a pocket rather than as separate destinations to manage in a table. Existing pockets become layer stacks when entered, inspected, or used by the HUD. Press `PageUp` or `PageDown` while standing on a Pocket Elevator to move to the adjacent connected space. If that space does not exist and the player has the configured layer-creation capability, the client asks for confirmation before creating it, registering a durable DimensionLib mapping, generating the central elevator, and continuing the transfer. Admin commands still expose signed layer indexes such as `Layer 0`, `Layer +1`, and `Layer -1` for diagnostics and recovery.

New layer stacks record the creating player's UID/name when available. Existing stacks without owner metadata continue to load as server/global stacks. Stack metadata also stores member UID/name lists for `OwnerMemberOrPrivilege` policies. There is not yet a player-facing member-management UI; server staff can manage membership by editing the persisted stack metadata or by future admin tooling.

Elevators validate the mapped destination before transfer. By default the target connected space must have a Pocket Elevator at the mapped landing and two clear blocks above it. If an existing connected space is clear but missing that elevator, the client asks whether to place one before continuing; if the mapped landing is blocked, the transfer fails with a blocked-way error. Newly created connected spaces auto-place the matching landing elevator so the confirmed creation flow can complete. The `ClearHeadroomOnly` and `AutoPlaceElevatorIfMissing` modes relax that rule for servers that prefer more seamless movement.

The client HUD shows the active pocket name and effective local coordinates while inside a Pocket Dimensions space. It uses DimensionLib's local-coordinate helper instead of showing sparse backing coordinates.

The Waystone model is a neutral JSON prop with generated stone, trim, and accent textures. See `docs/WAYSTONE_PROP_GUIDE.md` for design direction, model authoring rules, and the verification checklist.

Current crafting recipe:

```text
_N_
PGP
_N_
```

Where `N` is any metal nails and strips, `P` is any polished rock, and `G` is a temporal gear.
