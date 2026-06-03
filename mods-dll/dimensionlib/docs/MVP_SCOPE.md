# DimensionLib MVP Scope

DimensionLib's first release should be a small, reliable library for mods that create and operate custom dimensions. Cavern visuals, pocket-dimension gameplay, portals, admin rooms, and advanced worldgen are valuable, but they should validate the API from consumer mods rather than become core library scope.

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
- A root-only `standard` overworld source window experiment for validating large overworld-like dimensions without owning vanilla generator code in DimensionLib.
- Player transfer and return-position storage.
- Basic access/mutation policy hooks.
- Root-only `/dlib` debug and maintenance commands for developers, admins, and QA. These should validate and inspect the library, not become gameplay/admin products.
- Built-in transient debug/test fixtures sufficient to validate the core mechanics.
- Documentation that shows a simple consuming mod creating a void or generated dimension.

## Optional But Useful For MVP

- Minimal explicit visual settings support if it is needed to make non-overworld dimensions usable.
- Diagnostic command output for prepared state, bounds, generator id, and visual settings.
- Small demo/consumer mods in the repo that use public API only. Current examples: Pocket Dimensions in `mods-dll/dimensionpockets` and Cavern Dimension Demo in `mods-dll/dimensioncavern`.

## Not Core MVP

- Cavern-demo worldgen quality.
- Forked or copied vanilla overworld generation.
- Independent alternate-overworld seeds, climate rules, or worldgen rule packs.
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
- Cavern demo: stress-test generated dimensions and explicit visual settings without shipping content from DimensionLib.
- Vanilla overworld window: stress-test lazy standard-overworld source materialization and very large finite bounds before any Mystcraft-style copied/tweakable worldgen work.

Pocket dimensions are likely the first valuable follow-on mod, not a DimensionLib core feature.

If a feature is only needed by Cavern Demo polish, keep it in that consumer mod until another mod needs the same primitive.

## Split Criteria

Move code out of core DimensionLib when:

- It is content-specific (lava pools, ceiling shape, pillar density, mood tuning, custom blocks).
- It can be implemented using the public API without privileged internals.
- It does not need to participate in region allocation, persistence, transfer, or protection mechanics.

Keep code in core DimensionLib when:

- It is required for any consuming mod to safely create, prepare, enter, send, release, or protect a dimension.
- It exposes a narrow primitive that multiple dimension styles could reuse.
- It is a diagnostic or QA tool for validating the library's guarantees.

## Immediate Recommendation

Do not keep improving cavern content as if it were DimensionLib's product scope. Use the Cavern Dimension Demo only as an API stress test.

The next implementation pass should make the boundary explicit:

- Keep a tiny built-in debug/void fixture in DimensionLib.
- Keep root commands focused on debug, diagnostics, and QA workflows.
- Keep cavern generator, visual settings, blocks, and commands in the separate Cavern Dimension Demo mod.
- Move pocket-dimension/admin-room functionality into a separate consumer mod.
- Avoid adding new public API only to satisfy one Cavern Demo visual or terrain idea.

## Current Split Assessment

Cavern content is split from DimensionLib:

- Generator/content code: `CavernDimensionGenerator`, `CavernGenerationProfile`, and `dimensioncavern:cavernrock` assets live in `mods-dll/dimensioncavern`.
- Visual environment code: public `DimensionVisualSettings`, `VisualSettingsMapper`, `DimensionVisualSystem`, and `/dlib visual` tuning. These are core-owned and expose explicit per-spec fields rather than a named preset registry.
- Baked light-floor tooling has been removed from DimensionLib core. Keep future lighting experiments in consumer/demo code until a repeated reusable primitive appears.

## Public API Gaps Exposed By Cavern Demo

These are candidates, not commitments:

- Provide reusable visual-setting recipes only if repeated consumer-mod code proves noisy.
- Register baked light policies independently from visual settings.
- Provide a reusable dimension spec builder if repeated consumer-mod code proves noisy.
- Provide a sample consumer mod that depends on DimensionLib and uses only public APIs.

Avoid adding helpers just because a single consumer mod wants them. Start with examples; promote to API only when repeated consumer code makes the need obvious.

## Recommended Near-Term Work Order

1. Keep Pocket Dimensions small and use it to judge whether the public API is pleasant enough.
2. Keep the vanilla-overworld source window as a reusable primitive, but expose stress tests through consumer/demo code rather than a built-in `/dlib create-test` lab.
3. Keep Cavern Demo generator and visual experiments in the demo mod, not release-critical DimensionLib polish.
4. Promote repeated consumer boilerplate only after it appears in more than one consumer mod.
5. Only then decide whether to expose visual/light registration APIs or start a Mystcraft-style copied/tweakable overworld generator.
