# DimensionLib Product Direction

This file captures the durable product boundary for DimensionLib and its first consumer, Pocket Dimensions. Read this before adding gameplay-facing features to DimensionLib.

## Core Mentality

DimensionLib should be boring infrastructure. It should provide safe, reusable mechanics for bounded alternate spaces without deciding a server's lore, progression, UI, recipes, models, or player-facing fantasy.

Pocket Dimensions is the first product layer. It should prove useful gameplay flows on top of DimensionLib before any convenience helper is promoted into the library.

## Surface Design Principles

Simplicity and understanding are product goals for DimensionLib. A small, obvious surface that mod authors can reason about is more valuable than a broad surface that hides engine quirks behind clever helpers.

Before adding any new public API, command, generator hook, visual capability, or transfer capability, answer these gates in the change itself or nearby documentation:

- What concrete consumer needs this now?
- Can the same behavior be expressed with the existing `Dimension`, location, generator, policy, or preparation primitives?
- Is the mechanism explainable in one or two sentences to a mod author?
- Is the behavior durable engine mechanics, or product-specific fantasy/UX that belongs in Pocket Dimensions or a future utility layer?
- Does the feature make the pit of success clearer, or does it add another way to do the same thing?
- What is the fallback or demotion path if the abstraction proves too broad?

Prefer transparent mechanics over hidden magic. If DimensionLib must use a bridge or workaround, name it as such and keep it behind the smallest internal seam until a stable product requirement justifies promotion.

## Personas

- Mod authors need small, stable APIs they can call from blocks, items, commands, UIs, generators, and portals.
- Server admins need predictable commands, config, permissions, and cleanup behavior.
- Players need in-game affordances that do not require typing command names.
- Roleplay servers need lore-neutral mechanics they can rename, retexture, and explain in their own setting.
- Non-roleplay servers need practical private/admin spaces with low setup cost.

## DimensionLib Owns

- Dimension registration, sparse backing allocation, lookup, validation, persistence, release, and orphan handling.
- Chunk preparation, materialization, relighting, and force-send mechanics.
- Standard-overworld source window experiments that validate bounded alternate spaces and lazy generation without owning vanilla generator code.
- Location and transfer primitives: describe locations, capture a player's current location, teleport to a location, teleport into a dimension, and sync client visuals.
- Coordinate mapping primitives that let owner mods relate multiple registered dimensions as one cohesive space without owning product-specific triggers.
- Policy extension points for consumer-owned entry, block use, and block mutation rules.
- Explicit visual/environment settings and client-side application primitives.

## DimensionLib Does Not Own

- Waystone blocks, portal blocks, recipes, lore, models, textures, or progression.
- Pocket-specific commands or chooser UIs.
- Server-specific lore names such as shrines, altars, mirrors, machines, elevators, or rifts.
- Trigger UX for mapped transfers, including ability items, hotkeys, portal blocks, elevator controls, sounds, particles, cooldowns, and lore.
- Gameplay balance for who can create, enter, bind, or release a dimension.
- A forked/copy-owned implementation of vanilla overworld generation until a Mystcraft-style product actually needs tweakable vanilla-like world rules.

## Pocket Dimensions Owns

- The default playable pocket-dimension experience.
- Admin commands and config for pocket creation, entry, release, and future binding.
- Pocket-specific blocks such as floors, external Waystones, and generated return pedestals.
- Product rules such as indestructible floors and protected return pedestals.
- Lightly themed defaults that remain easy for server owners to override.
- The real Pocket Waystone prop design, documented in `mods-dll/dimensionpockets/docs/WAYSTONE_PROP_GUIDE.md`.

## Mystcraft Direction

Mystcraft-style dimensions should eventually feel vanilla-like while allowing authored/tweakable world rules. The near-term DimensionLib task is not to copy vanilla worldgen wholesale; it is to prove the mechanics needed by that future work:

- Bounded regions that can be very large without eager generation.
- Lazy chunk-window preparation around players.
- Transfers into standard-overworld source dimensions.
- Diagnostics that show prepared chunk counts, backing bounds, and generator identity.

