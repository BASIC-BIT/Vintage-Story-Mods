# DimensionLib Nether Visual Experiment Log

This is the working experiment log for `dimensionlib:nether-cavern` visuals. It exists because screenshot-only/VLM judgment has been unreliable. Do not make another baked visual/generator change without adding a new row and stopping for human subjective feedback.

## Method

- Change one independent variable per attempt whenever possible.
- If multiple variables must change together, name the bundle and explain why it is coupled.
- Always use a fresh dimension id after changing generated chunk light or generator shape; old chunks keep old baked light data.
- Capture the exact package SHA256, dimension id, seed, size, screenshot path, and all relevant visual/generator variables.
- After each attempt, pause and ask the human how it looks before changing values again.
- Treat assistant/VLM descriptions as weak notes only; human subjective feedback is the source of truth.
- Score each attempt on the same axes when possible: spawn readability, cavern mood, fog visibility, sky/backdrop weirdness, lava contribution, terrain detail, and performance/stability.

## Variables

Core lighting variables:

- `BlocklightFloor`: air-cell blocklight floor written into generated chunks.
- `SyntheticSunlightFloor`: air-cell sunlight floor written into generated chunks.
- `MinimumSceneLight`: client post-final-composition light lift.

Atmosphere variables:

- `SkyColor`: opaque pre-terrain background color.
- `FogColor`, `FogWeight`, `FogDensity`, `FogDensityWeight`, `FlatFogDensity`, `FlatFogDensityWeight`.
- `AmbientColor`, `AmbientWeight`, `SceneBrightness`, `SceneBrightnessWeight`, `FogBrightness`, `FogBrightnessWeight`.

Generator variables:

- `CeilingFormula`: controls apparent enclosure and how much background is visible.
- `ColumnThreshold`: controls pillar density.
- `LavaPoolThreshold`: controls lava contribution.
- `CeilingSpikeThreshold`: controls ceiling occlusion/detail.
- `SpawnPlateau`: controls open flat spawn area.

## Attempts

