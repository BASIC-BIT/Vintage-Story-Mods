# Ropeway v0.1 — known issues

State: build green, 218 ropeway tests passing — 111 `[Fact]` plus 107 `[InlineData]` across the five test
files in `mods-dll/ropeway.Tests`, which is where to re-derive this number rather than trusting the line.
(It read 136 for two rounds after the count moved, 147 for two more, 168 for two more again, 172 for one,
174 for one, 180 for one, 187 for one, 199 for two and 211 for one. It briefly read 195 in one round's
working tree, because two agents landed tests in parallel and each counted only its own.) Everything in the
tables below was found by reading code, not by playing — none of *it* has been observed in game.

## The shaft — a counterweighted lift, phase 1 (2026-08-10)

Three new blocktypes, no new block class, no new block-entity class, no new entity, one new renderer, and
**eleven placed blocks against a ropeway pair's thirty**. Design and adversarial round:
`docs/agentic/ingest/cablecar/ELEVATOR-DESIGN-2.md` and `ELEVATOR-CHALLENGE-2.md`. Renders:
`docs/agentic/ingest/cablecar/renders/elevator/`, built from the **shipped** shapes rather than from proposal
geometry.

**The ropeway did not move.** Every change to shared code is behind one flag or one optional parameter that
every shipped caller leaves at its default: `BEPylonBase.ShaftRole` (null on every ropeway footing),
`RopewayLine.IsShaft`/`ShaftFacing` (false/null unless `GetOrBuild` walks a shaft), `IsSpanClear`'s
`shaftAxis` (null at every ropeway call site), `BEBullwheel.WrapOffset`'s `shaftAxis` (ditto),
`BEBullwheel.RimShape` and `BullwheelRenderer.CullRadius` becoming defaults rather than the only values.
`AnchorOf`, `SheaveHeight`, `hangDrop`, `PassablePitchTan`, `TrimForTowers`, `ClearanceRows`, `ReturnLift`,
`WrapRadius`, `RimPivotY`, `HaulResistance`, `ClimbLoad` and every shipped shape are untouched.

### The four things a shaft had to be told

1. **Verticality is structural, not arithmetic.** Nothing anywhere asks `|dir.Y| > 0.999`.
   `SpanMath.ShaftLinkFits` refuses at link time every span two shaft stations could otherwise carry — out of
   the column, two sheaves, no sheave, the sheave underneath, or **the two footings facing different ways** —
   and `ScanCandidates` applies the same predicate so no offered row can fail on click. A ropeway tower and a
   shaft station will not link to each other at all.
   **The predicate is per-SPAN and on its own does not give the per-LINE invariant those branches want**, which
   this section claimed for one round. `MaxSpansPerTower` is 2, so "exactly one head" was false at line scale:
   `foot@0 → head@10` then `head@10 → foot@5` is a **fold** — `Cumulative [0, 10, 15]`, `DirectionAt` flipping
   to `(0,−1,0)` at t = 10, and `ShaftRenderer`'s counterweight mirror (about `Anchors[0]` and `Anchors[^1]`, by
   then both *feet*) drawing the mass at world **Y = −0.5** — and `foot@0 → head@10` plus `foot@0 → head@20`
   puts **two sheaves on one line**, each with its own `ShaftRenderer` drawing the whole rope. Both passed every
   clause of the predicate and every `ScanCandidates` filter, so the picker offered the row. What closes them is
   one rule at both callers: **on a shaft, refuse when either footing already carries a span**, which is exactly
   what "no intermediate floors" means. Predicate plus that rule is what makes every `line.IsShaft` branch safe,
   because the mixed line, the fold and the second sheave are all unbuildable.
2. **The heading is supplied, not derived.** `DirectionAt` on a vertical leg is `(0, ±1, 0)` and
   `Math.Atan2(0.0, 0.0)` is **0.0** — not NaN, a silent permanent south. The car's yaw is the **head's**
   `PassageFacing`, held constant everywhere, through the same `SquareTo` a parked ropeway cabin uses. There is
   no second *heading* to reconcile and nothing snaps at either end — but **the foot's facing is not inert**,
   and `shaftfoot.json` said it was. `BEPylonBase.Init` passes `RotationFor(side)` to `InitForUse`, which
   rotates every offset including the `tensionguide` cell `shaftfoot` requires, so the guide is dug along the
   *foot's* facing while the counterweight's lane follows the *head's*. `ShaftLinkFits` compares the two
   facings; without that, a foot facing east under a head facing north validated and the weight descended into
   undug rock.
   `RopewayLine.GetOrBuild` also reads **`IsShaft` and `ShaftFacing` off the head alone**. Off *any* shaft
   footing there was a window between the foot's `Initialize` and the head's where the line was
   `IsShaft = true, ShaftFacing = null` — not covered by `Truncated` at
   `travelled == MinTravel == MaxTravel == 0` — in which `Place` ran `SquareTo(null, 0f)` and got **due south**,
   `SegmentClear` silently took the ropeway frame, and `ShaftRenderer` drew no rope. A shaft has exactly one
   head, so keying both off it costs nothing.
3. **The corridor needs the shaft's own frame.** `IsSpanClear`'s near-vertical fallback hard-codes
   `right = (1,0,0)`, so its corridor is a fixed box in *world* axes — right for a car lying along Z and
   transposed for one lying along X, which would sweep two blocks of the car's nose and tail through
   uncertified rock. And at exactly vertical the derived rows are **half-integers** against a plan coordinate
   that is a block centre, so all four rays would run down block-boundary planes and the column the car's tail
   occupies would never be tested at any level of the shaft. `SpanMath.OnColumnCentres` re-lays the ladder on
   whole numbers — five rays, not four — and it is applied **only** where a shaft axis is supplied. The trim
   goes to zero on a shaft for the reason `TowerClearance` exists in the first place: it is there because a
   tower's posts are player-chosen blocks the filter cannot tell from terrain, and a shaft station has no
   player-chosen cells at all.
   **And the corridor is the CAR's volume, not the rope's segment** — the fourth thing, added after the first
   three shipped. On a level span the ladder *is* the car's height, because `up` is vertical. On a shaft `up` is
   **horizontal** (its Y is identically zero, since `dir` is `(0, ±1, 0)`), so every offset is a plan offset and
   every ray spans exactly `[anchorFoot, anchorHead]` in Y. The car's body hangs `rope−3.5 … rope−1.0`, so its
   swept volume is `[anchorFoot−3.5, anchorHead−1.0]` and **the bottom 3.5 blocks of the hoistway were never
   tested** — `footY+1.0 … footY+4.5`, over the whole 3 × 5 footprint, which is exactly where the car parks and
   the rider sits. Sink a shaft from the top, stop at the foot footing's own cell, and the link succeeded, the
   car parked in rock, and `RopewayCabinSeat.Landing` found no landing because every neighbour column was solid
   wall. `IsSpanClear` now drops the lower end of a shaft cast by `hangDrop + CabinHalfHeight` before laying its
   rays; the top needs nothing, because the car's roof is 1.0 *below* its rope point. The consequence the
   handbook now states is the real clear volume: **3 wide × 5 long, from the block above the foot footing up to
   and including the sheave's own row four blocks above the top landing** — a top station roofed at three blocks
   was always refused, and nothing said so.