The preferred long-term direction is true engine-supported native worldgen for alternate dimensions if Vintage Story exposes a maintainable path. The current `PeekChunkColumn` source-window materialization is a public-API bridge for proving lazy bounded generation; do not treat it as the final architecture or expand it into a broad copied-worldgen framework without a fresh design review.

If `standard` overworld source windows prove viable but insufficient, a later Mystcraft layer can copy or wrap vanilla systems with a clearer reason: alternate seed/rules/climate/structures. Keep that copied/tweakable generator outside DimensionLib until the stable mechanics and product requirements are clearer.

## Likely Utility Layer

A future `Dimension Utilities` mod may be useful after multiple consumers need the same product objects. Do not create this layer until at least two real consumers want the same behavior.

Good candidates for a utility layer:

- Generic linked-block behavior.
- Generic portal frame or trigger-volume behavior.
- Generic dimension chooser UI.
- Generic admin binding tools.
- Generic return-to-origin block behavior.

These should not move into DimensionLib core unless they are mechanics that every consumer needs and can be expressed without product fantasy.

## Location And Link Direction

Durable transfer APIs should be location-oriented, not player-return-oriented. A location may be an arbitrary point in space, a named Waystone, an entry point captured by an item, a portal endpoint, a machine endpoint, or a future generated-world anchor.

`ReturnPlayer(player)` is useful as a prototype/debug helper, but it is not the long-term API surface for DimensionLib. It hides the actual concept: a player entered from one endpoint and later used another interaction to travel to another endpoint. New product code should use explicit locations and consumer-owned links; use `CaptureLocation` only when the desired product behavior is actually source-point return. Future API work should add named/linkable location references.

This matters because Pocket Dimensions is likely to grow beyond player-only travel:

- Named Waystones and links between specific Waystones.
- Pocket teleporter items that take a player to a pocket and then back to the captured use location.
- Mechanical power, item, fluid, or signal transfer through pocket-linked endpoints.
- Quantum chest-like storage links.
- Machine-focused generated dimensions with world rules chosen for a purpose, such as wind, sun, temperature, resources, or other environment traits.

Design these as links between locations/endpoints. Do not bake the assumption that the only flow is "a player returns to wherever they were." Player return is one consumer of the more general location/link model.

Dimension mappings are the first promoted location/link primitive: a small, reusable way to map player-local coordinates from one registered dimension into another. The concrete consumer need is Devastation-style paired spaces, scaled dimension travel, and Pocket Dimensions layer elevators. Durable mappings are persisted by DimensionLib when their endpoints are durable dimensions; transient mappings remain runtime-only for QA and temporary links. The over-promotion risk is turning DimensionLib into a portal/product framework, so core owns only scale/offset coordinate mapping, simple durable relationship metadata, effective-coordinate helpers, non-teleporting mapped-location resolution, and transfer validation; consumer mods own triggers, content, UI, cooldowns, visuals beyond explicit settings, and gameplay rules. If the abstraction proves too broad, consumers can ignore mappings and continue using explicit `DimensionLocation` transfers.

## Pocket Dimensions Roadmap

Recommended sequence:

- Commands: create, enter, exit, list, inspect, release.
- Indestructible full-floor materialization.
- Protected return pedestal inside each pocket.
- Bindable overworld Waystone that enters one pocket.
- Craftable and breakable external Waystone blocks.
- Layered pocket stacks with generated/player-placeable elevators backed by DimensionLib mappings.
- Extend explicit locations into named links/endpoints once more product flows need them.
- Multi-pocket Waystone with a chooser UI.
- Walk-through portal or portal frame once the block interaction model is proven.

Waystones should come before portals. A Waystone is a single block interaction with simple protection and clear UX. A portal requires trigger volumes, shape validation, collision handling, visual effects, griefing rules, and more testing.

## Promotion Rule

Prove a feature in Pocket Dimensions first. Promote only the stable, lore-neutral mechanics to DimensionLib or a utility mod after the code has at least one concrete second consumer or an obvious repeated integration pattern. When in doubt, keep the core surface smaller and document the tradeoff rather than adding another public capability.
