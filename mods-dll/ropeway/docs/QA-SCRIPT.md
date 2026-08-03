# Ropeway v0.1 — in-game QA script

Manual operator checklist. One in-game session has happened: the mod loaded clean, and it produced the
four findings the previous round fixed — cabin built across the travel axis, cable rendering nothing, the
picker showing only link candidates, and no way to name a tower.

**This round restructured the tower** (`DECISIONS.md`, "2026-07-31 — tower restructure"): the rear gantry
is gone, and the controller moved from the pylon head at head height to a **pylon footing on the ground**.
Steps 0, 2, 4–8 and 11 are rewritten for it, and everywhere the old script said "right-click the pylon
head" it now says "right-click the footing" — every verb moved. Every measurement in this script was
re-derived from `blocktypes/pylonbase.json` and the shipped shapes on 2026-08-01, and the same numbers are
asserted by `renders/scenes/gen_manifests.py` and `RopewayAssetContractTests.TheCabinFitsThroughTheTower`.
See [KNOWN-ISSUES.md](KNOWN-ISSUES.md) for what source review already found and did not fix.


Creative mode is fine for steps 5+; do steps 1-4 in survival at least once to check the recipes.
Watch `%APPDATA%\VintagestoryData\Logs\client-main.log` and `server-main.log` throughout.

0. **Migration, if and only if you have a world with towers built before this round.** Load it.
   **PASS:** the world loads, and `server-main.log` carries one *"Failed loading blockentity PylonHead …
   Will discard it"* line per old tower — that is the intended migration, not a bug. The old towers are
   now inert decoration: their blocks are all still there, their spans and names are gone, and nothing
   crashes. Look at an old pylon head: **PASS:** *"Not part of a tower. A tower starts with a pylon
   footing on the ground…"*. **FAIL:** a crash on load, an old tower that still reports spans, or a
   cable still drawn between two of them.
   **Known and accepted:** a cabin that was on an old line stays hanging where it was, holding, with no
   line to resolve. Break it out with `/entity remove` or leave it; it is inert. Pre-release, no upgrader.

