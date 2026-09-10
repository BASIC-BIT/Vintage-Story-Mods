# Scene marker symbol from the icon catalog

The floating billboard symbol can now be any catalog icon, not only the six built-in shapes.

- `SceneDescriptionData.SymbolIconName` (tree `sceneSymbolIconName`, proto member 23) sits beside `Symbol`; empty means use the enum. Old saves read empty and are unchanged.
- `SymbolIconKey` (`SymbolIconName`, else `sym:<int>`) keys the renderer texture cache with the color, so a custom icon and an enum symbol never share an entry.
- `SceneTitleIcons.Draw` takes an optional `SceneMarkerColor` and tints both the DrawIcon and SVG paths; the six registered `thebasics-scene-title-N` icons now honour the rgba they are handed instead of hardcoding Parchment (so title icons chosen from them draw white like the rest of the catalog).
- Editor: `Other icons...` at (660, top+210) with a 22px indicator tile at (630, top+212). Its own picker field, closed with the editor. A symbol tile clears the name; a catalog icon unlights all six tiles.
- Fallback: if `SceneTitleIcons.Draw` returns false the renderer and the preview draw the enum symbol, never nothing.

Known risks (human visual QA):
- Catalog icons are antialiased; their soft edges may fringe against the billboard's straight-alpha blend, unlike the opaque enum artwork.
- Thin-stroke icons read poorly at billboard size and at low indicator scale.
- Third-party custom icon renderers may ignore the rgba they are given and draw in their own color.

Retire once PR #243 merges and smoke-test card 6.3 has passed once on the test server.
