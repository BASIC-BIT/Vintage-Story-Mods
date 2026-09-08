# Production-code UI image tests for The BASICs

Date: 2026-09-08. Scope: research only, no production changes or game launch. Local game source examined is decompiled Vintage Story **1.22.6**; online API pages are rolling documentation. Recheck implementation details when targeting another game version.

## Finding

**Yes. There are two useful levels: run our existing Cairo painting code directly to produce authentic component PNGs, then use an actual client render harness for complete settings-dialog screenshots.** The first is already demonstrated by the dice preview. The second is feasible from the available composition and screenshot APIs, but no automated client harness has been demonstrated in this investigation. A PNG artifact and an assertion comparing it to an approved image are separate capabilities.

## What the dice preview actually did

The earlier disposable probe sets `RuntimeEnv.GUIScale = 1`, calls production `DiceBubbleTexture.CreateSurface` with a real Cairo font, and writes that returned surface to PNG. Production `DiceBubbleTexture.Create` calls the **same surface method** before uploading it through `LoadOrUpdateCairoTexture`. This is an image of the production component's pixels, rather than a separate drawing approximating the implementation. It omits the subsequent game composition, world positioning, distance scaling, occlusion and fade. Sources: [probe lines 11-16](D:/bench/vs/work/thebasics-dice-design/.superpowers/sdd/2026-09-08-thebasics-dice/DicePreviewProbe.cs:11), [production surface/upload seam lines 22-31](D:/bench/vs/work/thebasics-dice-design/mods-dll/thebasics/src/ModSystems/ChatUiSystem/DiceBubbleTexture.cs:22).

