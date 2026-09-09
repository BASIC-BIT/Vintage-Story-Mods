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
