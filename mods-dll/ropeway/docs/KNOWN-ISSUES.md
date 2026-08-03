# Ropeway v0.1 — known issues

State: build green, 80 ropeway tests passing. Everything in the tables below was found by reading code,
not by playing — none of *it* has been observed in game.

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
| **The crossarm did not meet the posts** — a visible step where a narrow metal bracket sat on a full-width log, with the log's whole 16×16 top face on show around it. | The brace's beam was z[5,11] and its flanges z[4,12], bottoming out at y = 1/16 — a 6-wide bracket floating one pixel above a 16-wide post. | One shape, no new block variant. The brace grew a **foot plate**, `[0,0,0]–[16,2,16]`, reaching the block boundary so it lands flat on the log; its flanges start at y = 2 instead of y = 1 so nothing is coplanar with it. The pylon head grew the matching `footwest` / `footeast` stubs at x 0–5 and 11–16, leaving the sheave throat (x 5–11) clear for the mast, so the crossarm reads as one continuous girder across all five cells. Weighed against a separate end-piece block: the plate reads as a girder's bottom flange over the three interior cells and as a bearing plate over the two on posts, which is acceptable in both places, and a new block is a real cost. **It cost the cabin 1/16 of roof clearance** — 0.3125 → 0.25 — because the crossarm underside came down to the block boundary. `gen_manifests.py` now asserts both numbers together (`crossarm foot on the post top = 0`, `cabin roof under the crossarm = 4`), so they cannot drift apart silently. |
| **Only logs, debarked logs and planks were accepted as posts.** | Scope. | `game:@(log-placed-.*\|debarkedlog-.*\|planks-.*\|rock-.*\|cobblestone-.*\|drystone-.*\|rockpolished-.*\|stonebricks-.*)` — wood or dressed stone, so a tower can match what it stands next to. Deliberately **not** widened to slabs, stairs, chiselled blocks or soil: a post is a structural column, and a tower that accepts anything stops reading as a tower. `WildcardUtil` anchors an `@`-pattern as `^…$` (`RegexCache.IsMatch`), so `rock-.*` does not also swallow `crackedrock-*`. `RopewayModSystem.VerifyStructureWildcards` still passes — but note it only tests the whole key, so a dud **alternative** would hide behind the live ones; QA step 7 places one block of each family by hand for that reason. |
| **The rider stood up inside the cabin**, in the default standing idle, instead of sitting on a bench. | The `mountAnimations` map on the cabin entity was **dead JSON**. Only `EntityBoat` and `EntityElevator` read that key (`EntityBoat.cs:87`, `EntityElevator.cs:91`); the `seatable` behavior never looks at it, and `EntityRideableSeat.DidMount` starts `"idle"` on the **cabin**, not on the passenger. `SuggestedAnimation` was permanently null too - it casts the mount to `EntityBehaviorRideable`, which the cabin does not have. So no pose was ever started, and mapping the four keys to `"idle"` was never the cause. | `SeatConfig.Animation` per seat (`sitboatidle`, player.json:509 - the same code the sailed boat's non-controllable bench uses), played by an explicit `Passenger.AnimManager.StartAnimation` in `RopewayCabinSeat.DidMount` and stopped in `DidUnmount` **before** `base`, because `EntitySeat.DidUnmount` nulls `Passenger`. `eyeHeight` 1.4 -> 1.0: at 1.4 the eye sat at 1.0875, inside the roof slab that starts at 0.875. Both attachment points dropped 1/16 to the bench top, the sailed boat's convention. Seats stay `controllable: false` - nothing in the animation path reads it. Guarded by `BothCabinSeatsSitTheRiderWithTheEyeInTheGlazing`, which fails on an empty animation code or an eye outside the glazing band. |
| **You could not see the cabin you were riding in.** | First person is the default and the ride is a view; vanilla's F5 is three stops away and the player has to know to press it. | A second rider hotkey, `ropewayridecam`, default **O** (unbound in vanilla - only I, K, L, O, P, R and U are free, and R is the stop key). Client side only, no packet, no seat change. The camera mode is not a setting: `Camera.CameraMode` and `SetMode` are internal, so it is **read** through `IRenderAPI.CameraType` and **written** by invoking vanilla's own `cyclecamera` hotkey handler - a public field on a public `HotKey` that ignores its argument (`PlayerCamera.cs:132`). Cleaner than the reflection ModDB's `glideview` uses, and needs no extra assembly reference. A 250 ms poll on `MountedOn` catches every dismount path; it restores **only** if the camera is still the mode it set, so pressing F5 mid-ride makes the mod let go rather than fight. Deliberately shipped **without** distance or offset tweaks: each trades a new artefact (lost zoom, desynced crosshair, more first-person snaps at towers) for the one it fixes, and none address the root cause, which is that vanilla's third-person wall check raycasts blocks only and the cabin is an entity. |

## The truncated-line class (R1–R4)

All four share one root cause: **the cabin's position is a `Travelled` scalar along a line whose
identity, length and canonical direction are derived from which chunks happen to be loaded.** Three
rounds of patching this produced a new defect each time. See [CABIN-MOTION-REDESIGN.md](CABIN-MOTION-REDESIGN.md)
for the fix that removes the class rather than compensating for it.

**Mitigated, not fixed.** `maxLineLength` was reduced 512 → 320 (`blocktypes/pylonhead.json`). The
server keeps chunks loaded within `MaxChunkRadius` of a player — default 12 chunks / 384 blocks
(`ServerConfig.cs:925`), and `ServerMain.cs:789` only ever raises it. Chain length upper-bounds the
straight-line distance between any two towers, so on a line under that radius no tower can unload while
a rider is anywhere on it. **This makes R1–R4 unreachable under default server config.** They return if
`maxLineLength` is raised, or if a server sets `MaxChunkRadius` below 10.

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
- **The cable is one mesh per block entity**, so a long span disappears when its own chunk leaves the
  view frustum even while the cable is still on screen. Per-chunk segments are the fix if it reads badly.
- **Unlinking is not offered on a truncated line.** `SendCandidates` still refuses to open the picker when
  part of the line is unloaded, because the link rows would be unprovable. That also takes the unlink rows
  with it. Breaking the footing still works, so this is an inconvenience rather than a trap.
- **The cable is straight, not sagging.** The cabin travels the straight chord and `IsSpanClear`
  certifies a straight corridor; a drawn catenary would be a cable that lies about where the cabin goes.
- **Span ends are not clearance-checked.** `TrimForTowers` skips 4 blocks at each end so a tower's own
  posts don't block its own line, so an obstruction inside those end zones goes undetected.
- **Metal cost.** Closed by the restructure: one 5-wide crossarm is 4 braces (1 iron plate) plus the
  sheave, down from two gantries at roughly 2.5 plates. Inside the "don't gate this behind a bunch of
  metal" target in `DECISIONS.md`.