1. **Install and load.** Copy `ropeway_0_1_0.zip` into `%APPDATA%\VintagestoryData\Mods\`, start the game,
   open a world. **PASS:** no `Ropeway:` error lines in either log at startup. In particular there must be
   no *"multiblockStructure on ropeway:pylonbase-… lists '…', which matches no loaded block"* — that line
   means the post wildcard does not resolve at runtime and the fallback is to replace it with plain
   `game:planks-*`. The wildcard now accepts eight families:
   `log-placed-*`, `debarkedlog-*`, `planks-*`, `rock-*`, `cobblestone-*`, `drystone-*`, `rockpolished-*`
   and `stonebricks-*`. Note the verifier only tests the whole key, so a dud alternative hides behind the
   live ones — step 7 is what actually proves each family.

2. **Craft the parts.** Crafting grid:
   - **Ropeway brace ×4** — stick, metal plate (any metal), stick in a 1×3 *row*.
   - **Haul rope ×1** — rope / metal bit / rope in a 1×3 *column*.
   - **Pylon head ×1** — rope / brace / metal bit, top to bottom in a 1×3 *column*.
   - **Pylon footing ×1** — plank, metal bit, plank on the top row; three loose stones under them.
   - **Ropeway cabin ×1** — haul rope, empty, haul rope / brace, plank, brace / plank, plank, plank.
   **PASS:** all five appear in the crafting output and are named "Ropeway Brace", "Haul Rope",
   "Pylon Head", "Pylon Footing", "Ropeway Cabin" — no raw lang keys.

3. **Handbook.** Press `H`, find the **Ropeway** category tab. **PASS:** the tab is labelled "Ropeway"
   (not `handbook-category-ropeway`), both pages open, the `<itemstack>` renders spin, and the
   "Building a Line" ↔ "Aerial Ropeways" links work. **PASS:** the overview page describes a footing and
   one crossarm, not two gantries.

4. **Place the pylon footing.** Stand where you want the tower and place it **on the ground** — this is
   the first block of a tower and nothing has to exist above it.
   **PASS:** it lands flat, it is a half-height plinth you can walk over rather than a full cube, and it
   turns to face you.

5. **Read the guidance.** Look at the footing. **PASS:** the block-info panel says
   *"Tower is not complete, 15 blocks missing or wrong."* followed by *"The cabin will pass through
   \<direction\> to \<the opposite direction\>"*. **PASS:** those two directions are the axis you were
   standing on when you placed it — the crossarm goes across them. Turn the footing (break and replace
   facing the other way) and check the line changes with it; this is the only orientation cue there is,
   and building the crossarm 90° out is a tower no line can pass through.
   Right-click the footing. **PASS:** a red toast with the same missing-block message, and **15**
   translucent ghost cells light up above and around it — a seven-wide row four blocks up and two
   four-block columns under its ends — the colour of the block wanted where the cell is empty, red where
   the wrong block sits. **PASS:** no ghost cell anywhere in the five columns directly above the footing;
   that is the archway the cabin goes through.

6. **Open the guide.** Sneak (hold Shift) and right-click the footing with an empty hand.
   **PASS:** the "Ropeway Tower" dialog opens; the left three cells show the pylon footing, the pylon head
   and the brace slowly turning in 3D, the right cell shows the cabin turning, and the build steps are
   readable underneath. **PASS:** all four fit inside the inset — the cells got narrower this round.
   **FAIL modes to report:** cabin invisible (renderer/tesselation), cabin clipped out of the inset
   (the size/offset knob in §4.6), a `Ropeway: could not build the guide cabin preview` log line.

7. **Build the tower.** Following the guide: two posts of **four** blocks each, standing on the ground **three**
   blocks either side of the footing; then the crossarm across their tops,
   four blocks up: **ropeway braces** at x = ±1, ±2 and ±3 and the **pylon head** in the middle, directly
   above the footing. That is **16 cells** in all, two more than before the passage went from three
   wide to five — the extra pair of braces is the only thing in the station-rail work that costs the
   player anything, and it is charged on every tower of a chained route.
   **PASS:** each ghost cell disappears within ~0.5 s of you filling it, **without re-right-clicking** —
   this is the live-overlay fix; a stale ghost sitting on top of a placed block means it regressed.
   The count in the block-info panel counts down, and within ~1 s of the last block the panel reads
   *"Tower complete / Spans: 0/2"* and every remaining highlight clears itself.
   **PASS:** the posts stand **on the ground**, level with the footing — no gap under them. A tower whose
   legs start one block up is the "posts three tall" mistake and means the offsets moved.
   **PASS:** the tower is **one block deep**. There is no second gantry and nothing behind it.
   **PASS:** the pylon head validates whichever of its four facings you place it in. Point its throat down
   the line anyway; it is the slot the cabin's hanger blade rides in, it carries the station rails, and a
   crosswise sheave looks wrong. **PASS:** the rails and their flared mouths overhang the head's own cell by
   a block fore and aft, and no extra block is needed under them — that overhang is the shape, not a bug.
   **PASS — post materials.** Build one post out of **logs** and the other out of **stone bricks**; both
   must satisfy the structure. Then swap a post block for each of the remaining six accepted families in
   turn — debarked log, planks, raw stone (`rock-*`), cobblestone, drystone, polished rock — and check the
   count does not go up. **PASS:** a **stone brick slab**, a **stairs block**, **soil** or a chiselled block
   in a post cell counts as *wrong* (red ghost) — the list is structural columns, not "anything".
   **PASS:** the ghost colour for an empty post cell is still legible against the sky; it is the colour of
   whichever accepted block the game lists first, not necessarily a log any more.

7b. **The crossarm meets the posts.** Stand back and look at the joint where an outer crossarm cell lands on
   a post.
   **PASS:** the crossarm's foot plate covers the post's whole top face and sits **flat** on it — a
   continuous metal band running the full seven cells, broken only by the sheave throat in the middle.
   **FAIL:** the log's top face is visible around a narrower bracket, or there is a gap you can see through
   between the bracket and the log. That is the pre-fix shape.
   **PASS:** riding through (step 12) still leaves visible air over the cabin roof — the foot plate cost the
   roof 1/16 of a block, so the gap is now about **a quarter of a block**, not a third.

8. **Check the passage.** Walk through the tower between the posts, along the axis the block-info panel
   named in step 5.
   **PASS:** a clear 5-wide, 4-tall archway — no post in the way, and you walk over the footing rather
   than around it (its collision box is half a block). (It was 3-wide; the posts moved out to x = ±3, which
   is what step 12b's gentle-corner clearance depends on.)
   **PASS:** stand on the footing and look up: the sheave is four blocks above you, the underside of the
   crossarm three.
   The cabin's own dimensions are **4 blocks along travel × 2.875 across × 3.65 tall** (floor at shape y
   −20 to the top of the jaw at 38.4; the body alone is 2.5 and the hanger is the rest). The 2.875 is the
   one that has to fit between the posts; if you ever see the 4-block side facing them, the cabin shape
   has been re-authored along Z again and the previous round's item 1 has regressed.

9. **Build a second tower** 20-40 blocks away with clear line of sight, same procedure. Keep both towers
   at similar height for the first test. **Deliberately orient this one so its passage axis is 90° from
   the line between the two towers** — its crossarm then lies along the line instead of across it. That is
   the case the tower's own posts used to block silently, and it must still *link*: the clearance check
   trims four blocks off each end for exactly this. Its cabin fit will be wrong (step 16 covers that), the
   link must not be.

10. **Link them.** Right-click the first tower's footing with an empty hand.
    **PASS:** the "Tower connections" dialog opens. It lists the second tower as
    *"Link \<bearing\> - N blocks - M rope"*, where \<bearing\> is the eight-point compass direction you
    would walk to reach it — **never a raw coordinate and never the word "unnamed"**. It also shows your
    haul rope count. **PASS:** it is listed *despite* the 90° orientation from step 9 — an empty
    list here is the tower-post clearance bug back. Click the row.
    **PASS:** a chat line *"Span strung to \<bearing or name\>: N blocks, M haul rope."*, no error toast;
    both towers now read *"Spans: 1/2"* and *"End of line - the cabin stops here"*, and
    *"Line: N blocks, 2 towers"*. Your haul rope drops by `ceil(distance / 4)` — a 30-block span is
    **8**, not 30.
    **Also check the refusals:** with too little rope the row is prefixed `[!]` and clicking it gives
    *"Not enough haul rope"*; a tower with something solid between them does not appear in the list.

10b. **The cable is visible.** Look at the span you just strung.
     **PASS:** a thin rope-textured cable runs from each sheave to the midpoint of the span, **immediately,
     without reloading anything**. Each tower draws its own half, so the two halves meet in the middle and
     there is no z-fighting seam.
     **FAIL:** nothing there at all. That is the silent-mesh bug — `CubeMeshUtil.GetCube` hands back a mesh
     with `XyzFacesCount == 0` and the chunk tesselator's emit loop never runs, so the cable is dropped with
     no exception and no log line. Nothing in either log will tell you; only looking will.
     Then **quit to menu and reload the world**, and (multiplayer) have a **second player who did not build
     the line** walk up to it. **PASS:** the cable is there for them too, straight away.
     **PASS — the cable's colour.** It is one flat rope-brown along its whole length, the same at both ends
     and on every face. **FAIL:** it is striped in unrelated browns, greys or purples, or has transparent
     patches. That is the atlas bug: `CubeMeshUtil.ScaleCubeMesh` multiplies the cube's UVs by the axis
     scale, so a long cable runs them far past 1 and `MeshData.SetTexPos` maps them outside the rope
     texture's own sub-region, sampling whichever sprites sit next to it.
     **PASS — the cable's thickness.** About two pixels — a thin line, not a beam. Sight along a span from
     one tower: it should read as rope, not as a pipe.

10c. **Name the towers.** With the picker open on tower 1, type a name into the **"This tower:"** field at
     the top and press **Rename**. **PASS:** the row list refreshes and the block-info panel on that pylon
     footing now shows the name in quotes above *"Tower complete"*. Name tower 2 as well, from its own picker.
     **PASS:** each tower's picker row for the other now shows that **name instead of the bearing**, and a
     link or unlink chat line names it too.
     **PASS:** the names survive a save/reload, and (multiplayer) a second player sees them without
     relogging.
     **Check the sanitiser:** paste a name with tabs or newlines in it — **PASS:** it comes back as a
     single line with single spaces. Paste 200 characters — **PASS:** it is cut to 24 with no trailing
     space and no broken half-character. Save an all-whitespace name — **PASS:** the tower goes back to
     being called by its bearing, not by an empty label. Save `<font color="red">Hot` — **PASS:** the name
     comes back with the angle brackets gone and shows as literal text in the block-info panel and in the
     link/cut chat lines. **FAIL:** the panel or the chat line turns red, or part of the name vanishes into
     a tag — the name is reaching a VTML renderer unescaped.

10d. **Unlink from the picker.** Right-click tower 1 again. **PASS:** the span you strung is listed at the
     **top**, styled differently from the link rows and reading
     *"Connected: \<name\> - N blocks - click to cut (+M rope)"*. Click it.
     **PASS:** `floor(distance / 4)` haul rope comes back, both towers drop to *"Spans: 0/2"*, both halves
     of the cable disappear, and **the picker stays open and refreshes** to show the tower as a link
     candidate again. Re-link it before carrying on.
     **PASS:** with a rider seated on the line, the same click gives *"Someone is riding this line."* and
     changes nothing.
     Later, once a tower carries **two** spans (step 16), right-click it: **PASS:** the picker opens
     showing both connections rather than refusing with *"That tower already carries two spans."* — an
     unlinkable full tower is the whole point of the row list. It offers **no link rows**, because every
     one of them would fail on click.

11. **Hang the cabin.** Hold the Ropeway Cabin item and right-click the first tower's footing.
    **PASS:** a cabin appears hanging 2 blocks below the sheave — that is **at the tower it was placed on,
    inside its own archway**, not somewhere near it: its floor a little over a block above the footing, its
    roof just under the station rails, its hanger blade up between the sheave cheeks. **FAIL, and this is the one this round
    is most likely to get wrong:** the cabin appears four blocks lower, at footing height, sitting in the
    ground. That is `SpanMath.AnchorOf` handing back the footing centre instead of the sheave.
    **PASS:** the item leaves your hand (survival), and right-clicking the *middle* of a three-tower line
    with the cabin item instead gives *"The cabin can only be placed at an end tower."*

11b. **The cable meets the cabin.** Stand back and look at a strung span with the cabin parked on it.
     **PASS:** the drawn cable runs sheave to sheave, and the cabin's jaw is closed round it — a hairline of daylight on all four sides, not a gap and not a z-fighting seam. **FAIL:** the
     cable runs at footing level, four blocks under the cabin, or the cabin hangs four blocks under the
     cable — that is the cable mesh and `AnchorOf` disagreeing, which is the whole point of drawing the
     cable from the footing with the same offset `AnchorOf` uses.

12. **Board and ride.** Right-click the cabin with an empty hand — **aim at the roof or an upper wall
    panel, not at a seat.** That is the `mountAnySeat` fallback path and it is the single highest-risk
    untested thing in the mod. Then dismount and repeat aiming at the **floor or a lower wall panel,
    below the seats** — that band is what the §3c.4 selection-box fix added; before it the click hit
    nothing at all. **PASS:** both aims seat you, **from every side** — the override is
    `x ±2.05, y -1.3..2.45, z ±2.05`, square in x/z because `Entity.SelectionBox` is world-axis-aligned and
    is never rotated by yaw, so it has to circumscribe the cabin at any bearing rather than fit it at one.
    (The top was 2.05 while the mast stopped at 2.00; the hanger's jaw reaches 2.40, so it went to 2.45 —
    for a while the top 0.35 block of the hanger was not clickable at all.)
    **Do this on a line running north-south as well as one running east-west** — a box that fits only one
    of the two is the exact defect this round closed. **FAIL:** the lower half is dead to clicks, or the
    two ends of the cabin are dead while the sides work on one bearing but not the other, or the crosshair
    highlights a block *behind* the cabin's lower half instead of the cabin. Re-check this **after riding a
    full trip and after a relog** — §3d.1 is precisely about a later attribute sync putting the JSON box
    back, so a box that works on placement and dies later is the same bug returning.
    **PASS:** you are seated, you can look around freely, and after ~3 s it departs toward the far tower.
    If nothing happens, retry aiming directly at a seat; a seat-only mount means `mountAnySeat` is not
    reaching its non-controllable fallback loop and `controllable: true` on seat 0 is the fix (at the
    cost of a stutter for the controlling client).
    **PASS — the pose, and check it in third person (F5) because you cannot see yourself otherwise:** the
    rider is **sitting** on the bench, legs forward, facing **along the line** — not standing. Bring a
    second player or watch a friend board: a remote rider must be sat too, and both riders face the same
    way (forward), which is correct, not a bug. **FAIL:** a standing T-ish idle. That is the `sitboatidle`
    animation not being started — the pose comes from `RopewayCabinSeat.DidMount`, and a mistyped code
    fails **silently**. Also **PASS:** in first person your eye is in the **glazing band** — you can see
    out of the windows — not up inside the roof slab and not down at bench level. Get out again:
    **PASS:** you stand up immediately; **FAIL:** you walk away still stuck in the sit pose, which is the
    `DidUnmount` stop-before-base ordering having regressed.
    **PASS:** motion is smooth, not a 30 Hz stutter — this is the seat `controllable: false` fix; a
    stutter means the fix regressed.
    **PASS — the axis check, watch for this one:** the cabin's **long side points down the line**, so it
    goes through the tower nose-first. The fit is **deliberately tight: 1/16 of a block of air on each
    side** — a 2.875-block-wide cabin in the 3-block passage — so a gap you can barely see is the CORRECT
    result and is exactly what `gen_manifests.py` asserts (`roof to west/east post = 1.0 unit`). Do not
    report a fail for a tight fit; report one only if it clips. **FAIL:** it flies broadside, presenting
    its 4-block side to a 3-block gap and clipping both posts on every pass. That is the shape-axis bug and it means the cabin shape has gone back to being
    built along Z.
    **PASS — the vertical fit, and the reason the tower got a block taller:** as the cabin
    passes through a tower, its **floor clears the footing by half a block** and its **roof clears the
    station rails hanging under the crossarm by a quarter**. Both are visible margins, not hairlines.
    (`hangDrop` moved 2.0 → 2.25, the midpoint of its window, to buy the band above the roof that the
    rails and the guide rollers live in; the floor clearance went 0.75 → 0.50 to pay for it.)
    **FAIL:** the floor cuts through the footing plinth, or the roof eats into the rails. Either one means
    `SpanMath.SheaveHeight` and the cabin's `hangDrop` have drifted apart — the unit test
    `TheCabinFitsThroughTheTower` and `gen_manifests.py` both assert exactly these two gaps.
    The hanger blade **should** pass up between the two sheave cheeks and the jaw should close on the rope
    at the sheave centre with a hairline of daylight; that is the fit, not a clip. The guide rollers
    running inside the rails is likewise the fit.
    **PASS:** the sway animation rocks the cabin **fore and aft along the line**, like a real hanging
    cabin, not side to side.

12b. **Ride a corner, and expect the sharp one to clip.** Build three towers twice: once with a **gentle**
     bend at the middle one (30 degrees or less) and once with a **right angle**. Face each middle footing
     down one of its two legs.
     **PASS — gentle bend:** the cabin misses both posts. Measured at 0.000 blocks of penetration at 30
     degrees and 0.033 at 45; the 5-wide passage is what buys that, so a visible hit at 30 degrees means the
     tower's posts are back at x = ±2.
     **PASS — right angle, and this is a KNOWN LIMIT, not a bug:** the cabin **passes through a post**. A
     tower facing is one of four cardinals, so at 90 degrees the outgoing leg IS the post axis: the cabin's
     *origin* travels down the post column at tower-local x = 3. It is a translation, and no yaw law fixes a
     translation - one was tried (the "angle-station" law) and made 45- and 60-degree corners worse, so it
     was reverted. Record it and move on. The handbook tells players the same thing.
     **Also expected at a right angle, cosmetic:** the outgoing rope leaves the sheave *into* the crossarm
     and is buried in three brace blocks before it clears the tower. Known; do not report it.
     **FAIL:** anything at a **gentle** corner - a cabin eating a post at 15-30 degrees is a real regression
     and means the passage width or `SpanMath.TowerClearance` moved.
     **This step is about a corner the cabin RIDES THROUGH, and it must stay that way.** A cabin that does
     not stop is on the plain leg bearing from end to end - step 13's square-up applies only where it stands
     still. If you see the cabin turn square to a middle tower it is *passing*, that is the reverted law
     back, and it is a fail here whatever it looks like.

13. **Arrive, and watch it square up.** **PASS — do this at a tower on a line that TURNS, which is where it
    shows:** as the cabin settles it **turns to sit flush with the station**, square across the crossarm and
    parallel to the posts, instead of staying angled at the next tower. It is a turn of roughly **half a
    second** in place (an eased settle, frame-rate dependent), never more than a quarter turn.
    **FAIL:** it snaps instantly with no rotation (the client's entity interpolation is not running), or it
    turns the long way round.
    **NOT a failure, do not report it:** the turn overlapping the start or end of travel by a fraction of a
    block. The client interpolates position and yaw *independently*, so on departure roughly half the turn
    happens before visible motion and the rest across the first block or so, and on arrival the last sliver
    of the turn overlaps the last ~0.15 block. An earlier version of this step called that a FAIL and would
    have had you report correct behaviour as broken.
    **What 12b actually catches** is different: the cabin holding the tower's axis *all the way through* a
    corner while continuing to travel, so it crab-walks and drags its tail through a post. That is the
    reverted angle-station law, and it looks nothing like a half-second settle.
    **PASS — leaving:** the cabin turns back onto the span as it departs. A second rotation the same size,
    and expected — it reads as the cabin swinging out onto the line.
    **PASS:** it stops at the far tower and holds. Try to dismount while it is still moving —
    you should get *"The cabin is moving. It stops at the next tower."* and stay seated. Once stopped,
    right-click to get out; you should land on or beside the tower, not in the air.

13b. **Choose where you get off.** This one needs a **three-or-more-tower line** — build step 16 first if
     you have not. It is the rider's only control, and the thing whose absence made the ride feel like it
     had none.
     Board at one end. **PASS:** as you sit down, **two** chat lines, each naming **your own current
     binding** — one for asking for a stop, one for the outside view (13c). Now rebind either one in
     **Settings > Controls**, board again, and **PASS:** the line names the **new** key. **FAIL:** no such
     line, or a line still naming the old key — the hints are client-side, local-player-only and read the
     live binding, so both silence and a stale key are bugs.
     Press the key while riding. **PASS:** a chat line *"Stopping at \<name\>."* naming the next tower
     ahead, and the cabin **stops at that tower** instead of running on to the end. Once it is stopped you
     can dismount there.
     Press it **again** before you arrive. **PASS:** the message names the tower **after** that one and the
     cabin carries on to it. Keep pressing past the far end: **PASS:** the selection wraps and comes back
     down the line the other way, and the cabin turns around to go there — that wrap is the only way to
     reverse from inside.
     **The interior-station case, which is what this step exists for.** Call the cabin *backward* to a
     middle tower (step 15), board it there, and press the key until it offers a tower on the **far** side.
     **PASS:** it goes there. **FAIL:** you are carried to the end the cabin was already pointing at with no
     way to ask for the other — that is the pre-fix behaviour (KNOWN-ISSUES C3).
     **PASS:** pressing the key while the cabin is **parked** with you aboard departs immediately rather
     than waiting out the three-second boarding pause.
     **PASS:** with the cabin still, press it when there is nothing to offer (a two-tower line, standing at
     one end, having already selected the other) — you get *"No stop to ask for from here."*, never silence.
     **PASS:** look at the cabin from outside with interaction help on (`Ctrl+N`): a *"Choose where to get
     off"* line with the key on it, alongside the mount lines.
     **PASS:** the key is listed in **Settings > Controls** as *"Ropeway: ask for a stop"*, and rebinding it
     there works. **PASS:** pressing it while **not** riding does nothing at all and does not eat the key.
     **PASS:** motion is still smooth, not a stutter — the seats are still `controllable: false` and the
     stop key is a hotkey packet, not a seat control. A stutter means someone made a seat controllable.

13c. **The outside view.** The second rider key. It is **client-side only** — no packet, no seat change —
     so nothing here can affect anyone else on the server.
     Board, then press the outside-view key (**Ropeway: outside view while riding** in Settings > Controls;
     it is on **O** unless you rebound it, which is unbound in vanilla). **PASS:** the camera goes to third
     person and you watch the cabin run. Press it again: **PASS:** back to first person.
     **PASS:** with it on, dismount — the camera goes back to **first person by itself**. **FAIL:** you walk
     away in third person; that is the restore not firing, and the poll on `MountedOn` is what should catch
     every dismount path (normal, death, teleport, chunk unload).
     **PASS:** turn it on, then press **F5** yourself mid-ride. The mod **lets go**: it stops managing the
     camera for the rest of that ride and does **not** snap you back. **FAIL:** it fights you, flipping the
     camera back every quarter second.
     **PASS:** board while **already** in third person (F5 before you sit) — the mod does nothing, and
     dismounting leaves you in third person, because it was never ours to restore.
     **PASS:** turn it on mid-ride, then **relog** while still aboard. You come back in first person and
     nothing is stuck; the camera mode is never saved.
     **PASS:** pressing the key while **not** aboard the cabin does nothing at all and does **not** eat the
     key — like the stop key, it hands the press back.
     **PASS:** look at the cabin from outside with interaction help on (`Ctrl+N`): a *"Watch the cabin from
     outside"* line with the key on it, next to the *"Choose where to get off"* line.
     **Known and accepted, do not file:** in third person the camera can pass **through** the cabin shell —
     vanilla's third-person wall check raycasts blocks only, and the cabin is an entity — and passing a
     tower can snap you to first person for a frame where a post crosses the camera ray. Report only if it
     is constant rather than occasional.

14. **Return trip.** Board again at the far end. **PASS:** it departs back the way it came.

15. **Call it to any tower.** Do this one on a **three-or-more-tower line** — build step 16 first if you
    have not. Calling used to work at the two ends only, and the middle towers are the point.
    Park the cabin empty at one end, walk to a **middle** tower, right-click it with an empty hand.
    **PASS:** the chat line reads *"Cabin called to \<name\>."* — the name you set in step 10c, or a
    compass bearing if that tower is unnamed, never a coordinate triple — and the cabin travels to **that**
    tower and **stops there**. **FAIL:** it sails past you to the far end, or it stops at your tower and
    then slides off to an end a second later.
    Look at that tower's **footing** while the cabin is on its way — that is where every block-info line
    lives now — and stand where you can see the cabin, so it is inside your entity range.
    **PASS:** the block-info panel reads *"The cabin is on its way here."*, then *"The
    cabin is at this tower."* once it arrives. Look at another tower on the same line while still in sight
    of the cabin: *"The cabin is elsewhere on the line."* **FAIL:** all three lines are missing — this
    readout is client-side and matches the cabin by a key that used to exist only on the server, so silence
    here is the bug, not the range. Do not accept "the cabin must be out of range" as the explanation: put
    the cabin in range and read the panel again.
    Right-click that same tower again with the cabin sitting on it. **PASS:** *"The cabin is already at
    this tower."* — it must not be silent and it must not open the picker.
    Now call it **backward** to a tower the other side of it, and call it **across two spans**.
    **PASS:** both trips stop where you clicked; the two-span trip does not stop at the tower in between.
    **PASS:** an end tower still calls it, and a rider boarding there afterwards departs back down the line
    on the first boarding rather than needing a second.
    **PASS:** with someone seated on the line, calling from any tower gives *"The cabin is in use."*
    **Ctrl + right-click** any tower on the line, cabin away and again with someone seated. **PASS:** the
    picker opens both times, so you can name or unlink any tower without parking the cabin next to it
    first. Sneak + right-click must still open the *guide*, and a plain right-click must still call the
    cabin — if Ctrl calls the cabin instead, the modifier is not reaching the server. The one exception is
    a complete tower with **no spans yet**: a plain right-click there opens the picker, because there is no
    line to call anything along.
    **Relog mid-journey.** Call it across a long stretch and quit to menu **while it is still moving**.
    Reload. **PASS:** it carries on to the tower it was called to. **FAIL:** it parks at the nearest end —
    the destination is not surviving the save.
    **Abandon a call.** Call it across a span and, while it is still moving, wall off the span ahead of it
    or cut a span on its route. **PASS:** a chat line tells *you* the cabin you called stopped, and why.
    **FAIL:** it stops silently — you were already told *"Cabin called to \<name\>."*, so silence here is
    a promise the mod did not keep.

16. **Extend the line.** Build a third tower beyond the second and link tower 2 → tower 3.
    **PASS:** tower 2 now reads *"Spans: 2/2"* and no longer says "End of line"; towers 1 and 3 do.
    Ride end to end. **PASS:** the cabin passes through tower 2 without stopping and reverses only at the
    ends. Open tower 2's picker. **PASS:** two "Connected:" rows and **no link rows at all** — a full
    tower is not offered a fourth link it would then have to refuse (see step 10d).

17. **Break safety.** With a passenger seated, try to break any footing on that line.
    **PASS:** *"Someone is riding this line."* and the block survives. Dismount, then break an end
    tower's footing. **PASS:** you get `floor(span / 4)` haul rope back and the neighbouring tower
    drops to one fewer span. **PASS:** the cabin is still there, parked at an end of what is left of the
    line — not stuck mid-air where the removed span used to be.

17b. **Teardown returns the cabin.** Reduce the line back to two towers with one cabin on it, dismount,
     then break one of the two footings. **PASS:** the cabin disappears and **one Ropeway Cabin item
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
    set off a powder barrel on A's footing (or `/we` a fill of air over it, or `/blockset air` — any
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
