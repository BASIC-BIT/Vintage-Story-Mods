# Ropeway v0.1 — in-game QA script

Manual operator checklist. One in-game session has happened: the mod loaded clean, and it produced the
four findings the previous round fixed — cabin built across the travel axis, cable rendering nothing, the
picker showing only link candidates, and no way to name a tower.

**THIS ROUND MADE THE HAUL ROPE A LOOP** (`STACKED-LOOP-SPEC.md`, and
[KNOWN-ISSUES.md](KNOWN-ISSUES.md) "The haul rope is a LOOP"). Two strands stacked **1.33 blocks** apart —
one wheel diameter, which is where the number comes from — the cabin on the lower one, and a bullwheel at
each terminal taking the rope round from one to the other. **One cabin, and there is no second one to look
for.** Steps 0, 1b, 3, 10b, 11b, 12b, 23 and 27c-wheel carry new checks for it, and there are three new
steps: **10e** (the return strand at a plain tower), **11c** (the wrap at a terminal) and **21b** (the row of
clearance above the rope). If you are holding a copy that talks about "the cable" in the singular, it is
stale. (This list read *"1b, 7, 10b, 11b, 12b, 20, 21, 23"* for one round and named three steps that carry
no loop check at all — 7 builds a tower, 20 walls off a span and 21 raises a ridge **below** the rope, and
none of the three changed. The head's new mast and saddle are checked at **1b** on the block in hand and at
**10e** with a strand on them; the clearance change is **21b** alone. A step list is a walk order, so a
tester who cannot find the check the preamble promised has to decide whether the script or the mod is
wrong.)

**The round before made the drive and the tensioner STRUCTURE** (`STATION-DESIGN.md`): they are cells of two
new tower kinds, `drivestation` and `tensionstation`, rather than blocks you stand near the line. Steps 0,
1, 2, 3, 6, 11 and the whole of 27 are rewritten for it, and every "within eight blocks" in the old script
is gone — if you are holding a copy that still has one, it is stale.

**The round before restructured the tower** (`DECISIONS.md`, "2026-07-31 — tower restructure"): the rear
gantry is gone, and the controller moved from the pylon head at head height to a **pylon footing on the
ground**. Everywhere the old script said "right-click the pylon head" it now says "right-click the
footing" — every verb moved. Every measurement in this script was
re-derived from `blocktypes/pylonbase.json` and the shipped shapes on 2026-08-01, and the same numbers are
asserted by `renders/scenes/gen_manifests.py` and `RopewayAssetContractTests.TheCabinFitsThroughTheTower`.
**Walked front to back again on 2026-08-04 for the loop**, which is how steps 0, 3, 10e, 11c, 12b, 21b and
23's return-strand clause came to be where they are rather than bolted on at the end.
See [KNOWN-ISSUES.md](KNOWN-ISSUES.md) for what source review already found and did not fix.


Creative mode is fine for steps 5+; do steps 1-4 in survival at least once to check the recipes.
Watch `%APPDATA%\VintagestoryData\Logs\client-main.log` and `server-main.log` throughout.

0. **Migration, if and only if you have a world with a line built before this round.** Load it.
   **PASS:** the line itself survives completely — towers still read complete, spans still drawn, names
   still there, the cabin still hanging where it was. Nothing about the chain changed.
   **PASS — the loop arrives and nothing has to be rebuilt for it.** Every span now shows a **second strand**
   over the first, every pylon head has grown a **mast and a saddle** above its sheave, and each terminal
   station's hoop has become a rope **going round the wheel and coming back**. None of that is persisted
   state — it is drawn geometry and one extra row of clearance rays — so it simply appears on load. **FAIL:**
   a span that had a cable yesterday has none today, or a tower reads incomplete because of the head shape.
   **EXPECTED on a migrated world, and neither is a bug to file:** a plain **end** tower shows its two
   strands converging onto the sheave with an empty saddle above them (the line has no station there yet —
   step 10e), and a station the line runs **through** shows its wheel standing about seven eighths of a block
   higher than the shaft driving it (it is holding the return strand down — step 27c-wheel).
   **The one behaviour that genuinely changed:** a **new** span over terrain that rises to within a block
   above the rope line is now refused where it would have been allowed. Existing spans are never re-checked,
   so no built line loses anything — step 21b.
   **PASS:** `server-main.log` carries one *"Failed loading blockentity TensionWeight … Will discard it"*
   line per old tension weight. That is the intended migration, not a bug: the weight has no block entity
   any more, so every one of them fails at once and the blocks stay as decoration.
   **PASS:** the footing panel now says *"Nothing on this line is turning"* and *"This line has no tension
   station"*. Both are true — a free-standing drive housing is not a cell of a station, so it drives
   nothing however close it stands, and the old weight is not a tension station. The old housing keeps its
   axle and its own panel still reads what it is turning at; it simply turns nothing.
   **PASS:** a cabin already on the line stops and **is not stranded** — `IsMoving` is false, so right-click
   steps you out. A cabin cannot be *placed* on that line until a tension station exists.
   **EXPECTED, and it is the one migration case the list used to miss:** a **plain tower wearing a decorative
   bullwheel** now loads **incomplete**, and while it is incomplete it takes no clicks at all — no picker, no
   call, no rename. A plain footing's centre cell used to accept a pylon head *or* a bullwheel and the old
   guide text said the swap was "optional and changes nothing mechanical"; the wheel is a station cell now,
   so `ropeway:pylonbase` wants the head and nothing else. **The repair is one block:** put a pylon head
   back in the middle of the crossarm and the tower goes green and answers clicks again. **FAIL:** the tower
   reads complete with a bullwheel on it, or reads incomplete and its overlay does not name the centre cell.
   **The repair, and it is two towers per line.** Break each end tower's footing (that refunds the span's
   rope and unlinks it), re-place it as a **drive station footing** at the mill end and a **tension station
   footing** at the other, build the two legs, and re-link. The old housing and the old weight are picked
   back up and go straight into the new legs, so nothing is wasted.
   **FAIL:** a crash on load, a tower that lost its spans, or a cable that stopped being drawn.
   **Also still true from the round before:** a world with towers older than the ground footing carries one
   *"Failed loading blockentity PylonHead …"* line per old tower and those towers are inert decoration.

