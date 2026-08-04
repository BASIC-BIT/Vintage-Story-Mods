# Ropeway v0.1 — known issues

State: build green, 147 ropeway tests passing — 78 `[Fact]` plus 69 `[InlineData]` across the four files in
`mods-dll/ropeway.Tests`, which is where to re-derive this number rather than trusting the line. (It read
136 for two rounds after the count moved.) Everything in the tables below was found by reading code,
not by playing — none of *it* has been observed in game.

## Station rail (2026-08-03) — what shipped, what was reverted, what it costs

`RAIL-DESIGN.md`'s five-step ladder went in and **step 4 came back out.** Steps 1–3 (the split sheave, the
hanger blade and the jaw, `hangDrop` 2.0 → 2.25, the flared rail drawn on the pylon head's own shape) and
step 5 (the 5-wide passage) are shipped. Step 4, the angle-station yaw law, is **reverted**.

**The causation in the design was backwards, and an earlier version of this section repeated it.** Read
this before re-attempting anything here.

**The 5-wide passage is the fix.** Moving the posts from x ±2 to ±3 is what reduces post penetration:
45° goes **1.000 → 0.033 blocks** and 30° goes **0.450 → 0.000**. That is the whole of the improvement.
It is not "room and light".

**The angle-station yaw law was a regression and is gone.** Holding each tower's own passage axis across
the vertex threw the widening's gain away: 45° went **0.033 → 1.000** and 30° **0.000 → 0.331**. The
mechanism is direct — `RopewayLine.PositionAt` swings the cabin's ORIGIN onto the outgoing leg at the
vertex, so a cabin still holding the incoming axis crab-walks, and a crabbing 4-block cabin sweeps its tail
into the post on the outside of the bend. `DirectionAt` is the plain leg bearing again; `RopewayLine.Facings`,
`SquareHold`, `YawBlend` and the three tests that asserted the law are deleted. The original 0.000-at-every-angle
number came from a model whose cabin ran dead straight through the vertex, which `PositionAt` never does.

**The salvageable half DID ship: a cabin STOPPED at a tower squares up to that tower's passage.**
`EntityRopewayCabin.SquareTo`, gated on `!departed`, which is the exact predicate under which `Travelled`
cannot change. That is the whole difference from the reverted law: the law held the axis across a *window*
around the vertex, **while the cabin was moving**, which is what let `PositionAt` swing the origin out from
under it. Stationary there is no origin motion to crab away from, and a cabin merely *passing* a tower never
reaches the branch at all — a pass-through is byte-identical to the shipped leg bearing, which is why the
penetration numbers above are unchanged. Rotating in place at the tower centre sweeps the cabin's
half-diagonal, √(2.0² + 1.4375²) = **2.463 blocks against post inner faces at 2.5** — 0.037 blocks of margin,
and the 5-wide passage is the only reason it exists at all (at x ±2 it would sweep 0.463 blocks through a
post). `TheCabinCanTurnSquareAtATowerWithoutSweepingThroughAPost` asserts both numbers off the shipped shape
and the shipped multiblock. It is a snap on the server; the cabin's `interpolateposition` behavior eases
`Pos.Yaw` with a time constant of roughly 0.15 s, so the settle reads as about half a second of eased rotation in place (frame-rate dependent), and it rotates back
onto the span as it departs. Renders: `docs/agentic/ingest/cablecar/renders/parked/`.

**A right-angle corner can never be clean, under any yaw law.** The posts flank the passage at tower-local
x = ±3 and a tower facing is one of four cardinals, so at a right angle the outgoing leg *is* the post axis:
the cabin's **origin** travels through the post column. No rotation fixes a translation. This is a permanent
geometric limit of "four facings, straight chords", not a bug and not a tuning problem. The handbook says so
and `QA-SCRIPT.md` step 12b expects it. The only real cures are a diagonal tower facing or refusing sharp
bends at link time; neither is in v0.1.

