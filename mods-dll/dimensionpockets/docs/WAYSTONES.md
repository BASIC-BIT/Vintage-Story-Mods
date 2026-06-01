# Pocket Waystones

Pocket Waystones are the first in-game affordance for Pocket Dimensions. They make pockets useful without requiring players to type commands.

## Current Slice

- Each newly prepared pocket gets a protected `pocketdimensions:pocketreturnpedestal` near the center spawn area.
- The return pedestal is protected by Pocket Dimensions policy and is not breakable through normal player block-breaking hooks.
- Right-clicking the return pedestal uses DimensionLib's explicit `TeleportToLocation(...)` primitive. It returns through the player's active ingress Waystone, the captured command-entry location, or the single linked Waystone fallback, in that order.
- Pocket Dimensions records durable Waystone endpoint links plus minimal active trip state. This includes `player -> pocket -> endpoint` for Waystone ingress and `player -> pocket -> location` for unanchored command ingress, not a generic return stack.
- Externally placed Waystones are craftable, breakable, and bindable with `/pocket bind <name>`.
- Right-clicking a bound external Waystone records that endpoint as the player's active ingress for the destination pocket, idempotently ensures pocket infrastructure, and calls DimensionLib's `TeleportToDimension(player, dimensionId, RecordReturn=false)` primitive.
- The Waystone is intentionally product-layer code; DimensionLib should provide location/link and transfer primitives, not Waystone lore or assets.
- The current model is a first-pass neutral JSON prop with dedicated generated stone, trim, and accent textures. See `WAYSTONE_PROP_GUIDE.md` for the prop design, modeling, and verification plan.

## Placement Rule

The return pedestal is placed center-adjacent rather than exactly inside the spawn block. This keeps the player's spawn feet/head cells clear while still making the return affordance obvious at the center of the pocket.

## Future Slices

- Hand-tune or replace the generated stone, trim, and accent textures after visual feedback.
- Add an admin inspection/report command for Waystone links.
- Add a multi-pocket chooser UI for Waystones with more than one target.
- Add optional lore, model, texture, and naming overrides for servers.
- Explore walk-through portals only after Waystone flows are proven.

## Product Boundary

Do not move Pocket Waystone assets, recipes, names, or lore into DimensionLib. If a second consumer needs generic linked-block behavior, extract that behavior to a utility layer or a neutral API, not to core gameplay objects.

## Binding Flow

Place or craft a Pocket Waystone outside a pocket, look at it, then run:

```text
/pocket bind <name>
```

The target must be an existing Pocket Dimensions pocket. Pocket Waystones inside pockets are not bindable yet; the generated return pedestal is managed automatically.

Current implementation persists Waystone endpoint links, active ingress selections, and command-entry return locations in `ModData/pocketdimensions/waystone-links.json`. The link store maps each external Waystone endpoint to a target pocket and stores only the active `player -> pocket -> endpoint` or `player -> pocket -> location` choice needed for returns across restarts. The next API shape should promote this idea into named references so Waystones, pocket teleporter items, and machine endpoints can all use the same model.

Pocket command output uses short names such as `test3`. The fully qualified DimensionLib id remains `pocketdimensions:test3` internally and for low-level `/dlib` commands.

To clear an external Waystone binding, look at it and run:

```text
/pocket unbind
```
