# Vintage Story Model Viewer Roadmap

The deterministic renderer is the headless evidence backend for a future interactive model viewer. The viewer should make
the same geometry, texture, camera, and animation behavior explorable without weakening reproducible command-line output.

## Product goal

Open a locally installed Vintage Story asset or repository manifest and inspect it interactively:

- orbit, pan, zoom, fixed profiles, and opposing isometric views;
- wireframe, material-ID, and textured modes;
- authored animation selection, timeline scrubbing, playback speed, and static turntables;
- deterministic screenshot, contact-sheet, and video export through the existing backend;
- visible bounds, pivots, element hierarchy, texture assignments, and coplanar-overlap findings.

The viewer must consume local game assets. Do not redistribute Vintage Story models or textures.

## Held-model composition

The first bounded composition slice is implemented: one installed hairless Seraph, one right-hand collectible, one authored
animation, variant-aware transforms/animation selection, same-shape step parents, animated attachment anchors, and the exact
third-person item matrix order traced from Vintage Story 1.22.1. The CLI exports the same fixed views, metadata, and
fixed-camera animation evidence as other models. Authored `holdbothhands` poses are supported using the game's actual model:
the item stays on `RightHand` while the animation aligns the support arm.

Remaining work should extend that proven slice rather than creating a parallel scene system:

1. Assemble selected skin, hair, clothing, and other wearable/skinnable parts.
2. Reproduce runtime animation blending, easing, and animation-category selection.
3. Add left-hand and off-hand items. Add hard two-point constraints only if a future runtime system actually uses them;
   ordinary Vintage Story two-hand holds do not.
4. Reproduce the first-person arm-only mesh, player model matrix, hand FOV, and shader/depth pass. In 1.22.1 the player
   renderer still requests `HandTp`, so do not imply that `fpHandTransform` alone reproduces the visible first-person pose.
5. Add third-person front/back and first-person camera presets to the future interactive viewer.
6. Validate representative tools, blocks, weapons, and two-handed items against bounded in-game screenshots.

Continue tracing actual game code for each step. Do not infer grip placement, animation choice, or transform selection from
asset names alone.

## Delivery slices

### 1. Interactive inspection

- Reuse the Python package under `scripts/vintage_story_model_renderer/`.
- Add a small UI shell with asset-root and manifest selection.
- Keep camera and mode state outside the geometry loaders.
- Preserve the current CLI and exact headless output for automation and CI.

### 2. Definition-aware representations

- Implemented foundation: load an explicit collectible transform property from an item/block definition, reproduce the
  game matrix order in model units, hash the definition, and render a static neutral grip proxy with an explicit parity
  limitation.
- Remaining: resolve shapes, textures, wildcard variants, and game defaults directly from item, block, and entity
  definitions instead of requiring the manifest to name geometry and textures.
- Expose placed, inventory, ground, first-person, and third-person representations explicitly in the future viewer UI.
- Report unresolved runtime alternates or transforms instead of silently substituting a rest pose.

### 3. Seraph and held items

- Implemented foundation: one hairless Seraph plus one right-hand collectible, definition-backed transform selection,
  variant-aware `*ByType` resolution, truthful default/two-hand animation selection, same-shape step-parent resolution,
  animated attachment tracking, and deterministic still/video evidence.
- Remaining: wearable/skinnable-part assembly, animation blending/easing, left/off-hand items, the
  first-person arm-only render pass, and representative in-game calibration.

### 4. Broader model support

- Add OBJ through the existing face representation.
- Consider GLTF only after proving which Vintage Story paths actually consume it.
- Add runtime animation blending, emissive channels, and atlas behavior only with parity fixtures.

## Architecture guardrails

- The reusable package owns parsing, geometry, animation sampling, rendering, audits, and export.
- The CLI and future UI are thin adapters over the package.
- Interactive state must not leak into deterministic evidence generation.
- New formats enter through explicit adapters rather than expanding one universal loader.
- Every viewer feature needs a small fixture plus a headless regression before UI integration.
- Vintage Story remains authoritative for registration, shaders, lighting, animation blending, and final in-game appearance.

## Visual-judge calibration

The headless renderer can optionally prepare or execute an advisory VLM review. Keep it separate from deterministic
correctness:

- deterministic blockers: render completion, exact input hashes, complete 24-view coverage, unresolved textures, winding,
  coplanar overlap, and exact definition-backed transform metadata where requested;
- advisory visual review: missing-looking faces, clipping, disconnected construction, texture readability, proportions,
  camera continuity, and animation-loop continuity;
- human/in-game authority: taste, intended construction, lighting, atlas behavior, collision, selection, registration,
  complete Seraph-held pose, and runtime animation blending.

Calibrate the judge against a small corpus containing known-good models and deliberately seeded defects. Record
false-positive and false-negative rates by category before promoting any VLM category to a CI blocker. Preserve the
neutral-describe-first leg, artifact hashes, exact view/timestamp evidence, and human adjudication for disagreements.
