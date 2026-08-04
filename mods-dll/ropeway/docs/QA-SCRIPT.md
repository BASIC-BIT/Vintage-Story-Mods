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
   - **Tension weight ×1** — metal bit, loose stone, metal bit on the top row, then plank, loose stone,
     plank twice under it. A line will not take a cabin without one (step 11).
   - **Drive housing ×1** — three metal plates across the top row, three planks under them. Nothing on a
     line moves without one (step 11).
   **PASS:** all seven appear in the crafting output under real names rather than raw lang keys.
   **Quantities are one craft's output, and one craft is not a tower.** A tower is a footing, a head and
   **six** braces — plus the brace that goes inside each head — so budget two brace crafts per tower, and
   enough haul rope for `ceil(span / 4)` on every span you string; a 30-block span is 8 rope on its own.
   **The tower count is much more than three.** Steps 9 and 16 want three; 12b wants two separate
   three-tower lines, 18b a line whose first hop doubles back, 27a a fresh pair, 27d an uphill line, and 25
   wants five (singleplayer, slider at 128) or seven on a stock server. Each of those lines also wants its
   own tension weight and drive housing — see step 11. Do steps 1-4 in survival once for the recipes and
   then take the rest in creative; nobody is meant to hand-craft that in survival.

3. **Handbook.** Press `H`, find the **Ropeway** category tab. **PASS:** the tab is labelled "Ropeway"
   (not `handbook-category-ropeway`), all **three** pages open — *Aerial Ropeways*, *Building a Line*,
   *Power and the Drive* — the `<itemstack>` renders spin, and every link between them works, including
   50 → 51 → 52 and both pages' way back. **PASS:** the overview page describes a footing and one
   crossarm, not two gantries. **PASS:** the power page describes a drive that turns the rope and a
   tension weight that keeps it taut, and says nothing about winding, charge or paying for a trip.
   **PASS:** the power page carries a *"A windmill needs room"* section, and it states the room as
   **clear blocks under the hub** — four for three sails, six for a maxed five, eleven for a maxed metal
   rotor — not as a height above anything. It also says the eight blocks are measured through the air so
   height counts. **FAIL:** it gives those numbers as *"the hub four blocks up"* with nothing saying what
   "up" is counted from. That reading is one short from the ground you stand on, and it fails the last sail
   on the tester who trusts it. **FAIL:** it still tells you a mill standing
   beside the tower reaches the housing with two or three axles and nothing to climb. That is the stale
   copy, it describes a windmill vanilla will not let you build, and step 27c is where the player finds
   out.

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
   **PASS:** the "Ropeway Tower" dialog opens; **six** cells turn in 3D — pylon footing, pylon head, brace,
   **bullwheel**, **drive housing**, then the cabin — and the build steps are readable underneath.
   **PASS:** all six fit inside the inset; the cells got narrower again when the drive pair was added.
   **PASS:** the text has a *"Making it move"* paragraph naming the drive housing and the 8-block rule.
   **FAIL:** that paragraph says the mill stands **on the ground** beside the tower. A windmill cannot —
   see 27c — and the guide is the last place still saying so if it does.
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
   trims four blocks off each end for exactly this. Its cabin fit will be wrong and the link must not be —
   a mis-faced tower parks the arriving cabin **across** its own rope, which is known and recorded in
   [KNOWN-ISSUES.md](KNOWN-ISSUES.md), not something to file from step 12 or 13.
   **Once step 10's link check has passed, turn tower 2 back down the line** (break the footing and replace
   it facing the right way, then re-link). Everything from step 11 on rides through this tower, and there is
   no point watching a known-crosswise park forty more times.

10. **Link them.** Right-click the first tower's footing with an empty hand.
    **PASS:** the "Tower connections" dialog opens. It lists the second tower as
    *"Link \<bearing\> - N blocks - M rope"*, where \<bearing\> is the eight-point compass direction you
    would walk to reach it — **never a raw coordinate and never the word "unnamed"**. It also shows your
    haul rope count. **PASS:** it is listed *despite* the 90° orientation from step 9 — an empty
    list here is the tower-post clearance bug back. Click the row.
    **PASS:** a chat line *"Span strung to \<bearing or name\>: N blocks, M haul rope."*, no error toast;
    both towers now read *"Spans: 1/2"* and *"End of line - rides turn around here"*, and
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

