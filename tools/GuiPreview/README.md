# Standalone production GUI previews

This executable runs The BASICs production dialogs and Vintage Story's compiled GUI widgets without opening the game. Their actual Cairo painters, bounds, input handlers and render calls produce PNGs through a software implementation of the GUI graphics operations.

## Run

A complete Vintage Story installation is required for full scenes, including its assets and native Cairo/Skia libraries. The small CI dependency bundle can compile the tool and run compositor/comparison tests, but cannot render full dialogs. Windows is the validated host; other platforms need matching native libraries and fonts.

From the repository root:

    $env:VINTAGE_STORY = 'D:\Games\Vintagestory'
    .\scripts\preview-gui.ps1
    .\scripts\preview-gui.ps1 -Scenario language-dropdown -Scale 1.25
    .\scripts\preview-gui.ps1 -Test

Pass -DotNet PATH if the .NET 10 SDK is not on PATH. Output defaults to ignored .superpowers/sdd/gui-preview/output. Each scene produces a PNG and a JSON provenance manifest. Use a separate output directory for each scale or environment.

Scenes include language editor default, focused input, open dropdown, button hover, tooltip, error status; admin chat, bubble and Discord settings; and dice bubbles. Fixtures use local callbacks and do not send settings to a server.

The image-producing tests assert real control state, unsupported-call absence and repeatable frames. They are opt-in through the script because normal CI lacks full game assets. The normal suite still runs known-pixel compositor and image-comparison tests.

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

This does not implement arbitrary world rendering, custom shaders, item/block meshes, animation timing, world-space speech-bubble placement or multiplayer behavior. Focus outline pixel boundaries and hardware depth precision may differ from GPU output. Unsupported future GUI graphics operations stop the render instead of silently producing an incomplete passing image.

Keep live dice QA and game screenshot calibration separate from standalone conformance tests. Do not mark either complete based on these artifacts.
Future coverage includes viewport-edge tooltips, long text selection, additional locales and more settings groups. The current fixtures do not claim exhaustive GUI coverage.
