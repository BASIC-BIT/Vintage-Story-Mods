# Rendering And Lighting

These notes are based on Vintage Story `1.22.1` decompiled source under `D:/bench/vs/source/vintagestory/1.22.1/decompiled` and assets under `D:/Games/Vintagestory/assets`.

## Sky, Sun, Moon

Relevant render order in `VintagestoryLib/Vintagestory/Client/NoObf`:

- `SystemRenderNightSky`: `EnumRenderStage.Opaque`, render order `0.1`.
- `SystemRenderSkyColor`: `EnumRenderStage.Opaque`, render order `0.2`.
- `SystemRenderSunMoon`: `EnumRenderStage.Opaque`, render order `0.3`.
- Terrain opaque rendering: around render order `0.37`.

Implication:

- A mod renderer at `Opaque` order `0.34` to `0.36` can draw an opaque replacement background after vanilla sun/moon and before terrain.
- Disable depth test and depth writes for the replacement background.
- Terrain renders afterward and naturally overwrites the replacement where blocks exist.
- Do not use sky opacity as the main readability control; fix lighting and fog separately.

Cloud caveat:

- Simple and volumetric clouds render later in `OIT`.
- A full replacement sky may need per-dimension cloud suppression or an OIT-aware cloud mask.

## Sealed-Dimension Lighting

Chunk light storage:

- `VintagestoryAPI/Vintagestory/API/Common/IChunkLight.cs`
- `VintagestoryLib/Vintagestory/Common/ChunkData.cs`
- Sunlight is stored in low 5 bits.
- Blocklight brightness is stored in bits `5-9`.
- Blocklight hue and saturation are stored above brightness.

Terrain shader contract:

- `assets/game/shaders/chunkopaque.vsh` treats vertex light alpha as sunlight and RGB as blocklight.
- Ambient color is multiplied by sunlight contribution. If sealed-dimension sunlight is zero, ambient color alone does not restore terrain detail.

Useful DimensionLib levers:

- Synthetic sunlight channel for sealed dimensions.
- Non-block blocklight floor in generated air cells.
- Per-dimension ambient modifier for color and scene brightness.
- Client-only minimum scene light overlay for black lift, with the limitation that it cannot recover texture detail lost before fog/lighting.

Avoid by default:

- Generated fake light blocks as a purely technical lighting workaround.

## Vanilla Cave Fog

Vintage Story has a built-in ambient modifier named `blackfogincaves`:

- `VintagestoryLib/Vintagestory/Client/NoObf/AmbientManager.cs`
- The client raises its fog weight when local sunlight is low.
- Sealed custom dimensions can therefore get black distance fog even if a mod sets a red fog color.

Dimension visual systems should explicitly neutralize this modifier while custom dimension visuals are active. A good timing point is a `Before` stage renderer after `AmbientManager.UpdateAmbient`, so the suppression applies before terrain rendering.

## Fog Controls

Vanilla fog shader includes:

- `assets/game/shaderincludes/fogandlight.vsh`
- `assets/game/shaderincludes/fogandlight.fsh`

Key ambient modifier values:

- `FogDensity`: exponential distance fog.
- `FogDensity.Weight` is squared during blending in `AmbientManager.UpdateAmbient` (`weight * weight * value + (1 - weight) * (1 - weight) * previous`). Low weights can make apparently nonzero fog density effectively invisible.
- `FogMin`: minimum fog amount.
- `FlatFogDensity`: layer/height fog that can create a wall-like cutoff.
- `FogColor`: fog target color and blend weight.
- `FogBrightness`: post-blend brightness influence.
- `SceneBrightness`: global scene brightness influence.

Sharp cutoff symptoms usually mean one or more of:

- `blackfogincaves` is active.
- `FlatFogDensity` is too high or badly placed for the custom dimension Y range.
- `FogDensity` or `FogColor.Weight` is too high.
- Chunk sunlight is zero, so distant terrain is already dark before fog blends over it.

## Custom Depth Fog

A custom depth-aware fog pass is feasible from a mod.

Public pieces:

- `IRenderer` stages: `AfterPostProcessing`, `AfterFinalComposition`, `AfterBlit`.
- `capi.Render.FrameBuffers[0].DepthTextureId` for scene depth.
- `capi.Render.PerspectiveProjectionMat` and `capi.Render.CameraMatrixOriginf` for inverse view-projection reconstruction.
- `capi.Shader.NewShaderProgram()` and file/memory shader registration.

Built-in patterns:

- `VSSurvivalMod/Vintagestory/GameContent/RiftRenderer.cs` samples primary scene color and depth.
- `VSEssentials/FluffyClouds/CloudRendererVolumetric.cs` reconstructs world-space depth for raymarching.

Safe first custom pass:

- Fullscreen quad.
- Sample depth only, not scene color, to avoid framebuffer feedback.
- Alpha-blend colored fog based on reconstructed distance.
- Use this after fixing sky replacement and vanilla cave-fog suppression, otherwise the custom pass can mask the wrong underlying problem.
