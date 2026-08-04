# Ropeway v0.1 — known issues

State: build green, 172 ropeway tests passing — 82 `[Fact]` plus 90 `[InlineData]` across the four files in
`mods-dll/ropeway.Tests`, which is where to re-derive this number rather than trusting the line. (It read
136 for two rounds after the count moved, 147 for two more, and 168 for two more again.) Everything in the tables below was found by
reading code, not by playing — none of *it* has been observed in game.

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

**The metal cost per tower DOUBLES — this is the price BASIC is actually paying.** `recipes/grid/brace.json`
is `stick + metalplate + stick → 4 braces`. A 5-wide crossarm needed 4 braces = **exactly one metal plate**.
A 7-wide crossarm needs 6, which is not divisible by the recipe's yield of 4, so it is **two crafts = two
metal plates** (plus 2 spare braces). Marginal metal per tower therefore goes **1 plate → 2**, not "+2
braces", and across a chained route that is the multiplier `DECISIONS.md` §3's marginal-cheapness rule
exists to protect. A ten-tower route now costs 20 plates of braces instead of 10. Named here because it was
chosen knowingly but had never been stated at its true price anywhere.

**Reverting the widening is a JSON-only change** if that price is too high — but it costs back the 45°/30°
penetration above, which is the honest trade now that the yaw law is not there to be credited for it. In
`pylonbase.json` drop the two `x: ±3, y: 4` brace offsets and move the eight post offsets from ±3 back to
±2. Then move the numbers that quote it — `gen_manifests.py`'s `cells()` and its 17.0-unit roof-to-post
clearance, `SpanMath.TowerClearance`'s note, `MultiblockOffsetsAreTheTowerShellAndNothingElse`, the two
handbook pages, `ropeway:dlg-guide-body`, `README.md` and `QA-SCRIPT.md` steps 5, 7 and 8. No C# behaviour
depends on the width.

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
| **Every reload put the cabin back at the start of the line.** | Two independent teleports, both on the load path, diagnosed in `docs/agentic/ingest/cablecar/RELOAD-DIAGNOSIS.md`. (1) **`WalkChain` canonicalises by the two ends the WALK reached.** At world load the tower chunks register one column at a time, so the walk produces a *prefix* of the line — and a prefix whose far end sorts below `LineKey` **reverses**. `Towers[0]` then stops equalling the cabin's `LineKey`, and `ServerTick`'s re-base branch read that as "the chain re-canonicalised under us" and ran `Hold` → `RebaseTo` → `ParkAtNearestEnd` → `Place`, rewriting `LineKey`, `Travelled` **and** `Pos` from a chain `MarkLoadedEnds` had already flagged `Truncated`. It was the one branch in the tick that treated a truncated line as whole, and it is self-reinforcing: it re-keys onto an *interior* tower, which can never be `Towers[0]` of the finished chain, so it fires again when the last column lands and parks the cabin at `MinTravel` = the start of the line. (2) **`departed` was not persisted.** Restored false, so a cabin saved *in motion* mid-span looked like a cabin stopped in mid-air, and the `!departed && !IsAtTower` recovery parked it at an end. A called trip already survived (through `Destination`); an ordinary ride had nothing to survive on. | (1) One guard, stated once — **a truncated chain's `Towers[0]` is not evidence of anything**. `EntityRopewayCabin.NotReady` is every way there is to be waiting on the world (no line at all; a truncated chain measuring from a different tower; a truncated chain that no longer reaches the cabin), and its single call site at the top of `ServerTick` is the whole recovery: **stand still and write nothing else**. It has to be one predicate rather than three branches, because the first attempt *was* three and the third of them called `Hold` — which clears `departed`, undoing (2) and handing the cabin back to the mid-span park one tick later. `RebaseTo` carries the same rule for the link-service callers as `RebaseMustWait`, plus the clause that matters more than the guard: **hold only while the cabin's key is still on that chain**. `LineKey` is always an end tower, so "the broken tower was the key" is `UnlinkAll`'s *ordinary* case, and refusing to re-key there leaves the cabin keyed to a block `Forget()` is about to remove — `ResolveLine` null forever, and `DropAndDie` unable to fire because it requires `LoadedTowers` to contain `LineKey`. An uncollectable cabin hanging in mid-air with its item destroyed is strictly worse than the teleport, so the guard asks whether there is anything to hold *for*. No delay and nothing to serialise: `MarkLoadedEnds` widens the window by itself and `BEPylonBase.Initialize` drops the cached line, so a genuine re-base still runs on the tick after the last tower registers. (2) `departed` is written in `ToBytes` and read in `FromBytes`, and **only `Hold` may clear it**. A cabin saved mid-span comes back exactly there and carries on in the direction it was going, `lastSegment` still -1 so the span it resumes into is re-checked for clearance. That deliberately includes a cabin saved in motion whose **rider is still offline**: it resumes and runs to the end of the line, because the alternative is to drop the trip and dropping the trip is the bug. `DropGhostPassengers` still unseats a despawned player at a tower, and `RopewayCabinSeat.CanUnmount` keeps whoever reconnects into it aboard until it stops — QA step 19 checks that shape, not the old "parked at an end, empty" one. (3) A **rider is never teleported**, which was claimed before and not true: the re-base branch was already gated on `HasPassenger`, the mid-span park is now gated on it too (every `Hold` lands a cabin in that branch on the next tick, so a blocked span alone reached it), and `TryLink` refuses to merge an occupied line exactly as `TryUnlink` already did. A seated rider's cabin stops where it stands instead — it is not moving, so they can step out, and the stop key aims it at a station and sets it going again, including back the way it came. Guarded by `TheWorldIsNotReadyUntilTheChainCanVouchForWhereTheCabinIs`, `ARebaseWaitsForChunksOnlyWhileTheOldKeyIsStillOnTheChain` and `APartialChainCanCanonicaliseTheOppositeWayFromTheWholeLine`; the tick ordering they feed needs QA 18/18b. |
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
closes the rope round it as a sixteen-chord ring. 0.146 blocks clear of the parked grip **in plan**, so no
vertical margin is load-bearing. At a station the line runs *through* there is no dead side and no wrap is
drawn: a ring dropped to the rope on either side of such a tower would have a passing cabin's grip inside it
for a block of travel, every trip. `TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach` sweeps the
cabin against both poses and is what replaced `TheTurningWheelStaysAboveTheCellTheCabinPassesThrough`, whose
premise the wrapped pose no longer has. Full derivation:
`docs/agentic/ingest/cablecar/BULLWHEEL-WRAP-SPEC.md`.

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
on the line stops where it is and is **not** stranded (`IsMoving` false, so the rider steps out), but a
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

