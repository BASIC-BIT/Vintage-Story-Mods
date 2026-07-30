---
name: vintage-story-model-renderer
description: Required visual review workflow whenever editing or reviewing Vintage Story models. Render authored UVs and textures plus supported procedural geometry in wireframe, material-ID, and textured modes from six orthographic and two opposing isometric views; inspect the images with the model's visual reasoning and present bounded evidence in chat for human review.
---

# Vintage Story Model Renderer

Use `scripts/render_vintage_story_model.py` before and after every model change. Treat its output as automated geometric evidence, not human in-game approval.

The renderer requires Python 3 with Pillow and NumPy. Its material and textured modes use a deterministic software depth
buffer; no GPU, display server, Blender installation, or native OpenGL context is required.

The command is a compatibility shim over the reusable `scripts/vintage_story_model_renderer/` package. Keep parsing,
geometry, animation, rendering, audits, and export in their focused modules so future viewer work does not grow the CLI
back into a monolith. See `VIEWER_ROADMAP.md` before adding interactive or held-model features.

## Workflow

1. Create or update a render manifest beside the mod's model tooling. Include authoritative shape files, texture overrides, and supported procedural geometry.
2. Render all fixed views and modes:

   ```powershell
   python .opencode/skills/vintage-story-model-renderer/scripts/render_vintage_story_model.py `
     --manifest <manifest.json> `
     --output-dir <bounded-output-directory> `
     --fail-on-coplanar-overlap
   ```

   Every manifest produces 24 primary images: wireframe, material-ID, and textured renders from front, back, left, right,
   top, bottom, isometric, and isometric-opposite views. It also produces one contact sheet per mode and a combined
   24-image `contact-sheet.png`.
3. Inspect `contact-sheet.png` at original resolution with the model's visual reasoning:
   - front/back: pivot, hub centering, and marker continuity;
   - left/right: depth, axle fit, plates, and frame clearance;
   - top/bottom: support symmetry and disconnected braces;
   - both isometrics: silhouette, material grouping, intersections, floating parts, and one-sided winding or clipping.
   - wireframe: disconnected edges, buried elements, doubled geometry, and unintended gaps.
   - material-ID: stable material grouping without lighting-dependent color changes.
   - textured: resolved texture identity, authored or generated UV scale/orientation, seams, stretching, and variant identity.
   Use the model's visual reasoning to inspect the rendered image itself. Do not substitute numeric bounds or source review
   for image inspection. Call out apparent missing faces, reversed winding, clipping, gaps, z-order artifacts, and ambiguous
   construction even when the authored dimensions are correct.
4. Read `render-metadata.json`. Confirm expected input hashes, representation, element count, bounds, exactly 24 primary
   images, no unexpected unresolved textures, and `coplanarOverlapCount: 0`.
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

For a single-shape entity manifest, render a looping authored animation from a fixed camera:

```powershell
python .opencode/skills/vintage-story-model-renderer/scripts/render_vintage_story_model.py `
  --manifest <manifest.json> `
  --output-dir <bounded-output-directory> `
  --animation <animation-code> `
  --animation-output <video.mp4> `
  --animation-view isometric `
  --animation-fps 60 `
  --animation-source-fps 30 `
  --animation-cycles 4 `
  --animation-orbit
```

Animation video output requires `ffmpeg`. It linearly interpolates independently keyed offset, rotation, stretch, and origin
channels with the same end-to-start wrapping used by repeating Vintage Story animations. Output rates above
`--animation-source-fps` produce genuine sub-frame geometry samples rather than duplicated frames. `--animation-orbit`
completes one smooth 360-degree camera revolution across all requested animation cycles while retaining constant framing.
The clip plays the model in place; entity movement through the world is runtime behavior and is not inferred from the shape
animation.

To isolate camera motion from animation, render the authored rest pose as a static turntable:

```powershell
python .opencode/skills/vintage-story-model-renderer/scripts/render_vintage_story_model.py `
  --manifest <manifest.json> `
  --output-dir <bounded-output-directory> `
  --turntable-output <video.mp4> `
  --turntable-view isometric `
  --turntable-fps 60 `
  --turntable-seconds 12
```

## Manifests and representations

Paths are relative to the manifest. Give each materially different representation its own manifest. At minimum, cover the
placed model and any different inventory, ground, first-person, or third-person held shape. A transform alone does not need
a second manifest when it reuses identical geometry, but its in-game pose still needs human QA.

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

Flywheel Power also keeps its cuboid inventory/held shapes synchronized with the runtime dimensions:

```powershell
python mods-dll/flywheelpower/scripts/generate-preview-shapes.py --check
```

Run the generator without `--check` after changing `FlywheelModelDimensions.cs`, then review both the placed/runtime and
inventory-and-held manifests. The Flywheel evidence script performs the drift check automatically.

Texture locations resolve against each `--assets-root`. Missing PNGs use deterministic fallback colors and appear in metadata.
For an installed Vintage Story tree, the renderer searches the `game`, `survival`, and `creative` content packs while
preserving the `game:` asset domain used by block definitions. Shape face UV rectangles and 90-degree face rotations are
honored. When a face omits UVs, the renderer generates a size-proportional cuboid mapping and records the source shape hash.

## Coplanar overlap and Z-fighting audit

The renderer compares every transformed face polygon against every face from another primitive. It reports faces when:

- their outward normals point in the same direction;
- every vertex lies on the same plane within the deterministic tolerance; and
- their convex projected intersection has positive area, rather than merely sharing an edge.

Opposite-facing faces at an intentional internal joint do not count. Each finding records both element and face names,
overlap area, and plane distance in `render-metadata.json`. Use `--fail-on-coplanar-overlap` for model evidence and CI so a
new conflict cannot be hidden by painter ordering or a favorable static view. Fix the source geometry with an intentional
micro-offset, inset, phase change, or non-overlapping construction. Do not allowlist an overlap merely because a still image
happens not to flicker.

## Geometry capabilities and limits

- Vintage Story JSON shapes are hierarchies of rotated cuboid elements. The renderer composes nested parent translation
  and rotation for a static authored pose, so ordinary block, item, and entity shapes can share the same evidence workflow.
  Complex silhouettes are commonly built from many cuboids, which remains the most portable authored format.
- `CompositeShape` can also point to OBJ, and the game API can render arbitrary `MeshData`. Flywheel Power already uses
  procedural quads for its round wheel, felloe, spokes, bearing, hub, and marker.
- This renderer currently supports Vintage Story cuboid JSON plus the Flywheel procedural manifest. Entity shapes render
  in their authored rest pose by default. Pass `--animation <code>` to render a fixed-camera textured MP4 of one looping
  shape animation; runtime shape alternates, blended animations, step-parent attachments, OBJ/GLTF import, atlas stitching,
  emissive/glow channels, and player-hand/body backdrops are not yet reproduced.
- Add importers behind the same `Face` representation rather than converting external meshes into hundreds of cuboids.
  Triangulated OBJ is the sensible next importer; embedded GLTF should remain experimental until its game support is proven.
- Texture mode samples source PNGs and UVs deterministically, but Vintage Story remains authoritative for atlas padding,
  mipmapping, filtering, lighting, animation, and held transforms.

## Evidence boundary

This deterministic renderer supports geometry review and regression evidence. Its winding tests cover every named cuboid
plane plus procedural cylinder caps and radial walls, preventing inward faces or a far cap from masquerading as visible
geometry. Vintage Story remains authoritative for atlas behavior, runtime registration, animation, lighting, and final
visual taste.
