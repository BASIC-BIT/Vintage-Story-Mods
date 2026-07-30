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

A later milestone should compose a complete Seraph with an item rather than rendering the item in isolation:

1. Load the selected Seraph body shape, skin, and relevant attachment shapes.
2. Resolve the collectible's first-person and third-person held transforms from its registered item or block definition.
3. Select and play the appropriate Seraph animation, including hand and arm motion.
4. Attach the held model to the correct animated hand or step-parent anchor.
5. Show first-person, third-person front/back, and neutral inspection presets.
6. Export the same deterministic evidence used for placed and inventory representations.

Before implementing this milestone, trace Vintage Story's actual held-transform, step-parent, animation-selection, and
attachment code. Do not infer grip placement or animation choice from asset names alone.

## Delivery slices

### 1. Interactive inspection

- Reuse the Python package under `scripts/vintage_story_model_renderer/`.
- Add a small UI shell with asset-root and manifest selection.
- Keep camera and mode state outside the geometry loaders.
- Preserve the current CLI and exact headless output for automation and CI.

### 2. Definition-aware representations

- Load item, block, and entity definitions in addition to raw shape JSON.
- Expose placed, inventory, ground, first-person, and third-person representations explicitly.
- Report unresolved runtime alternates or transforms instead of silently substituting a rest pose.

### 3. Seraph and held items

- Add scene composition and animated attachment anchors.
- Support one Seraph plus one held collectible before attempting arbitrary entity assemblies.
- Validate representative tools, blocks, weapons, and two-handed items against bounded in-game screenshots.

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
  and coplanar overlap;
- advisory visual review: missing-looking faces, clipping, disconnected construction, texture readability, proportions,
  camera continuity, and animation-loop continuity;
- human/in-game authority: taste, intended construction, lighting, atlas behavior, collision, selection, registration,
  held pose, and runtime animation blending.

Calibrate the judge against a small corpus containing known-good models and deliberately seeded defects. Record
false-positive and false-negative rates by category before promoting any VLM category to a CI blocker. Preserve the
neutral-describe-first leg, artifact hashes, exact view/timestamp evidence, and human adjudication for disagreements.