Cairo itself supports rendering image surfaces to PNG. Thus a test can execute the production painter, check dimensions/content invariants, save its surface, and optionally compare decoded pixels. It needs compatible native Cairo and fonts, but not the later game texture-upload call. [Cairo's image-surface example](https://www.cairographics.org/documentation/cairomm/reference/image-surface_8cc-example.html).

There is an important fidelity qualification: the old probe supplied a fixed size-25 font for every example. Production selects `25 * mode multiplier`, including 0.75 for whisper and 1.3 for yell. Also, the game replaces the API's default `sans-serif` font name with the client's selected default font during startup. The old images prove the shared painter executed, but not that its inputs matched the complete production presentation path. A durable fixture should invoke the real upstream presentation/font selection with controlled settings, rather than supply plausible handwritten inputs. Sources: [SpeechBubbleVtmlPatches.cs:219](D:/bench/vs/work/thebasics-dice-design/mods-dll/thebasics/src/ModSystems/ChatUiSystem/SpeechBubbleVtmlPatches.cs:219), [GuiStyle.cs:266](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiStyle.cs:266), [ClientProgram.cs:161](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryLib/Vintagestory/Client/ClientProgram.cs:161).

The current production surface is already `internal`, so a durable test should call it through the repository's normal test-assembly access rather than perpetuate the temporary reflection probe. Restore process-global GUI scale/font state after a fixture, and serialize fixtures that alter it. This is a design recommendation, not a completed pipeline.

## Why the settings GUI needs more than saving one surface

The current admin settings path builds `JsonDialogSettings` and constructs `ConfirmingConfigAdminDialog`, which derives from `GuiJsonDialog`. The production layout/schema should remain the source of truth: tests should instantiate that dialog with controlled settings and dependencies, not reproduce it in HTML or a parallel drawing routine. Sources: [ChatUiSystem construction at 1231](D:/bench/vs/Vintage-Story-Mods/mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs:1231), [settings builder at 1303](D:/bench/vs/Vintage-Story-Mods/mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs:1303), [dialog subclass at 2420](D:/bench/vs/Vintage-Story-Mods/mods-dll/thebasics/src/ModSystems/ChatUiSystem/ChatUiSystem.cs:2420).

The game has a useful separation, but not a turnkey offscreen PNG mode:

| Stage | Authoritative behavior | Consequence for tests |
| --- | --- | --- |
| Dialog definition | `GuiJsonDialog.ComposeDialog` creates a composer, converts JSON elements into actual controls, then calls `Compose`. | A schema test can verify fields, options and dimensions without proving appearance. |
| Static composition | `GuiComposer.Compose` calculates bounds, draws elements onto an `ImageSurface`, uploads it, then disposes it. | A capture hook at upload can capture that surface, but it is only one layer. |
| Interactive controls | Buttons create separate normal/pressed/hover/disabled textures. Sliders, text inputs and dropdowns also paint separate textures and render them later. | Capturing only the composer's static surface produces an incomplete settings preview. |
| Final dialog rendering | `GuiComposer.Render` draws the static texture and invokes interactive render methods in order, then focus overlays. | Complete appearance includes texture placement, clipping, selected control state and overlays. |
| GPU upload | Windows platform texture upload checks main thread, creates/binds an OpenGL texture and calls `TexImage2D` or `TexSubImage2D`. | Calling the ordinary production composer with only a trivial fake API is insufficient. |

Exact local source evidence:

- [GuiJsonDialog.cs:66](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiJsonDialog.cs:66), composition through line 91; controls through line 250.
- [GuiComposer.cs:356](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiComposer.cs:356), bounds/surface/upload/disposal through line 425; [render at 707](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiComposer.cs:707).
- [GuiElementTextButton.cs:96](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiElementTextButton.cs:96), texture variants through line 133; [render at 192](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiElementTextButton.cs:192).
- [GuiElementSlider.cs:249](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiElementSlider.cs:249), texture rendering and scissor state; [GuiElementTextInput.cs:98](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiElementTextInput.cs:98), text clipping/focus; [GuiElementDropDown.cs:273](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiElementDropDown.cs:273), selected value and menu rendering.
- [ClientPlatformWindows.cs:2281](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryLib/Vintagestory/Client/NoObf/ClientPlatformWindows.cs:2281), main-thread/OpenGL upload through line 2306.

The public documentation corroborates the compose/render separation: [GuiComposer API](https://apidocs.vintagestory.at/api/Vintagestory.API.Client.GuiComposer.html). It does not document a complete headless image-export function.

There are additional dependencies beyond OpenGL: patterned elements retrieve real assets and decode PNGs; scale affects bounds and pattern transforms. Stubbed empty assets or substituted fonts can make the output misleading. [GuiElement.cs:338](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiElement.cs:338), pattern cache, GUI scale and asset decoding through line 369; [scale helper at 192](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/GuiElement.cs:192).

## Complete screenshots: viable integration path

The public `IRenderAPI.GrabScreenshot(width, height, scaleScreenshot, flip, withAlpha)` returns a bitmap. In the examined implementation it reaches `Screenshot.GrabScreenshot`, which uses `GL.ReadPixels`. This supports a future test-only client harness that opens the real dialog, applies a deterministic state, waits for composition/rendering and captures its rendered region. It requires an initialized client/OpenGL context and correctly timed capture; an offscreen standalone OpenGL host has **not** been proven sufficient to initialize all Vintage Story services. [Official IRenderAPI](https://apidocs.vintagestory.at/api/Vintagestory.API.Client.IRenderAPI.html), [API source at 361](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryAPI/Vintagestory/API/Client/IRenderAPI.cs:361), [native screenshot at 52-55](D:/bench/vs/source/vintagestory/1.22.6/decompiled/VintagestoryLib/Vintagestory/ClientNative/Screenshot.cs:52).

An alternative would intercept texture uploads, retain CPU copies and emulate every needed render operation into a Cairo target. That could execute original control painters, but it would also implement clipping, texture state, ordering, alpha behavior and more. **Recommendation: do not start by building a general fake game renderer.** Prove one real settings-dialog screenshot through the client first. Consider a narrowly bounded CPU adapter only if running a client is demonstrably too expensive and its coverage can be checked against real screenshots.

## Assertions, baseline review and determinism

Suggested outputs per fixture: current PNG, approved baseline, visual diff on mismatch, plus a small manifest recording game version, mod commit, fixture inputs, font, locale, GUI scale and viewport. Run cheap semantic assertions too: text value, expected marker, nonzero dimensions, control bounds, selected settings and safe clipping. Image comparisons detect changed appearance, not whether the UI is correct or usable.

For .NET, Verify can own baseline acceptance and `Verify.ImageMagick` provides image comparers, a configurable threshold and transparent-background handling. Its documented default alternative is binary comparison, which is less appropriate when PNG encoding changes but pixels do not. Evaluate decoded-pixel comparison with a small justified tolerance; the library's example threshold is **not** a recommended project threshold. [Verify.ImageMagick documentation](https://github.com/VerifyTests/Verify.ImageMagick/blob/main/readme.md).

Pin the render environment: game/native library versions, OS, fonts, locale, GUI scale, viewport, theme/colors, time and input state. For real-client cases also control cursor location, caret blink, hover delay, animation/fade time and the background. Run several unchanged renders before making comparison a gate. These are recommended experimental controls inferred from the rendering dependencies above. Playwright independently documents environment-sensitive screenshot baselines, but is relevant as testing practice, **not as a renderer for Vintage Story**. [Playwright visual-comparison guidance](https://playwright.dev/docs/test-snapshots).

Baseline changes should be reviewed with before/after/diff artifacts and an explanation of the intended change. Never automatically approve a new image because a test produced it. Keep uncertain client cases as artifacts until repeatability is demonstrated rather than hiding unstable regions with broad tolerances.

## Recommended first implementation slice

1. Promote the dice surface probe into a small image-producing test fixture that calls production presentation/font selection and the existing surface method. Cases: d20, `(W)`/`(Y)`, success pool, long result, large side label, and two supported GUI scales. Save PNG artifacts even before adding baselines.
2. Add reviewed baselines and focused semantic assertions once repeated runs are stable. Prove a deliberate visual regression fails and an intentional baseline update is reviewable.
3. Build a test-only client fixture for **one existing settings dialog**, using the actual dialog builder. Capture default state, dropdown open, focused input and a scrolled state where the dialog supports scrolling. Start with local execution and artifact capture; CI/client automation is a separate feasibility check.
4. Expand by reusing these two seams, production component surfaces and production dialog captures. Do not introduce a new presentation framework simply to make tests easier.

This can remove much repetitive appearance checking and make PRs visually reviewable. It does not by itself prove multiplayer routing, input behavior, lifecycle or how an overhead bubble appears in a live world. Keep those existing QA requirements until equivalent runtime evidence is actually established.

## Next experiment

This investigation recommends the small dice image-test pilot first, followed by a bounded settings-client capture experiment. Exact hosting, fixture isolation and CI execution of the real client remain open experiments, not established capabilities. No implementation was made during this research.