11. **Hang the cabin — but build the line's two power blocks first, or nothing after this step works.**
    A line refuses a cabin outright until a **tension weight** stands within 8 blocks of one of its towers,
    and a cabin will not depart until a **drive housing** on that line is being turned. Both are in step 2's
    craft list; both are explained in full at **27a** and **27c**. Build them now:
    - the **tension weight** anywhere within 8 blocks of either tower — it does not matter which, and it
      takes no axle;
    - the **drive housing** with a mill running into it, **following 27c** — read 27c before you place
      anything, because the mill's hub cannot sit at ground level and the housing has to climb with it.
      Give it **five** sails, not three, and **pin the weather now** — `/weather acp false`, *then*
      `/weather setw strongbreeze`.
      **`acp false` first.** Vanilla re-rolls the wind pattern by itself every few game hours, uniformly over
      all five patterns, so `setw` alone does not hold. A **three**-sail mill needs a wind above **0.4** to
      shift the cabin at all, which only `strongbreeze` and `storm` clear — three of the five it can land on
      would stop your line dead in the middle of a motion step. Five sails needs only **0.24**, so it keeps
      going down to a `mediumbreeze`, which is the fallback if you lack `controlserver`. A becalmed line is
      not explained until **27f**, twenty steps later; do not spend steps 12-26 debugging one.
      **`acp` is not persisted** — it is a field on the weather mod system, so it is back to `true` after
      every reload — re-run both commands after every one. Steps 18, 18b, 19, 26e and 27f all reload, and
      27f is the one that bites: 27g needs the mill turning and nothing between them tells you to re-pin.
    **Every line you build from here on wants its own pair**, because a housing drives the one line whose
    footing is nearest it — counting only footings that are themselves on a line. That includes the corner
    lines in 12b, the doubling-back line in 18b, the three-tower line in 16, the long one in 25 and the
    uphill one in 27d.
    **27a and 27b are the deliberate runs *without* them**, so do those two on a fresh pair of towers rather
    than tearing these out.
    Now hold the Ropeway Cabin item and right-click the first tower's footing.
    **PASS:** a cabin appears hanging 2 blocks below the sheave — that is **at the tower it was placed on,
    inside its own archway**, not somewhere near it: its floor a little over a block above the footing, its
    roof just under the station rails, its hanger blade up between the sheave cheeks. **FAIL, and this is the one this round
    is most likely to get wrong:** the cabin appears four blocks lower, at footing height, sitting in the
    ground. That is `SpanMath.AnchorOf` handing back the footing centre instead of the sheave.
    **PASS:** the item leaves your hand (survival). The companion check — right-clicking the *middle* of a
    three-tower line with the cabin item gives *"The cabin can only be placed at an end tower."* — wants a
    middle tower, so run it when step 16 has given you one.

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
    **PASS:** you are seated, you can look out of either side, and after ~3 s it departs toward the far tower.
    **PASS — the look limit.** Hold the mouse hard left, then hard right. The view must stop at about a
    **quarter turn each way** from the seat's facing and refuse to go further: you can look out either side
    window, you cannot end up looking backwards over your own backrest. It must also **follow the cabin**
    round a bend rather than staying pinned to a compass bearing. **FAIL:** you can spin a full circle —
    `ConstrainRiderYaw` is not running, or `bodyYawLimit` went back to `null`. **FAIL the other way:** the
    centre is 180° out, i.e. you can only look toward the wall behind you — then `mountRotation.y: 180` on
    both seats is the knob, because the model cannot prove which way the benches face and this check can.
    Note the limit is on the seated player's **own camera only**; another player watching you already sees
    you squared to the cabin whichever way you are looking, and always did.
    If nothing happens, retry aiming directly at a seat; a seat-only mount means `mountAnySeat` is not
    reaching its non-controllable fallback loop and `controllable: true` on seat 0 is the fix (at the
    cost of a stutter for the controlling client).
    **PASS — the pose, and check it in third person (F5) because you cannot see yourself otherwise:** the
    rider is **sitting** on the bench, legs forward, facing **along the line** — not standing. Bring a
    second player or watch a friend board: a remote rider must be sat too, and both riders face the same
    way (forward), which is correct, not a bug — the engine forces a remote rider's body yaw to the
    cabin's (`EntityPlayerShapeRenderer.cs:429-431`) and there is no per-seat facing to be had, which is
    why **both benches face forward with their backrest behind them, like tram seating**, rather than
    facing each other. **FAIL:** a standing T-ish idle. That is the `sitboatidle`
    animation not being started — the pose comes from `RopewayCabinSeat.DidMount`, and a mistyped code
    fails **silently**.
    **PASS — where the rider lands on the bench, and this is the one to actually look at.** Board the
    **front** seat (the bench toward the outbound end), F5, and orbit until you see the seated body from
    the side. **PASS:** rear on the pan, back against the backrest behind you, knees just proud of the
    front lip with the shins hanging down in front of it, feet clear of the floor. **FAIL, and each
    failure names its own cause:** rear hanging off the back of the pan and over the aisle = the seat's
    `riderOffset` is missing or has the wrong sign (`entities/cabin.json`, `x: -0.5`, vanilla's own
    number from `boat-sailed.json:53-54`); shins disappearing *into* the pan rather than in front of it =
    the pan is deeper than 10 units again; facing your own backrest with your back to the aisle = a
    backrest has gone back onto an end wall. **Then board the rear seat and repeat — the two must look
    the same.** Two seats behaving differently is the specific signature of a facing problem rather than
    an offset one. A second player standing outside is the cheapest observer for all of it, because that
    forced remote yaw is exactly the case under test.
    **PASS — the two rows read as evenly placed.** From outside, through the glazing: the same amount of
    clear floor in front of each rider's feet (12.34 units by construction, so it should read as *equal*,
    not merely "enough"), and neither rider's toes near a wall. The front bench moved back 10 units for
    this; **FAIL:** the front rider is jammed against the end wall again, or the interior now looks
    bunched at one end.
    Also **PASS:** in first person your eye is in the **glazing band** — you can see
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
    you should get *"The cabin is moving. Wait until it stops at a tower - or hold [your sneak key] for 2
    seconds to jump out, and take the fall."* and stay seated. (The wording matters: C2 in KNOWN-ISSUES
    killed "it stops at the next tower", which is a lie on a line whose middle towers a plain ride runs
    straight through.) **PASS:** the key it names is **your own
    current sneak binding** — rebind sneak in Settings > Controls and the message follows it. Once stopped,
    right-click to get out; you should land on or beside the tower, not in the air.

