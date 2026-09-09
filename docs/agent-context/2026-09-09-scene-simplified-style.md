# Simplified scene style and bubble size

BASIC requested removal of the reading cue, basic color names, one permanent partially transparent plain style, a fix for the missing diamond glyph in the selector, independent bubble size, and an optional title-icon selector window.

- Hidden bodies produce only the title, with no read cue. Untitled markers with hidden bodies produce no floating bubble. Full reading remains available.
- Labels are Yellow, White, Blue and Green. Existing palette values and stored IDs remain compatible.
- Indicator opacity is fixed at 68 percent before distance fade. The effect picker, scan lines and shimmer are removed. Legacy effect values normalize to Plain; wire/storage members remain reserved.
- Indicator tiles draw actual artwork instead of font characters, including the diamond. Color changes redraw the artwork.
- Bubble size is 50-200 percent, default 100. It scales text and background together after the existing layout/height cap, independently of indicator size. Culling radius accounts for the enlarged panel.
- Title icon opens a separate six-symbol picker with None. Selection stays in the unsaved editor until Save. The optional icon appears to the left of nonempty titles, in both preview and world. Namespaced custom icons register client-side and are removed on disposal.
- Bubble scale/member20 and title icon/member21 persist through save, pickup and fresh-marker appearance defaults. Both controls honor locks. Bubble remains stationary.

Validation: 796 tests passed. Visual QA pending.

QA cards:
1. Compare all six selector buttons to the preview. Diamond must display artwork, and every button remains clickable. Change color: labels use base names and selector artwork updates.
2. Confirm no effect picker or read cue. Compare old Plain and Hologram markers: both use the same steady translucent style, still obscured by walls.
3. At fixed indicator size, compare bubble size 50/100/200 percent. Text and background scale together. Check long titles/descriptions at screen edges and steep angles.
4. Open Title icon, choose each symbol and None. Expect icon left of title; cancel the parent editor without saving and verify no change. Save/reopen and pick up/replace to verify persistence. Closing the parent must close its picker.
5. Lock the marker: bubble-size input and icon picker must be disabled. Fresh markers inherit appearance preferences without content.

Review correction: replaced the game's leaking dynamic custom-draw helper with a scene-scoped element that owns and disposes its LoadedTexture. Main preview, selector artwork and title-picker artwork all use it. Both review axes clear; 796 tests pass.

QA package: thebasics_5_9_1.zip, 602233 bytes, SHA256 5B0BAE674C4AE15E85EBEDFE31D1CBEBC8C14D93E79E68E905F3A76B000018A8. Packaged DLL matches the tested DLL; packaged language and handbook JSON parse successfully. Server readback and both profile copies match this hash. Previous packages preserved under .tmp/scene-style-qa-2026-09-09. Both QA clients relaunched at their menus without auto-connect. Human visual acceptance remains pending.
Server reached RunGame at 2026-09-09 05:57:51 UTC with SceneDescriptionSystem loaded. No scene startup exception observed; existing unrelated startup warnings remain.
