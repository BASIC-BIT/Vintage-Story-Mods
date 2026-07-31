# Ropeway v0.1 — in-game QA script

Manual operator checklist. Nothing in v0.1 has been play-verified; this is the script that does it.
See [KNOWN-ISSUES.md](KNOWN-ISSUES.md) for what source review already found and did not fix.


Creative mode is fine for steps 5+; do steps 1-4 in survival at least once to check the recipes.
Watch `%APPDATA%\VintagestoryData\Logs\client-main.log` and `server-main.log` throughout.

1. **Install and load.** Copy `ropeway_0_1_0.zip` into `%APPDATA%\VintagestoryData\Mods\`, start the game,
   open a world. **PASS:** no `Ropeway:` error lines in either log at startup. In particular there must be
   no *"multiblockStructure on ropeway:pylonhead-… lists '…', which matches no loaded block"* — that line
   means the `@(log-placed-.*|debarkedlog-.*|planks-.*)` wildcard does not resolve at runtime and the
   fallback is to replace it with plain `game:planks-*`.

2. **Craft the parts.** Crafting grid:
   - **Ropeway brace ×4** — stick, metal plate (any metal), stick in a 1×3 *row*.
   - **Haul rope ×1** — rope / metal bit / rope in a 1×3 *column*.
   - **Pylon head ×1** — rope / brace / metal bit, top to bottom in a 1×3 *column*.
   - **Ropeway cabin ×1** — haul rope, empty, haul rope / brace, plank, brace / plank, plank, plank.
   **PASS:** all four appear in the crafting output and are named "Ropeway Brace", "Haul Rope",
   "Pylon Head", "Ropeway Cabin" — no raw lang keys.

3. **Handbook.** Press `H`, find the **Ropeway** category tab. **PASS:** the tab is labelled "Ropeway"
   (not `handbook-category-ropeway`), both pages open, the `<itemstack>` renders spin, and the
   "Building a Line" ↔ "Aerial Ropeways" links work.

4. **Place the pylon head.** Stand where you want the tower and place it at head height or above.
   **PASS:** it turns to face *you*, and the small spur on the crossarm points **away** from you — that
   spur is the only asymmetric part of the shape and it points at where the rear gantry goes.

5. **Read the guidance.** Look at the pylon head. **PASS:** the block-info panel says
   *"Tower is not complete, 21 blocks missing or wrong."* followed by *"Rear gantry goes three blocks to
   the \<compass direction\>"*. **PASS:** that direction is the one the spur points at, and it is the
   opposite of the direction you were standing in when you placed it. Right-click it. **PASS:** a red
   toast with the same message, and 21 translucent ghost cells light up around it — the colour of the
   block wanted where the cell is empty, red where the wrong block sits.

6. **Open the guide.** Sneak (hold Shift) and right-click the pylon head with an empty hand.
   **PASS:** the "Ropeway Tower" dialog opens; the left two cells show the pylon head and the brace slowly
   turning in 3D, the right cell shows the cabin turning, and the build steps are readable underneath.
   **FAIL modes to report:** cabin invisible (renderer/tesselation), cabin clipped out of the inset
   (the size/offset knob in §4.6), a `Ropeway: could not build the guide cabin preview` log line.

7. **Build the tower.** Following the guide: four more braces either side of the pylon head (x = ±1, ±2)
   to make a 5-wide front gantry; five more braces three blocks behind it **in the direction the block
   info panel named in step 5** (away from where you stood); then three blocks of log/debarked
   log/planks under each of the four outer corners.
   **PASS:** each ghost cell disappears within ~0.5 s of you filling it, **without re-right-clicking** —
   this is the live-overlay fix; a stale ghost sitting on top of a placed block means it regressed.
   The count in the block-info panel counts down, and within ~1 s of the last block the panel reads
   *"Tower complete / Spans: 0/2"* and every remaining highlight clears itself.

8. **Check the passage.** Walk through the tower at ground level between the posts.
   **PASS:** a clear 3-wide, 4-long, 3-tall tunnel — no post in the way.

9. **Build a second tower** 20-40 blocks away with clear line of sight, same procedure. Keep both towers
   at similar height for the first test. **Deliberately orient this one so its facing is 90° from the
   line between the two towers** — that is the case the tower's own posts used to block silently.

10. **Link them.** Right-click the first tower's pylon head with an empty hand.
    **PASS:** the "Link to tower" dialog lists the second tower with its distance and rope cost, and shows
    your haul rope count. **PASS:** it is listed *despite* the 90° orientation from step 9 — an empty
    list here is the tower-post clearance bug back. Click the row.
    **PASS:** a chat line *"Span strung: N blocks, M haul rope."*, no error toast; both towers now read
    *"Spans: 1/2"* and *"End of line - the cabin stops here"*, and *"Line: N blocks, 2 towers"*. Your
    haul rope drops by `ceil(distance / 4)` — a 30-block span is **8**, not 30.
    **Also check the refusals:** with too little rope the row is prefixed `[!]` and clicking it gives
    *"Not enough haul rope"*; a tower with something solid between them does not appear in the list;
    right-clicking a tower that already carries two spans gives *"That tower already carries two
    spans."* instead of an empty picker.

11. **Hang the cabin.** Hold the Ropeway Cabin item and right-click the first tower's pylon head.
    **PASS:** a cabin appears hanging 2 blocks below the sheave, the item leaves your hand (survival), and
    right-clicking the *middle* of a three-tower line with the cabin item instead gives
    *"The cabin can only be placed at an end tower."*

12. **Board and ride.** Right-click the cabin with an empty hand — **aim at the roof or an upper wall
    panel, not at a seat.** That is the `mountAnySeat` fallback path and it is the single highest-risk
    untested thing in the mod. Then dismount and repeat aiming at the **floor or a lower wall panel,
    below the seats** — that band is what the §3c.4 selection-box fix added; before it the click hit
    nothing at all. **PASS:** both aims seat you. **FAIL:** the lower half is dead to clicks (the
    `EntityRopewayCabin.SetSelectionBox` override regressed), or the crosshair highlights a block *behind*
    the cabin's lower half instead of the cabin. Re-check this **after riding a full trip and after a
    relog** — §3d.1 is precisely about a later attribute sync putting the JSON box back, so a box that
    works on placement and dies later is the same bug returning.
    **PASS:** you are seated, you can look around freely, and after ~3 s it departs toward the far tower.
    If nothing happens, retry aiming directly at a seat; a seat-only mount means `mountAnySeat` is not
    reaching its non-controllable fallback loop and `controllable: true` on seat 0 is the fix (at the
    cost of a stutter for the controlling client).
    **PASS:** motion is smooth, not a 30 Hz stutter — this is the seat `controllable: false` fix; a
    stutter means the fix regressed. The cabin passes *through* the far tower's gantry without clipping
    the posts. Expect the mast/grip to visually pass through the gantry beams — known, cosmetic.

13. **Arrive.** **PASS:** it stops at the far tower and holds. Try to dismount while it is still moving —
    you should get *"The cabin is moving. It stops at the next tower."* and stay seated. Once stopped,
    right-click to get out; you should land on or beside the tower, not in the air.

14. **Return trip.** Board again at the far end. **PASS:** it departs back the way it came.

15. **Call it home.** Walk to the other end tower with the cabin parked and empty at the far end.
    Right-click that tower with an empty hand. **PASS:** the empty cabin travels back to you.

16. **Extend the line.** Build a third tower beyond the second and link tower 2 → tower 3.
    **PASS:** tower 2 now reads *"Spans: 2/2"* and no longer says "End of line"; towers 1 and 3 do.
    Ride end to end. **PASS:** the cabin passes through tower 2 without stopping and reverses only at the
    ends. Try to link a fourth tower to tower 2. **PASS:** *"That tower already carries two spans."*

17. **Break safety.** With a passenger seated, try to break any pylon head on that line.
    **PASS:** *"Someone is riding this line."* and the block survives. Dismount, then break an end
    tower's pylon head. **PASS:** you get `floor(span / 4)` haul rope back and the neighbouring tower
    drops to one fewer span. **PASS:** the cabin is still there, parked at an end of what is left of the
    line — not stuck mid-air where the removed span used to be.

17b. **Teardown returns the cabin.** Reduce the line back to two towers with one cabin on it, dismount,
     then break one of the two pylon heads. **PASS:** the cabin disappears and **one Ropeway Cabin item
     goes into your inventory** (or drops at the cabin if your inventory is full), along with the rope
     refund. **FAIL:** a cabin still hanging in mid-air that you cannot break, collect or interact with
     — that was the item-loss blocker. Also verify it is not possible to accumulate two cabins from one
     item by relogging between the break and the pickup.

18. **Persist.** Save, quit to menu, reload the world. **PASS:** towers still read complete with the right
    span counts, the cabin is still on the line at an end tower, and it is rideable. If the cabin was
    parked mid-span before the save it must snap to the nearer end tower rather than resume mid-air.

19. **Relog while riding** (multiplayer, or singleplayer alt-F4 while seated). Reconnect.
    **PASS:** you are standing at or near an end tower, not falling, and the cabin is parked there empty.

20. **Blocked span.** Wall off the middle of a span with stone while the cabin is parked, then ride.
    **PASS:** the cabin holds at the tower before the obstruction instead of dragging you into the wall.
    Clear the wall and call it — it moves again.

21. **Clearance follows the cabin, not the rope.** Build a stone ridge across the middle of a span whose
    top sits **two blocks below the rope line** — clear of the rope, in the cabin's way. **PASS:** the
    tower does not appear in the picker while the ridge is there, and if you build the ridge after the
    link, the cabin holds at the tower before it. **FAIL:** a link succeeds and the cabin drives a seated
    rider through solid stone.

22. **Link while riding.** With a rider seated on line A–B, have a second player link a new tower C to
    A. **PASS:** the cabin (and rider) snap to an *end tower* of the new A/B/C line, not to a point
    tens of blocks away in mid-air.

23. **Short spans.** Link two towers only ~6 blocks apart. **PASS:** it links (the clearance check trims
    4 blocks off each end for the towers' own structures, and never trims more than half). Known
    consequence: an obstruction inside those trimmed end zones is not detected.

24. **Blow up a tower** (§3c.2 — C2). On a two-tower line A–B with the cabin parked and **nobody seated**,
    set off a powder barrel on A's pylon head (or `/we` a fill of air over it, or `/blockset air` — any
    path that is not a pick/hand break).
    **PASS:** B's block-info panel drops to *"Spans: 0/2"* immediately and B no longer says *"End of line"*.
    **PASS:** B's half of the cable disappears too.
    **PASS:** the cabin either re-bases onto a surviving line or, with no survivor, despawns and drops
    **one Ropeway Cabin item** at its position — you should be able to pick it up.
    **PASS:** no rope refund lands in anyone's inventory (there is no breaker to pay).
    **PASS:** save/quit/reload and B still reads *"Spans: 0/2"*.
    **FAIL — this is the exact regression:** B still reads *"Spans: 1/2"*, or *"Line: N blocks, 2 towers"*
    naming a tower that is now air, or B refuses a new link because it thinks it is full. That state is
    unrecoverable except by breaking and rebuilding B.
    Then repeat the **ordinary hand break** on a fresh two-tower line: **PASS:** it still refunds
    `floor(span / 4)` haul rope exactly once — a double refund means the new `OnBlockRemoved` unlink is
    not early-returning on the already-emptied `Spans`.
    Then the **mid-line explosion with a rider** (§3d.4): on `A–B–C–D–E`, seat someone in the cabin between
    `A` and `B`, and blow up `C`. **PASS:** the rider is **unseated where the cabin is** and the cabin
    re-bases onto the `A–B` half — the half it was actually on. **FAIL:** the rider is carried to an end
    tower, or the cabin lands on the `D–E` half it was never on.
    Finally, **walk away until the tower's chunk unloads and come back** (or reload the world).
    **PASS:** the spans are still there. Chunk unload must not unlink anything.

25. **A line that reaches past the loaded chunks** (§3c.1 — C3). Build a line of **five or more** towers,
    long enough that the far two are outside the loaded radius when you stand at the near end (a server
    with a small view distance is the easiest way; `maxLineLength` is now **320**, chosen so a whole line fits inside the default server chunk radius — which means on a default server this step may be **unreproducible**, and that is the intended outcome. To exercise it deliberately, lower the server's `MaxChunkRadius` below 10). Note that the
    chain walk stops one hop *past* the loaded region, so one unloaded tower still yields the full chain —
    two consecutive unloaded towers is what actually shortens it.
    Park the cabin at the near end, board, and ride outward.
    **PASS:** the cabin **departs** and rides out to the **last loaded tower**, then stops there and stays
    stopped — it must not reverse at that tower and it must not jump backwards. You get one toast:
    *"The line carries on into unloaded chunks. Holding at this tower."*
    **PASS:** because you rode out there, your own chunk loading has moved with you, so the tower beyond is
    normally loaded by the time you arrive; step out and back in and it carries on outward. Repeat to the
    end of the line. (Earlier drafts of this step claimed the cabin resumes *on its own* while parked at the
    near end — it cannot. A parked cabin does not move, so nothing loads the far chunks. Riding is what
    loads them, which is why the cabin now departs instead of refusing to.)
    **PASS:** the near tower's block-info panel reads the short *"Line: N blocks, K towers"* **followed by**
    *"Part of this line is in unloaded chunks. The cabin stops at the last loaded tower."* A short line
    figure with no such line means the truncation flag is not reaching `GetBlockInfo`.
    **FAIL — this is the regression:** the cabin turns around at an intermediate tower and carries you back
    to where you started, or snaps some distance along the line the instant a chunk boundary is crossed.
    Both are the truncated chain being driven on.
    Also check the empty-cabin case: park it at the far end, walk to the near end until the far end
    unloads, then right-click the near tower with an empty hand to call it. **PASS:** you get
    *"Part of this line is in unloaded chunks…"* — an honest refusal, not a silent no-op and not the picker.
    Walk back: **PASS:** the cabin is exactly where you left it, and calling it now works.
    Finally, **try to link a new tower onto a line whose far end is unloaded.** **PASS:** *"Part of that
    line is in unloaded chunks, so it cannot be measured. Get closer to the rest of it first."* — the
    picker must not open. This is the cycle guard; a link that succeeds here can produce a looped line.