**Recommended, not built: `TryLink` should WARN (never refuse) on a sharp bend.** After `AddSpan`, a tower
that now carries two spans can have the angle between them measured, and a bend under ~150° gets one chat
line telling the player the cabin will clip a post there. Warn only — refusing would make a legal, buildable
route unbuildable for a cosmetic reason, and players do build ugly corners on purpose. Not implemented here
because it is not the two lines it looks like: an angle helper on `SpanMath`, the check on both ends of the
new span, a lang string, and a test — call it 20 lines across four files. Worth doing next time this file
is opened; the handbook and QA-SCRIPT carry the warning in the meantime.

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

**Cosmetic, at corners:** the drawn rope leaves the sheave along a bearing that runs *into* the crossarm.
The rope sits at scene y 35–37 and the brace beams occupy 30–42, so at a right angle the outgoing rope is
buried inside **three** brace blocks before it clears the tower — it was two before the widening, i.e. the
widening made this one slightly worse. Only visible at sharp corners, where the cabin already clips.

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

## The drive came down off the crossarm (2026-08-03)

The bullwheel trial is **resolved, and split**. The mechanical consumer is now `ropeway:drivehousing`, a
block you build within eight blocks of any tower on the line — usually on the ground, and up beside a
windmill's hub when the mill needs the height; the bullwheel stays on the crossarm as **decoration that
turns**. Design and reasoning: [POWER-AND-STORAGE.md](POWER-AND-STORAGE.md).

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

| | before | now |
|---|---|---|
| the consumer | `ropeway:bullwheel`, 4 blocks up, on a tower cell | `ropeway:drivehousing`, its own block within 8 |
| mill → line | ~16 blocks, whatever the mill | **3** (housing + 2 axles, no gears) for a water wheel or a wooden rotor whose housing rides up to hub height; **5** (2 gears + 3 vertical axles) for a maxed metal rotor |
| binding | none — the wheel was a tower cell | proximity within 8 blocks, the tension weight's pattern |
| axle faces | up, down, and both cells **along the line** | horizontal only |
| the bullwheel | the intake | decoration, on no network, and it **turns** |

**B1 (build-order dead end) is gone for the gearless layouts and back for the metal one.** It was
`BlockAngledGears.TryPlaceBlock` refusing to sit beside an axle that fails `IsAttachedToBlock`. There is no
angled gear in the water-wheel build or in a wooden-rotor build whose housing sits at hub height, and
`BEBehaviorMPAxle.IsAttachedToBlock` passes a ground-level `we` axle on the block below it — the ground. A
maxed metal rotor needs eleven clear blocks under its hub whatever the intake does, so that drive still descends through
two gears and a `woodenaxle-ud` column, and the column still needs a wall beside it because every block of
the tower is `sidesolid: all false`. An earlier version of this line said the dead end was gone outright.

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

**A bare scouting footing can no longer take a housing off its line.** `BEPylonBase.Initialize` registers a
footing in `LoadedTowers` unconditionally — before any completeness check, and whether or not it carries
spans — so a bare one dropped while marking the next tower position used to be a candidate for
`ServingTower`. Put it within 8 blocks of a working housing and nearer to it than the line's own footing and
the housing fell to `IdleResistance` with `Serves(realLine)` false: the line stopped, with the mill visibly
turning three blocks away and the footing panel telling the player to build a drive housing they had already
built. `ServingTower` now filters to footings that **resolve to a line** — `RopewayLine.GetOrBuild` is null
below two towers, which is exactly the test — and `ABareFootingCannotTakeAHousingOffTheLineItDrives` pins it.
The exact tie went the same way: `NearestTower` breaks equal distances on `RopewayLine.ComparePos` rather
than on whichever entry the `Dictionary` yields first, so two equidistant footings resolve identically on the
server, on every client and across a restart. `EquidistantFootingsAreDecidedOnPositionAndNotOnChunkLoadOrder`
is the guard.

**B2 (an axle on the haul rope) is gone.** The housing connects on horizontal faces only, four blocks below
the rope line.

