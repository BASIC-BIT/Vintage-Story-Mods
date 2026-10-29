# Standalone production GUI previews

This executable runs The BASICs production dialogs and Vintage Story's compiled GUI widgets without opening the game. Their actual Cairo painters, bounds, input handlers and render calls produce PNGs through a software implementation of the GUI graphics operations.

## Run

A complete Vintage Story installation is required for full scenes, including its assets and native Cairo/Skia libraries. The small CI dependency bundle can compile the tool and run compositor/comparison tests, but cannot render full dialogs. Windows is the validated host; other platforms need matching native libraries and fonts.

Wizard PR captures use `scripts/collect-gui-captures.ps1` to supply the full environment: SHA-256-pinned official Windows server binaries and native libraries, with official Linux client assets. Only Windows binaries are executed. The `GUI Captures` workflow runs without write permissions; the trusted `GUI Capture Report` workflow compares its immutable artifacts and updates the PR's sticky report after checking the current source identity.

The automatic reference is the regenerated PR base, explicitly labeled separately from a human-reviewed screenshot baseline. Added, changed, and removed frames require review. The manual report workflow accepts an explicitly selected reviewed baseline. Neither path promotes captures or completes gameplay QA.

From the repository root:

    $env:VINTAGE_STORY = 'D:\Games\Vintagestory'
    .\scripts\preview-gui.ps1
    .\scripts\preview-gui.ps1 -Scenario language-dropdown -Scale 1.25
    .\scripts\preview-gui.ps1 -Test

Pass -DotNet PATH if the .NET 10 SDK is not on PATH. Output defaults to ignored .superpowers/sdd/gui-preview/output. Each scene produces a PNG and a JSON provenance manifest. Use a separate output directory for each scale or environment.

Use `-Wizard` for all registered setup scenes at scales 1 and 1.25, including chat, language definitions, travel, player display, character sheets, notes and the open privilege picker. Seraphs use the same appearance/outfit fixtures as the native wizard, the real player and attachment shapes and PNG textures, the game's shape tessellator and its skeletal animator. A software triangle renderer draws these meshes without starting a client or creating a game window. Missing model assets fail the capture; there is no cartoon fallback.

The script stamps a normalized source-tree hash into the built mod; a later test build can remove that stamp, so rerun the preview script before capturing. A claimed hash that does not match the compiled mod is rejected. Wizard manifests label this coverage `standalone-gui-and-seraph`. They record used assets and hashes, mesh counts and sampled poses separately from native-client GPU evidence.

The wizard language editor captures use custom Dock Cant and Quayside definitions. `wizard-language-configured` shows the configured color and syllables, `wizard-language-edited` shows an edited draft, `wizard-language-invalid` shows validation feedback, and `wizard-language-grants` shows class, trait and model grants. Both understood and unfamiliar speech use the selected row. Each wizard manifest records setting values and language definitions together in `fixture`.

For the language color picker, `wizard-language-color-palette` shows the swatches, `wizard-language-color-custom` opens the spectrum under Custom, and `wizard-language-color-selected` shows a changed color in the chat examples. These scenes also work in native captures when explicitly selected; the default native set stays at 26 scenes.

`wizard-chat-colors` and `wizard-chat-colors-custom` show the emote and OOC colors beside the chat preview. `wizard-nametag-colors` uses the game theme, while `wizard-nametag-colors-alpha` and `wizard-nametag-colors-custom` use translucent background and border colors with the palette or Custom panel open. These five scenes are opt-in for native captures and do not change the 32-scene session limit.

The other production editors are available as `language-color-palette`, `language-color-custom`, `admin-chat-colors`, `admin-chat-colors-custom`, `admin-nametag-colors`, and `scene-marker-colors`. The scene marker editor keeps its five stored color presets. Capture that existing large editor at 1600 by 1000; it does not fit a 1280 by 720 viewport at scale 1.25.

For native client captures, Profile2 provisioning, launch, scale probes and sampled-pose clips, use `.opencode/skills/vintage-story-agent-control/SKILL.md`. That workflow keeps source identity, installed-package hashes and restoration receipts together.

Scenes include language editor default, focused input, open dropdown, button hover, tooltip, error status; admin chat, bubble and Discord settings; and dice bubbles. Fixtures use local callbacks and do not send settings to a server.

The image-producing tests assert real control state, unsupported-call absence and repeatable frames. They are opt-in through the script because normal CI lacks full game assets. The normal suite still runs known-pixel compositor and image-comparison tests.

## Animation evidence

Scene marker custom colors have production-dialog fixtures `scene-marker-colors`, `scene-marker-custom-color`, `scene-marker-custom-picker`, `scene-marker-custom-icon`, and `scene-marker-custom-locked`. The spectrum fixture clicks Custom and fails if its popup is not open. Use a 1600x1000 or larger viewport for this existing editor, especially at GUI scale 1.25.

