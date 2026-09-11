# Scene marker readability and editor layout QA follow-up

User requests: clear last title icon reliably; restore full 100% default and make long descriptions legible even at 40%; tighten pagination arrows; put Icon beside title; add Red; place display and include-description controls together; replace Save & lock with a top-right lock icon.

- Legacy title icon migration now consumes its integer selection once. A regression first failed with a cleared name becoming the old diamond again, then passed. The renderer cache captures text/icon values rather than retaining a mutable data reference.
- Restored full BubbleScale baseline. World size is texture dimensions divided by 60 GUI-scaled texels per block, multiplied by 40-200% scale. No fixed bubble width, tall-panel shrink, or approach-distance shrinking. A 360px panel at40% is2.4blocks wide versus old0.6;1200pxheight is8blocks. Text culling uses real bubble extents. Preview uses the same dimensions but fits the combined scene into its thumbnail.
- Icon button sits left of title input. Page controls are adjacent. Description-display dropdown and include-body toggle are side by side.
- Red appended as enum ID4, existing IDs unchanged.
- Top-right lock glyph toggles pending lock state on unlocked editable markers; Save applies it. Cancel discards it. Already-locked markers use the same authorized unlock callback. No Save & lock button remains.

Validation:803tests pass including migration clear, cache key changes and constant letter density across short/long panels and GUI scales. Review and human QA pending.

QA cards:
1. Clear a legacy and a named icon, Save/reopen, pick up/replace, then place a fresh marker. Confirm no icon remains or reappears.
2. At40%, compare short and long descriptions at the same distance. Letters remain equally sized; long bubble grows. Repeat100/200%, approach and walk back, view at screen edges/steep angles.
3. Open Icon beside title, page arrows should hug page count; select Red and inspect preview/world.
4. Toggle description display and include-body adjacent controls independently. Default body remains hidden.
5. Toggle top-right lock closed then open, Save; verify correct state. Cancel a pending lock; no state change. Save a pending lock and verify authorized unlock still works; unauthorized player cannot toggle.
Review fixes: retain visited offscreen descriptions in the zero-visible render path, and include GUI scale in texture cache validity. Both review axes clear;803tests pass after fixes. Standard build/package passed; package DLL matches tested DLL, packaged JSON parses. Candidate SHA256 6D52EF455B441E1871CB4A6C004B290E43F34511C272EB74F2DA945AD24F525E.
QA deployment: server readback and Profile2/Profile3 packages match candidate hash. Previous packages and logs preserved under .tmp/scene-layout-qa-2026-09-09. Human visual acceptance pending.

Size follow-up: 100 percent was far too large in-world, so texel density doubled from 60 to 120 GUI-scaled texels per block. A 360px panel at 100 percent is now 3 blocks wide instead of 6; the 40-200 percent range and letter-density behavior are unchanged. Preview and world share the constant.

Icon follow-up: the editor now draws the selected title icon in a 22px tile above the Icon button, redrawn with the preview; empty when no icon is chosen.
QA package (half-size bubble + icon tile): thebasics_5_9_1.zip, 600362 bytes, SHA256 476C48A3987D737D1B23FF32814F36C847E8F5CB6E0D74328025F0FF186C1E85. 803 tests pass. Server upload via SFTP (WinSCP needs Windows PowerShell 5.1, not pwsh 7) and both profile copies match this hash. Server restarted 2026-09-10 00:10 UTC. Human visual acceptance pending.

## 2026-09-10 follow-up slice (approved by BASIC via the QA-gates session)

- EnableSceneMarkers server toggle (ProtoMember 155, default true, restart-required). Off: recipe removed at AssetsFinalize, placement and editing refused on both sides, overlay and pick patch skipped per frame; block, entity, and handbook stay registered. Client reads the synced config and defaults to enabled until it arrives.
- Scene marker item shows the exclamation glyph via shapeInventory with a chalk accent texture; placed block unchanged. Glyph is chalk-white, not the per-marker color, because color is applied per block entity at render time.
- Editor hides only the text-distance row outside always-nearby mode and pulls height and size up 66px. Icon distance stays in every mode because it drives indicator fade everywhere; the original brief would have hidden a live setting.
- FEATURES.md, smoke-test card 6, and rp-culture Entry 6 rewritten from the code. Note from that pass: a locked marker is read-only for everyone including its creator until unlocked.

QA package: thebasics_5_9_1.zip, 601879 bytes, SHA256 78142B7D0D8EEC549F1A62162C30A74570182F6A8A5987094DF35A7917B9302D. 805 tests pass, tooling check passes. Server upload and both profile copies match this hash. dotnet format whitespace is red on pre-existing lines in the tests project; not touched. Human visual acceptance pending.

Polish 2026-09-10: placed markers register with the renderer on first server sync instead of at Initialize, removing the default-gold flash before the placer preferences arrive. Lock hover now reads "Lock this marker when you save. Only you or an admin can unlock it." Display-mode label is "Show bubble". QA package: thebasics_5_9_1.zip, 601960 bytes, SHA256 001CA1EB36D4F9789F0CFF69D65E42F693BDB8B1083768F110F7569E6FE44279; 805 tests pass; server, Profile2, Profile3 match.