1. **Install and load.** Copy `ropeway_0_1_0.zip` into `%APPDATA%\VintagestoryData\Mods\`, start the game,
   open a world. **PASS:** no `Ropeway:` error lines in either log at startup. In particular there must be
   no *"multiblockStructure on ropeway:… lists '…', which matches no loaded block"* — that line
   means a structure wildcard does not resolve at runtime. **Three footings carry a structure now** —
   `pylonbase`, `drivestation` and `tensionstation` — so read the block code in the message before you
   assume it is the post wildcard. The post wildcard accepts eight families:
   `log-placed-*`, `debarkedlog-*`, `planks-*`, `rock-*`, `cobblestone-*`, `drystone-*`, `rockpolished-*`
   and `stonebricks-*`. Note the verifier only tests the whole key, so a dud alternative hides behind the
   live ones — step 7 is what actually proves each family.
   **PASS — the recipes resolve clean.** **No** `failed to resolve` line anywhere in the startup log.
   The 15 files in `assets/ropeway/recipes/grid/` used to expand to about **19,000** registered recipes,
   because every wildcard carried a `name` and a named wildcard makes the loader expand the file once per
   metal, wood and rock — all held in RAM and serialised to every joining client, for outputs that never
   used the name. Three of those expansions asked for `metalplate-blistersteel` and two siblings, which do
   not exist, so `drivehead` logged three resolve errors at **every** start. The names are gone and so are
   the errors, and that is the whole of what this step checks.
   **FAIL:** any `Grid Recipe with output 'ropeway:…' contains an ingredient that cannot be resolved`, or
   any `failed to resolve N recipes`.
   **Do not go looking for a recipe count, and do not fail anything on one.** `RecipeLoader` logs the
   number of JSON **files** it parsed, never the expanded total — the 19,000 never appeared in any log line
   and neither does the 15, because the mod logged 15 files before the fix as well. That line is also
   global across vanilla and every other mod, so the ropeway's own recipes cannot be read out of it.
   **PASS — no missing textures.** Grep the client log for `Missing mapping for texture code`. There must be
   none. This is the one class of defect the suite genuinely cannot see: the asset tests check that every
   `#key` a face uses is *declared*, and that the blocktype and its shape shadow copy agree, but a declared
   key pointing at a path that does not exist on disk resolves to the unknown-texture checker at tesselation
   time and logs on that thread. A green build and a magenta block are compatible.

1b. **Look at all fifteen blocks.** In creative, place one of each — footing, drive station footing, tension
   station footing, pylon head, brace, bullwheel, drive housing, drive shaft, drive head, lay shaft, tension
   weight, tension guide, tension head — plus the cabin. **PASS:** nothing draws the magenta-and-black
   checker, and the palette reads as a ladder: riveted iron on the crossarm and lattice, dark tarnished
   castings on the drum, gearbox, sheave cheeks and wheel, bright steel on the shafts and bearing caps, cool
   grey andesite on the plinths and the counterweight. **PASS at range:** from about 30 blocks the drive
   station's dark gearbox and the tension station's pale mass are both still readable against a plain tower.
   That distinction is the whole reason the bullwheel exists, and it is the one thing no test can assert.
   **PASS — the pylon head has grown, new this round.** A short riveted **mast** rises out of the top of its
   sheave housing with a small dark **saddle** on top of it, about a block and a third above the throat.
   That is the **return shoe**, and it is what the loop's upper strand rides on at every plain tower. It is
   there on a bare block in your hand as well as on a built tower — it is authored geometry, not something
   drawn only when a span exists. **FAIL:** the bullwheel has grown one too; that block carries neither,
   because at a station the wheel is the carrier.