13a. **Bail out of a moving cabin.** The emergency exit, and it is meant to cost you. Ride a span whose
     middle is well off the ground.
     **PASS:** a single **tap** of sneak mid-span gets the refusal above and nothing else — you stay seated.
     Two or three taps in a row, still nothing. That is the accident guard; a rider brushing the key over a
     ravine must not fall out.
     **PASS:** now **hold sneak down** and keep holding. About **two seconds** later you are out of the
     cabin, in mid-air, and you **fall**. One chat line: *"You jumped out of the cabin between X and Y. It
     carries on without you."*, naming the two towers of the span you were over (their given names if you
     have named them, otherwise a compass bearing).
     **PASS:** you take **normal fall damage for the height you actually jumped from** — a bail-out three
     blocks above a hillside costs nothing, one over a fifty-block drop kills you. **FAIL:** you land
     unhurt from any height (something is softening it), or you die from a short drop after riding down a
     long descent — that is the fall being measured from the platform you boarded at, the
     `PositionBeforeFalling` datum in `RopewayCabinSeat.DidUnmount`. Check the same thing on an ordinary
     **downhill** ride: board at a high tower, ride to a low one, step out normally. **PASS:** no damage.
     **PASS:** the cabin **carries on without you** to wherever it was going and stops there. It does not
     halt, reverse, or park.
     **PASS:** the tension weight is untouched and unchanged by any of this. It is a tensioner, not a
     battery — no gauge, no number, nothing that moves. If you find a charge reading anywhere in this mod,
     that is the deleted store having come back.
     **PASS — do this one in multiplayer, it is the whole reason the permission is a synced flag.** Have a
     second player watch from the ground while you bail. **PASS:** they see you leave the cabin and fall.
     **FAIL — the regression this guards:** the watcher sees you still sitting in the cabin, riding on
     without a body, for the rest of the session. Every client answers the unmount exactly once, so a
     client that refuses it never gets a second chance.
     **PASS:** walk to a tower, board the same cabin again, ride off, and **tap** sneak once. You get the
     refusal, not an instant ejection — the bail permission is retired when you board. Do it once more
     after a **relog**, since the permission rides on your player and players are saved.
     **PASS — the accident case, and the one worth being fussy about.** **Crouch-walk** onto the platform
     and, *without ever letting go of sneak*, right-click the cabin to board, then keep holding through the
     departure and for a good ten seconds after. **PASS:** you stay in the cabin. **FAIL:** you are thrown
     out mid-span having pressed nothing and seen no message — the hold must need a fresh press *after* the
     cabin is moving, because boarding copies the key you were already holding into the seat. Now let go
     for a moment and hold again: **PASS:** two seconds later you bail normally.
     **PASS:** holding sneak while the cabin is **stopped** just gets you out normally, as always. Keep
     holding as it sets off again: **PASS:** nothing happens until you release and press afresh.
     **PASS — the trap this exists for.** Do this one on a **three-or-more-tower line** — build step 16
     first if you have not — because there has to be a surviving line to re-base onto; break a tower of a
     two-tower line and you are testing 17b instead. Bail out mid-span, leaving the cabin empty between two
     towers,
     then break a tower on that line (step 17 no longer refuses you: nobody is riding). **PASS:** the empty
     cabin re-bases and parks at an end tower rather than being stranded. Calling it (step 15) from a tower
     while it hangs mid-span **PASS:** works — the cabin simply re-aims and carries on to the tower you
     called it to.

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

17. **Break safety** (the first half is multiplayer — someone has to be seated while someone else swings).
    With a passenger seated, try to break any footing on that line.
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
    span counts, the cabin is still on the line where you left it, and it is rideable.
    **The cabin does not move on a reload. At all.** That is the whole check, and it replaces the old
    "it must snap to the nearer end tower" — snapping *was* the bug. Note where it is before you quit
    (a screenshot of the position readout is enough) and compare.

18b. **Reload it mid-span, twice.** Stand where you can see the cabin, send it across a long span and quit
     to menu **while it is between two towers** — an ordinary ride with no destination, not a call, because
     the called trip already survived and the plain one did not. Reload. **PASS:** it is exactly where it
     was, and then carries on in the same direction. **FAIL (the old bug):** it is at an end tower.
     Then do the same on a line whose **first hop goes back on itself** (tower 2 west of tower 1, the line
     then running east) and whose towers span several chunk columns, so that at load only some of them are
     registered: reload from far enough away that the columns stream in one at a time. **PASS:** the cabin
     sits still through the load and is where you left it once the last tower is in — it must not park, and
     it must not end up at the start of the line. Reload twice more: the old failure got *worse* each time,
     because it re-keyed onto an interior tower.

19. **Relog while riding** (multiplayer, or singleplayer alt-F4 while seated). Reconnect.
    **PASS:** you are on solid ground at or near a tower, not falling — or still seated in a cabin that is
    finishing the trip it was on. Both are correct now and which one you get depends on whether the server
    kept running: `departed` is persisted, so a cabin saved **in motion** resumes and drives itself to the
    end of the line while you are away, and the seat holds you until it stops (`CanUnmount` refuses while
    it is moving). If your player entity despawned while seated, `DropGhostPassengers` unseats you at a
    tower and puts you on the footing, which is the singleplayer alt-F4 path and the old PASS text.
    **FAIL:** you are falling, you are inside a block, or you are standing somewhere the cabin never went.

20. **Blocked span.** Wall off the middle of a span with stone while the cabin is parked, then ride.
    **PASS:** the cabin holds at the tower before the obstruction instead of dragging you into the wall.
    Clear the wall and call it — it moves again.

21. **Clearance follows the cabin, not the rope.** Build a stone ridge across the middle of a span whose
    top sits **two blocks below the rope line** — clear of the rope, in the cabin's way. **PASS:** the
    tower does not appear in the picker while the ridge is there, and if you build the ridge after the
    link, the cabin holds at the tower before it. **FAIL:** a link succeeds and the cabin drives a seated
    rider through solid stone.

22. **Link while riding** (multiplayer — it needs a rider and a linker at once). With a rider seated on
    line A–B, have a second player link a new tower C to
    A. **PASS:** the link is **refused** with *"line in use"* — the same rule unlinking already had, because
    a merge re-bases the cabin and re-basing parks it at an end of the new chain, which is an arbitrary
    teleport of whoever is sitting in it. **FAIL:** the link succeeds and the rider moves.
    Get out and link again: **PASS:** it links, and the empty cabin re-bases onto an end tower of A/B/C.

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
    Then the **mid-line explosion with a rider** (§3d.4, multiplayer — someone has to be seated while
    someone else destroys `C`): on `A–B–C–D–E`, seat a player in the cabin between
    `A` and `B`, and blow up `C`. **PASS:** the rider is **unseated where the cabin is** and the cabin
    re-bases onto the `A–B` half — the half it was actually on. **FAIL:** the rider is carried to an end
    tower, or the cabin lands on the `D–E` half it was never on.
    Finally, **walk away until the tower's chunk unloads and come back** (or reload the world).
    **PASS:** the spans are still there. Chunk unload must not unlink anything.

