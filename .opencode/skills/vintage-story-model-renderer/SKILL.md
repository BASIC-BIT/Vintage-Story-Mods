---
name: vintage-story-model-renderer
description: Render Vintage Story shape JSON and supported procedural flywheel geometry into deterministic six-direction orthographic views, an isometric view, a contact sheet, and machine-readable bounds metadata. Use whenever editing or reviewing Vintage Story block/entity models, pivots, proportions, frame connections, texture identity, marker continuity, or packaged model assets.
---

# Vintage Story Model Renderer

Use `scripts/render_vintage_story_model.py` before and after model changes. Treat its output as automated geometric evidence, not human in-game approval.

## Workflow

1. Create or update a render manifest beside the mod's model tooling. Include authoritative shape files, texture overrides, and supported procedural geometry.
2. Render all fixed views:

   ```powershell
   python .opencode/skills/vintage-story-model-renderer/scripts/render_vintage_story_model.py `
     --manifest <manifest.json> `
     --output-dir <bounded-output-directory>
   ```

3. Inspect `contact-sheet.png` at original resolution:
   - front/back: pivot, hub centering, and marker continuity;
   - left/right: depth, axle fit, plates, and frame clearance;
   - top/bottom: support symmetry and disconnected braces;
   - isometric: silhouette, material grouping, intersections, and floating parts.
4. Read `render-metadata.json`. Confirm expected input hashes, element count, bounds, and unresolved textures.
5. Compare before/after contact sheets. Do not infer in-game lighting, animation, selection, collision, or mechanical alignment.
6. After automated rendering passes, run relevant build/tests and use the repository `human-qa` skill for bounded in-game observation.

## Manifests

Paths are relative to the manifest:

```json
{
  "name": "example",
  "shapes": ["../assets/example/shapes/block/model.json"],
  "textures": {
    "wood": "game:block/wood/planks/generic"
  }
}
```

For Flywheel Power's runtime mesh, add:

```json
{
  "proceduralFlywheel": {
    "dimensionsSource": "../src/FlywheelModelDimensions.cs",
    "size": "full"
  }
}
```

Texture locations resolve against each `--assets-root`. Missing PNGs use deterministic fallback colors and appear in metadata.

## Evidence boundary

This deterministic orthographic renderer supports geometry review and regression evidence. Vintage Story remains authoritative for atlas behavior, runtime registration, animation, lighting, and final visual taste.
