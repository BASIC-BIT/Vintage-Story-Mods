# DimensionLib MVP Scope

DimensionLib's first release should be a small, reliable library for mods that create and operate custom dimensions. Nether visuals, pocket-dimension gameplay, portals, admin rooms, and advanced worldgen are valuable, but they should validate the API rather than become core library scope.

## Release Goal

Ship a clean modder-facing foundation that lets consuming mods create isolated spaces without carving holes into the main world.

The minimum game-changing consumer-mod use case is:

- Create a void or prepared dimension from another mod.
- Build in it or materialize blocks into it.
- Teleport players into and out of it.
- Keep it isolated from normal world bounds and player interactions.
- Persist registered dimensions across restart.

## Core MVP

- Public `IDimensionLibApi` for registration, lookup, preparation, transfer, release, and policy/generator extension points.
- `DimensionSpec` / `Dimension` model with stable owner, bounds, spawn, mutability, access, generator, and visual fields.
- Sparse region allocation and overlap validation.
- Manifest persistence and orphan handling.
- Chunk creation, materialization, relight, force-send, and lazy generated-window preparation.
- Player transfer and return-position storage.
- Basic access/mutation policy hooks.
- Root-only `/dlib` debug and maintenance commands for developers, admins, and QA. These should validate and inspect the library, not become gameplay/admin products.
- Built-in transient debug/test fixtures sufficient to validate the core mechanics.
- Documentation that shows a simple consuming mod creating a void or generated dimension.

## Optional But Useful For MVP

- Minimal explicit visual settings support if it is needed to make non-overworld dimensions usable.
- Diagnostic command output for prepared state, bounds, generator id, visual settings, and light policy.
- A small demo/consumer mod in the repo that uses public API only. Current example: Pocket Dimensions in `mods-dll/dimensionpockets`.

## Not Core MVP

- Nether-specific worldgen quality.
- Pocket-dimension gameplay/admin features.
- Portals, teleporters, keys, ritual blocks, or in-game assets.
- Biomes, large lava oceans, generated structures, and polished terrain algorithms.
- Full custom post-process rendering pipeline.
- Admin UX beyond root commands.
- Content-pack-level art direction.
- Persistent user-facing admin room creation commands.

## Demo Mod Boundary

A demo mod should prove that DimensionLib is easy to consume. It can be bundled in the repo but should not contaminate the core library API.

Good consumer/demo candidates:

- Pocket dimensions: create a void room / isolated build zone and teleport players in/out.
- Nether prototype: stress-test generated dimensions, baked light policies, and explicit visual settings.

Pocket dimensions are likely the first valuable follow-on mod, not a DimensionLib core feature.

If a feature is only needed by Nether polish, keep it in a demo/consumer mod or behind an internal test fixture until another mod needs the same primitive.

## Split Criteria

Move code out of core DimensionLib when:

- It is content-specific (`nether`, lava pools, ceiling shape, pillar density, mood tuning).
- It can be implemented using the public API without privileged internals.
- It does not need to participate in region allocation, persistence, transfer, or protection mechanics.

Keep code in core DimensionLib when:

- It is required for any consuming mod to safely create, prepare, enter, send, release, or protect a dimension.
- It exposes a narrow primitive that multiple dimension styles could reuse.
- It is a diagnostic or QA tool for validating the library's guarantees.

## Immediate Recommendation

Do not keep improving Nether as if it were DimensionLib's product scope. Use Nether only as an API stress test unless/until it is split into a separate consumer mod.

The next implementation pass should make the boundary explicit:

- Keep a tiny built-in debug/void fixture in DimensionLib.
- Keep root commands focused on debug, diagnostics, and QA workflows.
- Move Nether generator, visual settings, and commands into a separate demo mod once the public API can support it cleanly.
- Move pocket-dimension/admin-room functionality into a separate consumer mod.
- Avoid adding new public API only to satisfy one Nether visual or terrain idea.

## Current Split Assessment

Nether is not cleanly separable yet because it crosses three areas:

- Generator/content code: `NetherCavernDimensionGenerator`, `NetherCavernGenerationProfile`, and `dimensionlib:netherrock` assets. These are good candidates for a demo/consumer mod.
- Visual environment code: public `DimensionVisualSettings`, `VisualSettingsMapper`, `DimensionVisualSystem`, and `/dlib visual` tuning. These are core-owned and expose explicit per-spec fields rather than a named preset registry.
- Light policy code: `DimensionLightPolicy.NetherCavern` and baked light-floor application. This is still core-owned and does not have a public policy registration API.

Because only the generator seam is public today, a full Nether split would currently require either:

- Adding new public APIs for named visual presets and baked light policies now.
- Keeping Nether visuals/light policies inside DimensionLib while moving only terrain generation out.

Neither is ideal for the MVP. The safer next step is to keep Nether as an internal stress fixture while validating the public generator, preparation, transfer, protection, and persistence APIs. Split only when the missing seams are clearly needed by more than one consumer.

## Public API Gaps Exposed By Nether

These are candidates, not commitments:

- Provide reusable visual-setting recipes only if repeated consumer-mod code proves noisy.
- Register baked light policies independently from visual settings.
- Provide a reusable dimension spec builder if repeated consumer-mod code proves noisy.
- Provide a sample consumer mod that depends on DimensionLib and uses only public APIs.

Avoid adding helpers just because a single consumer mod wants them. Start with examples; promote to API only when repeated consumer code makes the need obvious.

## Recommended Near-Term Work Order

1. Keep Pocket Dimensions small and use it to judge whether the public API is pleasant enough.
2. Keep Nether generator and visual experiments as internal validation, not release-critical polish.
3. Promote repeated consumer boilerplate only after it appears in more than one consumer mod.
4. Only then decide whether to expose visual/light registration APIs or carve Nether into a separate mod.
