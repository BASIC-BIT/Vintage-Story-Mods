---
name: vintage-story-model-renderer
description: Required visual review workflow whenever editing or reviewing Vintage Story models. Render shape JSON and supported procedural geometry into deterministic six-direction orthographic views, an isometric view, a contact sheet, and machine-readable bounds metadata; inspect the images with the model's visual reasoning and present bounded evidence in chat for human review.
---

# Vintage Story Model Renderer

Use `scripts/render_vintage_story_model.py` before and after every model change. Treat its output as automated geometric evidence, not human in-game approval.

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
   Use the model's visual reasoning to inspect the rendered image itself. Do not substitute numeric bounds or source review
   for image inspection. Call out apparent missing faces, reversed winding, clipping, gaps, z-order artifacts, and ambiguous
   construction even when the authored dimensions are correct.
4. Read `render-metadata.json`. Confirm expected input hashes, element count, bounds, and unresolved textures.
5. Compare before/after contact sheets. Do not infer in-game lighting, animation, selection, collision, or mechanical alignment.
6. Present the bounded contact sheet or the relevant fixed views in chat so the human reviewer can inspect the same evidence.
   State clearly that the image is an automated render and record any human feedback separately.
7. After automated rendering passes, run relevant build/tests and use the repository `human-qa` skill for bounded in-game observation.

When changing this renderer, run its regression tests:

```powershell
python -m unittest discover `
  -s .opencode/skills/vintage-story-model-renderer/tests `
  -p "test_*.py"
```

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

This deterministic renderer supports geometry review and regression evidence. Its winding tests cover every named cuboid
plane plus procedural cylinder caps and radial walls, preventing inward faces or a far cap from masquerading as visible
geometry. Vintage Story remains authoritative for atlas behavior, runtime registration, animation, lighting, and final
visual taste.
