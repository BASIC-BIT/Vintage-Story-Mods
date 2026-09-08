# Scene marker polish

BASIC approved all six proposed improvements, excluding personal already-read feedback. Scope: gentle target growth, independent nearby-text range, height adjustment, four colors, complete editor preview, and remembered appearance for the next fresh marker.

New markers default to 24-block icon range and 8-block text range. Both have independent Unlimited switches. Existing saved markers migrate to their previous shared range. Height offset is -0.5 to 4 blocks. Colors are warm gold, parchment, muted blue and soft green. The symbol eases toward 110 percent size when targeted.

The preview shares world text layout and bounded floating previews, updates while editing, and hides floating text for On interaction. Server player mod data remembers the last accepted appearance settings, including display mode and height. Fresh placements inherit those settings with empty content; picked-up markers retain their stored data. Failed saves do not update preferences.

A client-only selection supplement tests raised symbols, honors the existing block hit distance and filter, and leaves collision tests unchanged. The server continues to enforce existing claim, lock and reach checks. Its Harmony patch is removed on shutdown.

QA cards, pending human observations:
1. Aim at and away from each symbol. Expect gentle growth and return, retained idle bob, and no flicker.
2. Set Always nearby, icon distance 24 and text distance 8. Walk toward and away: expect separate fades; test each Unlimited independently, including text range greater than icon range.
3. Try heights -0.5, 0 and 4. Aim at the actual symbol and right-click/Shift-right-click. Expect inspector, read and permitted edit behavior. Try behind a solid wall and beyond reach: no interaction through either.
4. Compare all four colors and three symbols in preview and world. Edit multiline text and title, including long words. Expect matching wrap and no raw markup. On interaction preview should show only the symbol.
5. Save appearance; place a freshly crafted marker. Expect same appearance, empty title/body. Place a picked-up marker: expect its own data. Reconnect and repeat with a fresh marker; another player must have independent defaults.
6. Lock the marker and use another claim member. Expect new controls disabled; no denied edit may change defaults. Reopen, pick up/replace, and restart to check settings persistence.

Local implementation only until normal review/build and authorized QA refresh. No release or merge implied.

Verification: 784 tests passed, including Harmony installation/disposal, range/default persistence and shipped metadata sight-policy regression. Standard build/package passed; packaged DLL matches test output and packaged language/handbook JSON parses. Both independent review rechecks found no remaining blockers. SHA256: 4D6329A6B3730CDA37442D0CB1038A54358F93BDDA6FB740E10FB9ACA427B8E0.

Review correction: native client selection boxes are empty so terrain traversal reaches intervening walls. Supplemental hits run only inside player mouse picking, with exception-safe cleanup. Server bounds remain for interaction reach, and transparent asset metadata prevents default server sight occlusion. Client target growth replaces the vanilla box outline.

Independent size follow-up: BASIC requested separate indicator size and height controls. Indicator size accepts 25-300 percent, defaults to 100 percent for existing markers, and leaves the center height and description text size independent. It persists in protobuf member 16, item/block attributes and per-player appearance defaults. Rendering, preview, culling, client picking and server reach geometry account for size. QA: compare 25/100/300 percent at fixed height, then change height at fixed size; check preview, locks, pickup/replacement and fresh-marker defaults. Automated suite: 786 passed.
Size follow-up package SHA256 CBDFEC619F8FB549CFB917D9D227DBE0BCD87348E7ED3B9376635F1A96027E57 matches the tested DLL. Both review axes found no blockers; server and both QA profile package hashes verified. Visual acceptance remains pending.