The GUI test command also checks actual seraph poses, same-identity actor isolation, capture versus live playback, drag momentum and production scene integration. `SeraphSceneTests.TravelSequenceKeepsRealActorsAcrossWarmupFadeArrivalAndReplay` writes 80 deterministic frames under `<output>/seraph-travel-motion` when `THEBASICS_GUI_OUTPUT` is set. The preview script sets this variable for `-Test` runs. Encode those frames with the existing FFmpeg installation, for example:

    ffmpeg -framerate 10 -i .scratch/current/seraph-travel-motion/frame%03d.png -c:v libx264 -pix_fmt yuv420p .scratch/current/seraph-travel.mp4

This is standalone animation evidence, separate from native-client frame-rate or GPU calibration checks.

## Compare

    .\scripts\preview-gui.ps1 -Output .scratch/current -Baseline .scratch/reviewed
    dotnet tools/GuiPreview/bin/Debug/net10.0/TheBasics.GuiPreview.dll compare --expected before.png --actual after.png --diff difference.png

A mismatch or missing baseline returns failure. Changed pixels are magenta in the diff. Baselines are never automatically written or approved. Review candidate images before copying them to a separate baseline directory. Match game binaries, assets, operating system, font environment, viewport, scale and scene state. Generated candidates are not calibrated game screenshots.

## Extend

Add a fixture to PreviewScene that constructs the production dialog and drives its actual input events. Freeze clock and fixture data. Do not copy widget layouts or drawing code into the preview. Private game constructors and existing private admin entry points are accessed by small reflection bridges; a changed game API must fail visibly.

PreviewHost supplies only the client services exercised by these scenes. Unsupported calls fail and are recorded; production logger errors also fail. Add a supported operation only after tracing its game behavior and adding independent pixel or lifecycle tests. The host temporarily isolates GUI scale, fonts, language and pattern caches, and restores them when disposed. Use hosts sequentially because the original GUI has process-global state.

SoftwareCanvas implements texture upload/update/deletion, standard and premultiplied alpha, tint, repeat sampling, nearest/linear filtering, translation, clipping, depth and focus outlines. It replays the real GUI draw calls. ImageComparison compares decoded RGBA pixels rather than PNG file bytes.

## Fidelity and limits

These are complete production GUI renders through a bounded software backend, not an HTML approximation. They have **not yet been calibrated against in-game screenshots**. Default English, sans-serif/Lora font choices, fixed input/time and a neutral background are explicit fixture inputs. Font substitution and rasterization remain platform dependent.

The bounded seraph backend supports the shipped Pip/Mara fixtures, native skeletal poses, textured triangles, depth, transparent pixels, rotation and opacity. Its lighting is a readable fixed approximation of GUI lighting. It does not implement arbitrary world rendering, custom GPU shaders, general item/block rendering, world-space speech-bubble placement or multiplayer behavior. Focus outline pixel boundaries and hardware depth precision may differ from GPU output. Unsupported future GUI or model operations stop the render instead of silently producing an incomplete passing image.

Keep live dice QA and game screenshot calibration separate from standalone conformance tests. Do not mark either complete based on these artifacts.
Future coverage includes viewport-edge tooltips, long text selection, additional locales and more settings groups. The current fixtures do not claim exhaustive GUI coverage.

## Character-sheet builder fixtures

The eight `wizard-sheet-builder*` scenes use a complete server-style field snapshot and the production `characters.builder` page. Fixture manifests include `sheetFields` alongside scalar values and languages. The portrait is the existing actual seraph renderer.

| Scene suffix | Expected state |
|---|---|
| (none) | First portrait-side field, fictional answers, body fields and consecutive half-width pair |
| selected | Last body row selected through All fields, scrolled into view, compact Back to sheet exercised |
| choice | Calling selected, third choice input focused so all three choice rows are in view |
| hidden | Staff-only continuity field selected in Player context, hidden explanation and inspector |
| empty | Empty fictional answers through the actual Filled/Empty control |
| inspector | Long-text field selected, Label input has keyboard focus, returning inspector below 850 content pixels |
| invalid | Actual Add choice appends a blank row, the invalid fourth choice is focused, then the scrollbar moves one step to reveal its error, validation appears and last-valid preview remains |
| review | Label edited through the actual inspector input, grouped review shows its net change |

Capture each at 1600x1000 and 1280x720, scales 1 and 1.25. Production sheet width is 550 logical pixels. The inspector returns to the sheet below 850 content pixels. The selected fixture reaches the last scroll row; the default fixture starts at the first. Native fixture tests cover the full 32-frame matrix. Button clicks intentionally record `sound:menubutton_down`; every other effect and every unsupported call fails the fixture test. No player-answer save is permitted.

Use `scripts/preview-gui.ps1 -GamePath D:/Games/Vintagestory -Scenario wizard-sheet-builder-choice -Scale 1.25 -Width 1280 -Height 720 -Output <new-directory>`. Keep package receipts, source identity and archived ZIP hashes beside dated review galleries. These are standalone software renders, pending human appearance review and game/GPU calibration.