25. **A line that reaches past the loaded chunks** (§3c.1 — C3). You need a line whose far end is outside
    the loaded radius when you stand at the near end. The loaded radius is
    `min(MaxChunkRadius, ceil(viewDistance / 32))` chunks, and `MaxChunkRadius` 12 is the *cap*, not the
    value: at the shipped `viewDistance` of 256 that is **8 chunks = 256 blocks**, while `maxLineLength` is
    **320** and `maxSpan` is **48**, so a tower buys you at most 48 blocks a hop.
    **Do this in singleplayer, with the view-distance slider wound down to 128.** A singleplayer client
    skips the cap and gets its own slider, so 128 is 4 chunks = **128 blocks**, and **five** towers — four
    spans — put the last one, and usually the last two, outside it. The tower at 192 is always out; whether
    the one at 144 goes with it depends on where the near tower sits inside its own chunk, because the
    keep-set is whole chunk columns out to ring 4 inclusive. One dark end tower is all this step needs.
    **On a stock server it takes seven towers, not five, and an earlier draft of this step said five.**
    Five towers is four spans = at most **192** blocks, which is inside the 256-block window, so nothing
    truncates and every PASS below is unreachable — the flag is never set and the feature reads as broken
    when it is the arithmetic that is. **Seven** towers is six spans = **288 > 256**, and one tower past the
    window is enough: `MarkLoadedEnds` sets `Truncated` on either end tower of the walked chain being
    unloaded. Getting the far **two** out needs **eight**, i.e. the full 320-block `maxLineLength`.
    Note that the chain walk stops one hop *past* the loaded region, so one unloaded tower still yields the
    full chain — two consecutive unloaded towers is what shortens `TotalLength`. That is about the length,
    not about the flag; one is enough for everything below.
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