2. **Craft the parts.** **Chisel your bits first.** Every station recipe pays its fastenings in **metal
   bits, eight to a slot**, and a whole station wants about a hundred of them. Chisel + ingot = 20 bits,
   chisel + metal plate = 40, so budget about **six ingots of bits per station** before you sit down at the
   grid. Drop a stack into each slot and it takes 8 out of it; you need one stack per bit slot, so split
   them rather than carrying one pile of 200. Then, in the crafting grid — **the quantity after the ×
   is one craft's output**:
   - **Ropeway brace ×8** — stick, metal plate (any metal), stick in a 1×3 *row*.
   - **Haul rope ×4** — rope / metal bit / rope in a 1×3 *column*.
   - **Pylon head ×1** — rope / brace / metal bit, top to bottom in a 1×3 *column*.
   - **Bullwheel ×1** — **8 metal bits**, **pylon head**, **8 metal bits** in a 1×3 *row*, so each one eats
     a pylon head of its own. **Craft two.** It is the centre crossarm cell of a drive station *and* of a
     tension station, so a two-station line wants one each and neither structure completes without it —
     this list used to omit it entirely and sent testers out to step 11 two bullwheels short.
     **The two pylon heads it eats are the same two the finished line has**, so if you follow this script's
     walk — plain towers at steps 7 and 9, converted at step 11 — you want **two more pylon heads** than
     the bill funds, for the plain crossarms you build first and break again. Steps 5+ are creative, so
     `/giveblock` them; the bill is right and prices the **finished** line, not the walk. The same is true
     of posts: the walk peaks at 16 placed against a bill of 8, because step 11 hands a column back at each
     end.
   - **Pylon footing ×1** — plank, metal bit, plank on the top row; three loose stones under them.
     **Craft two**, and the bill below buys exactly two: one for tower 1 at step 4 and one for tower 2 at
     step 9. At step 11 you break both back out of the ground and they become the two station footings.
   - **Ropeway cabin ×1** — haul rope, empty, haul rope / brace, plank, brace / plank, plank, plank.
   - **Tension weight ×1** — **8 bits**, loose stone, **8 bits** on the top row, then plank, loose stone,
     plank twice under it. Cell [3,0,0] of a tension station (step 11).
   - **Drive housing ×1** — **8 bits**, metal plate, **8 bits** across the top row, three planks under them.
     Cell [3,0,0] of a drive station (step 11).
   **The station legs. The two station FOOTINGS are not crafted here — they are crafted at step 11**, out
   of the two plain pylon footings you break back out of towers 1 and 2. Both station-footing recipes eat a
   whole `pylonbase`, and the bill below funds exactly two `pylonbase` crafts, so crafting them at this
   step spends both and leaves you nothing to place at step 4. Their grids are written out at step 11 where
   you need them; you still pay for them out of this bill (a metal plate and 16 bits), just later.
   - **Lay shaft ×2** — **8 bits**, metal plate, **8 bits** in a 1×3 *row*. Both stations want two, so that
     is one craft per station, exact.
   - **Drive head ×1** — **8 bits**/plate/**8 bits** on the top row, **8 bits** in all three cells under it.
     Forty bits and one plate; it is the most expensive block in the mod and it is meant to be.
   - **Drive shaft ×3** — stick, plate, stick on the top row, then stick, **8 bits**, stick. A leg is three
     cells, so that is one craft, exact.
   - **Tension head ×1** — **8 bits**/plate/**8 bits** on the top row, **8 bits**/stick/**8 bits** under it.
   - **Tension guide ×3** — stick, plate, stick on the top row, then **8 bits**, **rope**, **8 bits**. One
     craft per leg, exact.
   **PASS:** all thirteen appear in the crafting output under real names rather than raw lang keys — the
   two station footings at step 11 make fifteen.
   **PASS:** every one of them takes **any** metal — try a bit or plate of a metal you have spare rather
   than iron, and try a plate of one metal beside bits of another in the same grid. The wildcards carry no
   `name` any more, so nothing couples the two.
   **The quantities divide now, and that is the thing to check.** A tower is a footing, a head and **six**
   braces — plus the one that goes inside the head — so **seven**, against a brace craft of eight: one
   craft, one spare. A station's leg is **three** cells against drive-shaft and tension-guide crafts of
   three: one craft, none spare. A station's crossarm wants **two** lay shafts against a craft of two.
   Haul rope is still `ceil(span / 4)` per span — a 30-block span is **8** — against a craft of four, so
   that span is two crafts. Nothing on this list needs a second craft for a spare part any more; if
   something does, the yield regressed.
   **What a whole first line costs**, two stations plus **one 30-block span** plus the cabin: **10 metal
   plates and 215 metal bits — call it 31 ingots** (10×2 + 215×0.05 = 30.75) — with 15 planks, 11 sticks,
   9 loose stones, 9 ordinary rope and 8 post blocks. Every extra **plain** tower after that is **one plate
   and two bits**, and every extra 30-block span is **four rope and two bits**.
   **Say the span or the figure means nothing.** 30 blocks is the canonical one, here and in the handbook
   and in `RECIPE-LADDER.md`. A **20**-block span instead prices out at **214 bits and 7 rope** — one
   haul-rope craft less — and that is a different line, not an error in either number. The reason the
   30-block line needs the third craft: `ceil(30/4)` = **8** haul rope is two crafts exactly with nothing
   over, so the cabin's own two have to come out of a third.
   **The tower count is much more than three.** Steps 9 and 16 want three; 12b wants two separate
   three-tower lines, 18b a line whose first hop doubles back, 27a a fresh pair, 27d an uphill line, and 25
   wants five (singleplayer, slider at 128) or seven on a stock server. **Each of those lines wants one
   drive station and one tension station of its own**, which is two of its towers built as stations rather
   than two extra blocks beside it — see step 11. Do steps 1-4 in survival once for the recipes and
   then take the rest in creative; nobody is meant to hand-craft that in survival.

3. **Handbook.** Press `H`, find the **Ropeway** category tab. **PASS:** the tab is labelled "Ropeway"
   (not `handbook-category-ropeway`), all **three** pages open — *Aerial Ropeways*, *Building a Line*,
   *Power and the Drive* — the `<itemstack>` renders spin, and every link between them works, including
   50 → 51 → 52 and both pages' way back. **PASS:** the overview page describes a footing and one
   crossarm, not two gantries.
   **PASS:** the overview page carries a *"What it costs"* section and **every number in it matches what
   you actually spent in step 2** — a plain tower about two ingots, a drive station about 16 and a tension
   station about 13, a whole short line about 31 for **ten plates and 215 bits**. It is the
   only page that quotes prices, so it is the one that lies if a recipe is ever retuned without it.
   **FAIL:** it still says a tower costs two metal plates because six braces is two crafts — true at a
   brace yield of 4, false at 8.
   **PASS — the short line's sentence names its span, and it is 30 blocks.** The same figure priced at a
   20-block span is 214 bits and 7 rope; both are right and an unlabelled one is neither.
   **FAIL:** any cost sentence on the page that quotes a bit count or a rope count without saying which
   span it is pricing.
   **PASS — the span is priced in whole crafts, not at a rope-per-block rate.** Haul rope comes four to a
   craft, so a 20- and a 30-block span cost the same four rope and two bits, and 48 blocks costs six and
   three. **FAIL:** *"one rope for every eight blocks"* or any other per-block rate stated as a price — it
   rounds the wrong way on about a third of legal span lengths and sends a tester out one craft short.
   **PASS:** page 51's rope paragraph says one craft makes **four** haul rope and that a stack of 16 covers
   any single span. **FAIL:** it says a long span is paid out of several stacks; the longest span in the
   game is 12 haul rope. **PASS:** the power page describes a drive that turns the rope and a
   tension weight that keeps it taut, and says nothing about winding, charge or paying for a trip.
   **PASS — all three pages describe the haul rope as a LOOP, new this round.** Page 50's opening says two
   strands, one cabin, and its word list has entries for the **going strand**, the **return strand** and the
   **return shoe**; page 51's *"What blocks a span"* says the corridor runs from a block **above** the going
   rope down past the cabin's floor; page 52's bullwheel section says the rope goes round the wheel and
   leaves higher. **FAIL:** anywhere on any of the three that still says the cabin runs on "a haul rope"
   with nothing coming back, or that describes the span check as running "from the rope line down". A tester
   who reads that goes looking for one rope and files the second one as a bug.
   **FAIL:** anything on any page suggesting a **second cabin** on the return strand. There is one cabin, on
   the lower strand, and the upper one carries nothing.
   **PASS:** the power page carries a *"A windmill needs room"* section, and it states the room as
   **clear blocks under the hub** — four for three sails, six for a maxed five, eleven for a maxed metal
   rotor — not as a height above anything. **FAIL:** it gives those numbers as *"the hub four blocks up"*
   with nothing saying what "up" is counted from. That reading is one short from the ground you stand on,
   and it fails the last sail on the tester who trusts it.
   **PASS:** the power page describes the drive as a **station** — one of the line's own towers, with a
   machine leg — and tells you to run the mill down the outside of that leg on a vertical axle column.
   **FAIL:** it describes the drive as a block bound to the line **by distance** — a sphere, a radius, a
   housing standing beside the tower rather than being a cell of it, or a housing riding up to hub height.
   Every one of those is the deleted rule, and a tester who follows it builds a drive that turns nothing.
   **Read the sentence, not the number.** An earlier version of this criterion failed on the string *"eight
   blocks"*, which the page then legitimately carried for a while as a station **spacing** rule — so a tester
   walking front to back filed a FAIL against the one paragraph that existed to prevent it. That rule is
   retired (the shared machine leg is closed in code), so the page should say nothing about spacing at all
   now; but if a distance ever comes back, judge it by whether it is telling you where to STAND the drive.

4. **Place the pylon footing.** Stand where you want the tower and place it **on the ground** — this is
   the first block of a tower and nothing has to exist above it.
   **This script deliberately builds both end towers plain first and converts them at step 11**, because a
   station footing is crafted *from* a plain one and the two you break back out of towers 1 and 2 are what
   pays for them. The cost of doing it that way is one re-link per end, at step 11, and the span's rope
   comes back when you break the footing. If you are building a line for real and already know which two
   towers are the stations, craft their footings up front and skip the rebuild — but then buy two more
   `pylonbase` crafts (+4 planks, +6 stone, +2 bits) than the step 2 bill lists, because the bill funds
   exactly two and both are standing in the ground by step 9. Steps 4–9 are written for the plain tower
   because that is what every intermediate tower is.
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
   **PASS:** the "Ropeway Tower" dialog opens; **seven** cells turn in 3D — pylon footing, **drive station
   footing**, **tension station footing**, pylon head, brace, **bullwheel**, then the cabin — and the build
   steps are readable underneath.
   **PASS:** all seven fit inside the inset; the cells got narrower again when the two station footings
   were added.
   **PASS:** the text has a *"Making it move"* paragraph, and it names the two **stations** and the seven
   machine-leg blocks. **FAIL:** it mentions **eight blocks** or a housing standing beside the tower; that
   is the deleted rule, and the guide is the last place still saying so if it does.
   **FAIL modes to report:** cabin invisible (renderer/tesselation), cabin clipped out of the inset
   (the size/offset knob in §4.6), a `Ropeway: could not build the guide cabin preview` log line.

7. **Build the tower.** Following the guide: two posts of **four** blocks each, standing on the ground **three**
   blocks either side of the footing; then the crossarm across their tops,
   four blocks up: **ropeway braces** at x = ±1, ±2 and ±3 and the **pylon head** in the middle, directly
   above the footing. That is **16 cells** in all, two more than before the passage went from three
   wide to five. That extra pair of braces used to be the one thing in the station-rail work that cost the
   player anything, because six braces did not divide by a yield of four; the yield is eight now, so a
   whole tower's seven braces are one craft with one over and the widening is free.
   **PASS:** each ghost cell disappears within ~0.5 s of you filling it, **without re-right-clicking** —
   this is the live-overlay fix; a stale ghost sitting on top of a placed block means it regressed.
   The count in the block-info panel counts down, and within ~1 s of the last block the panel reads
   *"Tower complete / Spans: 0/2"* and every remaining highlight clears itself.
   **PASS:** the posts stand **on the ground**, level with the footing — no gap under them. A tower whose
   legs start one block up is the "posts three tall" mistake and means the offsets moved.
   **PASS:** the tower is **one block deep**. There is no second gantry and nothing behind it.
   **PASS:** the pylon head validates whichever of its four facings you place it in. Point its throat down
   the line anyway; it is the slot the cabin's hanger blade rides in and a crosswise sheave looks wrong.
   **PASS:** an unlinked tower has **no station rail at all**. Every authored rail element is gone from the
   head shape this round — the ±8° flared mouths *and* the one straight plate per side that outlived them —
   because all of it is now drawn on the rope by the footing, so the rail only exists once the tower is
   linked. Check it at **10b**.
   **FAIL:** flares, a plate under the sheave, or any rail on an unlinked tower.
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

9. **Build a second tower** 20-40 blocks away with clear line of sight, same procedure. **Make it 30 or 32
   blocks**, not an arbitrary distance: haul rope comes four to a craft, so a span whose length is not a
   multiple of four loses a rope every time you re-link it, and step 11 re-links twice. At 33-35 or 37-39
   you run out mid-step and the failure looks like a bug in the refund. Keep both towers
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

10b. **The cable is visible — and this round there are TWO of it.** Look at the span you just strung.
     **PASS:** a thin rope-textured cable runs from each sheave to the midpoint of the span, **immediately,
     without reloading anything**. Each tower draws its own half, so the two halves meet in the middle and
     there is no z-fighting seam.
     **PASS — the return strand, new this round.** A second identical strand runs the whole span **directly
     above** the first, about a block and a third up. Sight along the span from one tower: the two are
     exactly one over the other with no sideways offset at all, the whole way. **FAIL:** there is only one
     strand (the loop is not drawn), or the upper one wanders sideways from the lower (it is not the same
     curve), or the two touch (the separation is not the wheel's diameter). Two ropes is the *whole line's*
     rope, not two lines: there is still one cabin and it hangs on the **lower** one.
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
     **PASS — the station rail came with it, new this round.** A pair of thin metal bars now runs out of each
     tower **under and either side of the cable**, about four blocks along the span before stopping — the same
     curve, from the same call, so on a straight span they are two straight bars parallel to the rope. They
     start at the tower centre, under the sheave, so the two runs meet there with no gap and no step. On a
     **short** span (step 23) they are shorter or absent, which is correct: the run is the same window the
     bend uses, and a span under about two blocks has none. **FAIL:** the bars run straight across the
     crossarm on a cardinal instead of out along the rope, or a straight plate sits under the sheave that
     does not turn with them — that is the authored fixture back.

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

10e. **The return strand at a plain tower, and at a plain END tower.** Walk to the tower in the *middle* of a
     three-tower line (build step 16 first if you have not) and look at the top of its pylon head.
     **PASS:** the upper strand runs **over the saddle** on top of the head's mast, touching it, and carries
     straight on to the next tower. It sits above the crossarm with daylight under it, not inside it.
     **FAIL:** the strand runs *through* the crossarm or the braces, or the saddle stands a visible distance
     off it.
     Now look at an **end** tower of a line whose ends are **plain towers** (no station yet — if both your
     ends are already stations, break one back to a plain footing or build a fresh two-tower line for this).
     **PASS:** the two strands **converge onto the sheave** over the last four blocks, so the rope reads as
     doubled back on itself and there is no cut end hanging in the air. The saddle above it stands empty.
     **That is correct and is not a bug to file** — the loop has nothing to turn round at a plain tower,
     because the thing that turns it is a bullwheel and a sheave throat is far too narrow. Build a station
     there and it becomes a proper loop (step 11c). **FAIL:** the upper strand simply stops in mid air over
     the sheave, or the two flicker against each other where they meet.

11. **Hang the cabin — but build the line's two STATIONS first, or nothing after this step works.**
    A line refuses a cabin outright until one of its towers is a **finished tension station**, and a cabin
    will not depart until a **drive station** on that line is being turned. A station is not an extra block
    beside the line: it is one of the line's own towers, built on a station footing instead of a plain one,
    with one leg of machinery instead of posts. Both are explained in full at **27a** and **27c**.
    **You built two plain towers in steps 7 and 9. Rebuild them as stations now**, or build the stations
    from the start next time:
    - break tower 1's **footing** (that cuts the span and refunds its rope). **The plain pylon footing you
      just picked up is the ingredient for the station footing** — that is why step 2 did not craft these
      two and why its bill buys exactly two `pylonbase`. Craft, from what you are now holding:
      - **Drive station footing ×1** — metal plate, **pylon footing**: **two cells side by side, a 2×1
        pair**, not a three-wide row. It is the one recipe in the mod that is not 1×3, 3×1, 3×2 or 3×3, and
        it is that shape because one plate cannot sit in the middle of a symmetric row and
        `bit/footing/bit` is the tension station's own grid.
      - **Tension station footing ×1** — **8 bits**, **pylon footing**, **8 bits** in a 1×3 *row*. Craft
        this one when you break tower 2's footing, below.
      **PASS:** both appear under real names, and between them and step 2's thirteen you have now seen all
      fifteen recipes. **FAIL:** either grid does not craft, or a station footing turns out to want
      something step 2's bill did not buy.
      Place the **drive station footing** in tower 1's spot facing the same way, and re-link;
    - build its crossarm: three braces on the plain side, a **bullwheel** in the middle instead of the pylon
      head, two **lay shafts** running out to the machine leg, a **drive head** on the crossarm end, three
      **drive shafts** below it and a **drive housing** on the ground;
    - do the same at tower 2: break its footing, craft the **tension station footing** out of the pylon
      footing that comes back, place it, re-link, then a **tension head**, three **tension guides** and a
      **tension weight** on the ground;
    - right-click each footing as you go — the overlay lights every cell that is still missing, in the
      wanted block's own colour, and that is faster than counting;
    - then run a mill into the drive housing, **following 27c**.
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
    **Every line you build from here on wants one of each**, because a drive station drives the line it is
    a tower OF and no other. That includes the corner lines in 12b, the doubling-back line in 18b, the
    three-tower line in 16, the long one in 25 and the uphill one in 27d. Build those lines' end towers as
    stations from the start; rebuilding a footing costs a re-link.
    **27a and 27b are the deliberate runs *without* them**, so do those two on a fresh pair of plain towers
    rather than tearing these out.
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
     **PASS — and it is closed round the LOWER strand.** The jaw is on the strand at sheave height; the
     return strand runs a clear block and a bit over the cabin's roof and never comes near it, parked or
     moving, at any bearing. There is 1.12 blocks of daylight between the top of the jaw and the underside
     of the upper strand, which is 450 times the play the jaw has on the rope it *is* clamped to, so this is
     a look-and-move-on check rather than a measurement. **FAIL:** the cabin hangs on the upper strand, or
     anything on it touches the upper one at any point of a ride.

11c. **The wrap at a terminal — the loop closing, and the picture this round exists for.** Stand off to one
     side of a **station at the END of a line** (one span only) and look at it side-on, along the crossarm,
     so you are looking at the plane the wheel turns in.
     **PASS:** the rope arrives on the **lower** strand, runs past the tower and out onto the bullwheel a
     cell beyond it, goes **half way round the wheel**, and leaves on the **upper** strand back down the
     line. In low, round, out high. The two strands are exactly the wheel's own diameter apart because the
     wheel is what sets them apart.
     **PASS:** the wheel is carried on **two brackets** running from its bearings on the sheave cheeks out
     and down to its hub, so nothing floats.
     **FAIL:** the rope makes a closed **hoop** round the wheel with one strand leaving (that is the old
     drawing, before the loop), or the upper strand leaves at a tangent that misses the top of the wheel, or
     the rope leaves the wheel and stops in mid air short of the tower.
     **PASS — with the cabin parked at that terminal**, nothing about the cabin touches the wheel or either
     rope but the jaw on its own strand. Watch a full revolution with the mill running.

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
     degrees and **0.004** at 45 with the footing down a leg — it was 0.033 before the path was bent through
     the tower this round. The 5-wide passage is what buys the bulk of that, so a visible hit at 30 degrees
     means the tower's posts are back at x = ±2.
     **PASS — right angle between two CARDINAL legs, and this is a KNOWN LIMIT, not a bug:** the cabin
     **passes through a post**. A tower facing is one of four cardinals and this corner's halfway line is a
     diagonal, so whichever way the footing points the outgoing leg IS the post axis: the cabin's *origin*
     travels down the post column at tower-local x = 3. It is a translation, and no yaw law fixes a
     translation - one was tried (the "angle-station" law) and made 45- and 60-degree corners worse, so it
     was reverted. Record it and move on. The handbook tells players the same thing.
     **PASS — the chat warning, new this round, and it fires on the SECOND span of the corner.** Linking the
     span that makes a middle tower a corner prints one line naming the turn in degrees. On the gentle bend
     with the footing turned off the halfway line it names the facing to use instead; on the right angle it
     says no facing carries a corner that sharp. **It is a warning and never a refusal** - the link is paid
     for and made either way. **FAIL:** the link is refused, or nothing is printed on a right-angle corner.
     **PASS — the right angle that IS clean, and it is worth building once.** Make the same 90 degrees out of
     two **diagonal** legs — in from the **south-west**, out to the **south-east** — and face the middle
     footing **east**. Those two headings are 45 and 135 degrees, so the turn really is 90 and the halfway
     line is due east, a cardinal the tower can actually face. No warning is printed, and the cabin comes
     through without touching a post. `KNOWN-ISSUES.md` recorded this case as impossible for two rounds; it
     is not.
     **Check the turn the chat line names before you record anything.** An earlier version of this step said
     *"in from the south-west, out to the north-east"*, which is the **same heading twice** — a straight line,
     turn 0 degrees. Every PASS above is then satisfied vacuously (no warning, because there is no corner;
     no post touched, because a 4.861-block yawed footprint fits a 5.000 passage at any yaw), and the tester
     records a PASS for a case that was never built.
     **Also expected at a right angle with the footing down a leg, cosmetic:** the rope on the *incoming*
     side runs along the crossarm axis and is buried in brace blocks before it clears the tower. Known; do
     not report it. The bend fixed the outgoing side and cannot fix this one — the rope arrives from twenty
     blocks out and a curve confined to the last four cannot change where it comes from. At the clean corner
     above (both legs diagonal, tower facing the halfway line) the rope barely leaves the sheave's own cell.
     **FAIL:** anything at a **gentle** corner - a cabin eating a post at 15-30 degrees is a real regression
     and means the passage width or `SpanMath.TowerClearance` moved.
     **This step is about a corner the cabin RIDES THROUGH, and what to watch is HOW it turns, not whether.**
     Since the path itself bends this round, a passing cabin is on the curve's own tangent: it sweeps
     continuously from the incoming leg, through the corner's halfway line *at* the tower, and on to the
     outgoing leg, over the four blocks either side. So at a tower facing that halfway line it does come
     through square, and that is the curve rather than step 13's square-up, which applies only where the
     cabin stands still.
     **FAIL:** the cabin **holds one fixed heading** across the tower and then steps to the next in one tick -
     a crab-walk that drags its tail through the post on the outside of the bend. That is the reverted
     angle-station law, and it looks nothing like a continuous sweep.
     **PASS — the loop does not scissor at the corner, new this round, and the view is from ABOVE.** Fly up
     over the corner tower and look straight down. The two strands are **one line in plan**: the upper one is
     exactly on top of the lower one all the way through the bend, so you should not be able to tell there
     are two of them from directly overhead. **FAIL:** they separate into two curves through the corner, one
     bowing wider than the other, or they cross. Either means the return strand is being bent on its own
     curve rather than being the going strand plus a height.

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

21b. **Clearance reaches ABOVE the rope too, new this round, and this is a behaviour CHANGE.** The return
     strand runs 1.33 blocks over the going one, so one row of the clearance check now sits above the rope
     line rather than all four below it — rays per span went 12 to 15.
     Build a stone overhang across the middle of a clear span so that its **underside is one block above the
     rope line** — well clear of the cabin, in the upper strand's way. **PASS:** the two towers no longer
     appear in each other's picker, and if you build the overhang after the link, the cabin holds at the
     tower before it exactly as a low obstruction makes it.
     **PASS — and one row, not two.** Raise the same overhang so its underside is **two** blocks above the
     rope line and the link is offered again. **FAIL:** it is still refused — a second row of rays is being
     cast over nothing and legal spans are being turned down for it.
     **This is the one thing in this round a player can notice as a loss:** a span over ground that rises
     to within a block above the rope line is refused now where it would have been allowed before. It is
     deliberate — without it a link that passes puts the return strand through a hillside — and **existing
     spans are never re-checked**, so nothing already built comes apart.

22. **Link while riding** (multiplayer — it needs a rider and a linker at once). With a rider seated on
    line A–B, have a second player link a new tower C to
    A. **PASS:** the link is **refused** with *"line in use"* — the same rule unlinking already had, because
    a merge re-bases the cabin and re-basing parks it at an end of the new chain, which is an arbitrary
    teleport of whoever is sitting in it. **FAIL:** the link succeeds and the rider moves.
    Get out and link again: **PASS:** it links, and the empty cabin re-bases onto an end tower of A/B/C.

23. **Short spans.** Link two towers only ~6 blocks apart. **PASS:** it links (the clearance check trims
    4 blocks off each end for the towers' own structures, and never trims more than half). Known
    consequence: an obstruction inside those trimmed end zones is not detected.
    **PASS — both strands are still drawn on a span too short to have a bend window at all**, and they are
    still the full block and a third apart in the middle of it. The station rail is shorter or absent there
    (step 10b) because the rail is the window; the rope is not. **FAIL:** the upper strand is missing on a
    short span, or the two are closer together on a short span than on a long one.

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
    **two angled gears**, and both station kits from step 2's craft list. **Every windmill now needs the two
    gears and a vertical axle column** — that is the price of fixing the intake to a cell, and the column
    leans on the drive leg rather than on a wall you build. Creative and `/giveblock` are fine.
    **The two rotors take different sails**, and this is the one that wastes an afternoon: `sail` for the
    wooden rotor, **`sail-large-oak`** for the metal one, 4 per length either way
    (`windmillrotor.json`'s `sailStack`). Offer the wrong sail and vanilla's `OnInteract` returns without a
    message — no toast, no chat line, nothing — which reads exactly like a broken block.

    **27a — the tensioner is a build requirement, and it is STRUCTURAL.** This wants a line with no
    tension station on it, so build a **fresh two-tower pair of plain towers** well away from the one you
    have been riding.
    On that finished plain two-tower line, hold the **ropeway cabin** and right-click an end footing.
    **PASS:** it refuses — *"This line has no tension station to keep the rope taut. Build one of its towers
    as a tension station and finish it first."* — and the cabin item is **not** consumed.
    Look at a footing: **PASS:** the panel says the line has no tension station.
    **PASS — proximity is dead, and this is the check that proves it.** Place a bare **tension weight** on
    the ground beside the tower, and then another one twenty blocks out in a field. Both place without
    complaint (there is no placement rule left) and **neither changes anything**: the panel still says the
    line has no tension station and the cabin still refuses. **FAIL:** the cabin goes on — that is the old
    eight-block rule still live.
    Now rebuild one of the two towers as a tension station: break its **footing**, place a **tension station
    footing** facing the same way, re-link, and build the leg — tension weight on the ground, three tension
    guides, tension head on the crossarm end, two lay shafts, and the bullwheel in the middle.
    **PASS — build it HALF way first and read the panel.** With the leg part built the footing says the
    tower is not complete and lights the missing cells, and the cabin still refuses. **That is new**: a
    half-built tensioner used to be indistinguishable from a finished one, because a block was either in
    range or not.
    Finish it. **PASS:** the panel goes green, stops mentioning the tensioner, and the cabin goes on.
    **PASS:** the weight is a mass **hanging low in its guide**, and the rails and rope carry on above it
    through the three guide cells to the head sheave, with the rope leaving the sheave westward as the tie
    rod to the bullwheel. It **never moves**, however long you run the line. **FAIL:** the mass slides up
    and down — that is the deleted gauge; or the rails stop at the top of the weight's own block — that is
    the guide cells missing.
    **PASS:** break any cell of the finished station with the cabin already hanging. The cabin keeps
    working; the footing panel says the tensioner is missing. That leak is deliberate — it is a build check,
    not a runtime state.

    **27b — no drive is a cabin that waits, not an error.** Use 27a's fresh pair, once its tension station
    is finished and it has taken the cabin — it is the line whose other tower is still plain. With no drive
    station anywhere on that line, board and
    sit. **PASS:** after the three-second pause nothing happens: no red toast, no chat line, no refusal.
    The cabin simply does not move. **FAIL:** any message about power, a store, a tension weight not being
    wound, or a trip being too dear — those states are deleted and any of them means old code is live.
    **PASS:** the footing panel says *"Nothing on this line is turning, so the cabin will not move"* and
    tells you one of its towers has to be a finished **drive station**. **FAIL:** it mentions eight blocks,
    or tells you to read a nearby housing's own panel to find out which line it decided to drive — both are
    the deleted rule.
    **PASS:** get out. You can, because it is not moving.
    **PASS — calling refuses out loud.** Still with no drive on the line, stand at a tower with an empty
    hand and **call the cabin** (plain right-click). You get one **red error toast** — the same channel as
    step 5's, not a chat line — telling you nothing on this
    line is turning and to build a drive station, and the cabin does not take the call. **FAIL:** the
    click is silent and the cabin latches onto a trip it can never make — that was the old behaviour, and
    it looks exactly like a broken call rather than an unpowered line. Build the drive (27c) and call it
    again: **PASS:** it comes.

    **27c — build the drive station, and the ladder.**
    **PASS — membership, not proximity, and this is the check that proves it.** Place a bare **drive
    housing** on the ground beside a plain tower and run a mill into it. It places (there is no rule left),
    the housing's own panel reads out what it is turning at, and **the line does not move**: the footing
    panel still says nothing on this line is turning. **FAIL:** the cabin moves — that is the old
    eight-block rule still live.
    Now build the drive station properly. Break the tower's **footing**, place a **drive station footing**
    facing the same way, re-link, and build the fifteen cells over it: four posts of your own material on the
    plain side, three braces, the **bullwheel** in the middle, then the machine leg — **drive housing** on the
    ground, three **drive shafts** above it, the **drive head** on the crossarm end, two **lay shafts**
    running in to the wheel. Right-click the footing as you go and follow the overlay.
    **PASS:** the panel counts **fifteen** and goes green when the last one lands — the footing you are
    clicking is the sixteenth block of the tower and is never in that count, so do not wait for a sixteen
    that never comes. The tower still reads as an ordinary tower in every other way — same footprint, same
    archway, same spans.
    **PASS — look along the crossarm from the side.** The shaft runs **unbroken at one height** from the
    gearbox on the leg, through both lay shafts, into the bullwheel's hub between its two bearings. **FAIL:**
    the shaft is three segments that stop short of each other, or the wheel sits over the crossarm joined to
    nothing.
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
    **The intake does not climb any more, so the mill comes down to it.** From the hub: an **angled gear**,
    then a column of **`woodenaxle-ud`** down the outside of the drive leg, a second **angled gear** at the
    bottom, and one horizontal **wooden axle** into any of the housing's four sides. For three sails that is
    about seven vanilla blocks; for a maxed five, nine.
    **PASS — and this is the one line worth the whole step: the column leans on the drive leg.** Run the
    `woodenaxle-ud` column hard against the three **drive shaft** cells and place the bottom angled gear.
    It places. **FAIL:** *"axlemusthavesupport"* — that means `driveshaft` has lost its `sidesolid: all
    true`, which is the one attribute that makes a windmill drive buildable without standing a wall up
    beside the tower first.
    **PASS:** the drive leg is still **see-through** — you can see daylight through the lattice — and casts
    no shadow. Solid sides are for the axle rule only; `sideopaque` and `lightAbsorption` stay off.
    **PASS:** nothing is built **on the crossarm** and nothing climbs past the crossarm. The descent runs
    down the outside of a leg that is already there.
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
    **PASS:** build the drive station at the **far** end of the line instead and it works exactly the same.
    Which tower carries the drive does not matter; being a tower of the line is the whole of the rule.
    **PASS:** break any cell of the drive station — a post, a brace, a drive shaft. The drive **stops** if
    you broke the leg (the intake is no longer reachable) and keeps running if you broke a post, and the
    footing's overlay says which cell is missing either way. **FAIL:** the drive keeps running with the
    housing removed, or a cell you replaced correctly leaves the tower reading incomplete.

    **27c-metal — the maxed metal rotor still needs a column, and that is a REDUCTION, not a regression.**
    Only worth running once, and only if you want the 3.0 rung.
    Ten sails need **eleven** clear blocks every way in the disc, which on flat ground stands the hub
    eleven blocks above the footing block. It is the same build as the wooden rotor's in 27c, only taller:
    an **angled gear** at the hub, **eleven** `woodenaxle-ud` down the outside of the drive leg, a second
    **angled gear** at the bottom, and one horizontal axle into the housing. About fourteen vanilla blocks.
    **PASS:** the column leans on the three **drive shaft** cells for its bottom three, and wants a wall for
    the rest — the leg is four cells tall and the hub is eleven up, so the top of the column stands beside
    open air. **That part has not changed and is vanilla's rule, not ours.**
    **Build order decides whether the support refusal fires at all, so do not report its absence as a bug.**
    `BlockAngledGears.TryPlaceBlock` walks `BlockFacing.ALLFACES` — horizontals before up and down — and
    applies the support check only to the **first** connectable neighbour it finds. A bottom gear placed
    beside an already-built housing finds the housing on a horizontal face and never looks up at the axle,
    so the check is skipped. That is a build-order accident, not a licence: `BlockAxle.OnNeighbourBlockChange`
    breaks an unattached axle that loses its support.
    **PASS:** with the column in, the cabin runs at about **3.0 blocks a second**.
    **Do not report the gears here as a regression.** Every windmill needs the two gears now, this one
    included. What this rung buys back is that there is **no placement envelope at all**: the old
    eight-block sphere could not be met from any position at eleven blocks up, so this drive always
    descended, and it was the one build the sphere never helped.

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
    **PASS:** two or three level wooden axles reach from the wheel's hub to the drive housing at the foot
    of the station's leg. Nothing climbs, no gears, no column — this is the one drive the fixed intake
    costs nothing at all, because a water wheel's hub sits at most one block above the water anyway.
    **PASS:** the cabin tops out around **1.8 blocks a second** however fast the water runs, and it holds
    that speed through weather that stops every windmill on the map. That is the trade.

    **27c-wheel — the bullwheel belongs to a STATION, and it TURNS.** It is the centre cell of both
    stations' crossarms, and a plain tower no longer accepts one.
    **PASS:** put a **bullwheel** on a plain `pylonbase` tower's crossarm in place of the pylon head. The
    tower now reads **incomplete** and the overlay marks that cell red. **FAIL:** it still reads complete —
    the centre-cell wildcard has not been narrowed, and a wheel joined to nothing can turn on a tower that
    drives nothing. Put the pylon head back.
    **PASS:** the reverse, on a station: put a **pylon head** in a station's centre cell and it reads
    incomplete too. Put the bullwheel back.
    **PASS:** the cabin still passes through a station without catching: the throat and the station rails
    are the sheave's, unchanged.
    **PASS:** its **spoked wheel stands above the crossarm** and is obviously a wheel from thirty blocks
    away — you can tell a station from a plain tower at a glance.
    **PASS — the wheel is JOINED to something, and this is the fixture check.** Stand off to one side and
    look along the wheel's axle line. Two **bearing standards** rise out of the sheave cheeks either side of
    the rim, and the **hub axle** runs out through them along the crossarm into the lay shaft next door.
    **FAIL:** the wheel balances on the small square boss with a visible gap under it and nothing running
    out of its hub — that is the fixture missing.
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
    has been left on it and the drive is back four blocks up. The hub axle is geometry, not a connector.
    **PASS — the wheel now stands in one of THREE places, new this round, and which one is decided by the
    rope over it rather than by the block.** Check all three; they are cheap to build and the wrong one is
    obvious.
    - **A terminal** (the station carries **one** span): the wheel stands **one cell out past the tower** on
      the side nothing runs, dropped so its groove is on the rope, with the rope half way round it —
      step 11c. This is the one that reads from across a valley.
    - **A station the line runs THROUGH** (**two** spans — link a third tower to a station and you have one):
      there is no side that nothing runs on, so no wrap is drawn, and the wheel **rises about seven eighths
      of a block** and sits with its groove on top of the **return** strand, pressing it down. It stands
      higher than the lay shaft it is bolted to, on the same two brackets, and it keeps turning.
      **This is correct and is not a bug to file.** **FAIL:** the wheel stays level with the shaft and the
      upper rope runs **through the middle of the rim** — that is the whole reason this pose exists.
    - **A station with no spans at all** (unlink both): the wheel drops back to the middle of its own cell,
      level with the shaft, with no brackets and no rope. **FAIL:** it stays lifted, or it stays out to one
      side, on a tower with nothing strung to it.
    Now **cut and re-make a span** on a terminal station while watching the wheel. **PASS:** it moves between
    poses within about half a second and the drawn rope follows on the same re-tesselation. A brief
    disagreement on the one tick a terminal stops being one is expected; a permanent one is not.

    **27d — climbing costs.** Build (or ride) a line with one clearly uphill span and one level one.
    **PASS:** the cabin visibly **slows on the way up** and picks up again on the level or the way down.
    **PASS:** it does **not** stop on the climb with a mill that hauls it on the flat. **FAIL:** it stalls
    halfway up a hill — the climb term is meant to be visible, never fatal.

    **27e — pooling.** Build a **second drive station** as another tower of the same line, on its own axle
    network and its own mill. **PASS:** the cabin gets **faster** — the drives add up — and the footing
    panel's line figure goes up with it. **PASS:** it works whichever towers you pick; a line may carry more
    than one drive station and no tower is special.
    **Then the case that is not pooling:** run **one** axle line along the ropeway and drive **three** drive
    stations' housings off that same network. **PASS:** the line figure does **not** climb with the number
    of stations — one network is one drive however many housings touch it — and the cabin is if anything
    slower, because every hookup declares the full haul load. **FAIL:** each extra station adds another
    drive's worth of speed. That is free speed for adding load, and it is the one thing a load model must
    never do.
    **And the case that used to give speed away for free.** Build a **second, separate line** whose nearest
    tower sits a few blocks from a tower of the first — two short lines side by side, close enough that a
    free-standing housing would have been in range of both. Hang a cabin on each, and build the drive station
    on **one** of them.
    **PASS:** only that line runs, and the other reads *"Nothing on this line is turning"* — whichever
    line's tower happens to be nearer the mill, because the mill is not what decides.
    **FAIL:** both cabins move; that is one mill hauling two cabins while only one line's load was ever
    charged for it.
    **Now do it deliberately, because until this round it worked.** Build the second line's **drive station**
    at the tightest sharing geometry there is: put its footing **three blocks along the first station's
    crossarm and three along its passage** — 4.243 blocks away, facing **east** or **west** where the first
    faces north — so the two want the *same* machine-leg column. Two perpendicular lines meeting at a
    junction, which is a thing you would build on purpose.
    **PASS:** you cannot finish both. Build the leg once and **one** of the two stations goes green while the
    other's panel still counts a missing cell and reddens the head at the top of that leg — the two want a
    `drivehead` facing different ways, and one block cannot be both.
    **FAIL:** both footings read complete off one leg, and both cabins run at full speed off one mill. That
    is the bug this step exists for, and it is *free speed plus unpaid load* — the one thing a load model
    must never do. Try the same at **six blocks on the opposite facing**, and try both with two **tension**
    stations.
    **There is no longer a spacing rule to observe.** An earlier version of this step told you to leave eight
    blocks; that rule stood in for the fix above and is retired. Towers closer than seven blocks can still
    want the same cell — a station's leg landing in a plain neighbour's post cells leaves one of them
    incomplete and therefore un-clickable — but that is visible on the panel and in the overlay, and it costs
    a rebuild rather than silently stealing power. See "One machine leg, one station" in
    `docs/KNOWN-ISSUES.md`.
    **What you no longer have to measure is which mill is nearest.** The old rule turned on that and needed
    two blocks of daylight between the distances before the result could be read at all. Stand the mill
    wherever you like.

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
    turning, and the line's drive stations write `HaulResistance` until something does. Do not run this check on such a
    line and do not file it; `KNOWN-ISSUES.md` records the trade. It clears itself the moment anything turns.