4. **The sheave has to face the footing, and the multiblock cannot make it.** `shafthead.json` names its
   sheave cell as the wildcard `ropeway:shaftsheave-*` and `MultiblockStructure.InCompleteBlockCount` matches
   with `WildcardUtil.Match`, so **any of the four side variants completed the structure**. `shaftsheave` is
   the least symmetric block in the mod — headframe columns, a beam reaching the hub, a chain case on one face,
   and an authored wrap arc that turns in *one* vertical plane — and it is a `HorizontalOrientable`, so it takes
   the **player's** facing at placement. `BullwheelRenderer.YawFor` then reads the *sheave's* side while
   `BEBullwheel.WrapOffset` and `ShaftRenderer` read the *footing's*: three of four placements put the wheel,
   the headframe and the rope in different orientations, the overlay said complete, and the lift ran.
   `BEPylonBase.OwnTheHeadCell` — which exists for exactly this and whose own comment states the test for
   inclusion ("NEW and untracked, so no saved world can hold a wrongly-faced one, and both are visibly
   asymmetric") — now narrows **three** families instead of two.

### Why the rope cannot be chunk mesh, and why the loop cannot close

A haul loop's geometry never changes, which is what makes `BEPylonBase.OnTesselation` affordable: the cabin
is a **slider** on a rope whose two ends are wheels. Stand the machine on end and the car becomes the rope's
**end** — the going strand is `H − travelled` long and the return strand `travelled`, both changing every
tick — and a rope that simply ran past the car would run down through the roof and out through the
passengers, because on a vertical leg the rope is directly above the roof's centre rather than out in open air
beside it. `Lift` is the same fact from the other side: it offsets in **+Y**, which on a vertical leg is
*along* the rope, so the two strands would be collinear and z-fight for the whole shaft. So `OnTesselation`
early-returns on a shaft and `ShaftRenderer` draws the two strands and the counterweight per frame off the
cabin's synced `Pos.Y`. The **wrap** is authored on `shaftsheave.json` instead, because it is the one part of
the rope that never moves.

**The loop cannot close at the bottom, and that is a proof rather than a preference.** A 180° wheel spanning
the lane has radius `r = lane/2` with its centre at the bottom anchor, so its rim reaches `r` *below* that
anchor over a plan range containing the parked car's roof. It clears the roof only if its centre is past the
car's nose (`r ≥ 2.0`) and fits under the strand only if `r ≤ 1.0`. Both cannot hold, at any radius. So the
rope is **open** — car, head sheave, counterweight — which is what a 1:1 traction elevator actually is, and
the counterweight is the second body on the second strand rather than a wheel.

### The top station has no floor, and the dismount is what answers it

The car parks with its floor level with the top landing and then descends through its **whole 5 × 3
footprint**, so that footprint has to be a hole in the landing. A rider stepping out there is over the hole.
Vanilla's own dismount teleport (`EntityRideableSeat.tryTeleportToFreeLocation`) probes exactly **two**
columns, one block either side of the mount, and one block from the axis is still inside a three-wide
hoistway — so both are air, no teleport happens, and `RopewayCabinSeat.DidUnmount` has just re-datumed
`PositionBeforeFalling` to the seat. The rider would fall the length of the shaft and be billed for all of it.

`RopewayCabinSeat` overrides that method — it is `protected virtual`, which is the sanctioned extension point
— and widens the search: `base` first, then, **only on a shaft**, Chebyshev rings out to
`ShaftExitReach` = 3 columns for a solid top face with room to stand, in a fixed order so the server and both
clients pick the same block. On a ropeway nothing here fires: the gate is a shaft flag and `base` has already
run. Vanilla's own elevator answers the same question by writing `meta-collider` blocks into the world at
every stop and taking them out again, which is a block write per stop on a machine that already knows where
its landing is.

### What is deliberately not cured, and what it costs

- **The head's own footing is in the car's path.** `AnchorOf` stays `CentreOf + SheaveHeight`, so the car's
  parked floor is `headY + 1.0` — the top landing's own face — and the footing sits in the middle of the
  opening the car descends through. It is a **half-block plinth**, so it is inside the car's body from
  `rope = anchor − 3.5` to `rope = anchor − 0.5`: **0.5 blocks of steel, over 3.0 blocks of travel, once each
  way, at the top station only.** Compare the crossarm defect below — 0.938 blocks over 1.6, at *every* steep
  tower, both ways, every trip. It is not curable without moving the anchor, and the real reason that is
  refused is not cost: `RopewayLine.FromTowers` is **pure**, and a per-block anchor offset needs the peer's
  *facing*, which needs the peer's block entity — a cross-chunk read on the tesselation thread, from
  `BEPylonBase.LocalLine`. The plinth is not free to thin either: `DropGhostPassengers` puts a relogging rider
  at `footingY + 0.5` and that face has to be there.
  `AGhostPassengerDroppedAtEitherShaftFootingLandsOnItsPlinth` pins both footings to it.
- **The counterweight's lane is not certified.** `IsSpanClear` sweeps the car's 5 × 3 columns and nothing
  else. A player who digs the car's hole and not the lane gets a counterweight visibly inside stone in a
  column nobody stands in — the same status the return strand has on a ropeway. What *is* enforced is the
  bottom of that column: `ropeway:tensionguide` is a required cell of `shaftfoot`, so the multiblock refuses
  to complete until the lane is at least started.
- **The counterweight stands proud of the top landing.** With the car at the bottom the weight's rope point
  is the head anchor, so the mass occupies `headY + 1.0 … headY + 3.5` — entirely above the landing floor, in
  the lane column, beside where the player waits. Correct for a hoistway, and it has no collision.
- **An empty shaft shows no rope.** The rope is *open*, so it terminates on a car that is not there yet;
  `ShaftRenderer` draws nothing until the cabin is hung. Different from a ropeway, where the loop is chunk
  mesh and exists without one. The handbook says so.
- **The jaw is a clamp authored on a horizontal rope.** A vertical strand enters it through the top plate.
  Cosmetic, and it is the only part of the cabin that still reads as built for the other machine.
- **The tower guide dialog is the ropeway's.** Sneak + right-click on a shaft footing opens the same
  seven-cell strip a tower gets. The handbook page *Sinking a Shaft* is the shaft's build order; branching the
  guide wants its own strip and its own body text and is not phase 1.
- **A shaft never warns about pitch.** `PitchTan` is `+∞` for a vertical span — deliberately, and documented
  there — so `WarnOnPitch` would have told the player their brand-new lift climbs at 90° against a ceiling of
  11, on the one machine in the mod that was given no crossarm precisely so it would not eat one. `TryLink`
  skips the warning on a shaft and nothing else about it changes.

### Two things the shaft gives back

- **`ropePerBlock` proves out as the per-blocktype knob it was written as.** A shaft prices its second strand
  at 0.5 in JSON, with no code, and that is the one lever free to move if a lift turns out to be too cheap a
  way to climb. It **is** cheap: a counterweighted shaft costs the network exactly what a *level* ropeway
  costs it (0.300), climbs one block of rise per block travelled, and is a third of the placed blocks. That
  is the balance decision, stated rather than presented as a maintenance economy.
- **`cargo` acquires a meaning.** On a ropeway it is "extra"; on a counterweighted lift it is the whole load,
  because the counterweight cancels everything else. Whoever lands cargo weight now has a case where the
  parameter is not a placeholder.

### What phase 1 is not

No intermediate floors — costed at one blocktype and **zero** lines of route code, because
`Bisect(null, null)` is null at a vertical interior tower, `MaxSpansPerTower` is already 2, and
`NextStop`/`PlanCall`/`TowerAt`/`SpanAheadOf` are written for N towers — but phase 1 should prove the
machine, not the product, and **`TryLink` and `ScanCandidates` refuse a second span on either shaft footing
until it lands.** That is the rule, restated: this line used to say `ShaftLinkFits` refuses a *foot-to-foot*
span, which is true and was irrelevant — a three-station shaft needs no foot-to-foot span, only two ordinary
foot-to-head ones, and it was buildable. Whatever lands intermediate floors takes the one-span clause out and
owns the fold and the second sheave. No doors, call
buttons or floor indicators. No cargo weight. No brake, gravity descent or overspeed governor — a second
motion authority in `ServerTick` is the one thing forty lines of comment in it warn against. No second car.
No mixed lines. Nothing touching `AnchorOf`, `SheaveHeight`, `hangDrop`, `TrimForTowers`, `ReturnLift`,
`WrapRadius`, `RimPivotY`, `PassablePitchTan`, `ClearanceRows`, `HaulResistance`, `ClimbLoad` or any shipped
shape.

**The bail-out is not the shaft's safety story, and it must not be presented as one.** `Jump` unmounts the
rider where they are with `DoTeleportOnUnmount = false` *because taking the drop is the price*; on a ropeway
that price is a hillside and in a shaft it is up to 48 blocks of pit. The honest position is
`RopewayPower`'s own: a becalmed cabin is **waiting**, not trapped — it resumes by itself, and in a shaft
that argument is *stronger* than on a ropeway, because the car always resumes to a station.

## A rider who steps out of a stalled cabin at height — FIXED (2026-08-10)

**It was live on every line the mod ships, it had nothing to do with the elevator question it was found
under, and it killed people.** Found while re-deriving `docs/agentic/ingest/cablecar/ELEVATOR-CHALLENGE.md`
§4; re-derived from the source before it was believed.

### The path, end to end

1. `RopewayCabinSeat.CanUnmount` read `if (!Moving || Bailing(...)) return true;` — one tap of sneak got the
   **ordinary** dismount whenever the cabin was not moving.
2. `Moving` is `Entity is EntityRopewayCabin { IsMoving: true }`, and `IsMoving` is false in far more states
   than "parked at a tower". `ServerTick` writes it false the moment the mechanical network stalls (the
   `speed <= 0` branch: *"Standing still is not stopping"*), on every `NotReady` tick, and on every `Hold` —
   a blocked span, a truncated chain, a re-based line. All of those leave the cabin **exactly where it is**,
   which is usually mid-span.
3. `EntitySeat.onControls` calls `TryUnmount()` on the sneak **press**, so the whole thing is one tap.
4. `DoTeleportOnUnmount` is true on that path (only `EntityRopewayCabin.Jump` clears it), so vanilla's
   `EntityRideableSeat.DidUnmount` runs `tryTeleportToFreeLocation` (`EntityRideableSeat.cs:239-259`). It
   checks **exactly two** candidate blocks — one to each side of the mount, at `Pos.Y - 0.1`, each needing
   `SideSolid[UP]` and a clear collision box. Out on a span both are air. Neither branch fires and the
   rider is simply left at the seat.
5. `RopewayCabinSeat.DidUnmount` then re-datums `PositionBeforeFalling` to that point — correctly, for the
   reason written there — so `EntityBehaviorHealth.OnFallToGround` bills the **entire** drop.

So the two-second sneak-**hold** that exists to make leaving a cabin at height a deliberate act with a known
price was silently converted into a **tap** by a lull in the wind. Nothing warned, nothing refused.

### The predicate it has now, and the two that were rejected

`CanUnmount` asks whether anything solid stands within `RopewayCabinSeat.FreeFall` = **3.5 blocks** under
the cabin's own origin, straight down one column.

- **3.5 is vanilla's, not ours.** `EntityBehaviorHealth.OnFallToGround:381-387` returns without damage while
  the fall is under `3.5 * fallDamageThreshold`. Ground within that is ground a rider steps onto for
  nothing, so a line running low over a hillside is not made annoying to get out of.
- **Measured from the cabin, not the rider**, which makes it conservative by the 1.25 blocks the rider sits
  below the cabin origin. The cabin is the datum because it is the position both sides agree on, and because
  `DropGhostPassengers` unmounts a rider on the very tick it parks the cabin at a tower — a rider's own `Pos`
  is a tick behind the seat it is pinned to.
- **A cabin at a tower passes**, and that is the case the rule was checked against first: the anchor is
  `SheaveHeight + 0.5` over the footing and the cabin hangs `hangDrop` under it, so the footing block is
  2.25 blocks under the cabin origin and the test is `TheCabinFitsThroughTheTower`'s frame exactly.
  `RopewayDismountTests` pins both halves off `SpanMath.SheaveHeight` and
  `EntityRopewayCabin.DefaultHangDrop`, so moving the tower moves the test.

**Rejected: `line.IsAtTower(Travelled, ArrivalTolerance)`,** which is the obvious predicate and cannot be
used here. `Travelled` lives in `Entity.Attributes`, and `Entity.ToBytes` writes that tree only
`if (!forClient)` while `FromBytes` reads it only `if (!isSync)` — **a client's `Travelled` is always 0**.
`CanUnmount` is answered on every machine, so a server-only predicate means the server and the rider's own
client disagree about whether they just got out, which is the failure `BailKey` exists to prevent. Blocks
are the one thing both sides hold identically. (The cabin itself *can* ask that question cheaply, and
should, for the arming half below.)

**Rejected: fixing it in `ServerTick`.** Nothing there is wrong: a stalled cabin standing still with
`departed` intact is the behaviour that lets a trip resume when the wind returns, and a second motion
authority in that method is the one thing forty lines of comment in it warn against.

### What else had to move for it

- **Every other client now answers `CanUnmount` yes.** A watching client reaches it through the `mountedOn`
  listener, answering an unmount the server has *already* made, exactly once — a client that refuses there
  keeps the rider drawn inside a cabin they have left for the rest of the session. It had nothing to refuse
  with that the server did not have. That was survivable while the answer was one synced bool; it is not now
  the answer is a block lookup, which a client at the edge of its loaded chunks can legitimately get
  differently. `RopewayCabinSeat.Answering` is the gate: the server, and the rider's own client.
- **A forced unseat is not a dismount.** `UnseatAll` used to clear its own way past the refusal by writing
  `IsMoving = false`, which no longer answers the question being asked. It now also calls
  `RopewayCabinSeat.ClearToLeave(this)`, which sets the bail-out clearance on each rider's own tree. Without
  it a tower blown out from under an occupied cabin leaves the rider seated: carried off by the re-base (the
  teleport the unseat exists to prevent), or still mounted when the no-survivor branch despawns the cabin
  under them, which is a softlock rather than a fall.
  **The clearance is in `UnseatAll` and not at its callers**, which is where it first landed. There are two —
  `RopewayLinkService.UnlinkAll` (the explosion / `SetBlock(0)` path) and `DropAndDie` (reached from
  `ServerTick`'s tower-vanished backstop) — and clearing only the first made *"the second can never hold a
  rider"* an argument about call order rather than something the code enforces. Every route to that backstop
  with a rider aboard does run through `UnlinkAll` first, so it was covered; one line at the chokepoint makes
  it true by construction and costs nothing.

### The arming line — CLOSED, and this section claimed otherwise for two rounds

`BailOut` used to arm the hold off `IsMoving`, and `HoldSneak` zeroes the accumulator whenever that is false —
so **the emergency exit was disarmed in exactly the state that refuses the ordinary one.** It is armed off
`RopewayCabinSeat.HeldIn` now (`Moving || !OverGround()`), which is the same question the refusal is raised
by, and `HoldSneak`'s `heldIn` parameter carries the rationale in its own doc comment. `EntityRopewayCabin`
line 932 and `RopewayCabinSeat.HeldIn` are the whole change.

**This paragraph is here as a tombstone, because the stale version of it was believed twice.**
`ELEVATOR-DESIGN-2.md` §10 made "ship the arming line first" a prerequisite for the elevator on the strength
of *this heading* rather than of the code, and `ELEVATOR-CHALLENGE-2.md` C5 caught it: the line had landed
about eighty minutes earlier and the commit that landed it did not update this file. A doc that says a defect
is open is a defect. Re-derive from `src/`, not from here.

What is genuinely still true is the *smaller* half of `docs/agentic/ingest/cablecar/HANDOFF-stalled-dismount.md`:
QA 13a's crouch-board step wants re-walking, because the edge trigger (`SneakReleased`) is what stops a rider
who boarded crouching from being ejected two seconds later having pressed nothing.

Two consequences of the fix that are deliberate and not bugs:

- **Water is not ground.** The probe reads `BlockLayersAccess.MostSolid`, so a cabin stalled over a lake
  refuses the step out even though the landing would be survivable. Refusing costs a wait; the alternative
  is a rule that has to be right about depth.
- **An unloaded chunk is not ground.** `GetBlockOrNull` comes back null and the refusal stands. A rider
  cannot see a landing there either.

## The cabin eats its own crossarm past 11.3 degrees — MEASURED, GUARDED, NOT CURED (2026-08-10)

Found in `docs/agentic/ingest/cablecar/ELEVATOR-CHALLENGE.md` §2, re-derived from the shipped shapes before
it was believed, and it is live on the mod's headline case. Renders:
`docs/agentic/ingest/cablecar/renders/steep/`.

**The arithmetic.** The archway is **3.5 blocks** tall — plinth top at `anchor-4.0`, crossarm cells'
underside at `anchor-0.5` — and the cabin is **2.5** tall hanging centred in it, so there is 0.5 of slack
over the roof and 0.5 under the floor. `EntityRopewayCabin.Place` writes `Pos.Yaw` and touches neither
pitch nor roll, so the cabin hangs plumb and stays **level**: leaving a tower on a climbing span its roof
rises with the rope while the crossarm does not, and it still overlaps the one-cell-deep crossarm row until
2.0 + 0.5 = **2.5 blocks** of plan out. `0.5 / 2.5 = 0.2` → **11.31°**. `TheCabinFitsThroughTheTower` could
not catch it because every height in it is a constant: it measures one cabin parked at one tower.

Three contacts, all re-derived, all pinned by `TheCabinFitsThroughTheTowerAtEveryPitch`:

| | clearance parked | reach | first contact | at 30° |
|---|---|---|---|---|
| roof vs the crossarm cells | 0.500 | 2.5 | **11.31°** | −0.943 blocks, over 1.634 of travel |
| floor vs the footing plinth (the mirror, on the way DOWN into a tower — not in the challenge doc) | 0.500 | 2.4375 | **11.59°** | −0.907 |
| roof vs the DRAWN station rail (follows the rope, so it tips; also not in the doc) | 0.250 | 2.0 | **7.13°** | −0.905 |

**Nothing in the cabin can fix it, and that is arithmetic rather than an opinion.** Trading roof height for
floor height moves both limits at once and the best split is *worse* — 9.6°, because the rail binds first.
Shortening the cabin to 3 blocks buys 14.0°; thinning both slabs buys 14.4°. Even a crossarm hollowed all
the way up to the rope line caps a level cabin at **26.6°** — past that its roof would have to rise through
the rope it hangs from — and no attitude at all gets past **30.6°**, because a 2.5-block section tilted by
φ crossing a one-cell-deep row stands `2.5·secφ + tanφ` tall and the archway is 3.5. Pitching the cabin with the rope is
not a cure either: it is the wrong field (`Pos.Roll`, not `Pos.Pitch` — the model's long axis is X and the
renderer applies pitch about world X *after* yaw), vanilla's `EntityRideableSeat.SeatPosition` reads
neither, so both riders would stay at their level seat positions — 0.66 blocks out of the bench at 30° —
and it runs out at 29.7° anyway when the tail digs into the plinth.

**The only lever with travel in it is the archway**, i.e. `SheaveHeight` and `hangDrop` together: each extra
cell of tower adds 0.5 of slack per side and 0.2 to the tangent — **5 → 21.8°, 6 → 31.0°, 8 → 45°**. That is
a multiblock change (three blocktypes, 15 → 19 offsets, the hanger art, `ClearanceBelow`) and it was not
made here. Costed, not started.

**What shipped instead:** the ceiling is a named constant, `SpanMath.PassablePitchTan`, derived in its own
doc comment and pinned to the shipped shapes by a test that sweeps 0–89° and fails in *both* directions —
if the cabin clips under it, and if the geometry would allow more. `TryLink` says so once, in chat, when a
span is strung above it (`ropeway:span-too-steep`): **warn, never refuse**, the same rule corner towers get,
because a ropeway that refused climbs would be refusing the thing it exists for. A mounted rider has no
block collision, so what a player sees above 11.3° is the roof passing through the brace, the sheave and a
station's lay shaft for ~1.6 blocks of travel, and the floor through the plinth coming back down.

## The corridor was certified for a level line only — FIXED (2026-08-10)

`IsSpanClear` laid its rays on a fixed ladder, `j ∈ [−ClearanceBelow, +ClearanceAbove]` = −3.5…+1.5 blocks
about the rope. That window is exact at zero pitch and wrong at every other, because `up` is perpendicular
to the **chord** and leans back with the pitch while the cabin hangs plumb and stays level — so the cabin's
own 4-block length projects onto `up` as a further `2·sin(pitch)` at each end. Required band:

```
-(2·sin + 3.5·cos)  …  max(2·sin - 1.0·cos, ReturnLift·cos)
```

Worst **under the floor at 29.74°** — `√(2² + 3.5²)` = 4.031 against 3.5 certified, **0.531 blocks of
uncertified ground under a seated rider**, on exactly the pitch a hill line is built at, and the challenge
doc's table skips 15–45° and so missed it. Worst **over the nose approaching vertical**, 2.0 against 1.5.

`SpanMath.ClearanceRows` now returns the ladder for the span's own pitch: one block per row over that band,
each ray down its row's centre. At zero pitch it *is* the old ladder (rays at −3…+1), so nothing about the
flat case moved; it costs a sixth row through the middle of the range where the leaning band is widest
(15 → 18 rays a span) and drops back to five near vertical as the strand collapses onto the rope line. It
also makes the near-vertical branch symmetric, which silently closes the direction-dependence the challenge
doc's §4 found: `right` is hard-coded there, so `up` flips with the direction of travel, and against an
asymmetric window a link clicked from the **top** tower certified `Z−1…Z+3` while the ride checked
`Z−3…Z+1` — a link that succeeded and a cabin that then refused to move. `TheVerticalCorridorIsTheSameOneFromEitherEnd`
is what keeps it shut.

## The haul rope is a LOOP (2026-08-04)

Two strands, stacked, one cabin. `BEPylonBase.OnTesselation` draws every half-span twice: once as it always
did, and once over `Lift(going, ReturnLift)` — the **same point list** with a constant added to Y.
There is no second cabin, no second `Travelled`, no new block and no new multiblock cell. Full derivation:
`docs/agentic/ingest/cablecar/STACKED-LOOP-SPEC.md`; renders under
`docs/agentic/ingest/cablecar/renders/loop/`.

**The separation is tied to the rim by an ASSERTION, not by a derivation, and that distinction is the whole
of what this paragraph now claims.** `ReturnLift = 2 * BullwheelRenderer.WrapRadius` = **1.3263 blocks**, and
it is a wheel *diameter* because `WrapPath` puts the groove tangent to the going strand at the BOTTOM of the
bullwheel — so a rope that wraps 180° leaves at the TOP. What this used to say is that re-authoring
`bullwheelrim.json` makes the loop follow the rim. **It does not**, and it was tested by doing it: the rim's
`R` was taken 9.0 → 8.5 and regenerated, and nothing about where the strands are drawn moved, because
`WrapRadius` is a **hardcoded `10.6104f / 16f`**. What happens instead is that the build goes RED, with the
number it is out by — `TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach` re-derives ρ off the
shipped rim's own swept corner and fails *"Expected 0.66315, Actual 0.63191"*.

**So re-authoring the wheel is a THREE-PLACE edit, and here are the three places.** Change them together or
the suite stops you:

1. `BullwheelRenderer.WrapRadius` — the literal, `(swept reach + 0.96) / 16`;
2. `pylonhead.json`'s `returnshoe` / `returnmast` `y` — the saddle's top face is `8 + 16 * (ReturnLift −
   CableRadius)`, generated but **typed into the shape**, and `TheTowerCarriesTheReturnStrandOnItsOwnShoe`
   compares the two;
3. the pinned constants in the suite — `1.3263` in `TheTwoStrandsAreOneCurveAWheelApart`, `1.1163` in the
   two cabin sweeps, `0.74` in `TheReturnStrandClearsTheBullwheelsOwnBearings`.

That is a refusal rather than a derivation and it is deliberately not being re-engineered: a hardcoded
constant caught by a test that fails with its own number is cheaper and louder than a build-time generator,
and the chain **rim → `WrapRadius` → `ReturnLift` → `returnshoe`** is held end to end by tests that were
watched failing.

**Nothing can scissor at a corner, because there is no second curve.** `RopewayLine.PositionAt` adds its bend
to X and Z only, so a path raised by a constant is the *same plan curve* — bit-identical at every sample over
a right angle. `renders/loop/corner-after/material/top.png` shows one curve in plan, because the return
strand is exactly on top of the going one. A LATERAL stack cannot have that: 2·rho on the inside of the
bend's tightest 1.317-block radius of curvature gives radius **−0.009** and cusps, it passes through the
`x = +1` brace at every tower, and it leaves the cabin 0.94 blocks of clearance against stacked's 1.1163.

**The wrap stopped being a ring.** `WrapPath` is a 180° arc now — twelve points, of which both stubs are
collinear with the chord they meet, so it emits **nine boxes against the closed ring's sixteen**. The ring's
own comment said a second strand "would be a cable the whole length of the line that nothing hangs on"; that
is now exactly what is drawn, so the collapse is undone and the arc is *cheaper* than the thing it replaced.
`terminal-after` reports 2 coplanar overlaps against `terminal-before`'s 4, and both survivors are the
cabin's own pre-existing `backrest`/`cargo` pair.

**What changed on the tower: two elements on `pylonhead.json`, and nothing else.** `returnmast` continues the
head's own centre column off the top of `housing` (which ends at y 14), and `returnshoe` is a flat saddle
whose top face IS the strand's underside — `8 + 16 * (ReturnLift − CableRadius)` = 28.2608, generated rather
than typed and held by `TheTowerCarriesTheReturnStrandOnItsOwnShoe`. The shoe is **short along the line**
(8 units, the sheave's own depth) and **flat rather than a channel**, both for the reason the deleted rail
plate paid for: a fixture on the tower's cardinal is only right where the path passes *through* the anchor,
and that is one point. At a 45° corner the strand runs off the side of an 8-unit shoe by 4 units, which a
flat plate merely stops being under and cheeks would clash with. `bullwheel.json` gets neither element: at a
terminal the wheel is the carrier. The mast starts where `housing` stops, so the 0.20-unit soffit clearance
over a parked cabin's grip — the tightest number in the machine — is untouched and `TheCabinFitsThroughTheTower`
does not move.

**`IsSpanClear` gained one row above the rope.** `SpanMath.ClearanceAbove = 1`, rays per span 12 → 15. Row
`j = +1` spans w 0.5 … 1.5 against a strand band of 1.2663 … 1.3863, so one row covers it with 0.766 blocks
of that row under the strand and 0.114 over; two "for margin" would refuse spans over nothing, because 2·rho
lands in one row and the arithmetic says which. **A span that would have been legal yesterday can be refused
today** if there is terrain a block above the rope line. That is the point of it, and existing lines are not
re-checked.

**The cabin cannot reach the return strand, and the proof is a pure vertical.** The whole cabin is under
`jawtop` at +0.15 blocks and the whole strand is over +1.2663, so the gap is **1.1163 blocks = 17.86 units**
at every position and every yaw — including parked at a terminal under the wrap's own arc — and no plan
geometry enters it, so no sweep can beat it. That is 446 × the jaw's own authored play on the rope it *is*
clamped to. `TheCabinNeverReachesTheReturnStrandAtAnyPositionOrYaw`.

**The tightest thing the loop creates is lateral: 0.74 units.** At a terminal the return strand threads
**between the bullwheel's own bearing caps**, whose tops stand 0.24 units into its vertical band. Nothing
vertical is load-bearing there. `bullwheel.json` cannot widen its caps after this, and
`TheReturnStrandClearsTheBullwheelsOwnBearings` is what says so. Everything else has most of a block:
0.891 to the pylon head, 0.829 to the braces, 1.766 to the station rail; the wheel brackets leave the sheave
throat's own 1.64.

**A station the line runs THROUGH lifts its wheel.** There is no dead side, and where the wheel rests the
return strand runs 0.22 blocks above its axle — **1.12 blocks of rope inside the swept rim, every
revolution**. So it rises `BullwheelRenderer.HoldDownRise` = 3·`WrapRadius` − (`RimPivotY` − 0.5) = **0.883
blocks** and becomes a hold-down sheave on the strand nothing rides on, its groove tangent from below exactly
as it is tangent to the going strand from above at a terminal. `CullRadius` 2.0 → 2.75 goes with it, and the
two brackets that carry the wheel out at a terminal become the two struts that carry it up here — one
function, `BEPylonBase.BracketPath`, keyed off the same `BEBullwheel.WrapOffset` the renderer's matrix reads.
Two alternatives were costed and rejected: **dropping the wheel in place** to carry both strands is refused
by the cabin (the rim's lowest swept point lands at +0.06 against a passing grip at +0.15, and the grip is
inside the rim for 0.90 blocks of travel every trip, both directions), and **refusing the second span at a
station** is cheaper than either but makes a legal, buildable route unbuildable, which this mod has declined
to do twice. Rendered: `renders/loop/through-{before,after}`.

**A plain END tower ENDS the return strand on its own shoe, and the separation is constant everywhere
(2026-08-04, and this replaces a pinch that shipped for one round).** The lift used to ramp back to zero over
the tower's own `TrimForTowers` window so the two strands converged onto the sheave. **The cabin parks on
that sheave**, and `TryPlaceCabin` only asks whether the line has a tension station *somewhere*, so
`[tension station] — [plain tower]` is buildable, takes a cabin, and `MarkLoadedEnds` puts `MaxTravel` at the
plain tower's own anchor: the ramp ran through the parked and departing cabin's `jawtop` plate for **0.77
blocks of travel, every trip, in both directions**, deepest 0.065 blocks out. That is the same defect at the
same scale as the one `BULLWHEEL-WRAP-SPEC` §3b refused a dropped wheel for (0.90 blocks), and it was
introduced by the fix for a different problem.

**There is no window to move the ramp into, which is why the answer is to delete it rather than shift it.**
The grip's plate stands w +0.0625 … +0.15 and the strand carries its own 0.06 either side, so a lift is
inside cabin metal for 0.2075 of its 1.3263 blocks — **16% of any window at any span length** — and travel is
clamped to the anchor while the plate reaches 0.131 blocks *past* it, so the cabin sweeps every point a ramp
could occupy. What the strand ends on instead is `returnshoe`, which was already under it: the saddle's top
face **is** the strand's underside, so the rope arrives at full height, lands on the saddle and stops. The
bare shoe the pinch left standing over such a tower is gone with it — the shoe now carries rope at every
tower, which is what it was authored for. Two other answers were re-priced and still cost what they cost: a
smaller return sheave has to have a 0.663-block radius against a 0.325-block throat and would be a new block,
and drawing one strand on a terminal-less line is a question about the whole LINE that `OnTesselation`'s two-
or three-tower `LocalLine` cannot answer without new persisted state. Held by
`TheReturnStrandStaysAWheelAboveTheCabinOnEveryTopology`, which sweeps every drawn strand point against every
position the cabin can hang at over five topologies — the plain END tower first — and fails at **−0.21
blocks** against the ramp, and by `TheTwoHalvesOfTheReturnStrandMeetAtTheSpanMidpointAtAnyPitch`.

**The pinch took two more defects out with it, both found in the same review.** It ramped over
`TrimForTowers` of the **3-D** span while dividing by the **horizontal** run, so on a pitched span each
tower's half was still climbing where it stopped and the peer's began at full height: a **0.33-block hole in
the rope** at the midpoint of a 5-block span at 53°, 0.83 at 71.6°, breaking whenever
`(L/2)·cos(pitch) < TrimForTowers(L)` — which is a tower on a ledge above another, the case a ropeway exists
for. And at a span of **one block or less** `TrimForTowers` is 0, which the ramp read as "no ramp", leaving
the strand at full height starting AT the sheave with nothing under it. Both are gone by construction: a
constant cannot be short at the midpoint and has no window to switch off. **No minimum span was added** —
nothing is wrong with a one-block span now, and a minimum would refuse a legitimate short hop between two
ledges to buy nothing.

**One z-fight note, pre-existing.** The return strand is drawn at the going strand's own thickness: the
`JointPhase` it used to be thinned by, and the cross-axis phasing that went with it, existed only because the
pinch put the two runs on one bearing, and both went with the pinch — re-rendered, the scene counts are
unmoved (`straight` 2, `corner` 10, `terminal` 2, `through` 2, `plainend` 2, `line` 6). What is **not** fixed
is that the return strand adds one more instance of the going strand's own pre-existing artefact at a corner:
the two half-span runs a two-span tower draws meet at the tower centre with both starting at phase 0, so
`corner-before`'s 8 coplanar faces become `corner-after`'s 10. Fixing that means seeding a run's parity from
outside `BuildRun`, which is worth doing for all four runs at once or not at all.

**At line scale the loop reads as a thicker cable, and that is the honest answer.** 1.33 blocks of separation
against a 5-block tower is about two pixels of gap at 900 px — `renders/loop/zoom-line-after.png`. It reads
as a loop when you are near a tower; what makes a terminal legible from across a valley is still the turning
wheel, which is what `BULLWHEEL-REVIEW` concluded.

**What a player with an existing line sees.** Nothing breaks and nothing has to be rebuilt. Every tower,
span, name, cabin, call and freight bay is untouched — the loop is drawn geometry and one extra row of
clearance rays, and neither is persisted state. On first load the line simply has a second rope over it, the
pylon heads have grown a mast and a saddle, and each terminal's hoop has become a rope going round the wheel
and coming back. **Two things a player could file as bugs and should not:** a plain end tower shows its
return strand running onto the saddle and simply stopping there, with no wheel to have turned it (the line is
unfinished — build a station there), and a station the line runs through shows its wheel standing a block
higher than the shaft that drives it (it is holding the return strand down). One thing genuinely changes: **a new span over terrain that
rises to within a block above the rope line is now refused** where it would have been allowed. Existing spans
are never re-checked, so no built line loses anything.

## Station rail (2026-08-03) — what shipped, what was reverted, what it costs

`RAIL-DESIGN.md`'s five-step ladder went in and **step 4 came back out.** Steps 1–3 (the split sheave, the
hanger blade and the jaw, `hangDrop` 2.0 → 2.25, the flared rail drawn on the pylon head's own shape) and
step 5 (the 5-wide passage) are shipped. Step 4, the angle-station yaw law, is **reverted**. (Step 3's
authored rail is gone entirely since 2026-08-04 — flares and plates both — and the whole rail is drawn on
the path. See "Closed: the drawn rope and the station rail follow the bend" below.)

**The causation in the design was backwards, and an earlier version of this section repeated it.** Read
this before re-attempting anything here.

**The 5-wide passage is the fix.** Moving the posts from x ±2 to ±3 is what reduces post penetration:
45° goes **1.000 → 0.033 blocks** and 30° goes **0.450 → 0.000**. That is the whole of the improvement.
It is not "room and light".

**The angle-station yaw law was a regression and is gone.** Holding each tower's own passage axis across
the vertex threw the widening's gain away: 45° went **0.033 → 1.000** and 30° **0.000 → 0.331**. The
mechanism is direct — `RopewayLine.PositionAt` swings the cabin's ORIGIN onto the outgoing leg at the
vertex, so a cabin still holding the incoming axis crab-walks, and a crabbing 4-block cabin sweeps its tail
into the post on the outside of the bend. `RopewayLine.Facings`, `SquareHold`, `YawBlend` and the three tests
that asserted the law are deleted. The original 0.000-at-every-angle number came from a model whose cabin ran
dead straight through the vertex, which `PositionAt` never does.

**Superseded on one point only: `DirectionAt` is no longer the plain leg bearing.** `PositionAt` now bends
the path through each tower — a cubic Hermite tangent to the corner's bisector, confined to the
`TrimForTowers` stretch no clearance ray ever visits — and `DirectionAt` returns that path's own tangent,
which AT a tower is the bisector. The reverted law was re-run on the bent path before this was built, which
is the condition the old tombstone set, and **it is still a regression**: held as a hard cardinal across the
window it measures 1.000 blocks of penetration at 90° with the tower on the bisector against 0.034 for the
plain bearing, 1.000 against 0.033 at 45°, and 0.740 against 0.000 at 30° — worse in three of nine cells and
better in none. What the bend changes is that the *bearing* no longer steps by the whole turn angle in one
tick, and measured across the same nine cells it is never worse than the straight path and better twice
(90°/ψ=0 0.034 → 0.000, 45°/ψ=22.5 0.033 → 0.004). Asserted by
`TheBentPathNeverDrivesTheCabinDeeperIntoAPostThanTheStraightOneDid`; full derivation in
`docs/agentic/ingest/cablecar/TURNING-SPEC.md`.

**"Never worse" is scoped to those nine cells and NOT to every corner (2026-08-04).** `DirectionAt`'s own
tombstone used to claim the tangent law is never worse than the plain bearing *anywhere*; measured over a
turn × ψ grid it is worse from about **125° of turn**, and worst at a **164.6° hairpin with the tower 45° off
the bisector — 0.529 blocks of post straight against a full 1.000 bent**. The mechanism is direct: a hairpin's
bisector is nearly perpendicular to both legs, so arriving on it points a 4-block cabin broadside across its
own passage where the straight path at least kept it along a leg. Nothing in `RopewayLinkService` constrains
the angle between two spans, so those corners are buildable — the closure is the link-time warning below, not
a curve. There is **no cell anywhere on that grid where the straight path was essentially clean (< 0.05
blocks) and the bend is not**; every regression is a corner already 0.17–1.0 blocks into a post. The hairpin
is now a pinned row of `TheBentPathNeverDrivesTheCabinDeeperIntoAPostThanTheStraightOneDid`, in the direction
it actually goes, so the word "anywhere" cannot come back without the test noticing.

**Closed: the drawn rope and the station rail follow the bend.** `BEPylonBase.OnTesselation` samples both off
`RopewayLine.PositionAt` — the same function the cabin's own position comes from — through the
`TrimForTowers` window at each end of a span, then one straight box for the middle the bend does not touch.
Rope, rail and cabin are one curve because they are one call. Before this the cabin's origin left the drawn
cable by up to 0.419 blocks at 90° (0.227 at 45°, 0.153 at 30°) against 0.0025 blocks of jaw play, and the
authored rail was a cardinal fixture the rollers rode 0.3–0.6 blocks clear of through every corner.

**Every authored rail element is now gone from both head shapes, and the two straight plates went last
(2026-08-04).** `TURNING-SPEC.md` §4 asked for all twelve out of `pylonhead.json` and `bullwheel.json`; the
eight **flares** went first and `railwest` / `raileast` survived one round on the *parked* cabin's argument —
a parked cabin squares to the tower's own cardinal, so a cardinal fixture under the sheave is right at any
yaw. What killed them is the **moving** cabin: at a 90° corner the drawn run leaves the plate's axis by 1.33
units and the guide roller ends up 1.37 units inside the plate's own metal, so the fixture that was right
parked is the one piece of geometry a passing cabin goes through. The run now starts at the tower centre and
the rail is **entirely** a runtime cross-section, pinned to the rollers it carries rather than to a shape —
`TheDrawnRailIsTheBarTheGuideRollersRideIn`, which replaced
`TheBullwheelKeepsTheSheavesThroatAndStationRails`. `BEPylonBase.RailStart` went with the plate.

**The salvageable half DID ship: a cabin STOPPED at a tower squares up to that tower's passage.**
`EntityRopewayCabin.SquareTo`, gated on `!departed`, which is the exact predicate under which `Travelled`
cannot change. That is the whole difference from the reverted law: the law held the axis across a *window*
around the vertex, **while the cabin was moving**, which is what let `PositionAt` swing the origin out from
under it. Stationary there is no origin motion to crab away from, and a cabin merely *passing* a tower never
reaches the branch at all — a pass-through takes `DirectionAt` and nothing else, which since the bend is the
path's own tangent rather than the leg bearing. The two now differ by ψ, the tower facing's error from the
bisector: a parked cabin sits on the tower's cardinal and a passing one on the bisector, so a badly-faced
corner tower still shows a swing as the cabin settles. That gap is the facing, not the curve.
Rotating in place at the tower centre sweeps the cabin's
half-diagonal, √(2.0² + 1.4375²) = **2.463 blocks against post inner faces at 2.5** — 0.037 blocks of margin,
and the 5-wide passage is the only reason it exists at all (at x ±2 it would sweep 0.463 blocks through a
post). `TheCabinCanTurnSquareAtATowerWithoutSweepingThroughAPost` asserts both numbers off the shipped shape
and the shipped multiblock. It is a snap on the server; the cabin's `interpolateposition` behavior eases
`Pos.Yaw` with a time constant of exactly **1/6 s** (`LerpRotation` resolves to `d(yaw)/dt = 6·(target−yaw)`
for any `dt` under 0.1 s — the 0.1 is a literal inside its clamp, not the constant, and two comments used to
quote it as though it were), so the settle reads as about half a second of eased rotation in place
(frame-rate dependent), and it rotates back onto the span as it departs. Renders:
`docs/agentic/ingest/cablecar/renders/parked/`.

**A right-angle corner is clean if and only if its bisector is a cardinal.** An earlier version of this line
said "can never be clean, under any yaw law", and that is true only when the tower faces one of the two
**legs**: then the other leg *is* the post axis, the cabin's **origin** travels through the post column, and
no rotation fixes a translation. But a 90° corner between two **diagonal** legs has a cardinal bisector, the
tower can face it, and the posts then sit across the passage rather than along a leg — measured, that corner
penetrates 0.034 blocks on the straight path and **0.000 with the bend**. The rig that produced the old
1.000 rounded its "nearest cardinal" onto the incoming leg, so the case had never been measured.

What is permanent is the tolerance, and it is ψ that owns it: the widest tower-facing error that keeps
penetration under the cabin's own 0.0625-block wall thickness is **±1.0° at 90°, ±23° at 45°, ±30° at 30°**.
Since `HorizontalOrientable` offers four cardinals, the achievable ψ is uniform on [0°, 45°], so roughly 2%
of arbitrary right-angle corners, 52% of 45° ones and 68% of 30° ones come out clean, and no yaw law in the
table changes that materially. The handbook says so and `QA-SCRIPT.md` step 12b expects it. The cures are a
tower turned onto the bisector — free, and nothing tells the player to do it — or refusing sharp bends at
link time; the link-time **warning** below is the cheap one and the tolerances above are the numbers it was
missing.

**BUILT (2026-08-04): `TryLink` WARNS, and never refuses, on a corner the cabin cannot pass cleanly.** After
both `AddSpan` calls, each end of the new span that now carries two spans gets its bisector read straight out
of `RopewayLine.DirectionAt` at the vertex — so the message talks about the direction the cabin actually
takes, by construction — and its crossarm's error from that bisector compared against
`SpanMath.CornerTolerance`. Two messages, because there are two answers: under a right angle a cardinal
usually exists that carries the corner and `ropeway:corner-facing` names it, and at or past one the best of
four cardinals is still outside the tolerance so `ropeway:corner-too-sharp` says that instead rather than
handing out advice that does not work. Warn only — refusing would make a legal, buildable route unbuildable
for a cosmetic reason, and players do build ugly corners on purpose.
`CornerTolerance` is **a fit to three measured points, not a derivation**, and says so in its own comment:
half the shortfall from a right angle reproduces the ±1.0° / ±23.2° / ±30.8° of the table above to within a
degree and errs toward warning. There is no closed form to find — the cabin fits the passage at *any* yaw
(2.463 against 2.5), so what a facing error costs is where the origin is twenty blocks out, which no local
geometry knows. Pinned by `ACornerTellsAPlayerWhichWayItsTowerWantsToFace`.

**The doubled metal cost per tower is PAID OFF, and the debt was booked against the wrong thing
(2026-08-04).** What this section used to say was true and is kept because it was a real trade: a 5-wide
crossarm needed 4 braces = exactly one metal plate, a 7-wide needs 6, and 6 is not divisible by
`brace.json`'s yield of **4**, so a tower was **two crafts = two plates** and a ten-tower route 20 plates of
braces instead of 10 — the multiplier `DECISIONS.md` §3's marginal-cheapness rule exists to protect.

**It was framed as a forced trade against reverting the 5-wide passage, and that was a false dilemma.** The
whole penalty came out of the *yield*, not the *width*: nobody had looked at the 4. The recipe pass raised
it to **8**, so a tower's seven braces — six on the crossarm plus the one the pylon head eats — are **one
craft with one over**, the marginal metal per tower is **1.85 ingots**, and the widening keeps its
45°/30° post-penetration gain for nothing. There is no longer any price to weigh the passage against, so
the paragraph that used to stand here telling you how to revert it is deleted rather than corrected: the
reason to revert is gone. (If the geometry ever needs undoing for some *other* reason, it is still
JSON-only — the offsets in `pylonbase.json` and the numbers quoting them — but that is a different
argument and it should be made on its own.)

**Cosmetic, at corners, and the bend halved it (2026-08-04).** The rope sits at scene y 35–37 and the brace
beams occupy 30–42, so any drawn cable inside the tower's own plane and beyond half a block along the
crossarm is inside a brace block. The rope now leaves the sheave on the corner's **bisector** rather than on
the leg bearing, which cuts the burial from half a block to a tenth at a tower facing that bisector — the
cable never leaves the sheave's own cell. **What it cannot fix is the incoming leg at a badly faced tower.**
At a right angle with the crossarm pointed down one of the two legs, that leg *is* the crossarm axis twenty
blocks out, and a curve confined to the last four cannot change where the rope arrives from: it lies along
the whole 3.5-block crossarm, as it always did. So this is now a symptom of ψ like everything else at a
corner, and the cure is the tower facing the link-time warning names.

## Tower restructure (2026-08-01) — what it costs

The controller moved from the pylon head at head height to a ground-placed `ropeway:pylonbase` footing and
the rear gantry is gone (see `DECISIONS.md`, "2026-07-31 — tower restructure"). Two consequences worth
knowing before playing:

- **Every pre-existing world loses its towers.** The block entity class name changed from `PylonHead` to
  `PylonBase`, so `ServerChunk` logs *"Failed loading blockentity PylonHead … Will discard it"* and drops
  it. That IS the migration: no crash, no half-converted tower, no orphaned span — a legacy tower comes
  back as inert decoration, because every tower in the world loses its block entity at once and there is
  nothing left holding a reference to anything. Deliberate, and cheaper and safer than an upgrader for a
  pre-release mod. `BlockPylonHead.GetPlacedBlockInfo` says so on the block: *"Not part of a tower…"*.
- **A cabin left on a legacy line is stranded.** Its `LineKey` names a tower that no longer registers, so
  `ResolveLine` returns null and the tick holds — which is exactly what an unloaded chunk looks like, so
  the `DropAndDie` backstop cannot fire without also eating cabins on genuinely unloaded lines. It hangs
  there inert. Remove it with `/entity remove` or ignore it. Not fixed: the cure is worse than the disease.

## Fixed after the first in-game session

Four findings from BASIC's first session in the world, all now closed. Recorded because three of them were
invisible to every gate the mod had, and the fourth was verified against the wrong axis. The last two rows
are the ride-feel pair that followed - a standing rider, and a camera that could not see the cabin - kept
here for the same reason: both failed silently, with a green build and a green test run.

| what was wrong | root cause | fix |
|---|---|---|
| **The cabin flew sideways** and presented its 4-block side to the tower's 3-block passage, so it did not fit the tower it passes through. | The shape was authored along Z. Vintage Story entity shapes face along **X** at yaw 0 — `EntityShapeRenderer` adds +90° to `Pos.Yaw` before building the model matrix (EntityShapeRenderer.cs:808), and every vanilla entity whose long axis is its heading (raft 4.5×2.25, arapaima 1.90×0.58, pike 1.35×0.56) is long in X. The yaw code was never the bug. | `shapes/entity/cabin.json` rotated −90° about Y, faces and UVs cycled with it, seat attachment points recomputed, the sway animation moved from `rotationX` to `rotationZ` so it rocks fore and aft. `EntityRopewayCabin.SetSelectionBox` re-derived. **Round-1 review correction:** swapping the box's axes to match the shape was itself wrong — `Entity.SelectionBox` is world-axis-aligned and is never rotated by yaw (`Entity.IntersectsRay` → `RayIntersectsWithCuboid(SelectionBox, Pos, …)` with no yaw term), so a box that fits the model fits one bearing and is transposed on the perpendicular one. It is now **square in x/z at ±2.05**: an AABB that cannot rotate must circumscribe, not fit. Guarded by `TheCabinIsBuiltAlongTheTravelAxis`, `TheHardcodedSelectionBoxCircumscribesTheCabinAtAnyYaw` (which asserts squareness, so a rectangular "optimisation" fails) and `TheSeatAttachmentPointsStayOnTheCentreLine`. |
| **The cable rendered nothing at all** — no exception, no log line. | `CubeMeshUtil.GetCube` returns a bare `new MeshData()` whose `XyzFaces` is `Array.Empty` and `XyzFacesCount` is 0. The chunk tesselator emits geometry only inside `for (l = 0; l < sourceMesh.XyzFacesCount; l++)` (JsonTesselator.cs:709), so `mesher.AddMeshData` copied zero vertices. `GetCube` was never a chunk-mesh source: every vanilla call site feeds a custom renderer or the particle system. | `CubeMeshUtil.SetXyzFacesAndPacketNormals` plus `mesh.WithColorMaps()` (the emit loop indexes both colour maps per face at JsonTesselator.cs:834, so the face count alone would only trade an invisible cable for an `IndexOutOfRangeException`), and the box is now centred by its own half-extents so the rotate-about-origin is not off by half a box per axis. `MarkBlockDirty` at the end of `FromTreeAttributes` (the `BlockEntityDisplay.cs:119-126` idiom) covers the client that receives spans after its chunk was already tesselated. Guarded by `TheCableMeshIsCentredAndCarriesTheFaceCountTheTesselatorLoopsOver`. |
| **The picker listed only link candidates**, so there was no way to see or cut an existing connection short of breaking the pylon head. | Scope. | Existing spans are sent in the same list with `Linked` set, rendered first and styled differently, and clicking one calls the new `RopewayLinkService.TryUnlink` — same refund, permission gate and cabin re-base as a block break, and the same "someone is riding this line" refusal. `SendCandidates` no longer refuses on a full tower, because a full tower is exactly when you want to unlink; `ScanCandidates` returns nothing there instead, so every listed link row still succeeds on click. |
| **Towers had no names**, only coordinates. | Scope. | `BEPylonHead.TowerName`, persisted in the tree and synced by `MarkDirty`, set from the picker's "This tower:" field, shown in `GetBlockInfo`, in every picker row and in the link/cut chat lines. Unnamed towers fall back to the eight-point compass bearing (`SpanMath.CompassKey`), never to a placeholder. `BEPylonHead.SanitiseName` is the trust boundary: control characters out, whitespace collapsed, 24 characters, no split surrogate pair, and (round-1 review) **angle brackets stripped** — `TowerName` reaches `GetBlockInfo`'s rich-text panel and the `span-linked`/`span-cut` chat lines, both VTML, and 24 characters is enough for a `<font>` or an `<a href>`. Stripped at the one chokepoint every display path routes through, not per surface. |

The scene renders under `docs/agentic/ingest/cablecar/renders/scenes/` were regenerated against the
re-authored cabin, and the numeric clearance asserts in `gen_manifests.py` now check the **Z** axis, which
is the one the tower's posts are actually on. Both scenes still render with `coplanarOverlapCount: 0` and
1.0 unit of lateral margin.

## Fixed after the second in-game session

Four things BASIC saw once the cable started rendering. All four are closed.

| what was wrong | root cause | fix |
|---|---|---|
| **The cable was striped** in unrelated browns, greys and purples along its length. | `CubeMeshUtil.ScaleCubeMesh` multiplies the cube's UVs by the axis scale (CubeMeshUtil.cs:230-251), so a half-span 24 blocks long left them running 0..48. `MeshData.SetTexPos` maps u through `x1 + u * (x2 - x1)`, which puts everything past 1 **outside** this texture's sub-region of the block atlas — the cable was sampling whichever sprites happened to sit next to the rope one. | `BuildHalfCable` flat-samples the sprite: `Array.Fill(mesh.Uv, 0.5f)` before `SetTexPos`, so every vertex lands on the middle of the sprite, far from its edges and therefore safe under mipmapping. A 2-pixel cable has nowhere to show lengthwise detail, and normalising to 0..1 instead would smear one 32×32 sprite over the whole span. The texture also changed to `game:block/cloth/reedrope`, the vanilla banner/crate rope. **That swap was cosmetic, not a fix** — an earlier version of this note claimed `game:item/resource/rope` was transparent at its edges and that this was the cause; it is not, it measures alpha 255 across all 32×32. The UV fix alone closes the bug. Guarded by `TheCableSamplesOnlyItsOwnCornerOfTheAtlas`. Thickness was re-verified and needed nothing: `GetCube` returns a 2×2×2 cube and `ScaleCubeMesh` does `xyz * scale + scale`, so with `translate = -CableRadius` the box spans exactly ±0.06 — 0.12 blocks, two pixels. |
| **Riders could not control where they got off.** Boarding departed after a grace period and ran to the END of the line, straight through every intermediate tower. Calling was for an empty cabin only. This was C3 below. | Scope: the ride had no rider input at all. | A **hotkey**, `ropewaystop`, default **R** (unbound in vanilla). Pressing it aims the cabin at a tower through the same `Destination` / `PlanCall` / `Reached` machinery a ground call uses — `Aim` is now the one place a trip starts, shared by both — so a rider choosing a stop *is* a call, made from the seat. `NextStop` steps the requested tower one along the chain in the direction of travel and **wraps at the ends**, which is also the direction control: a rider who boarded at an interior station on a cabin pointing the wrong way keeps pressing and the selection comes round the other way. Every candidate goes through `PlanCall`, so the tower the cabin is standing on and anything outside the loaded window are skipped rather than offered and refused. Discoverability was the actual bug, so: a chat hint on boarding naming the player's **own** binding (client side — the server cannot see it), a `"Choose where to get off"` interaction-help line on the cabin, the handbook, the tower guide, and a chat line naming the tower on every press. Guarded by `TheStopKeyStepsAlongTheLineAndWrapsBackTheOtherWay` and `TheStopKeyNeverOffersATowerOutsideTheLoadedWindow`. |
| **The crossarm did not meet the posts** — a visible step where a narrow metal bracket sat on a full-width log, with the log's whole 16×16 top face on show around it. | The brace's beam was z[5,11] and its flanges z[4,12], bottoming out at y = 1/16 — a 6-wide bracket floating one pixel above a 16-wide post. | One shape, no new block variant. The brace grew a **foot plate**, `[0,0,0]–[16,2,16]`, reaching the block boundary so it lands flat on the log; its flanges start at y = 2 instead of y = 1 so nothing is coplanar with it. The pylon head grew the matching `footwest` / `footeast` stubs at x 0–5 and 11–16, leaving the sheave throat (x 5–11) clear for the mast, so the crossarm reads as one continuous girder across all five cells (seven since the widening). Weighed against a separate end-piece block: the plate reads as a girder's bottom flange over the three interior cells and as a bearing plate over the two on posts, which is acceptable in both places, and a new block is a real cost. **It cost the cabin 1/16 of roof clearance** — 0.3125 → 0.25 — because the crossarm underside came down to the block boundary. `gen_manifests.py` now asserts both numbers together (`crossarm foot on the post top = 0`, `cabin roof under the crossarm = 4`), so they cannot drift apart silently. |
| **Only logs, debarked logs and planks were accepted as posts.** | Scope. | `game:@(log-placed-.*\|debarkedlog-.*\|planks-.*\|rock-.*\|cobblestone-.*\|drystone-.*\|rockpolished-.*\|stonebricks-.*)` — wood or dressed stone, so a tower can match what it stands next to. Deliberately **not** widened to slabs, stairs, chiselled blocks or soil: a post is a structural column, and a tower that accepts anything stops reading as a tower. `WildcardUtil` anchors an `@`-pattern as `^…$` (`RegexCache.IsMatch`), so `rock-.*` does not also swallow `crackedrock-*`. `RopewayModSystem.VerifyStructureWildcards` still passes — but note it only tests the whole key, so a dud **alternative** would hide behind the live ones; QA step 7 places one block of each family by hand for that reason. |
| **The rider stood up inside the cabin**, in the default standing idle, instead of sitting on a bench. | The `mountAnimations` map on the cabin entity was **dead JSON**. Only `EntityBoat` and `EntityElevator` read that key (`EntityBoat.cs:87`, `EntityElevator.cs:91`); the `seatable` behavior never looks at it, and `EntityRideableSeat.DidMount` starts `"idle"` on the **cabin**, not on the passenger. `SuggestedAnimation` was permanently null too - it casts the mount to `EntityBehaviorRideable`, which the cabin does not have. So no pose was ever started, and mapping the four keys to `"idle"` was never the cause. | `SeatConfig.Animation` per seat (`sitboatidle`, player.json:509 - the same code the sailed boat's non-controllable bench uses), played by an explicit `Passenger.AnimManager.StartAnimation` in `RopewayCabinSeat.DidMount` and stopped in `DidUnmount` **before** `base`, because `EntitySeat.DidUnmount` nulls `Passenger`. `eyeHeight` 1.4 -> 1.0: at 1.4 the eye sat at 1.0875, inside the roof slab that starts at 0.875. Both attachment points dropped 1/16 to the bench top, the sailed boat's convention. Seats stay `controllable: false` - nothing in the animation path reads it. Guarded by `BothCabinSeatsSitTheRiderWithTheEyeInTheGlazing`, which fails on an empty animation code or an eye outside the glazing band. **This closed the pose, not the seating** - see the row below. For two rounds afterwards the rider was correctly *animated* and still landing off the bench, because "is he sitting" and "is he sitting on anything" are different questions and only the first was ever under test. |
| **The rider sat 7 units off the back of his own bench** - and once the interior gained backrests, the front rider sat facing into his, back to the aisle. | Two independent gaps. (1) **No `riderOffset` on either seat.** `sitboatidle` does not put the rider on the AP: its frame-0 `LowerTorso offsetX 6.2` plus that element's rest roll puts the seated backside 7.22 units toward cabin +X of the rider's *origin*, box reaching 9.85. `boat-sailed.json:53-54`, the only vanilla `sitboatidle` bench, carries `riderOffset { x: -0.5 }` for exactly this, and without it vanilla's own rider misses vanilla's own plank by 2.7 units. The cabin had none - and an interior rebuild that narrowed the pans 18 -> 14 while moving the APs +/-15 -> +/-18 then pushed the contact patch fully off both pans with all 96 tests green. (2) **Backrests on the two end walls, facing each other.** `EntityPlayerShapeRenderer.cs:429-431` pins a remote rider's body yaw to the mount's, so both riders always face cabin -X; `AngleMode.Unaffected` gives no per-seat facing, and an AP's `rotationY` only reaches `seatPos.Yaw`, which that renderer ignores. Facing benches are not something this engine will render. | `riderOffset { x: -0.5 }` on both seats - vanilla's number, vanilla's sign, correcting the same pose. Pans cut 14 -> **10 deep and centred on their AP**, which is vanilla's plank copied exactly, so the shins hang off the front lip instead of coming down inside the pan. Backrests moved off the end walls to **+X of their own pan**: both rows face travel with their back behind them, tram seating, the only layout one forced yaw can seat correctly twice. APs to **-16 / +21**, deliberately not mirrored, because forward-facing rows need a footwell at the -X end that the +X row does not. Guarded by **`TheSeatedRidersContactPatchLandsOnItsPan`**, which ties the AP, the pan's x extent and `riderOffset` to each other and fails if any one of the three moves alone - the assert that was missing. Carried along, since the geometry was open anyway: the window mullions moved off the doorway onto the seat-back line, the rear backrest came 2 units off the end-wall glazing it had been reading as a window pane through, and the pans and aprons now run wall to wall so the under-bench void is closed. **Not fixable in JSON alone:** `bodyYawLimit` is read only by `EntityBehaviorRideable` and `EntityBoat`, and the cabin is neither, so the **self** player's body yaw was unconstrained and the seated mass swept a circle of radius 7.22 about the seat origin if they spun in place. No other player ever saw it, and it predated all of the above. Closed in C# in the round below. |
| **You could not see the cabin you were riding in.** | First person is the default and the ride is a view; vanilla's F5 is three stops away and the player has to know to press it. | A second rider hotkey, `ropewayridecam`, default **O** (unbound in vanilla - only I, K, L, O, P, R and U are free, and R is the stop key). Client side only, no packet, no seat change. The camera mode is not a setting: `Camera.CameraMode` and `SetMode` are internal, so it is **read** through `IRenderAPI.CameraType` and **written** by invoking vanilla's own `cyclecamera` hotkey handler - a public field on a public `HotKey` that ignores its argument (`PlayerCamera.cs:132`). Cleaner than the reflection ModDB's `glideview` uses, and needs no extra assembly reference. A 250 ms poll on `MountedOn` catches every dismount path; it restores **only** if the camera is still the mode it set, so pressing F5 mid-ride makes the mod let go rather than fight. Deliberately shipped **without** distance or offset tweaks: each trades a new artefact (lost zoom, desynced crosshair, more first-person snaps at towers) for the one it fixes, and none address the root cause, which is that vanilla's third-person wall check raycasts blocks only and the cabin is an entity. |

## Fixed after the third in-game session

Three things BASIC saw riding it. All three are closed.

| what was wrong | root cause | fix |
|---|---|---|
| **Every reload put the cabin back at the start of the line.** | Two independent teleports, both on the load path, diagnosed in `docs/agentic/ingest/cablecar/RELOAD-DIAGNOSIS.md`. (1) **`WalkChain` canonicalises by the two ends the WALK reached.** At world load the tower chunks register one column at a time, so the walk produces a *prefix* of the line — and a prefix whose far end sorts below `LineKey` **reverses**. `Towers[0]` then stops equalling the cabin's `LineKey`, and `ServerTick`'s re-base branch read that as "the chain re-canonicalised under us" and ran `Hold` → `RebaseTo` → `ParkAtNearestEnd` → `Place`, rewriting `LineKey`, `Travelled` **and** `Pos` from a chain `MarkLoadedEnds` had already flagged `Truncated`. It was the one branch in the tick that treated a truncated line as whole, and it is self-reinforcing: it re-keys onto an *interior* tower, which can never be `Towers[0]` of the finished chain, so it fires again when the last column lands and parks the cabin at `MinTravel` = the start of the line. (2) **`departed` was not persisted.** Restored false, so a cabin saved *in motion* mid-span looked like a cabin stopped in mid-air, and the `!departed && !IsAtTower` recovery parked it at an end. A called trip already survived (through `Destination`); an ordinary ride had nothing to survive on. | (1) One guard, stated once — **a truncated chain's `Towers[0]` is not evidence of anything**. `EntityRopewayCabin.NotReady` is every way there is to be waiting on the world (no line at all; a truncated chain measuring from a different tower; a truncated chain that no longer reaches the cabin), and its single call site at the top of `ServerTick` is the whole recovery: **stand still and write nothing else**. It has to be one predicate rather than three branches, because the first attempt *was* three and the third of them called `Hold` — which clears `departed`, undoing (2) and handing the cabin back to the mid-span park one tick later. `RebaseTo` carries the same rule for the link-service callers as `RebaseMustWait`, plus the clause that matters more than the guard: **hold only while the cabin's key is still on that chain**. `LineKey` is always an end tower, so "the broken tower was the key" is `UnlinkAll`'s *ordinary* case, and refusing to re-key there leaves the cabin keyed to a block `Forget()` is about to remove — `ResolveLine` null forever, and `DropAndDie` unable to fire because it requires `LoadedTowers` to contain `LineKey`. An uncollectable cabin hanging in mid-air with its item destroyed is strictly worse than the teleport, so the guard asks whether there is anything to hold *for*. No delay and nothing to serialise: `MarkLoadedEnds` widens the window by itself and `BEPylonBase.Initialize` drops the cached line, so a genuine re-base still runs on the tick after the last tower registers. (2) `departed` is written in `ToBytes` and read in `FromBytes`, and **only `Hold` may clear it**. A cabin saved mid-span comes back exactly there and carries on in the direction it was going, `lastSegment` still -1 so the span it resumes into is re-checked for clearance. That deliberately includes a cabin saved in motion whose **rider is still offline**: it resumes and runs to the end of the line, because the alternative is to drop the trip and dropping the trip is the bug. `DropGhostPassengers` still unseats a despawned player at a tower, and `RopewayCabinSeat.CanUnmount` keeps whoever reconnects into it aboard until it stops — QA step 19 checks that shape, not the old "parked at an end, empty" one. (3) A **rider is never teleported**, which was claimed before and not true: the re-base branch was already gated on `HasPassenger`, the mid-span park is now gated on it too (every `Hold` lands a cabin in that branch on the next tick, so a blocked span alone reached it), and `TryLink` refuses to merge an occupied line exactly as `TryUnlink` already did. A seated rider's cabin stops where it stands instead — and the stop key aims it at a station and sets it going again, including back the way it came. (This row used to add "it is not moving, so they can step out". That was the defect fixed on 2026-08-10 and recorded at the top of this file: mid-span, stepping out is the fall, and `CanUnmount` now refuses it.) Guarded by `TheWorldIsNotReadyUntilTheChainCanVouchForWhereTheCabinIs`, `ARebaseWaitsForChunksOnlyWhileTheOldKeyIsStillOnTheChain` and `APartialChainCanCanonicaliseTheOppositeWayFromTheWholeLine`; the tick ordering they feed needs QA 18/18b. |
| **A seated rider could spin all the way round** on a bench that faces one way. | `bodyYawLimit` was dead JSON, and the key is not decorative — `SeatConfig.BodyYawLimit` is only ever *read* by `EntityBoat.SeatsToMotion` and `EntityBehaviorRideable.SeatsToMotion`, and the cabin is neither, so nothing was going to apply it for us. | `EntityRopewayCabin.ConstrainRiderYaw`, eight lines on the tick, identical to vanilla's: `EntityPlayer.BodyYawLimits` / `HeadYawLimits` centred on `Pos.Yaw + mountRotation.y`, range `bodyYawLimit` (now **1.5707963 = ±90°**, so a rider can look out either side and not sit backwards). It needs **no controllable seat** — it constrains the passenger, not the mount, so `controllable: false` and the smooth-motion tests are untouched. What it clamps, exactly: `HeadYawLimits` is read by `ClientMain.UpdateCameraYawPitch` (:2377-2383), which clamps `mouseYaw`, so this is the seated player's **own camera**, client side; `BodyYawLimits` clamps that same player's rendered body through the `BodyYaw` setter. Neither reaches what other players see — `EntityPlayerShapeRenderer` (:429-431) already forces a remote rider's drawn body yaw to the mount's, so onlookers see him squared to the cabin whichever way he is looking. Running it server side would change nothing: the server assigns `BodyYawServer` from the position packet, not `BodyYaw`, so the clamping setter never sees it. |
| **The front seat was too far forward** — the rider's toes 2.34 units off the west wall while the rear row had 22.34 units in front of its own. | Forward-facing rows are asymmetric about the mast by construction (the rear row backs onto the east wall, the front row needs a footwell), and the front row was placed by mirroring the pan rather than by the clearance ahead of the rider. | The front bench moved back 10 units: pan −21..−11 → **−11..−1**, and `backrestwest`, `apronwest`, both mullions and both thresholds with it (the AP is `posX`-relative to the pan, so it follows on its own and stays at the pan's depth centre). Both rows face −X, so "evenly placed" is one number — the clear floor ahead of each rider's toes, which reach lip − 4.66 — and 10 is what equalises it at **12.34 each**. It also tiles: footwell 17 + pan 10 = a 27-unit seat bay, twice, in a 56-unit interior, with the rear row's 2-unit reveal off the east wall as the remainder. The threshold plank shortened 24 → 14 and its uv widths with it (size/4, like every face in that shape). `TheSeatedRidersContactPatchLandsOnItsPan` and `TheSeatAttachmentPointsStayOnTheCentreLine` both still pass, and the plan/section renders were re-read rather than the asserts alone. |

## Membership replaced proximity (2026-08-04)

The drive housing and the tension weight are **cells of a station**, not free-standing blocks bound to a
line by distance. Two new footings — `ropeway:drivestation` and `ropeway:tensionstation` — carry exactly the
fifteen offsets `pylonbase.json` already had; only which block each cell wants differs, and a station
REPLACES a tower in the chain rather than extending one. Design and the full deletion list:
`docs/agentic/ingest/cablecar/STATION-DESIGN.md`; build costs are in
[POWER-AND-STORAGE.md](POWER-AND-STORAGE.md).

**What that deleted.** `BETensionWeight.cs` and `BlockTensionWeight.cs` outright; `LoadedWeights` and
`LoadedHousings`; `ServingTower`, `Serves`, `Nearest`, `NearestTower`, `NearAnyTower`, both `towerRadius`
attributes, both placement refusals, the tie-break's **only caller**, and `BEDriveHousing`'s own tick
listener. **Nothing calls `Nearest` any more, because there is no `Nearest`** — that was the check that this
landed. `BEPylonBase.Intake` is one block-accessor call at an offset the station's own JSON names, and
`BEPylonBase.HasTensioner` is a walk over `line.Towers`.
`RopewayLine.ComparePos` itself **stays and must stay** — an earlier version of this line read as though it
went with the tie-break. `WalkChain` still calls it to canonicalise the chain's direction so `Travelled` is
measured from a stable `Towers[0]`, which is a different question from "which of two equidistant footings
owns this housing", and deleting it resurrects the reload teleport.

### One machine leg, one station — CLOSED, and the spacing rule is retired with it

**The bug, and it was real for two rounds.** `MultiblockStructure` has no notion of ownership:
`InCompleteBlockCount` asks only whether the block at each offset matches a wildcard, and nothing anywhere
asks whether some **other** footing is already claiming that cell. Derived off the shipped offsets by
`docs/agentic/ingest/cablecar/onetrack/sharedleg.py`, a `drivestation-north` at the origin shared its entire
machine leg — `drivehousing` at `(3,0,0)`, three `driveshaft` above it and `drivehead` at `(3,4,0)` — with
**three** other placements, and `tensionstation` with the same three:

| second station | footing separation |
|---|---|
| facing **east** at `(3, 0, −3)` | **4.243 blocks** |
| facing **west** at `(3, 0, +3)` | **4.243 blocks** |
| facing **south** at `(6, 0, 0)` | 6.000 blocks |

An earlier version of this section named only the third and reasoned from "a station's machine leg stands
three blocks out from its footing", which would lead a careful player to think a station four blocks away
*across the passage* was fine. It was not: the two 4.243 cases are two perpendicular lines meeting at a
junction, which is a thing a player builds on purpose. Both structures validated, so `DriveSpeedOn` resolved
the **same** `MPConsumer` from both lines and ran both at full speed, while `DeclareLoad` wrote `Resistance`
onto it from both footings on a 1 s tick and the last writer won — free speed **and** unpaid load, which is
verbatim what `RopewayPower.PoolSpeed`'s own comment calls the one thing a load model must never do and what
QA 27e exists to forbid.

**The fix is five lines and it is `BEPylonBase.OwnTheHeadCell` (2026-08-04).** Before `Init` hands the
structure to `InitForUse`, the footing's own copy of `blockNumbers` has `ropeway:drivehead-*` rewritten to
`ropeway:drivehead-<its own side>` and `ropeway:tensionhead-*` to `ropeway:tensionhead-<side>`.
`MultiblockStructure.BlockNumbers` is a public dictionary on a per-block-entity `AsObject` copy and
`BlockCodes` is built out of it inside `InitForUse`, so the rewrite is local to the tower and the build
overlay follows it. A shared head can face one way, so it can satisfy one station: **re-derived afterwards,
all three placements are gone, for both station kinds, at every separation the offsets can reach.**
`ASharedMachineLegSatisfiesAtMostOneStation` enumerates them and is what stops the tie coming back.

**Why only those two, when M4's looseness covers five blocks.** The refusal M4 defers is the one that would
bite `pylonhead` and `bullwheel`, and that argument is correct about them and irrelevant here. Those two are
symmetric along the rope axis, so a player who placed one from the other side of the tower has a
geometrically identical block that would stop validating — and an incomplete tower is un-clickable, so a
saved world would lose its picker, its call and its rename over a block that looks perfectly right. Neither
applies to the heads: **both blocktypes are new this round and untracked in git**, so no saved world can hold
a wrongly-faced one and the migration surface is empty; and both are visibly asymmetric —
`drivehead.shaftwest` is `x 0..4`, `tensionhead`'s tie rod is `x 0..12` against a sheave at `x 8..16` — so a
wrong facing is self-evident to the player rather than invisible. `pylonhead`, `bullwheel` and `layshaft`
keep the wildcard until M4's placement half lands.

**And the facing it now demands is the one that was already right.** Rendered both ways at
`docs/agentic/ingest/cablecar/renders/headfacing/{drive,tension}/material/right.png` (crops:
`zoom-right.png` / `zoom-wrong.png`, `zoom-tension-*.png`). At the head's own side the lay shaft, the
`drivehead`'s stub and the gearbox are one unbroken bar from the hub to the gearbox column; a half turn out,
the bar stops in open air short of the gearbox and the stub reappears on the far side of it pointing at
nothing. On the tension station the wrong facing throws the sheave inboard, runs the tie rod off the end of
the crossarm and hangs the counterweight rope outside the guide leg it is supposed to drop down. Nobody
builds that and thinks it looks finished.

**The eight-block spacing rule is retired.** It existed for this bug and nothing else, and the docs, the
handbook and QA 27e all carried it. What is left after the fix, re-derived over every facing and every
separation: two stations can still both validate while overlapping — but only on **post**, **brace** and
**lay shaft** cells. The first two are vanilla logs and `ropeway:brace-*`; the third is ours, and it is worth
naming rather than leaving under "post and brace", because ten overlapping placements per station kind
survive and eight of them share a `ropeway:layshaft-*` cell, the closest at **1.414 blocks** — two crossarms
crossing at one cell, both structures validating off it. It costs nothing mechanically and that is a property
of the block rather than of the geometry: `layshaft.json` declares no `class`, no `entityClass` and no
`entityBehaviors`, so there is no block entity to share and no consumer, speed or resistance can pass through
it. No placement shares a drive housing, a drive shaft, a tension weight, a tension guide or a head — those
are the cells that carry the machine, and those are closed.

**What is left is a build nuisance, not a rule.** A station's machine leg landing in a **plain** neighbour's
post cells wants two different blocks in one cell, so one of the two towers reads incomplete — and an
incomplete tower is un-clickable. It is self-diagnosing rather than silent: the panel counts the missing cell
and the overlay reddens it, and the repair is to move one tower. Seven blocks is the first separation at
which no two towers can want the same cell at all (the largest horizontal `|o₁ − o₂|` over the shipped
offsets across any two facings is exactly **6.000**), so if you want a number that ends the question, that is
the number — but you no longer *need* one, and nothing quietly steals power any more if you ignore it.

**No offset moved, and that is what made it affordable.** Every geometric number the tower was tuned
against — the 5-wide passage, `SpanMath.TowerClearance`, the cabin's 2.463-block turning sweep against post
inner faces at 2.5, the roof-to-crossarm clearance — is a property of the offset list, and all three
footings share one. `AllThreeFootingsShareOneCellList` is what keeps that true, and it is why
`TheCabinFitsThroughTheTower` and `TheCabinCanTurnSquareAtATowerWithoutSweepingThroughAPost` still read
`pylonbase.json` alone and are nonetheless true of a station.

**The offset list was not the whole of it, and the second half was asserted nowhere (2026-08-04).** "Post
inner faces at 2.5" is a fact about the offsets *and* about the blocks standing in them. On a plain tower
the second half is free — the post cells hold vanilla logs, planks and stone, which fill a cell and stop
there. A station fills the `x = +3` column with four shapes of **ours**, and a shape of ours may reach
outside its own cell: `bullwheelrim.json`'s felloe sweeps a whole unit past the cell face in each direction
along the passage, and `drivehead.json`'s gearbox hangs 1.5 units below its own. (This used to cite
`bullwheel.json`'s flared rail mouths, which were the same point until the rail moved onto the path; every
authored rail element is now deleted from both head shapes.) Re-derived rather than assumed:
`drivehousing`, `driveshaft`, `tensionweight` and `tensionguide` are all exactly `x, z ∈ [0, 16]`, including
the drive housing's 45°-turned drum chamfer, whose swept corner stops 0.93 units inside the cell. So a
station's leg presents the same 2.500-block face as a log post, the cabin's 2.463-block sweep keeps its
0.037 of margin, and the bent path's 0.034 / 0.033 / 0.000 penetration numbers hold unchanged for a station.
**`AStationsMachineLegStaysInsideThePostColumn` is the assert that was missing**; it fails with the number
it failed by, and it refuses a tilted box outright rather than mismeasuring one. Rendered with the cabin
parked at 45° in each station's own archway — the deepest pose either column ever sees — at
`docs/agentic/ingest/cablecar/renders/station/corner/{drive45,tension45}/`.

**The bullwheel is joined to something now.** It floated: `driveboss` is a 3×3 stub topping out at y 16
under a rim whose resting bottom is 16.685. It stands in two bearing standards rising out of its own sheave
cheeks, and a `hubaxle` at y 24.7–26.7 — the rim's own rotation centre, so it IS the axle the rim turns
about — runs out to the cell's east face and meets the `layshaft` next door with no seam. One piece of
geometry closes both complaints: the wheel is visibly bolted to the crossarm, and the thing that drives it
is visibly connected to it. Renders: `docs/agentic/ingest/cablecar/renders/station/{drive,tension}/`, 19 and
18 parts, `coplanarOverlapCount: 0`.

**At a TERMINAL the wheel now wraps the rope; everywhere else it still turns beside it (2026-08-04).**
`bullwheelrim.json` sweeps a radius of 9.6504 units about an axle at y 25.7, so a wheel standing over the
tower has its lowest swept point at y 16.05 — the top face of the head block — against a haul rope at y 8,
which is **0.443 blocks of daylight** over the rope's own surface. It cannot simply be lowered there: a
parked cabin's jaw is a clamp closed ON the rope with its top plate 0.15 blocks above the rope's centreline,
and a wheel tangent to that rope from above cannot share a point with the clamp closed round it. What buys
the room is the axis along the line, past the tower. At a tower carrying exactly one span the far side is
**dead** — nothing ever passes there — so `BullwheelRenderer.Offset` carries the wheel one cell out along it
and `WrapDrop` = 0.443 blocks down, its groove lands on the rope's centreline, and `BEPylonBase.WrapPath`
takes the rope round it. 0.146 blocks clear of the parked grip **in plan**, so no vertical margin is
load-bearing. At a station the line runs *through* there is no dead side and no wrap is drawn: a ring dropped
to the rope on either side of such a tower would have a passing cabin's grip inside it for a block of travel,
every trip. `TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach` sweeps the
cabin against both poses and is what replaced `TheTurningWheelStaysAboveTheCellTheCabinPassesThrough`, whose
premise the wrapped pose no longer has. Full derivation:
`docs/agentic/ingest/cablecar/BULLWHEEL-WRAP-SPEC.md`.

**Superseded on two points by the loop, above (2026-08-04).** The wrap was a *sixteen-chord closed ring* and
is now a nine-box **180° arc** that leaves on the return strand — `TheWrapLeavesOnTheReturnStrand`. And the
through-station's wheel no longer "stays where it was": the return strand would run through the middle of it,
so it rises `HoldDownRise` onto that strand. Everything else in this paragraph stands, including the reason
no wrap is drawn there.

**What breaks on a world built on the old scheme.** Towers, spans, the drawn cable, names, the cabin,
calling, riding, the stop key and freight all survive untouched — every one of them is keyed on
`ropeway:pylonbase` footings and their `Spans`. What every existing line loses on load is its **drive** and
its **tensioner**. `ropeway:drivehousing` keeps its code, its class, its block entity and its `MPConsumer`,
so the block and its axle survive intact and simply drive nothing, because no station structure contains
it; the footing panel says *"Nothing on this line is turning"*. `ropeway:tensionweight` keeps its code but
has lost its `entityClass`, and `TensionWeight` is no longer registered, so `ServerChunk` logs *"Failed
loading blockentity TensionWeight … Will discard it"* and drops every one of them — the same benign,
self-completing migration the `PylonHead` → `PylonBase` rename used, with the same property that they all
fail at once and nothing is left holding a reference. The block stays as decoration. A cabin already hanging
on the line stops where it is and is **not** stranded (it is not moving, so the rider steps out — at a
tower, or anywhere with ground under the cabin; see the 2026-08-10 dismount fix at the top of this file), but a
cabin cannot be *placed* on that line until a tension station exists. The repair is two towers of rework per
line: break each end tower's footing, re-place it as the station footing, build the leg. Breaking a footing
refunds its span's rope, so each end costs one re-link, and the old housing and weight are picked up and
reused as station cells. An upgrader was considered and rejected — converting "footing with a housing within
8 blocks" into a drive station means writing the proximity code one more time, in a migration path that runs
once, to save two towers of rework in a pre-release mod.

**One more case, and this list was a case short: a PLAIN tower wearing a decorative bullwheel now loads
INCOMPLETE.** `pylonbase.json`'s centre cell narrowed from `ropeway:@(pylonhead-.*|bullwheel-.*)` to
`ropeway:pylonhead-*`, and the old guide text told players a bullwheel on a plain tower was *"optional and
changes nothing mechanical"* — so this is real saved-world state, not a hypothetical. An incomplete tower is
also an **un-clickable** one (`BlockPylonBase.OnBlockInteractStart` returns on `!be.Validate()` before the
picker or the call), so it stops answering right-clicks entirely until a pylon head goes back in the middle
of the crossarm. It is surfaced rather than silent — the panel counts the missing cell and the overlay
reddens it — and the repair is one block. QA step 0 now carries it.

## Textures and recipes: both halves are CLOSED (2026-08-04)

**The textures were deferred; they are not any more, and nothing was drawn.** Every block used to put all of
its metal on one key, `#metal` — a crossarm flange, a gearbox, a bearing cap, a wheel rim and a counterweight
guide were the same pixels, over `game:block/metal/sheet/iron1` and `game:block/stone/rock/granite1`, neither
of which the mod references any more. They are now eight keys carrying seven sprites, all vanilla, chosen by
measuring the install's 9,587 PNGs rather than by taste. `docs/agentic/ingest/cablecar/PALETTE-SPEC.md` in the
parent repo has the survey, the per-element table and the renders that rejected each alternative.

| key | sprite | what it is |
|---|---|---|
| `girder` | `game:block/metal/riveted/iron1` | the crossarm and every lattice leg. A 6-texel rivet grid, which is exactly one rivet line on the mod's commonest 3-unit face, and no vanilla post family has a regular grid |
| `machine` | `game:block/metal/tarnished/iron` | drums, gearboxes, sheave housings, the wheel rim. The dark step, and the cue that names a drive station at silhouette distance |
| `shaft` | `game:block/metal/plate/steel` | anything that turns, bears or is ridden on. The bright step, deliberately confined to small parts so it reads as a highlight |
| `stone` | `game:block/stone/rock/andesite1` | pads, machine beds and the counterweight mass. The one cool grey in an otherwise warm palette |
| `hull` | `game:block/metal/plate/iron` | the cabin's frame and roof. Smooth on purpose: `cabin.json` authors its body UVs at size/4 and its running gear at 1:1, so one sprite appears at two scales on one entity |
| `metal` | `game:block/metal/plate/iron` | **not any block's decoration.** The station rail and the outriggers, drawn in C# off the *footing* block (`BEPylonBase.cs:603`) and flat-sampled, so only the sprite's centre texel is ever seen. Two 2.2×4-unit bars the length of every span at every tower — the mod's longest continuous geometry, and one step darker than `shaft` because it is a mass, not a highlight |
| `rope` | `game:block/cloth/reedrope` | every rope, drawn or authored, in all six files that declare one. Unchanged |
| `wood` | `game:block/wood/planks/oak1` | the cabin body. Unchanged, and the cabin's warmth is what makes it read as a different kind of object |

**No PNG was authored and both `assets/ropeway/textures/` folders are still empty.** That is the outcome, not
a shortcut: every role resolved inside the vanilla library, including the two hard ones (a structural metal
that survives a 2-unit flange, and a sprite that flat-samples to its own mean). The one thing an authored file
would buy is a grooved sheave face, on a 2-unit-wide element that would show four texels of the groove.

**Which copy the game reads.** Every key is declared twice — on the blocktype and again in the shape's own
`textures` map. **The blocktype is the source of truth**: `BlockTextureAtlasManager.ResolveTextureCodes` only
adds a shape's entry for a key the block did not already declare, so the shape's map is a *shadow copy* and
re-pointing it alone repaints nothing in game. `EveryTextureKeyAShapeUsesIsDeclaredWhereTheGameWillLookForIt`
now pins both halves: every `#key` a face names is declared somewhere the game will look, and where both
places declare a key the two values must match. Two shapes are exceptions and the test knows it —
`shapes/entity/cabin.json`'s map *is* the mapping (the entity declares no textures), and `bullwheelrim.json`
is tesselated by `BEBullwheel` against the `ropeway:bullwheel` **block**, so its own map is never consulted.

**The rim's GENERATOR had parted from the rim, and that is closed too (2026-08-04).** `bullwheelrim.json` is
the one shape in the mod written by a script, and `scripts/gen_bullwheelrim.py` was still emitting the
pre-palette `{"metal": "game:block/metal/sheet/iron1"}` with `#metal` on every face — so running the
documented way to re-author the wheel silently reverted the palette on the one shape the whole loop's
geometry is derived from, and the only thing standing between that and shipping was
`EveryTextureKeyAShapeUsesIsDeclaredWhereTheGameWillLookForIt` in a different lane (*"face key(s) #metal are
declared nowhere"*). The generator now writes `machine` — the key `blocktypes/bullwheel.json` actually
declares, which is the one that matters, since the rim is tesselated against the block — and **regenerating
is a byte-for-byte no-op against the shipped file**, checked by SHA-256 rather than by eye.

**Genuinely still deferred, and the reasoning is the valuable part** (PALETTE-SPEC §2 and §4b):

- **Glazing.** The cabin's windows are voids between mullions; there is no pane element. Adding glass is a
  geometry change plus `renderPass` handling the entity shape does not do — probably permanent, and the void
  reads as an opening in every render anyway.
- **Per-metal variants.** Every ropeway block is iron and steel whatever it was crafted from, and the recipes
  stay `metalplate-*`. The blocker is the **multiblock**, not the blocktype count: `pylonbase.json` matches
  `ropeway:brace-*`, so a `metal` variantgroup lets `brace-north-copper` and `brace-north-iron` both satisfy
  it and a crossarm built from two crafts comes out a patchwork. The count is the second reason — six
  four-sided blocks × 23 metals is **552** blocktypes, plus handbook, creative inventory and three sets of
  wildcards. If metal should ever show, the cheap honest version is **one part**: a runtime texture swap on
  `bullwheelrim` keyed off a metal stored on `BEBullwheel`, exactly `BlockPulverizer`'s cap pattern. One block
  entity, one key, one visible wheel — not variantgroups.

**The recipes were placeholders and are not any more.** All fifteen were audited against the 1.22.1 loader
and priced as one ladder — `docs/agentic/ingest/cablecar/RECIPE-LADDER.md` in the parent repo has the full
working. Three things came out of it:

- **Two real defects.** `drivehead.json` gave `metalplate-*` and `metalbit-*` the same `name`, and
  `RecipeBase.GetNameToCodeMappingForBasicWildcard` ends in a plain `mappings[ingredient.Name] = val`, so
  the last ingredient iterated wins outright — metalbit did, and it carries three variants metalplate does
  not, so three generated recipes asked for `metalplate-blistersteel` and friends and **logged three
  resolve errors at every server start**. `layshaft` and `tensionhead` were the same latent bug, fine only
  by Dictionary enumeration order. And the fifteen files registered **~19,137** grid recipes rather than
  15, held in RAM and serialised to every joining client, because a named wildcard triggers cartesian
  expansion and **not one output used a `{name}` placeholder**. `pylonbase` and `tensionweight` were 9,100
  each. Both are fixed by the same deletion: every `name` is gone from every wildcard in the folder.
- **The price was wrong by about a factor of two.** A minimum line — two stations, **one 30-block span** and
  a cabin, which is the span every figure in this mod is quoted against — was **61 ingots**: four suits of
  plate armour, six anvils, ten water wheels, against a vanilla mechanical tree that is *entirely wooden*
  except the water wheel's two hubs, and a vanilla ceiling of **one** metalplate per machine recipe. It is
  now **30.75** (10 plates + 215 bits): at most one plate per craft, fastenings paid in metal bits at 8 per
  station-machinery slot and 1 on anything a plain tower or a haul rope needs. Marginal tower 4.1 → **1.85 ingots**, 30-block span 16
  vanilla rope → **4**, drive station 32.4 → **15.9**, tension station 26.5 → **12.7**.
- **Two output quantities that did not divide their consumer.** `driveshaft` and `tensionguide` yielded 2
  against legs of **three** cells, so both really cost two crafts; both yield 3 now. `layshaft`'s yield of
  2 against a station's 2 already divided and was left alone.

The shipped ladder is quoted for players in handbook page 50 (*What it costs*) and for testers in
`QA-SCRIPT.md` step 2. **What was NOT done, deliberately:** `itemtypes/haulrope.json`'s `maxstacksize`
16 → 8 (which is what would make `DECISIONS.md` §5's multi-stack-per-span requirement reachable — the
longest legal span is 12 haul rope against a stack of 16, so it never has been), and a tool-durability
`isTool` ingredient on the station machinery, which every vanilla mechanical block spends and none of ours
does. Both are one-token edits, both are outside the recipe files, and neither changes a price.

## The drive came down off the crossarm (2026-08-03, superseded above)

The bullwheel trial was **resolved, and split**. The mechanical consumer became `ropeway:drivehousing`, a
block you built within eight blocks of any tower on the line — usually on the ground, and up beside a
windmill's hub when the mill needed the height; the bullwheel stayed on the crossarm as **decoration that
turns**. The eight blocks are gone and the housing is a station cell; the split is not, and the reasoning
below is why the intake is still not on the crossarm. Design:
[POWER-AND-STORAGE.md](POWER-AND-STORAGE.md).

**Why the trial failed.** Two findings, both from the hostile review
(`docs/agentic/ingest/cablecar/BULLWHEEL-REVIEW.md`):

1. **Getting power four blocks up cost about sixteen vanilla blocks** — a five-log support column, four
   vertical axles, a four-block run back across the crossarm and three angled gears — and the column was
   *mandatory*: vanilla refuses an angled gear beside an unsupported axle, and every block of the tower
   ships `sidesolid: all false`, so the run cannot lean on the tower it serves. In the render the column
   stood as tall as the tower's own posts and right beside them, so the tower read as two piers with a
   lean-to rather than one archway.
2. **The wheel failed at its own job.** Its silhouette measured near-identical to the pylon head's at any
   real distance, and it did not move. What actually marked a drive tower was the scaffolding.

**What each half is now.**

| | the trial | free-standing housing | station cell (now) |
|---|---|---|---|
| the consumer | `ropeway:bullwheel`, 4 blocks up, on a tower cell | `ropeway:drivehousing`, its own block within 8 | `ropeway:drivehousing`, cell [3,0,0] of a drive station |
| mill → line | ~16 blocks, whatever the mill | **3** (housing + 2 axles) for a water wheel or a wooden rotor whose housing rides up to hub height; **5** for a maxed metal rotor | **3** for a water wheel; **~7** for a 3-sail rotor, **~9** for a maxed wooden one, **~14** for a maxed metal one — an axle column down the outside of the drive leg |
| binding | none — the wheel was a tower cell | proximity within 8 blocks, the tension weight's pattern | **membership**: a cell of exactly one station, found at a known offset |
| axle faces | up, down, and both cells **along the line** | horizontal only | horizontal only |
| the bullwheel | the intake | decoration, on no network, and it **turns** | on no network, **turns**, and visibly geared to the intake through the crossarm |

**The middle column bought a cheap drive by paying in binding, and this change reverses the trade
knowingly.** A fixed intake means a descent: a windmill's hub sits 4 blocks up for three sails, 6 for a
maxed wooden five and 11 for a maxed metal ten — vanilla decides that, not us — and the station's tallest
cell is +4, so there is no fixed cell that matches an arbitrary hub. Two things are bought back for it. The
**eight-block ceiling is gone**: a maxed metal rotor could not meet the sphere at all (121 > 64) and needed
a descent regardless, and now no mill has a placement constraint. And the **scaffold is gone** —
`ropeway:driveshaft` is the one block of the tower with `sidesolid: all true`, so a `woodenaxle-ud` column
leans on the drive leg it feeds.

**B1 (build-order dead end) is closed for every drive rather than only the gearless ones.** It was
`BlockAngledGears.TryPlaceBlock` refusing to sit beside an axle that fails `IsAttachedToBlock`, and the wall
a vertical axle column needed was unbuildable against a tower whose every block ships `sidesolid: all
false`. The drive leg is now solid on all six sides (with `sideopaque` still false and `lightAbsorption`
still 0, so it neither hides the frame nor casts shade), which is exactly the wall those columns wanted, at
exactly the column where power is supposed to touch the tower. Two earlier versions of this line were both
wrong in different directions: one said the dead end was gone outright, the next that it was back for the
metal rotor.

**The docs described a windmill that cannot exist (2026-08-03, docs only).** `QA-SCRIPT.md` 27c, handbook
52 step 2, `50-ropeway.json` and the tower guide all told the player to stand a rotor on the ground two
blocks out from a ground-level housing. `BEBehaviorWindmillRotor.OnInteract` refuses a sail when
`obstructed(sailLength + 2)`, and `obstructed(len)` is a flat `(2len+1)²` square standing in the plane the
sails turn in — up and sideways count exactly as much as down, only the centre cell and the four extreme
corners are exempt, and nothing along the axle axis is ever scanned. **A rotor whose hub is one block off
the ground refuses its first sail.** Every one of those pages is rewritten; no code changed, because the
code was right. Two things the fact-finding turned up that the review raising it had also got wrong: the
**water wheel is not the easy ground-level alternative** (it only turns in worldgen-only `rapidwater` — plain
`water` declares no `flowSpeed` at all — and it is a six-stage build in 32 support beams and 96 planks), and
**`obstructed` never scans along the axle axis**, which is exactly why the drive train behind a mill is free
and why the housing can ride up beside the hub without blocking it. Working:
`docs/agentic/ingest/cablecar/HOUSING-FIX-FACTS.md`.

**The stray-footing family has no subject any more.** Two entries lived here: a bare scouting footing
dropped nearer a housing than the line's own footing silently taking the housing off its line, and two
equidistant footings needing `RopewayLine.ComparePos` as a tie-break so the server, every client and a
restart could not disagree about which line a mill was driving. Both were properties of "the nearest footing
within eight blocks". A housing is now a cell of exactly one station and there is nothing to be nearest to,
so `ServingTower`, its line-resolving predicate, the tie-break's only caller and the three tests that pinned
them are all deleted rather than kept passing.

**B2 (an axle on the haul rope) is gone.** The housing connects on horizontal faces only, four blocks below
the rope line, and it is now on the ground at the foot of the leg rather than anywhere within a sphere.

**L2 (no drive in the tower guide) is closed.** `RopewayGuideDialog` turns six blocks — all three footings,
head, brace and bullwheel — and the body walks the whole station build. The machine legs' seven blocks are
named in the text rather than shown: seven more portraits would shrink the row to nothing.

**L6 (the five-vs-four vertical axle count) died with the scaffold.** Nothing counts vertical axles any
more, here or in QA-SCRIPT.

**M4 is ACCEPTED, not fixed, and it is COSMETIC again (2026-08-04).** The bullwheel is still
`HorizontalOrientable`, so one placed while facing the wrong way validates the tower with its throat and
station rails running across the line — and now with its hub axle pointing at the braces instead of at the
lay shaft. `layshaft` inherits the same looseness, so the count is **3**: `pylonhead`, `bullwheel`,
`layshaft`. The fix is unchanged and is still one fix in one place: orient the crossarm cells from the
footing below them, for all of them at once. Marked `ponytail:` in `BEBullwheel`.
**It briefly was not cosmetic, and that half is now closed.** For one round M4 was also the only thing
stopping two stations sharing one machine leg, which is a structural bug and not a wrong-looking wheel.
`drivehead` and `tensionhead` — the only facing-carrying cells of a shared leg — are now narrowed to the
footing's own side in `BEPylonBase.OwnTheHeadCell`, which closes that outright without needing M4's placement
half; see "One machine leg, one station" above. So the count went 2 → 5 → 3, and what is left of M4 is a
wheel that looks wrong and drives correctly.

**Every placed bullwheel costs a server-side block entity that does nothing, and that is accepted.**
`entityClass: "Bullwheel"` is declared for both sides and `BEBullwheel.Initialize` returns at
`api is not ICoreClientAPI` before it registers anything, so on a server each wheel is a block entity that
is saved, loaded and never useful. Unavoidable while the client needs one to hang the renderer on, and
cheap — recorded because the handbook says a bullwheel *"costs the tower nothing"*, which is true of the
sixteen build cells and not quite true of the server.

**Migration, and it is not silent this time either.** A world saved on the trial build has its `MPConsumer`
on the bullwheel. That behaviour is no longer declared, so it is dropped on load (orphan tree attributes are
ignored — no crash), the wheel stops driving and the axle stub beside it will self-break on the next
neighbour change. The line then reports *"Nothing on this line is turning"* and names the drive housing.
Acceptable for a pre-1.0 mod; the rebuild is one block and two axles beside whatever the mill turned out
to be — on the ground for a water wheel, up at the hub for a windmill.

## From the housing-fix review — recorded, not fixed (2026-08-03)

Round-2 adversarial review of the drive housing, the `NoDrive` refusal and the bullwheel renderer
(`docs/agentic/ingest/cablecar/HOUSING-FIX-REVIEW-truth.md`, `-refusal.md`, `-renderer.md`). Its blocker and
its doc concerns were fixed in the same pass; these four were judged not worth the diff. The bare-footing
half of the stray-footing entry **was** fixed in round 2 and has moved up into the section above; the
truncated-boarding entry is round 3's, and is a consequence of a fix rather than a leftover.

**A rider held mid-span cannot re-aim until something turns.** `Hold` clears `departed`, and
`EntityRopewayCabin.MayStart` is `departed || truncated || lineSpeed > 0`, so a mid-span rider on a line that
is whole keeps the stop key only while `departed` or a turning drive holds. An ordinary stall keeps `departed` latched on purpose — that is the
anti-oscillation rule — so a cabin merely becalmed mid-span still takes the stop key with the wind at zero.
What is not covered is a `Hold` firing mid-span *while* the drive is also dead: a blocked span ahead, or a
call abandoned under them, on a line whose mill has stopped too. `RequestStop` then answers `NoDrive` and
the rider cannot point the cabin anywhere until the drive turns again. It is not a strand and it heals
itself: `departed` false means `IsMoving` false means `CanUnmount` true, so the ordinary dismount is open
the whole time — in mid-air with the fall, which is the deal every mid-span exit has — and the key works
again the instant anything turns. The two ways to close it, dropping the `departed` clause for
`RequestStop` alone or refusing `NoDrive` on a held cabin, both add a rule to a state machine that has just
had one deleted, for a state you reach by being unlucky twice.

**A real scrap line nearer a housing than the line it was built for takes it — DEAD, 2026-08-04.** Two
abandoned footings still linked to each other used to win `ServingTower` from the line the housing was built
for, correctly, because "the nearest footing that is on a line" was doing exactly what it was written to do.
There is no `ServingTower`: a housing is a cell of one station and cannot be taken off it by anything
standing nearby. Kept as a row because the symptom it produced — a stopped line beside a turning mill — is
still reachable, but only by an unfinished station now, and the tower's own overlay says which cell.

**On a truncated line the boarding grace latches `departed` with nothing turning, and that is the price of
the `truncated` term.** `MayStart` is `departed || truncated || lineSpeed > 0`, and its third caller is the
boarding grace in `EntityRopewayCabin.ServerTick`. The comment at that call site says what the gate exists to
prevent — boarding a line with no drive at all would otherwise latch `departed` for good, since only `Hold`
clears it and every `Hold` needs the cabin to move — and a truncated line now exempts it. A rider who sits
for the three-second pause on a line with a dark end departs with `lineSpeed` 0: nothing moves (the
`speed <= 0` branch writes `IsMoving = false`, so nobody is trapped and the dismount stays open) but
`IsHauling` is true, and every loaded drive station on that line writes the full `HaulResistance` onto its network
until something turns. **Deliberate.** The `truncated` clause is there because a zero speed on a truncated
chain is not evidence that there is no drive, and it lives in `MayStart` rather than at the two refusal sites
precisely so the rider who sits down and the rider who presses the stop key get the same answer — splitting
them is how these three drifted apart the first time. **It self-heals**: the moment anything on the line
turns, the cabin departs for real and the next `Hold` clears `departed`. QA 27g's closing check is therefore
scoped to a line that is whole, and says so.

**North- and south-facing bullwheels on one line turn opposite ways, and that is accepted.**
`BullwheelRenderer.YawFor` is a full 360° yaw — north 0, east 270, south 180, west 90 — matching
`bullwheel.json`'s own `rotateY` table so the rim's plane tracks the sheave throat on every variant. The
spin axis is not 360°: a 180° yaw maps the mesh axle from +X to −X, so one always-positive `angleRad` reads
clockwise on a north-facing wheel and anticlockwise on a south-facing one.
`ThePylonHeadShapeIsSymmetricAlongTheRopeAxis` already establishes that the two are identical standing
still, so both are ordinary placements on a north-south line and nothing tells a player to prefer either —
which is precisely how two drive towers on one rope end up disagreeing. Purely cosmetic: the rope's own
direction of travel is not modelled anywhere, so both directions are equally arbitrary and only the
disagreement is visible. The one-line collapse is `"east" or "west" => 90f, _ => 0f`, safe because the rim
mesh is itself symmetric under a 180° yaw; not taken, because it trades a real correspondence with the
static shape's yaw table for a tidy-up nobody can falsify. QA 27c-wheel says not to file it.

## The store, deleted (2026-08-03)

The wound tension weight is **gone**, and with it the whole charge/quote/credit apparatus. What ships is a
plain mechanical load: the cabin runs at `k × TrueSpeed` of the drives on its line, with no gate. The design
and the arithmetic are in [POWER-AND-STORAGE.md](POWER-AND-STORAGE.md); this is what it does to the issue
list.

**Why it could go.** The store existed to guarantee a started trip finished, because a cabin stopped
mid-span was a trap. Once nobody is trapped, "it stopped because the wind stopped" is ordinary machine
behaviour. **Corrected 2026-08-10:** this used to credit the bail-out and *"a stopped cabin lets them step
straight out (`IsMoving` false)"*, and that second clause was the defect at the top of this file rather than
a feature — a stall over a valley is not an arrival. What carries the argument is that a stalled cabin keeps
`departed` and `Travelled` and **finishes the trip by itself** when the network turns, so the tick guarantees
for free what the store was charging for. See POWER-AND-STORAGE.md.

**Closed outright by the deletion** — every one of these was a property of the store, the quote or the
weight's persisted binding, and none of them exists to be fixed any more:

| was | what it was |
|---|---|
| **F1** (blocker) | A 288-block line climbing 57 blocks was permanently unrunnable at a full store, told to wait for wind. No quote, no capacity, no dead lines. |
| **F2** | Pressing the stop key after departure charged twice. Nothing is charged. |
| **F3** | Recovery from a mid-span hold cost a fresh full quote. Same. |
| **F4** | Breaking a weight's anchor tower orphaned it with no re-bind. Nothing is bound, and the reason is now stronger than "proximity at lookup time": the weight is a **cell of a station**, so breaking the tower breaks the structure that contained it and the answer changes with the world rather than needing repair. |
| **F6** | Which of two merged weights was live came from dictionary order. There is no "live" weight and no dictionary; a line has a completed tension station on its own chain or it does not. |
| **F7** | A weight placed by schematic or worldedit was permanently orphaned. `Bind` is gone and so is the placement rule that replaced it — a schematic that lays down a whole station simply works, and one that lays down a lone weight produces a lone weight. |
| **F8** | Charge was only persisted on a 1/32 step boundary. There is no charge. |
| **F9** | `Wind`'s `dt` was unclamped. There is no `Wind`. |
| **F5** | "This line has no tensioner" could lie under truncation. **Narrowed twice and still open, 2026-08-04.** The question is now `IsTensioner && StructureComplete` over `line.Towers`, so it is asked out of the *same table* `WalkChain` walks — but a version of this row claimed that made "no tensioner" and `line.Truncated` **coincide exactly**, and it does not. `StructureComplete` is fifteen `GetBlockRaw` reads and `BlockAccessorRelaxed.GetBlockId` returns 0 — air — for an unloaded chunk, so a *loaded* footing whose own leg is three blocks away across an unloaded chunk boundary reads incomplete while `MarkLoadedEnds`, which only inspects the two ends of the walked chain, sees nothing wrong. Same residual band `DriveSpeedOn`'s comment has always been honest about, narrowed from eight blocks to three. **What is closed is the LIE on a long line**: `TryPlaceCabin` now branches on `line.Truncated` **before** the tensioner refusal and sends `err-line-truncated-link`, so the player standing at the drive end of a 320-block line with a perfectly good tension station at the far end is no longer told to go and build one. What is left underneath is the three-block residue, where the player is standing at the tower and its own overlay names the missing cell. Still asked at cabin **placement** and on the block-info panel rather than at every departure. The line that used to close this row outright (`maxLineLength` 320 < `MaxChunkRadius` 384) is **arithmetically wrong**: the stock loaded window is 256 blocks, not 384. |
| **F10** | The weight was a 3-block shape in a 1-block cell with no headroom check. **CLOSED, 2026-08-04.** The shape collapsed to five elements inside its own cell, and the three cells of `ropeway:tensionguide` above it are structure the station's multiblock check requires — so the headroom is checked by construction rather than not at all. `TheHangingMassStaysInsideTheGuideItHangsIn` now asserts every element stays under y 16. |
| **F11–F14** | Docs. QA step 27 is new, this file and the handbook are rewritten, and handbook 52 no longer recommends a flywheel that vanilla does not have. |

**New, and accepted:** a cabin can now stop mid-span because the drive stopped, and an *empty* one called
across a line whose mill is too small will sit there until the player builds a bigger one. That is the
design — a quern does the same — and the tower's block-info panel names it: what the line is turning at, and
what that comes to in blocks a second. The failure mode the old design had instead was a full gauge beside a
refusal telling the player to wait for wind that would never be enough.

**Not a regression, worth knowing:** the load is keyed on the cabin *trying* to move
(`EntityRopewayCabin.IsHauling`), never on whether it is moving. Keying it on real motion oscillates at
1 Hz — the load is what stalls the network, so dropping it on a stall restarts the cabin, which reapplies it.

## The truncated-line class (R1–R4)

All four share one root cause: **the cabin's position is a `Travelled` scalar along a line whose
identity, length and canonical direction are derived from which chunks happen to be loaded.** Three
rounds of patching this produced a new defect each time. See [CABIN-MOTION-REDESIGN.md](CABIN-MOTION-REDESIGN.md)
for the fix that removes the class rather than compensating for it.

**Mitigated, and less than the arithmetic here used to claim.** `maxLineLength` was reduced 512 → 320
(`blocktypes/pylonbase.json`). This section said that put a whole line inside the server's default
`MaxChunkRadius` of 384 and made R1–R4 unreachable on a stock server. **It does not.** `MaxChunkRadius` 12
(`ServerConfig.cs:925`) is a *cap*: `ServerMain.GetAllowedChunkRadius` (`:2527`) returns
`min(MaxChunkRadius, ceil(viewDistance / 32))`, the shipped `viewDistance` is 256 (`ClientSettings.cs:1958`),
and `ServerSystemUnloadChunks` (`:597`, `:734`) unloads everything outside exactly that radius. So the stock
window is **8 chunks = 256 blocks** against a line that may be **320**, and a rider standing at one end of a
full-length line has the far end unloaded. R1–R4 are **reachable at stock settings on a line built near the
cap**; what makes them rare is that most lines are nowhere near 320 blocks long, and anything under about
256 end to end really is inside one player's window wherever they stand on it. A singleplayer client skips
the cap entirely and gets its own view-distance slider, so raising that above 320 closes them there.
Working: [POWER-AND-STORAGE.md](POWER-AND-STORAGE.md), "The cabin reads live network state".

| id | severity | what happens |
|---|---|---|
| **R1** | HIGH | Once the `LineKey` tower's chunk unloads behind a long ride, `GetOrBuild` returns null and `ServerTick` holds forever. The `DropAndDie` backstop is gated on `LoadedTowers.ContainsKey(LineKey)` — the exact condition that just went false. Rider can only dismount mid-span into open air. Resumption is nondeterministic: another player merely looking at a pylon head can revive the ride via the cache. |
| **R2** | HIGH | When the far chunk loads, the cabin teleports forward and flips to homeward. `ServerTick` parks when `Travelled` is strictly inside the window; a cabin held at `MaxTravel` sits exactly at the edge until `MarkLoadedEnds` widens it, after which the same unchanged position is interior, so `ParkAtNearestEnd` snaps it to the new far edge and `Place()` carries the seated rider, skipping `SegmentClear`. Root cause: the recovery means "not at a tower anchor" but is coded as "not at a window end". |
| **R3** | MED | A rider held at an unproven end cannot ride back. `Outbound` stays true and re-boarding re-clamps and re-holds, so the trip is one-way; the only escape is dismounting and walking. R2's teleport is currently the only thing that flips `Outbound`, so fixing R2 alone makes this a hard dead end — fix them together. |
| **R4** | MED | `EntityRopewayCabin.cs:217` is the one write to `Travelled` that ignores the window. On a truncated line an obstruction in the loaded half of the last segment sets `Travelled = TotalLength`, teleporting the cabin onto the false endpoint where it latches. A `GameMath.Clamp` on that assignment closes it. |

## Independent of the above

| id | severity | what happens |
|---|---|---|
| **R5** | MED | `PickSurvivor`'s "first surviving half" fallback is also reached when the cabin's own half degenerates to a single tower (`FromTowers` needs ≥2). A–B–C–D with the cabin at A, explode B: the cabin re-bases *across the break* onto C–D and the player loses it. Should `DropAndDie`. **`RopewayMathTests.TheSurvivingHalfIsTheOneHoldingTheCabin` currently asserts the wrong behaviour** — fix the test with the code. |
| **R6** | LOW/MED | `RebaseTo` always parks, but extending a line at its far end leaves `Towers[0]` and every `Cumulative[i]` unchanged, so `Travelled` is still valid — `TryLink` teleports a mid-span seated rider to an end tower for nothing. One line: `if (line.Towers[0].Equals(LineKey)) return;`. Related: `UnlinkAll` unseats before re-basing while `TryLink` carries the rider. Only one of those can be the rule; pick one. |
| **R7** | LOW | `EntityRopewayCabin.cs:303` claims "the toast de-dupes on its code". It does not — `HudIngameError` overwrites the text and resets a 5 s timer, and the code is only used for the `ingameerror-<code>` fallback lookup. The one-shot property comes from `Hold()` clearing `departed`. Second provably-false justifying comment found in this file; both were caught by review, so treat confident comments here as unverified until checked. |
| **R8** | LOW | `OnTowerInteract` and `SendCandidates` answer the same click with two different truncation messages, and an empty recall that stops early tells the caller nothing (`NotifyRiders` is a no-op on an empty cabin). |
| **E3** | MED | The one-cabin-per-line guard is beatable across a chunk boundary. `FindCabin` scans `LoadedEntities`; with the existing cabin parked at a far endpoint in an unloaded chunk, the near end still resolves a line and a second cabin spawns. Two cabins then drive one line. Needs persisted line identity, deliberately out of v0.1 scope. |

## From QA round 1 review — recorded, not fixed

Source review of the four fixes above (`docs/agentic/ingest/cablecar/QA-ROUND-1-REVIEW.md`). The blockers
and the trust-boundary hole from that review were fixed in the same pass; these four were judged not worth
the diff before the next play session.

| id | severity | mechanism |
|---|---|---|
| **Q1** | LOW/MED | **"Every row the picker shows must succeed on click" is no longer true**, and it is quoted as load-bearing in three comments (`ScanCandidates`, `SendCandidates`, `TryLink`). Two rows can now error: a `Linked` row on a line someone is riding hits `err-line-in-use`, and *any* row on a tower inside a claim the viewer cannot build in hits `err-no-permission` — `SendCandidates`/`ScanCandidates` never call `Claims.TryAccess`, but `TryLink` and `TryUnlink` both do (now via `MayEdit`). Pre-existing for link rows; the unlink rows inherited it. Fix is either a claim filter in `ScanCandidates` or dropping the contract from the comments. |
| **Q2** | LOW | **`TowerCandidate.RopeCost` carries two different quantities**: `SpanMath.RopeCost` (ceil) on a link row and `SpanMath.RopeRefund` (floor) on a `Linked` row. Both display paths read it correctly today, but a future caller that charges `RopeCost` without checking `Linked` hands out free rope. A second field, or renaming it `Rope`, removes the trap. |
| **Q3** | LOW | **A rename re-tesselates the chunk.** `Rename`'s comment says "MarkDirty without redrawOnClient: … re-tesselating every cable on a rename would be silly", but the unconditional `MarkBlockDirty` at the end of `FromTreeAttributes` queues a re-tesselation on *every* BE sync, rename included. Harmless — one chunk, on a rare action — but the two comments now contradict each other and one will mislead the next reader. |
| **Q4** | LOW | **An unsaved rename is discarded by an unlink click.** `PylonPickerDialog.OnCandidates` resets `nameDraft = packet.FromName ?? ""` on every refresh, and `TryUnlink` ends with `SendCandidateList`. Type a name, click an unlink row before pressing Rename, and the typed text is gone. Related: `OnRenameRequest` only re-sends the list when `Rename` returns true, so a name that sanitises to the current one leaves the field showing the raw text with no feedback. |

## From the calling review — recorded, not fixed

Source review of destination-based cabin calling (`docs/agentic/ingest/cablecar/CALLING-REVIEW.md`). Its
three blockers and the mount race were fixed in the same pass; these five were judged not worth the diff.

| id | severity | mechanism |
|---|---|---|
| **C1** | LOW | **The resume path validates `Travelled` against the loaded window but not `Destination`.** A line that shrank while the cabin was unloaded can leave a destination that now lands mid-span; the cabin halts in mid-air for one tick before the `!departed && !IsAtTower` recovery parks it at an end. Self-correcting and visibly odd. Validating `Destination` with `IsAtTower` on the resume path is the cheap fix. |
| ~~**C2**~~ | — | **FIXED.** `ropeway:cantride-moving` claimed "It stops at the next tower."; it now reads "Wait until it stops at a tower." |
| ~~**C3**~~ | — | **FIXED** by the rider stop key above. BASIC hit this in play before the fix. |
| **C4** | LOW | **A stale `LineKey` falls through to opening the picker** rather than saying anything (`RopewayLinkService.cs:96-105`). Surprising, not silent, and self-healing: the next tick re-bases and the second click calls the cabin. |
| **C5** | — | **Interior towers lost the plain-click picker.** Any tower carrying spans now calls the cabin on a plain right-click; the picker is Ctrl-only there. Deliberate — note it in the release notes. |

### A tower facing the wrong way now parks the cabin across its own rope

`SquareTo` squares the parked cabin to the **tower**, not to the line, and nothing requires a footing to face down the line it carries — the picker will link any tower in range whichever way it points. Under the old leg-bearing law a mis-faced tower at least parked the cabin parallel to the rope; now it parks across it, which looks broken even though the tower is working correctly.

Not fixed. The cheap options, in order of preference: warn (do not refuse) at link time when a tower's `PassageFacing` is far off the span bearing; or say it in the block-info panel; or auto-face the footing to its first span. Any of them is small; none is done.

## Freight (2026-08-03) — what it is, and what it deliberately does not do

Cargo is vanilla's `attachable` + `CollectibleBehaviorHeldBag` + `AttachedContainerWorkspace`, wholesale.
There is no inventory, no dialog and no persistence surface of our own; the only C# is
`EntityRopewayCabin.UnloadCargo`, and it exists solely because vanilla's only unprompted drop is gated on
`EnumDespawnReason.Death` while every way this cabin goes away despawns with `Removed`.

It hangs off **`Entity.Die`, not `DropAndDie`**. An earlier note here claimed all three removal paths funnel
through `DropAndDie` "so this is fixed once and no call site is patched" — true of the *mod's* paths
(`RopewayLinkService.TryUnlink`, `UnlinkAll`, `ServerTick`'s `gone` backstop) and false of the *game's*:
`/entity remove` (`CmdEntity.cs:213`, `:230`, `:727`) and WorldEdit entity removal
(`BlockAccessorRevertable.cs:401`, `:490`) call `Die(Removed)` straight, and `/entity kill` calls
`Die(Death)`. Overriding `Die` is the same number of lines and covers every caller. Admin commands deleting
entities is normal and the cabin item was already lost on those paths, so this is not a regression being
fixed — it is the guard being put where all the callers actually meet.

- **The cargo slots ARE the seats**, hung off `SeatAP` / `Seat2AP` (boat-raft.json's pattern, not the
  sailed boat's separate deck squares). So a loaded bench cannot be sat on — `EntityBehaviorSeatable
  .CanSitOn` refuses any seat whose attachable slot holds something that is neither a saddle nor a thing
  declaring a `seatConfig`. **Two loads, or one load and one passenger, never two of each.** That is the
  design, not a limit waiting to be raised: freight competing with people is what the real machines did.
  It is also what keeps `selectionBoxes` and `wearableSlots` index-aligned, which is a hard requirement —
  `AttachableInteractionHelp` subscripts `wearableSlots` with the *selection box* index, so a divergent
  pair of lists throws while you merely look at the cabin. boat-raft.json:84 warns modders about this in
  as many words.
- **Baskets and chests — not crates.** `["basket", "chest"]` is vanilla's own cargo list minus the crate:
  boat-sailed.json:143 and :178, the two deck squares that carry no `ropetiepost`, read
  `["seat", "chest", "basket", "crate"]`, and this cabin's benches are those squares. The **crate** came off
  on 2026-08-03 on a *verb* argument, and that one stands: `BlockCrate` does not carry
  `CollectibleBehaviorHeldBag`; it carries `CollectibleBehaviorBoatableCrate`, which overrides `OnInteract`
  outright and never calls `base`, so **a crate on a mount has no dialog at all**. Plain right-click takes
  one item out; shift puts one in; Ctrl + right-click empties the first stack **and detaches the emptied
  crate in the same click**; sprint does nothing. One container verb, one true line of interaction help.
  The **chest** was off the list too until 2026-08-03, on the capacity argument "16 mixed slots against a
  basket's 8, and a gondola is not a warehouse". That was taste written up as a rule and an unrequested
  deviation from vanilla, so it is gone. A chest carries the same `BoatableGenericTypedContainer` a basket
  does — both subclass `HeldBag`, both override only `GetQuantitySlots` — so both answer the same verb and
  open the same floaty slot grid. Slot counts are the container's business.
  Category codes, read off the blocktypes rather than assumed: `"crate"` is hardcoded in
  `BlockCrate.GetCategoryCode`; `"basket"` is `reedchest.json`'s `attachableCategoryCode` for reed, papyrus
  and vine (`aged`/`aged2` are `null` there and do not attach at all); `"chest"` is declared **nowhere** —
  `chest.json` has no `attachableCategoryCode` key, so `BlockGenericTypedContainer.GetCategoryCode` falls
  through to its `AsString("chest")` default.
- **A trunk chest attaches and then does nothing, and that is vanilla's behaviour, not ours.**
  `chest-trunk` is a `BlockGenericTypedContainer` subclass with no `attachableCategoryCode`, so it reports
  `"chest"` and passes the category filter — but its `behaviors` list has no
  `BoatableGenericTypedContainer`, so it has no `IHeldBag` and a plain right-click on it opens nothing. It
  attaches, blocks the bench, and comes straight back off with Ctrl. Vanilla's sailed boat accepts it on
  exactly the same terms; filtering it out here would mean inventing a rule vanilla does not have, so it is
  recorded rather than fixed.
- **`dropContentsOnDeath` is deliberately absent**, and its assertion is now that it stays absent. It was
  the one real dupe vector: on `Die(Death)` vanilla runs `EntityBehaviorContainer.OnEntityDeath` (drops the
  container itemstack **with its `backpack` tree intact**) *and* `CollectibleBehaviorHeldBag.OnEntityDespawn`
  (spawns every content stack loose). Vanilla escapes because placing a container discards the tree
  (`BlockEntityGenericTypedContainer.OnBlockPlaced`) — but re-attaching that container to another cabin or a
  boat keeps it, and the goods then exist twice. `EntityRopewayCabin.Die` unloads on **every** reason,
  `Death` included, so the flag bought nothing the unload path did not already do.
- **Cargo spills on teardown; it does not ride inside the cabin item.** Handing back a *loaded* container
  would be a silent destroyer: `BlockEntityGenericTypedContainer.OnBlockPlaced` reads only `type` and
  `isPerPlayer` off the placed stack and then calls `base.OnBlockPlaced(null)`, so the `backpack` tree
  goes in the bin the moment the player puts the container down. Vanilla never meets this because
  `OnTryDetach` refuses to let a player pull a loaded container off a mount at all — a guard the unload path
  does not route through. So `UnloadCargo` hands out the goods, clears the container, hands out the
  emptied container, and only then clears the slot. Player inventory first, ground under the cabin
  otherwise. Nothing is destroyed, but taking down a loaded line does leave a pile.
- **Not done on purpose: cargo weight.** A loaded cabin still runs exactly as fast as an empty one.
  Vanilla has no precedent to copy — `EntityBoat.SpeedMultiplier` is a constant from JSON and
  `EntityProperties.Weight` is a static type property nothing sums cargo into — so any load effect is our
  invention with no calibration to inherit. What the power redesign gave it is a **home**:
  `RopewayPower.Resistance(hauling, climb, cargo)` already takes the term and the cabin passes 0, so when
  weight lands it lands in one function rather than being invented twice.
- **An empty bench still boards you on a plain right-click**, via `emptyInteractPassThrough`. With
  `interactMountAnySeat` on, so does clicking the cabin body — and that now skips loaded benches, which
  is why boarding a half-loaded cabin always lands you on the free one.
- **First asset-side user of the plain `attachable` key.** It is registered at `SurvivalCoreSystem.cs:903`
  but every vanilla entity JSON takes the `rideableaccessories` subclass instead. The subclass provably
  adds only `EntityBehaviorRideable` gating this cabin does not have, so the risk is low — but it is a
  smoke-test line (QA step 26a), not an assumption.
- **Teardown closes the dialog before it empties the slots**, and the order is not cosmetic. Vanilla's
  despawn fan-out (`EntityBehaviorAttachable.OnEntityDespawn:411-428`) dereferences `slot.Itemstack`, so a
  slot we have already nulled is skipped entirely — `CollectibleBehaviorHeldBag.OnEntityDespawn` never runs,
  `AttachedContainerWorkspace.OnDespawn` never runs, the server's `wrapperInv` is never
  `CloseInventoryAndSync`'d and leaks in `player.InventoryManager.OpenedInventories` for the rest of the
  session, and the client's dialog sits open over a removed entity until the player walks out of range. So
  `UnloadCargo` calls the per-slot `IAttachedInteractions.OnEntityDespawn` first. Not `OnDetached`, which is
  what vanilla's own detach path uses — it does `(byEntity as EntityPlayer).Player` and this path has no
  player at all. **Not a dupe either way:** a stale dialog cannot move items, because
  `OnReceivedClientPacket:435-451` only dispatches on a non-null `Itemstack`.
- **Nothing gates cargo on the cabin moving, and that is recorded rather than fixed.**
  `RopewayCabinSeat.CanUnmount` refuses while the cabin moves; attach, detach and opening a container have no
  equivalent, so a player at a tower can strip a bench off a cabin passing through, or load one, from the
  ground. Riding-and-opening is intended (QA 26d) and is the case a naive `IsMoving` guard would also break,
  so the gate would have to tell a rider from an outsider — more code and a second rule to explain than a
  case where nothing is lost and the dialog self-closes on range. Revisit if a moving cabin ever becomes
  reachable for longer than it is now.
- **Perishables do not tick in transit**, exactly as in a vanilla boat's storage. `InWorldContainer.OnTick`
  is never invoked from `EntityBehaviorContainer`, and the behavior's own inventory holds the container
  *item*, not the food. Do not "fix" it.

## Deliberate behaviour worth knowing

- **Reversing from inside costs presses.** The stop key steps one tower at a time and only wraps at the end
  of the line, so on a five-tower line a rider parked at tower 2 facing tower 0 needs three presses to select
  tower 3. Deliberate: one key with one meaning beats two keys or a dialog, every press names its tower in
  chat, and the alternative — a "reverse" key — is a second motion concept for a case that is not the common
  one. A picker dialog is the upgrade if long lines make this tedious.
- **The stop key departs immediately.** Pressing it while parked with a rider aboard skips the three-second
  boarding grace. That is what the rider asked for; the grace only exists for someone who boards and does
  nothing.
- **The cabin's roof clearance is 0.25 blocks, not 0.3125.** The crossarm's foot plate reaches the block
  boundary so it lands flat on the posts, which brought the crossarm underside down by 1/16. The sway
  animation (±2.5°) eats roughly another 1/16 at the ends of the swing. Still a visible gap, and
  `gen_manifests.py` asserts the joint and the clearance together so they cannot drift apart.
- **Ctrl + right-click is the picker.** A plain right-click on an end station calls the cabin home and
  stops there, which made naming and unlinking unreachable on exactly the tower a player most wants to
  name. Sneak + right-click is still the guide; the plain click still calls the cabin.
- **Link, unlink and rename packets are proximity-gated.** `RopewayLinkService.MayEdit` is the one guard
  for all three: the clicked tower must be inside the sender's `PickingRange + 3`, and both it and any span
  peer must pass `Claims.TryAccess`. Unlink is destructive *and* pays out rope, so a forgeable packet that
  reached any loaded tower in the world was the one worth closing.
- **A rider unseated mid-span by an explosion now falls.** The pre-fix behaviour put them at a tower,
  but that was a rider teleport (F3). Falling was chosen over teleporting; it is not an accident. It stayed
  the choice through the 2026-08-10 dismount fix: `UnlinkAll` clears the riders past the new refusal rather
  than keeping them aboard, because the alternative is the teleport again, or a rider mounted to a cabin
  that is about to despawn.
- **The bail-out clearance rides in the same packet as the unmount, on the rider's own tree.** `CanUnmount`
  refuses while the cabin moves — and, since 2026-08-10, while there is nothing under it either; a rider who
  holds sneak for `EntityRopewayCabin.BailHoldSeconds` gets out
  anyway. Every client answers the `mountedOn` removal by calling `TryUnmount` — and so `CanUnmount` —
  exactly once, from a listener that never fires again, and a client that says no there keeps the rider
  drawn inside a cabin they have already left for the rest of the session. So the clearance
  (`RopewayCabinSeat.BailKey`) is set on the **rider's** `WatchedAttributes` immediately before
  `TryUnmount`, which ends in `RemoveAttribute("mountedOn")` on that same tree and therefore calls
  `MarkAllDirty`: one full update carries both, and there is no ordering left to get wrong. *Publishing it
  a tick early on the cabin does not work.* Attributes flush every 0.2 s rather than per tick
  (`PhysicsManager.cs:313`), so the two changes land in one flush about five times in six; the cabin's is a
  *partial* update against the rider's *full* one, and `ClientSystemEntities` applies every full update
  before any partial — the permission arrived after the removal it authorised. For the same flush reason
  the clearance cannot be cleared by the tick that spends it (it would never reach the wire): it is retired
  in `RopewayCabinSeat.DidMount`, and immediately on a jump that did not happen.
- **The bail-out hold is edge-triggered, not level-triggered.** `EntityAgent.TryMount` copies the boarder's
  live control flags into the seat before `Passenger` is set, so a player who crouch-walks aboard has the
  seat's `Sneak` already true, and the false→true handler that is the *only* advertisement of the bail-out
  has already fired into a null passenger. Counting the held flag would eject them two seconds after
  departure having pressed nothing and read nothing — easier to hit by accident than on purpose. So
  `HoldSneak` counts nothing until that rider has been seen *not* sneaking while the cabin moves.
- **`PositionBeforeFalling` is re-datumed on every cabin dismount**, not only on the bail-out. A mounted
  player never touches the ground (`EntityBehaviorPlayerPhysics` forces `OnGround = false`), so
  `PositionBeforeFalling` still names the platform they boarded at and `Entity.OnFallToGround` bills the
  drop from there. Left alone, riding a line downhill and stepping out at the bottom charged the rider fall
  damage for the whole descent; the bail-out is only what made it visible. Fixed in
  `RopewayCabinSeat.DidUnmount`, which is the one funnel every dismount path goes through.
- **The cable is one mesh per block entity**, so a long span disappears when its own chunk leaves the
  view frustum even while the cable is still on screen. Per-chunk segments are the fix if it reads badly.
- **Unlinking is not offered on a truncated line.** `SendCandidates` still refuses to open the picker when
  part of the line is unloaded, because the link rows would be unprovable. That also takes the unlink rows
  with it. Breaking the footing still works, so this is an inconvenience rather than a trap.
- **The cable is straight between towers, not sagging.** The cabin travels the chord — bent at the towers,
  and the rope is bent with it — and `IsSpanClear` certifies that corridor; a drawn catenary would be a
  cable that lies about where the cabin goes. Sag needs the cabin to sag too.
- **The cabin's ground speed varies by roughly ±12% through a 90° corner, and its nose wags.** Both fall out
  of the bend and neither is a defect. `Travelled` is arclength of the **chord**, not of the bent path, so a
  cabin covering equal `Travelled` per tick covers slightly more ground where the curve is longest; forward
  progress is `cos(turn/2)` at worst and never negative. And `BendSlope` reaches −1/3 two thirds of the way
  through the window, so the heading **overshoots the leg bearing by 12.1°** at a 90° corner (7.1° at 45°)
  before turning into it — the nose swings out and comes back, on both sides of every corner. It is inherent
  to `s(1−s)²`; a curve without it is a curve that does not pass through the tower.
- **Span ends are not clearance-checked.** `TrimForTowers` skips 4 blocks at each end so a tower's own
  posts don't block its own line, so an obstruction inside those end zones goes undetected.
- **Metal cost — CLOSED (2026-08-04).** The restructure took it from two gantries at roughly 2.5 plates
  down to one 5-wide crossarm at 4 braces = **1 metal plate** plus the sheave. The station-rail widening
  then put it back up to a 7-wide crossarm: 6 braces, two crafts, **2 metal plates per tower**. The recipe
  pass raised `brace.json`'s yield 4 → 8, so a tower's seven braces (six on the crossarm, one inside the
  head) are **one craft with one over** and the marginal tower is back to **1.85 ingots**. See the
  station-rail section at the top of this file.