26. **Freight** (new this round — the whole feature, and the first asset-side user of the plain
    `attachable` behavior rather than the `rideableaccessories` subclass every vanilla entity takes).
    Craft or `/giveblock` two **reed baskets** — the block code is `stationarybasket-north`, *not*
    `stationarybasket-reed-north`: the basket's only variantgroup is `side`, and reed/papyrus/vine is a
    stack **attribute** with `defaultType: "reed"`. Park a cabin at a tower.
    You will also want a **chest** (code `chest-north`, `normal-generic`) for the second half of 26a and a
    **crate** (code `crate` — it has no variantgroups at all) for the one refusal check.

    **26a — attach.** Hold the basket, **Ctrl** and right-click one of the two benches.
    **PASS:** the basket appears sitting on that bench, centred on it, and the place sound plays. The
    basket must be *visible*: an attach that succeeds but renders nothing means `stepParentTo` lost its
    element (`cargofront` / `cargorear` in `shapes/entity/cabin.json`) and looks exactly like a failure.
    **PASS:** look at the bench — the interaction help lists *"Ctrl + right-click: Detach"* and
    *"Right-click: Load or unload the freight"*.
    **FAIL:** a crash or a red error line while merely *looking* at the cabin — that is the
    `wearableSlots` / `selectionBoxes` index alignment (`RopewayAssetContractTests`
    `TheCargoSlotsAreTheBenchesAndIndexAlignWithTheSelectionBoxes`) having drifted.
    Now the same with a **chest**, on the other bench. **PASS:** it attaches, exactly as the basket did,
    and a plain right-click opens the same floaty slot grid — a chest carries the same
    `BoatableGenericTypedContainer` behaviour a basket does. **FAIL:** it refuses — `forCategoryCodes` has
    been narrowed back to `["basket"]` and the cabin is again deviating from vanilla's own cargo list.
    **PASS (geometry):** on the **rear** bench the chest sits flush to the inner face of the east end wall
    and clears the roof by 7 units; its two rope handles poke a unit past that face into the open window
    band, which is expected and documented on `//cargorear`. **FAIL:** the chest's body passes through the
    end wall or the roof.
    Try a **crate**: **PASS:** it refuses. This is not about capacity — a crate carries
    `CollectibleBehaviorBoatableCrate`, which overrides `OnInteract` without calling `base`, so it has no
    dialog on a mount at all and Ctrl + right-click would empty it *and* detach it in the same click.
    **FAIL:** the crate attaches — `forCategoryCodes` has been widened back to include `"crate"` and
    every freight string in the mod now lies about the verb.
    A **trunk chest** (`chest-trunk-north`) will also attach and then open nothing. Known, inherited from
    vanilla — see `docs/KNOWN-ISSUES.md` — not a failure of this step.

    **26b — the bench is now freight, not a seat.** Right-click the loaded bench with an empty hand.
    **PASS:** the cargo dialog opens (a floaty slot grid on the cabin, not the chest GUI); you do **not**
    sit down. Put a few items in and close it. Now right-click the cabin **body** (roof or upper wall).
    **PASS:** you board the *other* bench, and only that one. Load the second bench too.
    **PASS:** the cabin can no longer be boarded at all — two loads or two riders or one of each.
    **PASS:** the empty bench, before you load it, still boards you on a plain right-click.

    **26c — detach is guarded.** With goods still inside, Ctrl + right-click the loaded bench with an
    empty hand. **PASS:** nothing comes off (vanilla's `OnTryDetach` refuses a non-empty container).
    Empty it through the dialog, then Ctrl + right-click again. **PASS:** the basket comes back to hand.

    **26d — it rides.** Reload one bench with goods, board the other, and ride a full span.
    **PASS:** the cargo stays on the bench the whole way and does not jitter, and the dialog stays open
    and follows the cabin if you open it mid-ride. **PASS:** the cabin runs at the same speed loaded as
    empty — cargo weight is deliberately not modelled yet (the load model has a term for it, unused).

    **26e — PERSISTENCE, the part that had to be right.** With goods on both benches:
    - Save, quit to menu, reload. **PASS:** both containers are still there with the same contents.
    - Walk away until the cabin's chunk unloads — leave it **mid-span** by riding, stopping at an interior
      tower and walking off — then come back. **PASS:** unchanged.
    - Stop and restart the **server** (or the singleplayer world) with the cabin mid-line. **PASS:**
      unchanged.
    **FAIL:** the containers come back empty, or come back at all but with the goods gone. Empty
    containers mean the `attachable` behavior fell off the **server** behavior list; missing goods mean
    something wrote the inventory without calling `storeInv`.

    **26f — TEARDOWN, and nothing may be destroyed.** Three separate runs, each with a loaded basket on
    **both** benches, and count the items before and after each:
    - **Cut the span the cabin hangs on** (Ctrl + right-click a footing, click the connected row).
    - **Break the last tower of a two-tower line** so the line has no spans left.
    - **Blow up / break every tower of the line** so the cabin's `gone` backstop fires with no player.
    **PASS**, all three: the goods come out **loose** as item stacks, the two **emptied** containers come
    back as items, and the **cabin item** comes back — into your inventory where there is room, on the
    ground under the cabin where there is not (and always on the ground for the third case, which has no
    player). Nothing is missing. **FAIL — this is THE regression:** the containers or their contents
    simply vanish. Vanilla's only unprompted drop is gated on `EnumDespawnReason.Death` and the cabin
    despawns with `Removed`, so `EntityRopewayCabin.UnloadCargo` is the only thing standing between a
    loaded cabin and a silent delete (`RopewayCargoTests` pins the pure half).
    **PASS:** pick the returned basket up, place it as a block. It is empty — as it should be; the goods
    were handed to you separately, precisely because a placed container drops any `backpack` tree the
    itemstack was carrying.

    **26g — teardown with the dialog OPEN.** Load a bench, **open it and leave it open**, then cut the span
    from where you stand. **PASS:** the dialog closes by itself the instant the cabin goes, and the goods and
    the emptied basket land as in 26f. **FAIL:** the dialog stays on screen showing the contents of a cabin
    that is no longer there, and only disappears when you walk away. That is the despawn hook being skipped
    because the slot was nulled first — the server side of the same fault leaks one `InventoryGeneric` into
    `player.InventoryManager.OpenedInventories` per teardown, invisibly, for the rest of the session.

    **26h — admin removal is not a shredder.** Load both benches, then `/entity remove` the cabin (or delete
    it with WorldEdit). **PASS:** the goods and both emptied baskets are on the ground where the cabin was.
    The cabin item itself is **not** returned on this path and is not expected to be — `/entity remove` is a
    delete, and it destroyed the cabin item before this change too. Repeat with `/entity kill`.
    **PASS:** same, and — the point of this one — **exactly one** copy of each stack. Two copies of the goods
    means `dropContentsOnDeath` has been put back on `cabin.json`, which drops the container with its
    `backpack` tree intact *and* spills the same goods loose.
    **FAIL:** everything vanishes. The guard is on `EntityRopewayCabin.Die`, not on `DropAndDie`, precisely
    so these two commands are covered.

    **26i — handbook and guide.** `H` → Ropeway → *Building a Line*: **PASS:** the **Carrying freight**
    section is there and says two loads, or one load and one passenger, and it names the **basket** and the
    **chest** as what fits. Sneak + right-click a footing: **PASS:** the guide's closing paragraph names
    both containers, the Ctrl verb and the "cannot be sat on" rule. **FAIL:** any of these still offers the
    crate, or still claims the basket is the only container that fits.

27. **Power — the drive is a real mechanical load** (this whole step is new: the tension weight used to be
    a battery you wound up, and that design is deleted. Nothing here stores anything.)
    You need a **windmill rotor** (`windmillrotor-wood-north`), **sails** (4 per length), **wooden axles**,
    a **tension weight**, a **drive housing** (`ropeway:drivehousing`) and, if you want to check the
    decoration, a **bullwheel** (`ropeway:bullwheel-north`). No angled gears for the wooden mill — that is
    the point; the maxed metal rotor in 27c-metal is the exception and needs two of them plus blocks to
    stand a wall out of. Creative and `/giveblock` are fine.
    **The two rotors take different sails**, and this is the one that wastes an afternoon: `sail` for the
    wooden rotor, **`sail-large-oak`** for the metal one, 4 per length either way
    (`windmillrotor.json`'s `sailStack`). Offer the wrong sail and vanilla's `OnInteract` returns without a
    message — no toast, no chat line, nothing — which reads exactly like a broken block.

    **27a — the tensioner is a build requirement, and it is the only one.** This wants a line that has
    never had a weight near it, so build a **fresh two-tower pair** well away from the one you have been
    riding rather than breaking that line's weight — a weight broken after the cabin is hung deliberately
    changes nothing (last check of this step), so it cannot get you back to the state under test.
    On that finished two-tower line with no tension weight anywhere near it, hold the **ropeway cabin** and
    right-click an end footing.
    **PASS:** it refuses — *"This line has no tension weight to keep the rope taut. Build one within 8
    blocks of any tower on it first."* — and the cabin item is **not** consumed.
    Look at a footing: **PASS:** the panel says the line has no tension weight and where to put one.
    Try to place the weight out in a field, 20 blocks from anything: **PASS:** it refuses,
    *"A tension weight has to stand within 8 blocks of a pylon footing."*
    Place it beside **either** tower — it does not matter which — and hang the cabin again. **PASS:** it
    goes on, and the footing panel stops mentioning the tensioner.
    **PASS:** the weight looks like a mass **hanging low in its guide** on a rod up to the head beam, and it
    **never moves**, however long you run the line. **FAIL:** it sits on the pad with nothing above it, or
    it slides up and down — the first is the hanger element missing, the second is the deleted gauge.
    **PASS:** place a **second** weight beside the other tower. It is allowed now (one per line was a store
    rule and is gone), it does nothing extra, and nothing anywhere calls it "spare" or "orphaned".
    **PASS:** break the weight with the cabin already hanging. The cabin keeps working; the footing panel
    says the tensioner is missing. That leak is deliberate — it is a build check, not a runtime state.

    **27b — no drive is a cabin that waits, not an error.** Use 27a's fresh pair, once its weight is in and
    it has taken the cabin — it is the line that has never had a housing near it. With no axle and no drive
    housing anywhere on that line, board and
    sit. **PASS:** after the three-second pause nothing happens: no red toast, no chat line, no refusal.
    The cabin simply does not move. **FAIL:** any message about power, a store, a tension weight not being
    wound, or a trip being too dear — those states are deleted and any of them means old code is live.
    **PASS:** the footing panel says *"Nothing on this line is turning, so the cabin will not move"* and
    tells you to build a **drive housing** within 8 blocks of a tower. **FAIL:** it says anything about
    putting a bullwheel on a tower — the wheel does not drive anything any more.
    **PASS:** get out. You can, because it is not moving.
    **PASS — calling refuses out loud.** Still with no drive on the line, stand at a tower with an empty
    hand and **call the cabin** (plain right-click). You get one **red error toast** — the same channel as
    step 5's, not a chat line — telling you nothing on this
    line is turning and to build a drive housing, and the cabin does not take the call. **FAIL:** the
    click is silent and the cabin latches onto a trip it can never make — that was the old behaviour, and
    it looks exactly like a broken call rather than an unpowered line. Build the drive (27c) and call it
    again: **PASS:** it comes.

    **27c — build the drive, and the ladder.** Try to place a **drive housing** out in a field, 20 blocks
    from anything. **PASS:** it refuses — *"A drive housing has to stand within 8 blocks of a pylon
    footing."* — and the block is not consumed.
    **First, prove where the mill's hub has to be, because it is not on the ground.** Stand a **wooden
    windmill rotor** (`windmillrotor-wood-north`) at about head height beside the tower and try to add one
    sail. **PASS:** vanilla refuses it — *"Cannot add more sails. Make sure there's space for the sails to
    rotate freely"*. That is not our block and not a bug: a rotor needs one clear block per sail plus one,
    in a flat disc standing square to its own axle, counted **upward and sideways as much as downward**. A
    rotor low enough to sit beside a housing on the ground cannot take its **first** sail. Earlier drafts
    of this step told you to build exactly that; if you are holding a script that says "two blocks out to a
    wooden windmill rotor" at ground level, it is stale and the build in it is impossible.
    Now build it with the room it wants, and **count the room rather than a height**: **four clear blocks
    under the hub for three sails, six for a maxed five** — and the same count up and sideways, since the
    disc is checked every way. Counting clearance is the only statement that cannot be read two ways; count
    a height and you are one out depending on whether you started from the grass or from the block resting
    on it. On flat ground four clear blocks under the hub puts the hub level with the fourth block above
    the footing beside you.
    Point the rotor's axle along the tower's **passage** axis (the direction the cabin travels), so the
    sail disc stands across the line and can never contain a tower cell.
    **Put the drive housing up beside the hub, at the same height** — set back from the rotor along the
    axle axis with two clear cells between them, not down on the ground — and fill those two cells with
    **wooden axles** running into **any of the housing's four sides**.
    **PASS:** the housing places there. The eight blocks are measured straight through the air, so a
    housing six blocks **above the footing block** and two across is well inside them. **FAIL:** *"A drive
    housing has to stand within 8 blocks of a pylon footing."* at that position — the radius has become a
    ground circle.
    **PASS — the edges of that sphere, worth two minutes** (every height here counted from the footing
    block itself)**:** the housing places **eight** blocks straight
    above the footing and refuses at nine; it refuses at eight up **and one across**; six up allows about
    five across. Do not build the tower's own cells over with it.
    **PASS:** that is the whole **drive train** — **three blocks between the mill and the line** (housing +
    2 axles), no support column, no vertical axles, **no angled gears**. Each of those three is placed
    against the block before it, so none of them needs scaffolding. **The rotor itself still has to be
    mounted against something at hub height**, and at +4 or +6 that is a mast you build; the three blocks
    are the run, not the total. What the drive housing deleted is the scaffold *on the tower*: nothing is
    built on the crossarm and nothing climbs the tower. The mill still stands as high as vanilla makes it
    stand, and the housing goes up beside it. **FAIL:** an axle or the housing refuses to place for want of
    support, or you find yourself building a column up the tower.
    Now add sails one length at a time in good wind. **Do not wait for weather — set it, and pin it:**

    ```
    /weather acp false
    /weather setw strongbreeze
    ```

    **`acp false` first, and it is not optional.** `autoChangePatterns` defaults **true**, and every few
    game hours the server re-rolls `CurWindPattern` as a **uniform** pick over all five shipped patterns —
    `still`, `lightbreeze`, `mediumbreeze`, `strongbreeze`, `storm` — ignoring their weights. `setw` on its
    own sets the pattern and the next re-roll takes it away again, and three of the five it can land on
    stop a wooden mill dead. `acp false` is vanilla's own switch for exactly this.
    **`setw strongbreeze` does not pin the wind at 0.6, and an earlier draft of this step said it did.**
    It picks the pattern; the strength is `strongbreeze.json`'s `strength { avg: 0.6, var: 0.15 }` drawn
    **uniform on [0.45, 0.75]** at the moment the pattern begins, plus a simplex term clamped to [0, 1]
    whose amplitudes sum to 0.85. The familiar 0 / 0.15 / 0.3 / 0.6 / 1.0 ladder is the `avg` field alone.
    What makes the rungs below repeatable is not the wind holding still — it is `TargetSpeed = min(0.6, w)`
    **saturating**: at any wind of 0.6 or better every sail count reads the same number, so the ladder is a
    property of the mill rather than of the weather. Below 0.6 the whole ladder scales down together, and a
    low draw with the noise at zero can read a 3-sail mill at a quarter of the figure quoted for it. The
    *relative* rungs still separate; the absolute numbers are for a wind at or over 0.6.
    (Both commands want the `controlserver` privilege. Without it, check the rotor's own panel for a wind
    speed at 0.6 or better before trusting an absolute figure, and expect a becalmed or turbulence-halved
    mill to under-read.)
    **PASS, and this is the point of the whole redesign — you can FEEL the difference:**
    - **2 sails:** the cabin does not move at all. The mill turns; it cannot carry the load.
    - **3 sails:** it crawls, about **1.2 blocks a second** — slower than you walk.
    - **5 sails** (maxed wood): about **2.2 blocks a second**.
    - a maxed **metal** rotor (10 sails): about **3.0** — you cannot keep up with it on foot. That rung has
      its own step, **27c-metal**, because it cannot be wired the same way.
    Time a span of known length if you want to be exact; ±20% is fine, the point is that the rungs are
    obviously different. **FAIL:** every sail count feels the same — that is the old flat design back, and
    it is what this change exists to kill.
    **PASS:** the **housing's** panel reads what it is turning at in rps; **any footing's** reads what the
    **line's** drives come to in blocks a second, and tracks what you just did.
    **PASS:** build a second drive beside the **far** tower instead and it works exactly the same. There
    is no drive station, and no tower is special.
    **PASS:** break the tower the housing was built beside, on a line long enough to have another within 8
    blocks. The drive keeps working — nothing was bound at placement, so nothing came unbound.

    **27c-metal — the maxed metal rotor still needs a column, and that is a REDUCTION, not a regression.**
    Only worth running once, and only if you want the 3.0 rung.
    Ten sails need **eleven** clear blocks every way in the disc, which on flat ground stands the hub
    eleven blocks above the footing block. **PASS:** at that height the housing cannot reach it — eleven
    blocks of height alone is outside
    the eight, wherever you stand the housing, so there is no horizontal run to be had. The drive has to
    come down: an **angled gear** at the hub, three **`woodenaxle-ud`** below it, a second **angled gear**
    beside a housing standing about **seven blocks above the footing and two across**.
    **PASS:** the vertical column needs a **wall beside it**. Try the **bottom** gear first with nothing
    next to the axles **and the housing not yet placed**: vanilla refuses it (*"axlemusthavesupport"*). The
    tower's own blocks are all `sidesolid: false`, so the column cannot lean on the tower it serves — build
    the wall, then the gear.
    **Build order decides whether that refusal fires at all, so do not report its absence as a bug.**
    `BlockAngledGears.TryPlaceBlock` walks `BlockFacing.ALLFACES` — horizontals before up and down — and
    applies the support check only to the **first** connectable neighbour it finds. A bottom gear placed
    beside an already-built housing finds the housing on a horizontal face and never looks up at the axle,
    so the check is skipped. That is a build-order accident, not a licence: `BlockAxle.OnNeighbourBlockChange`
    breaks an unattached axle that loses its support. Build the wall either way.
    **PASS:** with all five blocks in, the cabin runs at about **3.0 blocks a second**.
    **Do not report the gears here as a regression.** The old crossarm hookup made this same drive descend
    to the crossarm four blocks above the footing; it now stops seven above it, which is one or two fewer vertical axles
    and the same two gears. The handbook says so in as many words; **FAIL** is the handbook claiming this
    rung needs no gears.

    **27c-water — the water wheel.** A water wheel only turns in `rapidwater`, which generates rarely in
    mountain-side streams and **cannot be placed or made by a player in survival** — ordinary water will not
    move it however deep or fast it looks, and that is vanilla's rule, not ours. If you go looking, vanilla's
    own handbook page for the water wheel says the same thing.
    **You do not have to go looking to run this step**, and earlier drafts said you did. `rapidwater-still-7`
    is in the creative block list under both *General* and *Terrain*, and `/giveblock rapidwater-still-7`
    works; place a source, let `FiniteSpreadingLiquid` make the flowing variants downhill of it, and those
    are what the wheel actually reads. SKIP only if you are running this pass in survival.
    Where you do have rapids: the wheel is crafted from two iron four-way hubs on an axle and then **built
    in six right-click stages** (32 support beams, 96 planks, 12 resin, 8 nails-and-strips), and it makes
    no torque at all until the last stage is done. Its axle comes out of **both** ends, at most one block
    above the water surface, and the eight cells ringing it must be clear.
    **PASS:** stand the drive housing on the bank **at hub height** and two or three level wooden axles
    reach it. Nothing climbs, no gears, no column — this is the one drive that genuinely hooks up at ground
    level, when the bank happens to sit level with the hub.
    **PASS:** the cabin tops out around **1.8 blocks a second** however fast the water runs, and it holds
    that speed through weather that stops every windmill on the map. That is the trade.

    **27c-wheel — the bullwheel is decoration, and it TURNS.** Break the **pylon head** on any tower's
    crossarm and put a **bullwheel** in its place.
    **PASS:** the tower still reads *"Tower complete"* — the wheel is a swap for the sheave, not a
    sixteenth-plus-one cell.
    **PASS:** the cabin still passes through that tower without catching: the throat and the station rails
    are the sheave's, unchanged.
    **PASS:** its **spoked wheel stands above the crossarm** and is obviously a wheel from thirty blocks
    away — you can tell a drive tower from a plain one at a glance, which the previous wheel could not
    manage at ten.
    **PASS, and this is the whole reason the block survived:** with the mill running, the wheel **turns**,
    faster with more sails. Stop the mill (break a sail, or wait for calm) and it **stops**. **FAIL:** it
    is still. A still wheel is what made the first attempt worthless.
    **PASS — now watch it from the SIDE for a full turn, and be fussy about this one.** The rim turns **in
    place**, about its own axle, the way a wheel on a shaft does: the hub stays where it is and the spokes
    go round it. **FAIL:** the whole wheel **swings** instead — it rides up, sweeps over the top, and at
    the bottom of the swing dips **below the crossarm**, down into the slot the cabin's hanger blade rides
    through. That is the renderer turning the wheel about the block's centre instead of about the rim's
    own axle, and no test in the mod can see it: the authored shape is clean and the fault is in the
    transform. Park the cabin at that tower while you watch; the dip through the cabin is the tell.
    **One thing you will see from this exact view that is known and accepted — do not file it.** On a line
    with **two** drive towers facing opposite ways, the two wheels **turn against each other** — a north-
    and a south-facing wheel are identical standing still and their yaws are 180° apart, so one positive
    spin reads as opposite rotation in the world. It does not touch the cabin: report only a dip below the
    crossarm.
    **PASS:** the wheel takes **no axle** and its panel says nothing about power. Try to run an axle into
    it: **PASS:** nothing connects, because it is on no network. **FAIL:** it accepts one — the consumer
    has been left on it and the drive is back four blocks up.
    **PASS:** a line with **no bullwheel anywhere** still runs perfectly. It is a marker, not a part.

    **27d — climbing costs.** Build (or ride) a line with one clearly uphill span and one level one.
    **PASS:** the cabin visibly **slows on the way up** and picks up again on the level or the way down.
    **PASS:** it does **not** stop on the climb with a mill that hauls it on the flat. **FAIL:** it stalls
    halfway up a hill — the climb term is meant to be visible, never fatal.

    **27e — pooling.** Put a **second** mill beside a **different** tower of the same line, on its own axle
    network and its own drive housing. **PASS:** the cabin gets **faster** — the drives add up — and the footing panel's line figure
    goes up with it. **PASS:** it works whichever towers you pick; there is no drive station.
    **Then the case that is not pooling:** run **one** axle line along the ropeway and drive **three**
    drive housings off that same network. **PASS:** the line figure does **not** climb with the number of
    housings — one network is one drive however many housings touch it — and the cabin is if anything
    slower, because every hookup declares the full haul load. **FAIL:** each extra housing adds another
    drive's worth of speed. That is free speed for adding load, and it is the one thing a load model must
    never do.
    **And the other way a housing could give speed away.** Build a **second, separate line** whose nearest
    tower sits about six blocks from a tower of the first — two short lines side by side, one drive housing
    between them, in range of both. Hang a cabin on each. Put the housing **clearly nearer one of the two
    footings**, two blocks or more of daylight between the distances, so that which line it ought to drive
    is not in doubt while you read the result. An exact tie is not undefined — it resolves on block
    position, the same way on the server and on every client — it is just not a thing you can settle by
    looking. **PASS:** only **one** of the two lines runs — the
    one whose footing is *nearest* the housing — and the other reads *"Nothing on this line is turning"*.
    **FAIL:** both cabins move. One mill would then be hauling two cabins while only one line's load was
    ever charged to it, which is the same free speed as the case above wearing a different hat.

    **27f — dead calm, and the thing that used to be impossible.** `/weather setw still` **while the cabin
    is mid-span with you in it**, and `/weather setw strongbreeze` to bring it back. Breaking a sail does
    the same job without the privilege; waiting for the weather to do it by itself works too and is the
    slowest of the three — and it never happens at all if you turned `acp` off back in step 11, which is
    the point of turning it off.
    **PASS:** the cabin **stops where it is**. No message, no toast, nothing in the log.
    **PASS:** it starts again **by itself** when the wind comes back, going the same way, and finishes at
    the tower it was heading for.
    **PASS:** while it is stopped you can **right-click to get out** normally — you are in mid-air, you
    fall, and that is correct. You are never trapped; that is why the gate could be deleted.
    **PASS:** save and reload while it is stopped mid-span. It comes back exactly there, still pointed the
    same way, and carries on when there is wind. **FAIL:** it teleports to a tower on reload.

    **27g — the ropeway is a citizen of your network.** Put a **quern** on the same axle network as the
    ropeway's mill and grind something while the cabin runs. **PASS:** both work, and both are slower than
    they would be alone — a maxed wood mill carries a ropeway plus a quern, and this is what "a real load"
    means. **PASS:** park the cabin at a tower and the quern speeds back up: a ropeway with nothing to haul
    drops to a nearly-zero idle load, so a finished line does not tax the mill it shares forever.
    **FAIL:** the whole network stalls dead with only the ropeway and a quern on a maxed five-sail mill.
    **PASS, the cabin that never started — on a line that is WHOLE:** break the sails (or use a line with no
    drive at all), sit in the cabin through the boarding pause, get out, then put the sails back with the
    cabin standing there empty. The quern runs at its full unloaded speed the whole time. **FAIL:** the quern
    stays permanently slow afterwards — that is a cabin that declared itself hauling without ever having
    moved, and only breaking it would clear it.
    **A truncated line exempts this, deliberately, for the same reason the call refusal is exempt.**
    `MayStart` is `departed || truncated || lineSpeed > 0`, and the boarding grace is its third caller — so
    on a line with a dark end (step 25) a rider who sits through the pause latches `departed` with nothing
    turning, and the housings write `HaulResistance` until something does. Do not run this check on such a
    line and do not file it; `KNOWN-ISSUES.md` records the trade. It clears itself the moment anything turns.
