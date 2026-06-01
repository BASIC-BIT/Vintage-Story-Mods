# DimensionLib Render Effects Notes

This note records the current render/lighting research direction for DimensionLib dimensions. It is a design note, not a committed public API contract.

Reusable Vintage Story rendering findings are also summarized in `../../../docs/vintage-story-modding-knowledge/rendering-and-lighting.md` as the seed of a future shared modding skill/knowledge tree.

## Current Finding

The nether-cavern visual stack should not rely on a translucent sky overlay to hide the sun and moon. The safer model is:

- Render an opaque DimensionLib sky/background replacement after vanilla sky, sun, and moon renderers.
- Let terrain render after that replacement so terrain still naturally occludes the background.
- Handle dimension lighting and fog separately instead of reducing sky opacity to recover readability.

Vintage Story's relevant opaque-stage render order is:

- `SystemRenderNightSky`: `Opaque`, order `0.1`.
- `SystemRenderSkyColor`: `Opaque`, order `0.2`.
- `SystemRenderSunMoon`: `Opaque`, order `0.3`.
- Terrain opaque: around order `0.37`.

A DimensionLib replacement sky renderer at `Opaque` order `0.34` to `0.36`, with depth test disabled and depth writes disabled, should hide the vanilla sky, sun, and moon while still being overwritten by terrain.

Clouds are separate. Simple and volumetric clouds render in `OIT`, after opaque terrain/sky. A full custom-sky mode may need to disable or mask clouds per dimension.

## Lighting Model

Vintage Story terrain expects chunk light data, not just ambient color:

- Chunk light stores sunlight in the low 5 bits.
- Blocklight brightness is stored in bits `5-9`.
- Blocklight hue/saturation are stored above that.
- Terrain shaders multiply ambient visibility by the sunlight channel.

For sealed dimensions, zero sunlight means ambient color alone cannot recover terrain detail. Lava and blocklight are local and will still fall off quickly.

The current air-cell blocklight floor improves nearby readability, but terrain ambient still depends on the sunlight channel. The built-in nether-cavern settings therefore also apply an experimental synthetic sunlight floor to generated air cells in the cavern's vertical band. The more correct DimensionLib model is probably a first-class ambient-light policy for generated dimensions:

- Optional synthetic skylight channel for sealed dimensions.
- Optional blocklight floor for air cells where that is the desired visual model.
- No generated fake light source blocks unless they are deliberate, non-interactable world features.

Fresh QA on 2026-05-30 showed an air-cell sunlight floor of `16`, and then `5` with a high blocklight floor, made distant nether terrain readable but too close to fullbright. The current default is intentionally much lower and should be judged in a fresh dimension because already-baked chunks keep their older light values.

The generator also needs to keep the cavern ceiling low enough that the opaque background does not read as a huge open red skybox.

## Vanilla Cave Fog Issue

Vintage Story has a built-in `blackfogincaves` ambient modifier. The client raises this modifier when local sunlight is low. In sealed generated dimensions, that can make distance fog mix toward black and produce the current hard transition from nearby detail to silhouettes.

DimensionLib currently neutralizes this modifier while the player is inside a DimensionLib dimension with explicit visual settings that request cave-fog suppression. This is separate from tuning `fogdensity` and `flatfogdensity`.

## Custom Fog Engine

A depth-aware custom fog pass appears feasible from a normal client mod.

Public/mod-accessible pieces:

- `IRenderer` stages including `AfterPostProcessing`, `AfterFinalComposition`, and `AfterBlit`.
- `capi.Render.FrameBuffers[0].DepthTextureId` for scene depth.
- `capi.Render.FrameBuffers[0].ColorTextureIds[0]` for scene color, with framebuffer feedback caveats.
- `capi.Render.PerspectiveProjectionMat` and `capi.Render.CameraMatrixOriginf` for inverse view-projection reconstruction.
- Shader creation via `capi.Shader.NewShaderProgram()` and file/memory shader registration.

Useful built-in patterns:

- `VSSurvivalMod/Vintagestory/GameContent/RiftRenderer.cs` samples primary scene color/depth from a mod renderer.
- `VSEssentials/FluffyClouds/CloudRendererVolumetric.cs` reconstructs world-space depth from the primary depth texture.

A first DimensionLib custom fog pass should probably be a fullscreen depth-aware alpha overlay that does not sample scene color. This avoids framebuffer feedback while giving control over fog curve, color, and softness.

## Visual Environment Shape

Possible future internal pieces:

- `SkyReplacement`: color, gradient, texture, opacity, cloud policy.
- `AmbientLight`: synthetic skylight level, blocklight floor, color temperature.
- `Fog`: vanilla ambient fog values plus optional custom depth-curve pass.
- `PostEffect`: color grading, shimmer, distortion, vignette, or other dimension-specific effects.
- `Suppression`: vanilla cave fog, temporal instability visuals, vanilla clouds, sun/moon visibility.

Possible public API direction later:

- Keep the stable public API at the level of explicit `DimensionVisualSettings` fields unless repeated consumer code proves a reusable recipe layer is needed.
- Keep low-level shader knobs internal/debug until the model is proven.
- Expose live tuning through `/dlib visual` or a future in-game editor before freezing public API.

## Recommended Next Implementation Order

1. Use `VISUAL_EXPERIMENT_LOG.md` for every visual attempt.
2. Change one independent variable per attempt whenever possible.
3. After each screenshot, pause for human subjective feedback before changing code or defaults again.
4. Promote successful behavior into a named ambient-light policy instead of hardcoded built-in-generator constants.
5. Prototype a custom depth-aware fog pass only if the lighting policy still cannot produce a soft readable distance falloff.