## Deferred on purpose: textures and recipes (2026-08-04)

**Both stations wear borrowed vanilla textures and both stations' recipes are placeholders, and neither is an
oversight.** Every shape shipped this round — `drivestation`, `tensionstation`, `layshaft`, `drivehead`,
`driveshaft`, `tensionhead`, `tensionguide`, and the bullwheel's new fixture — samples exactly three vanilla
sprites: `game:block/metal/sheet/iron1`, `game:block/stone/rock/granite1` and the `game:block/cloth/reedrope`
the cable already uses. Nothing in the mod has a texture of its own. That is because the geometry was the
question this round, and a texture that reads correctly at 16 pixels is a different skill from a shape that
does. The recipes are the same shape of placeholder: they are *balanced* against the tower's existing
cost and they resolve against real vanilla items, but they were written to make the blocks craftable for QA
rather than to say anything about what a drive station ought to be worth.

**Scoped, so it is a decision rather than a shrug.** The author's ask is *"we'll want to texture this at some
point and come up with good recipes"*, and it is its own round: a texture pass wants a palette decision
across all fifteen blocks at once, and a recipe pass wants the whole ladder priced together (a station
footing against a plain one, a drive head against a lay shaft, both against what a windmill costs) rather
than block by block. Doing either piecemeal now would mean doing it twice. **Nothing else in this file is
waiting on it** — no clearance, no test and no behaviour reads a texture or a recipe — so it can land whole,
later, without unpicking anything.

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
mid-span was a trap. The phase-1 bail-out ended that: a rider can always get out, and a stopped cabin lets
them step straight out (`IsMoving` false). Once nobody is trapped, "it stopped because the wind stopped" is
ordinary machine behaviour.

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
  but that was a rider teleport (F3). Falling was chosen over teleporting; it is not an accident.
- **The bail-out clearance rides in the same packet as the unmount, on the rider's own tree.** `CanUnmount`
  refuses while the cabin moves; a rider who holds sneak for `EntityRopewayCabin.BailHoldSeconds` gets out
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
- **Metal cost.** The restructure took it from two gantries at roughly 2.5 plates down to one 5-wide
  crossarm at 4 braces = **1 metal plate** plus the sheave. The station-rail widening then put it back up
  to a 7-wide crossarm: 6 braces, two crafts, **2 metal plates per tower**. Not closed — see the station-rail
  section at the top of this file, which states the doubling against `DECISIONS.md` §3.