**L2 (no drive in the tower guide) is closed.** `RopewayGuideDialog` turns five blocks now — footing, head,
brace, bullwheel, drive housing — and the body names the drive.

**L6 (the five-vs-four vertical axle count) died with the scaffold.** Nothing counts vertical axles any
more, here or in QA-SCRIPT.

**M4 is ACCEPTED, not fixed.** The bullwheel is still `HorizontalOrientable`, so one placed while facing the
wrong way validates the tower with its throat and station rails running across the line. The pylon head has
carried exactly the same looseness since the pattern was written; the fix is to orient the crossarm's centre
cell from the footing below it for **both** blocks in one place, and a private rule on the decorative half
would leave the bug and add a rule. Marked `ponytail:` in `BEBullwheel`.

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

**A real scrap line nearer a housing than the line it was built for takes it, and correctly.** Two
abandoned footings still linked to each other, standing nearer the housing than the line it was meant to
drive, win `ServingTower`: that is a line, and "the nearest footing that is on a line" is doing exactly what
it was written to do. The bare-footing version of this — a single unlinked footing — is **fixed** and is
recorded above. What is left is the case where the rule is right and the world is wrong, and the fix is to
break the scrap line. Recorded because the symptom is identical to the fixed one: a stopped line beside a
turning mill. Check for a linked pair before filing.

**On a truncated line the boarding grace latches `departed` with nothing turning, and that is the price of
the `truncated` term.** `MayStart` is `departed || truncated || lineSpeed > 0`, and its third caller is the
boarding grace in `EntityRopewayCabin.ServerTick`. The comment at that call site says what the gate exists to
prevent — boarding a line with no drive at all would otherwise latch `departed` for good, since only `Hold`
clears it and every `Hold` needs the cabin to move — and a truncated line now exempts it. A rider who sits
for the three-second pause on a line with a dark end departs with `lineSpeed` 0: nothing moves (the
`speed <= 0` branch writes `IsMoving = false`, so nobody is trapped and the dismount stays open) but
`IsHauling` is true, and every loaded housing on that line writes the full `HaulResistance` onto its network
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
| **F4** | Breaking a weight's anchor tower orphaned it with no re-bind. Nothing is bound: `BETensionWeight.OnLine` asks proximity at lookup time. |
| **F6** | Which of two merged weights was live came from dictionary order. There is no "live" weight; a line has a tensioner or it does not. |
| **F7** | A weight placed by schematic or worldedit was permanently orphaned. `Bind` is gone; placement does nothing but check it is near a tower. |
| **F8** | Charge was only persisted on a 1/32 step boundary. There is no charge. |
| **F9** | `Wind`'s `dt` was unclamped. There is no `Wind`. |
| **F5** | "This line has no tension weight" could lie under truncation. It still resolves through the walked chain, but it is asked at cabin **placement** and on the block-info panel rather than at every departure — one question at build time rather than a gate on every trip. The line that used to close this row outright (`maxLineLength` 320 < `MaxChunkRadius` 384, so a player standing on the line holds all of it) is **arithmetically wrong**: the stock loaded window is 256 blocks, not 384. See the truncated-line section below. It is narrowed to placement, not eliminated. |
| **F10** | The weight is a 3-block shape in a 1-block cell with no headroom check. **Still true**, still cosmetic. |
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
- **The cable is straight, not sagging.** The cabin travels the straight chord and `IsSpanClear`
  certifies a straight corridor; a drawn catenary would be a cable that lies about where the cabin goes.
- **Span ends are not clearance-checked.** `TrimForTowers` skips 4 blocks at each end so a tower's own
  posts don't block its own line, so an obstruction inside those end zones goes undetected.
- **Metal cost.** The restructure took it from two gantries at roughly 2.5 plates down to one 5-wide
  crossarm at 4 braces = **1 metal plate** plus the sheave. The station-rail widening then put it back up
  to a 7-wide crossarm: 6 braces, two crafts, **2 metal plates per tower**. Not closed — see the station-rail
  section at the top of this file, which states the doubling against `DECISIONS.md` §3.