| Attempt | Package SHA256 | Dimension / Seed / Size | Changed Variables | Screenshot | Human Feedback | Result |
| --- | --- | --- | --- | --- | --- | --- |
| A: opaque sky + cave fog suppression baseline | not recomputed | `dimensionlib:qa-nether-skyfog-20260530`; seed unknown; size unknown | `SkyColor=(0.24,0.03,0.01)`, `SkyAlpha=1`, `BlocklightFloor=15`, `SyntheticSunlightFloor=0`, `MinimumSceneLight=0.16`, `blackfogincaves` suppressed | `C:\Users\steve\AppData\Local\Temp\opencode\vintagestory-skyfog-2.png` | Not formally scored. | Historical baseline only. |
| B: game-domain asset fix, no synthetic sunlight | not recomputed | `dimensionlib:qa-nether-clean-20260530`; seed unknown; size unknown | Same visual defaults as A, custom nether rock asset fixed to `game:` refs | `C:\Users\steve\AppData\Local\Temp\opencode\vintagestory-game-domain-nether-2.png` | Terrain/lava visible but silhouettes too dark. | Led to sunlight-floor hypothesis. |
| C: high synthetic sunlight floor | `C67133789ACCFA13286DCA51B2402DA74B0DBF5B847614B01E2F8ADEAE5426CB` | `dimensionlib:qa-nether-sunfloor-band-20260530`; seed `20260531`; size `5` | `BlocklightFloor=15`, `SyntheticSunlightFloor=16`, `MinimumSceneLight=0.16`, `SkyColor=(0.24,0.03,0.01)`, fog density `0.00045@0.08` | `C:\Users\steve\AppData\Local\Temp\opencode\vintagestory-sunfloor-band-nether-2.png` | "Now it's like full bright in there. What the heck? It still does not look good." | Rejected: too bright/flat. |
| D: dimmed synthetic sunlight | `E4B4E4DBB7DA8C2E0D3E391550FAE94B741A0BEC3609B94E77467D7B034DCF2B` | `dimensionlib:qa-nether-dimmer-20260530`; seed `20260532`; size `5` | `BlocklightFloor=12`, `SyntheticSunlightFloor=5`, `MinimumSceneLight=0.16`, sky/fog still near C | `C:\Users\steve\AppData\Local\Temp\opencode\vintagestory-dimmer-nether-1.png` | Not formally scored. Assistant note: still visually flat. | Inconclusive; assistant-only judgment. |
| E: darker fill and atmosphere | `4A0941B26EFC30BFB6BD86851E0EEA5779539225EE6DB385C8750F7A710F8A52` | `dimensionlib:qa-nether-dark-20260530`; seed `20260533`; size `5` | `BlocklightFloor=7`, `SyntheticSunlightFloor=2`, `MinimumSceneLight=0.08`, `SkyColor=(0.12,0.012,0.006)`, `FogColor=(0.24,0.045,0.018)@0.16`, `FogDensity=0.0009@0.12`, `Ambient=(0.74,0.34,0.20)@0.48`, `SceneBrightness=1.0@0.45`, `FogBrightness=0.95@0.20` | `C:\Users\steve\AppData\Local\Temp\opencode\vintagestory-dark-nether-2.png` | Not formally scored. | Inconclusive; assistant-only judgment. |
| F: enclosed generator pass | `659EAFF8274A0305002F508CC66F835974337A142765FB98484CDB0F472A9F03` | `dimensionlib:qa-nether-enclosed-20260530`; seed `20260534`; size `5` | Same lighting as E; lower ceiling formula, lower column threshold `0.66`, lower lava threshold `0.50`, lower ceiling spike threshold `0.68`, spawn plateau blend `10`, drop `1.8` | `C:\Users\steve\AppData\Local\Temp\opencode\vintagestory-enclosed-nether-2.png` | Not formally scored. | Inconclusive; assistant-only judgment. |
| G: dark backdrop + stronger fog density | `47450CE115280AC66CF78853CC7216851B740B0916D548EC455006DADEB4FE22` | `dimensionlib:qa-nether-darkbackdrop-small-20260530`; seed `20260536`; size `5` | Same generator as F; `SkyColor=(0.035,0.0035,0.002)`, `FogDensity=0.0016@0.16` | `C:\Users\steve\AppData\Local\Temp\opencode\vintagestory-darkbackdrop-small-nether-2.png` | "Spawn still has no lighting and there's no fog anywhere. Whatever you've done, I really don't think it's the right answer." | Rejected/current baseline for diagnosis. |
| H: light-policy bug fix + live fog probe | `A22374C6706EF6E2059B46DD2C13454DDB7B79B15812F47CB758D85F18EE70B5` | `dimensionlib:qa-lightfix-20260530`; seed `20260532`; size `5` | Bug fix: `MinimumSceneLight` no longer suppresses nether baked `BlocklightFloor=7` and `SyntheticSunlightFloor=2`; live probe overrides `FogDensity=0.028`, `FogDensityWeight=1`, `FogWeight=1`, `FogColor=(0.45,0.06,0.015)`, `FogBrightness=0.75@0.7`; defaults otherwise same as G | `C:\Users\steve\AppData\Local\Temp\opencode\dimensionlib-fogprobe-20260530.png` | Directionally promising. Strong red glow everywhere; nearby rock looks good. Fog falloff is good and strong, not necessarily too strong. Distant result is too red/saturated and too dark maroon; should be more mellow/gray and less skybox+fog stacked. Spawn itself still black, likely generation workflow issue. Terrain generation is too flat and artificial; columns read as generated instead of naturally mixed. Lava pools read like replaced terrain, need smoothing, variable recessed/same-level pools, and future ocean-scale lava. Ceiling is consistently too high; top and bottom should vary like hills/mountains that sometimes merge into solid zones. | Keep as evidence, not final defaults. Next visual variable should reduce fog saturation / gray the distance, but product priority shifts to DimensionLib API/MVP boundaries before more Nether tuning. |

## Stability Notes

- A 9x9 dark-backdrop test dimension (`dimensionlib:qa-nether-darkbackdrop-20260530`, seed `20260535`, package `47450CE...`) froze Profile2 after teleport. Keep visual tuning on 5x5 until lazy generation/performance is fixed separately.
- Full-height synthetic light writes previously froze Profile2. Bounded vertical-band light writes avoided that issue in 5x5 tests.
- Attempt H previously found a bug in the now-removed baked-light policy path: positive `MinimumSceneLight` disabled the nether `BlocklightFloor` and `SyntheticSunlightFloor`. This remains historical evidence only; baked light floors are no longer automatic defaults or public visual settings.

## Next Experiment Template

Before changing code, fill this in:

| Field | Value |
| --- | --- |
| Hypothesis |  |
| Single variable to change |  |
| Control attempt |  |
| Expected human-visible effect |  |
| Package SHA256 |  |
| Dimension id / seed / size |  |
| Screenshot path |  |
| Human feedback |  |
| Decision |  |
