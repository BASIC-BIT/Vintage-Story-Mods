# Power — design direction

**Status:** built, and rebuilt. The store this file used to describe is **deleted**; what ships is a plain
mechanical load. The storage taxonomy at the bottom is still live, as a note about a block that does not
exist yet.

## The decision

**The ropeway is an ordinary machine on the mechanical network.** The drive turns the haul rope, and the
cabin runs at `k × TrueSpeed` of the drives on its line. There is no store, no charge, no quote, no credit
and no gate. A network that stops is a cabin that stops, exactly as a network that stops is a quern that
stops, and it starts again by itself when the wind comes back.

### Why the store went

It existed to guarantee that a trip which *started* would *finish*, because a cabin stopped mid-span was a
trap: the rider could not get out. **That is no longer true**, and the reason has since been corrected.
Once nobody can be trapped, "the cabin stopped because the wind stopped" is ordinary machine behaviour —
and the entire apparatus built to prevent it (charge arithmetic, capacity, `paidTo` and the `Fare` credit
rule, `Quote`/`TripCost`/`WorstTripCost`, the link-time steepness refusal, and the `NoStore` /
`StoreUnreachable` / `NoPower` / `TooDear` refusal states with their strings, block-info lines and tests)
was dead weight protecting against a problem that had already been solved somewhere else.

**The escape this rests on is not the one written here originally, and the difference matters.** This used
to say *"the bail-out means a rider can always leave, anywhere on the line, and a stopped cabin lets them
step straight out because `IsMoving` is false"*. Both halves are gone: `RopewayCabinSeat.CanUnmount` refuses
the ordinary step out with nothing under the cabin (a `IsMoving`-false stall over a valley used to hand the
rider the whole remaining drop on one tap of sneak), and the bail hold is armed off `IsMoving` too, so it
counts to nothing while stalled. What actually holds the argument up is **the cabin itself**: a stall leaves
`departed` and `Travelled` intact, so the trip resumes by itself when the network turns and finishes at a
tower, where the rider gets out for free. The store guaranteed a trip would finish; so does the tick, for
nothing. See `KNOWN-ISSUES.md`, *"A rider who steps out of a stalled cabin at height"*.

**The link-time steepness *refusal* stays deleted.** `ropeway:span-too-steep` is a chat **warning** about a
different thing entirely — the cabin's roof clipping its own crossarm, geometry, not power — and it never
refuses a link. Nothing about the store came back with it.

It also closes `POWER-REVIEW.md` **F1 through F10** outright — every one of them was a property of the
store, the quote or the weight's persisted binding.

### Why the tension weight was never the battery it was named after

Arithmetic, not taste. The drawn mass is 0.156 m³ of granite — 422 kg — raised 2 m: **8.3 kJ**. Against it,
400 blocks of level travel is roughly **45 kJ** and a 40 m climb about **224 kJ**. Short by 5× and 27×. The
old `capacity: 400` was not a physical quantity at all, it was a game number wearing a physical name, which
is exactly why `Quote` and `maxLineLength` could not be made to agree (F1).

And a real tension weight does not store energy. It keeps the haul rope taut. Two different machines were
wearing one name; they have been separated.

## What the tension weight is now

`ropeway:tensionweight` is a **tensioner**: cell `[3,0,0]` of a `ropeway:tensionstation`, at the foot of that
tower's machine leg. It holds no charge, has no gauge and no capacity, is not a mechanical power node, has
no block class and no block entity, and is not bound to anything.

- **It is a build requirement, not a runtime state**, and the check is at **cabin placement**:
  *a line will not carry a cabin until one of its towers is a completed tension station.* One sentence, told
  once, while the player is building — rather than a refusal state with a message, a toast and a wait
  attached. Break the station afterwards and the cabin keeps running; the tower panel says the tensioner is
  missing.
- **Membership, not proximity (2026-08-04).** The rule was *any loaded weight standing within its own
  `towerRadius` of any tower on the line*, which needed `LoadedWeights`, `Nearest`, `NearAnyTower`, a block
  entity on the weight whose only job was to register itself in that table, and a placement refusal to keep
  the block off a random hillside. `BEPylonBase.HasTensioner` is now `IsTensioner && StructureComplete` over
  `line.Towers` — a walk over a list the caller already holds — and all of that is deleted, along with
  `BETensionWeight.cs` and `BlockTensionWeight.cs` entirely. Three things improve for free.
  **`StructureComplete` gates it**, which proximity could not express at all, so a half-built tension
  station is not a tensioner and the tower's own overlay says which cells are missing. **It is visible**:
  "which tower tensions this line" was previously unanswerable by looking at the world. And **tensioning the
  wrong line by accident is CLOSED** — two lines passing within eight blocks of one weight both used to
  count it. Membership narrowed that to one residue (two tension stations sharing a machine leg, because
  `MultiblockStructure` has no notion of ownership), and `BEPylonBase.OwnTheHeadCell` closed the residue by
  narrowing `ropeway:tensionhead-*` to the footing's own side before `InitForUse`. Exactly the drive's
  residue below and exactly the same fix; both are derived in
  [KNOWN-ISSUES.md](KNOWN-ISSUES.md) under "One machine leg, one station". Review findings **F4, F6 and
  F7** stay closed and their reason gets stronger — "it is a cell of a structure" rather than "it is asked
  at lookup time".
- **The mass moved into the shape, and then into one cell.** It used to be chunk mesh drawn by the block
  entity at a height that *was* the charge gauge. It is a static element hanging near the bottom of its
  guide, which is where a rope tensioner rests. The guide rails and the rod above it reached three cells up
  out of a one-cell block with nothing checking the headroom (**F10**); they are now
  `ropeway:tensionguide`, three real cells the station's multiblock check requires, and the head beam is
  `ropeway:tensionhead` on the crossarm end. So the headroom is checked by construction, and the rope leaves
  the head sheave on tangents that land on the lay shaft at one end and the guide's own hanger column at the
  other — which is what makes the counterweight read as pulling the return wheel rather than as a rock
  parked beside a tower.

## The speed ladder, read from vanilla

A rotor settles where its torque meets the load. `BEBehaviorMPRotor.GetTorque` supplies
`(capableSpeed − speed) × TorqueFactor` against network resistance, so with our resistance `R` the network
settles at

```
s* = TargetSpeed − R / T          TargetSpeed = min(0.6, windSpeed)      T = sails/4 × powerMul
```

`powerMul` is 1 for the wood rotor (max 5 sails) and 1.25 for the metal one (max 10), from
`survival/blocktypes/mechanics/windmillrotor.json`.

**That expression is nearly flat for small `R`** — which is what the old design got wrong. At the old
`WindingResistance` of 0.12, three sails gave 0.44 and ten gave 0.56: a 22% spread, invisible in play, and a
mod where building a bigger mill did nothing you could feel. The lever is `R`, so `R` is now large:
`HaulResistance = 0.30`, three times a quern's 0.1 and 40% of a maxed wood mill's 0.75 stall budget.

At `k = BlocksPerNetworkSpeed = 6.0`, in good wind, on the level:

| drive | T | s* | cabin |
|---|---|---|---|
| 2-sail wood | 0.50 | 0 | **stalls** |
| 3-sail wood | 0.75 | 0.20 | **1.2 blocks/s** — a walk |
| 4-sail wood | 1.00 | 0.30 | 1.8 |
| 5-sail wood (maxed) | 1.25 | 0.36 | **2.2** — the old fixed speed |
| 5-sail metal | 1.56 | 0.41 | 2.4 |
| 10-sail metal (maxed) | 3.13 | 0.50 | **3.0** — a run |

A 2.5× spread across the drives a player will actually build, with a stall at the bottom, so "build a bigger
mill" is a legible answer to a slow line. `RopewayPowerTests.ABiggerDriveIsAVisiblyFasterCabin` asserts both
the separation and the absolute rungs.

**"Good wind" means `windSpeed >= 0.6`, and that is a saturation rather than a setting.** `min(0.6, w)`
clips, so every rung above is the same number for any wind from 0.6 up — which is what makes the ladder a
property of the mill and reproducible in QA at all. It is not what `/weather setw strongbreeze` gives you:
that pattern's `strength` is `{ avg: 0.6, var: 0.15 }` drawn **uniform on [0.45, 0.75]** when the pattern
begins, plus a simplex term clamped to [0, 1] with amplitudes summing to 0.85 (`WindPattern.OnBeginUse` /
`.Update`, `NatFloat`'s default distribution being UNIFORM). Below 0.6 every rung scales down together and
the stall creeps up the table — a 3-sail mill needs `w > R/T = 0.3/0.75 = 0.4` to shift the cabin at all.
The pattern also re-rolls itself: `WeatherSystemBase.autoChangePatterns` defaults **true** and
`WeatherSimulationRegion` picks uniformly over all five patterns once `durationHours` (avg 6 for
strongbreeze) elapses, ignoring their weights. `/weather acp false` is what holds it, and QA 11 and 27c both
say so.

## Resistance reflects load

`RopewayPower.Resistance(hauling, climb, cargo)` is the whole model:

```
idle                                        when nothing is hauling
HaulResistance × (1 + 0.5·max(0,climb) + cargo)    when a cabin is trying to move
```

- **`hauling` is the cabin *trying* to move, not moving.** The load is what slows the network, so keying it
  on real motion is a feedback loop: a weak mill stalls, the load vanishes, the network speeds up, the cabin
  starts, and it stalls again a tick later. `EntityRopewayCabin.IsHauling` is `departed`.
- **`climb`** is the vertical component of the unit direction of travel, so 0.5 is a 30° span. At half
  weight that is +25% load — a maxed wood mill goes 2.2 → 1.8 blocks/s — and a 45° span +35% → 1.6. Bounded
  deliberately: a mill that hauls on the level must still climb, slowly. A cabin stuck halfway up a hill is a
  bug report; a cabin crawling up one is a machine working.
- **`cargo`** is extra load as a fraction of the empty cabin's, and is passed 0. There is no cargo-weight
  rule yet and this does not invent one — it gives the one that eventually lands exactly one home, so it
  cannot be invented separately in the cabin and in the tower.
- Descending is clamped to level. Gravity assisting a descent is real, but a negative resistance is a
  ropeway that drives the network.

**Every drive station declares the same load**, not a share of it. A share would have to be divided by how
many other drives are powered — a number that changes when somebody walks away and a chunk unloads, which is
the coupling this design refuses to have. Each drive pulls its own weight and their speeds add.

### A SHAFT declares no climb, and the term is cancelled rather than discounted (2026-08-10)

`BEPylonBase.DeclareLoad` passes `climb = 0` on a shaft line, and the argument is one sentence:
`ClimbLoad · climb` is the cost of lifting **the car's own mass** up the grade, and a counterweight is
precisely that mass hung on the other strand. What is left for the drive to lift is the **imbalance**, which
is `cargo`, which is 0. So there is no new constant, no `CounterweightRelief`, and no ropeway span's number
moves: on every line that is not a shaft the climb term is untouched and fully legible, which is exactly the
objection a global discount would have earned. The player has built the thing that removes it.

Two consequences fall out and neither needs a case:

- **Descending already costs the level figure**, because `Resistance` clamps a negative climb to zero. So the
  counterweight makes going *up* cost what coming *down* already cost.
- **An over-counterweight would be free power** — a heavier weight lowers the empty car by itself — and the
  model cannot express that, for the reason directly above: a negative resistance is a ropeway that drives
  the network. So the counterweight is exactly matched and never over.

**The ladder, in blocks of RISE per second.** On a vertical leg blocks along the line *are* blocks of rise,
so no `sin θ` appears anywhere:

| drive | torque factor | counterweighted, R = 0.300 | bare, R = 0.450 |
|---|---|---|---|
| 2-sail wood | 0.500 | stalls | stalls |
| **3-sail wood** | 0.750 | **1.20** — a knife edge, see below | **stalls** (0.6 − 0.6 = 0 exactly) |
| **4-sail wood** | 1.000 | **1.80** — the tier to rely on | 0.90 |
| 5-sail wood | 1.250 | 2.16 | 1.44 |
| **10-sail metal** | 3.125 | **3.02** | 2.74 |
| water wheel, flow `f` | `f` | `6(0.3 − 0.3/f)`, ≤ 1.80 hard cap | ≤ 1.80 |

That is **the level-line ladder, unchanged**, which is the point: one table, and
`RopewayPowerTests.ABiggerDriveIsAVisiblyFasterCabin` keeps its numbers.

**The three-sail row is a knife edge and the headline has to say so.** `0.6 − 0.3/0.75 = 0.2` *exactly*, and
three independent variables sit on that edge: `TargetSpeed` is `min(0.6, windSpeed)`, so any wind under 0.4
stalls it outright; and `BEBehaviorWindmillRotor`'s `turbulenceExposed` halves the torque factor to 0.375,
giving `0.6 − 0.8 < 0` — **a turbulence-exposed three-sail mill stalls a counterweighted lift.** The honest
claim is *"a sheltered three-sail mill in good wind"*, and the tier a player can rely on is **four**.

**Say the balance decision rather than presenting it as an economy.** A counterweighted shaft is *strictly*
cheaper on power than a steep ropeway, *strictly* steeper, and a third of the placed blocks:

| | load on the network | rise per block travelled | placed blocks | rope |
|---|---|---|---|---|
| level ropeway | 0.300 | 0.00 | 30 | 0.25/blk |
| 70° ropeway | 0.441 | 0.94 | 30 | 0.25/blk |
| **counterweighted shaft** | **0.300** | **1.00** | **11** | **0.50/blk** |

**The 0.94 column is NOT a speed ratio, and reading it as one is a mistake this table has already caused
once.** It says a 70° span turns 0.94 blocks of *travel* into a block of *rise*; it says nothing about how
fast the cabin travels. The two columns compound, because the 70° span pays 0.441 where the shaft pays 0.300
and `s* = 0.6 − R/T` is what that buys. Height gained per second, as a fraction of the shaft's:

| torque | 3-sail wood | 4-sail wood | 5-sail wood (maxed) | 10-sail metal (maxed) | T → ∞ |
|---|---|---|---|---|---|
| 70° ropeway ÷ shaft | **6%** | **50%** | **64%** | **86%** | 94% |

So 0.94 is a **ceiling the machine never reaches**, and at the four-sail tier this document calls the one to
rely on, a shaft climbs **twice** as fast as the steepest ropeway. Handbook page 53 and QA step 3 carried
*"96% of a shaft's rate"* until 2026-08-10, which was this column transposed into a rate; both now quote the
half. The row is asserted rather than described, for exactly that reason.

The only axis it loses on is haul rope — ten extra on a 40-block climb — and digging, which is a wash: a
vertical shaft for 40 blocks of rise is 40 × 16 = 640 columns against a 70° corridor's ~640 block-volumes,
and the steep ropeway is free *only* where it runs up a cliff face. That is intended under the author's
premise (*"we should be able to drive an elevator with relatively little force"*), and the one lever free to
move if it turns out too strong is `ropePerBlock`, which is JSON.
`RopewayPowerTests.ACounterweightedShaftCostsTheNetworkWhatALevelLineCosts` pins every claim in this section.

**There is no hand crank, and inventing one would be a new mechanical power source in a transport mod.**
Vanilla 1.22.1 ships none: the only `BEBehaviorMPRotor` subclasses are the windmill rotor, the water wheel
and the creative rotor. The honest equivalent is the table above — the counterweight is the difference
between *"the first windmill you build cannot move the lift at all"* and *"the first windmill you build runs
it at a walk."* `HaulResistance` is three times a quern's 0.1, so a crank sized like a quern's would not turn
it anyway, and that is the right answer rather than a disappointing one: a lift you can raise by hand is a
lift with no reason to have a mill.

## The drive station — where power enters (2026-08-04)

**The consumer is `ropeway:drivehousing`, cell `[3,0,0]` of a `ropeway:drivestation`**: the foot of that
tower's machine leg, on the ground, inside the tower's own footprint. It carries the `MPConsumer`, and the
line's speed pools over drive stations. The footing stays what it was — the controller, the multiblock
owner, the span owner, the name holder — and it now also owns the intake, at an offset its own JSON names.

**Which line it drives is a STRUCTURAL fact.** `BEPylonBase.Intake` scans the footing's own
`TransformedOffsets` for the block number `driveIntakeCell` names and takes the block entity at that offset.
One block-accessor call, already rotated for the tower's facing, with no radius, no candidate scan, no
acceptance predicate and no tie-break. `DriveSpeedOn` then walks `line.Towers` and reads each station's
intake, where it used to scan a table of every loaded housing asking each one which footing was nearest it.

**What that replaced, stated once so nobody rebuilds it.** The housing stood free within an eight-block
sphere and answered "which line am I on" at lookup time. That needed: `LoadedHousings` kept in step with
chunk loads; `ServingTower` walking every loaded footing; a predicate demanding the footing resolve to a
real line, because a bare footing dropped while scouting could otherwise steal the housing; a `ComparePos`
tie-break so two equidistant footings could not make the server and a client disagree about which rope was
turning; `Serves`; a placement refusal; and a `towerRadius` attribute. Two lines passing within eight blocks
of one housing was a real configuration with a real wrong answer. **None of those questions exists once the
housing is a cell of exactly one station**, and every one of those members is deleted.

**"A cell of exactly one station" is now the code as well as the design (2026-08-04).**
`MultiblockStructure` has no notion of ownership — `InCompleteBlockCount` asks only whether the block at each
offset matches a wildcard, and nothing anywhere asks whether some other footing is already claiming that
cell. For one round that put the two-lines-off-one-mill answer back at three geometries: a second station
**4.243 blocks away on a perpendicular facing** (either side) or **six on the opposite one** shared the whole
machine leg and both validated off it. `BEPylonBase.OwnTheHeadCell` narrows the leg's one facing-carrying
cell — `ropeway:drivehead-*` / `ropeway:tensionhead-*` — to the footing's own side before `InitForUse`, and a
shared head can face one way, so it satisfies one station. All three geometries are gone, re-derived, and
`ASharedMachineLegSatisfiesAtMostOneStation` enumerates them. The **spacing rule that stood in for the fix is
retired** — the derivation, and what remains (two towers may still share a leg of logs, as two plain towers
always could), are in [KNOWN-ISSUES.md](KNOWN-ISSUES.md) under "One machine leg, one station".

**The eight-block sphere is gone, and with it the raised housing.** `Nearest` squared `dy` alongside `dx`
and `dz`, which was what let a housing climb to a windmill's hub and keep the axle run horizontal. The
intake is now at a fixed cell, so it cannot climb — see the honest cost below. What the sphere's removal
buys back is that there is **no placement envelope at all**: a maxed metal rotor could not meet the sphere
from any position (`121 > 64`) and needed a descent regardless, and now no mill has a placement constraint.

**`ropeway:driveshaft` is the one block of the tower with `sidesolid: all true`**, and that is the line that
deletes the mandatory scaffold. Vanilla refuses an angled gear beside an unsupported axle, and every other
block of a tower ships `sidesolid: all false`, so a vertical axle column could not lean on the tower it
served and the player had to build a wall first. The drive leg is that wall, at exactly the column where
power is supposed to touch the tower. `sideopaque` stays false and `lightAbsorption` stays 0, so the leg is
still see-through and casts no shade, and it has no effect on spans — `SpanMath.RopewayBlockFilter` returns
false for `Code.Domain == "ropeway"` before it ever looks at `SideSolid`.

**There is no placement refusal any more.** A housing built anywhere else simply drives nothing, which needs
no rule, cannot be got wrong halfway through a build, and is said by the block's own panel.

**It takes an axle on any HORIZONTAL face**, and that is the fix rather than a simplification. The trial's
bullwheel accepted power from either end *along the line*, which at sheave height is the haul rope's own
path and the cells the cabin's hanger travels through: the handbook was telling players to build an axle
where the cabin flies. Anywhere off the crossarm there is no rope to build across — on the ground, or up
beside a mill's hub, which for a five-sail rotor is two clear blocks above the rope line — and horizontal
faces are where vanilla axle runs already live. `BlockMPBase.WasPlaced(world, pos, null)` probes `BlockFacing.HORIZONTALS` and nothing else,
which is now exactly and only the set this block connects on — so the "documented entry the auto-connect
cannot see" failure the crossarm hookup carried does not exist here either.

**What horizontal-only costs, stated once: one angled gear, and now on every windmill.** A `woodenaxle-ud`
column cannot terminate on the housing's roof, so a descent is two gears and N−1 axles rather than one gear
and N. The water wheel still pays nothing, because its run never leaves the horizontal. The raised-housing
windmill used to pay nothing either, and that layout is gone with the sphere.

**No `side` variant.** Every horizontal face connects, so orientation decides nothing — and a block with no
orientation cannot be placed ninety degrees out.

### What the player actually has to build

**The mill decides the height, and an earlier version of this section pretended it did not.** It put the
housing, two axles and "the mill itself" in a row on the ground and called the build three blocks. A
windmill rotor at ground level refuses its *first* sail, so that row of blocks was not a build a player
could complete. Full working, read from the decompiled source:
`docs/agentic/ingest/cablecar/HOUSING-FIX-FACTS.md`. No code was wrong; the table was.

`BEBehaviorWindmillRotor.obstructed(len)` is **not** a column under the hub. It is a flat square one block
thick standing in the plane the sails turn in — `i` sweeps the horizontal axis *perpendicular* to the axle
and `j` sweeps vertically, so cells above and sideways count exactly as much as cells below, and
`(2len+1)² − 5` of them are checked (centre cell and the four extreme corners exempt). Nothing along the
axle axis is ever scanned. A rotor carrying *n* sails needs `len = n + 1` clear every way; `OnInteract`'s
`sailLength + 2` is `CheckWindSpeed`'s `sailLength + 1` evaluated for the length you are about to reach, so
placement and shedding use identical clearance and there is no window where a sail can be placed and then
dropped. Clearance is counted in blocks, not altitude — a trench does as well as air.

| sails | tier | clear blocks every way | hub level, counted from the tower's footing block |
|---|---|---|---|
| 1 | either | 2 | +2 |
| 3 | either | 4 | +4 |
| 5 | wood, maxed | 6 | +6 |
| 10 | metal, maxed | 11 | +11 |

**Tell a player the clearance and never the height.** The clearance is what `obstructed` counts and it
cannot be read two ways; a height is only meaningful against a stated datum, and every off-by-one in this
area is somebody reading one datum where another was meant. The second column exists for the arithmetic
below and not for the handbook: its datum is the **footing block**, because that is what `LoadedTowers` is
keyed by. Against the ground *surface* the same hub is one cell higher again — the footing itself is a
block, and it stands on the ground rather than in it. **The height column is now a descent, not a
placement.** With the intake at a fixed cell there is no hub height the housing can rise to meet, so what
this table gives is how far the axle run has to come down the outside of the drive leg.

Two real hookups follow from that.

**Water wheel — three blocks, on the ground, nothing to climb, and it is the expensive one.** The wheel
drives an axle out of *either* end of its own axis and its hub sits at most one block above the water
surface, so a straight `woodenaxle-we` run of two or three from the bank into the intake is the whole
hookup. It is the one drive the fixed intake costs nothing at all. **It is not the easy example the review nominated.** `BEBehaviorMPWaterWheel.CheckWater`
counts a ring cell only when `flowSpeed > requiresMinFlowSpeed` (1.5), and only `rapidwater` declares one
(2) — plain `water` has no `flowSpeed` key at all, so **ordinary water never turns a wheel in 1.22.1**.
Rapid water is worldgen-only, ~5% in mountain-side rivulets, and vanilla's own handbook says players cannot
place it. The block is a six-stage `RightClickConstructable` costing 32 support beams, 96 planks, 12 resin
and 8 nails-and-strips, on top of a craft needing two iron 4-way hubs. One size, 3m, one cell, with 8 ring
cells that must be non-solid.

**Windmill — the mill comes down the leg, and this is the price of the change.** Four clear blocks under the
hub for three sails, six for a maxed five, eleven for a maxed metal ten, and the intake is at +0. Put the
rotor's axle along the tower's passage axis so the sail disc can never contain a tower cell, then run down
the outside of the drive leg:

| what | where, footing-relative | 3 sails | 5 sails | 10 sails |
|---|---|---|---|---|
| `windmillrotor-*` | hub at +4 / +6 / +11, disc square to its axle | — | — | — |
| `angledgears` | at the hub, turning the run vertical | 1 | 1 | 1 |
| `woodenaxle-ud` | down the outside of the drive leg, leaning on it | 4 | 6 | 11 |
| `angledgears` | at the bottom, turning it back horizontal | 1 | 1 | 1 |
| `woodenaxle-{ns,we}` | into any of the housing's four sides | 1 | 1 | 1 |
| | | **~7** | **~9** | **~14** |

The column leans on `ropeway:driveshaft`, which is the only reason those builds close at all — every other
block of a tower is `sidesolid: all false`, and an unsupported `ud` column refuses its angled gear. Nothing
in the drive train can obstruct the mill it drives: the rotor's axle axis is the one direction `obstructed`
never scans, and the whole descent is behind the rotor on that axis.

**It was sixteen, then it was three with a floating housing, and it is now a plain shaft down the outside
of the drive leg.** All three numbers are true of different builds and the middle one is the one that no
longer exists. The trial put the intake on the bullwheel four blocks up, so every drive paid a five-log
support column, four vertical axles, a run back across the crossarm and three angled gears — and the support
column was mandatory because nothing on the tower was solid. The free-standing housing bought that down to
three blocks for a water wheel or a wooden rotor, by letting the intake climb to meet the hub, and paid for
it in binding: a table, a nearest-footing scan, a line-resolving predicate and a positional tie-break, with
a real wrong answer where two lines passed close together. **Fixing the intake to a cell hands the descent
back and takes the binding away**, and it buys two things: no mill has a placement constraint any more (the
maxed metal rotor could never meet the sphere at all), and the scaffold is gone for *every* drive rather
than only the gearless ones, because the leg is the wall. Rendered at
`docs/agentic/ingest/cablecar/renders/station/drive/` — `textured/right.png` is the elevation that shows the
shaft running unbroken from the gearbox to the wheel hub. The older `renders/drive/` scene is the
free-standing housing and is kept only as the record of what it looked like.

**The build-order dead end is closed for every drive.**
`BlockAngledGears.TryPlaceBlock` refuses to sit beside an axle that fails `IsAttachedToBlock`, which is what
forced one legal build order and dead-ended the documented one. A `woodenaxle-ud` column needs a
horizontally adjacent solid side, and it now has one: `ropeway:driveshaft` ships `sidesolid: all true` and
runs the full height of the leg the column comes down. That is the wall, already built, in the one place a
column wants it. (A
bottom gear placed beside an already-built housing happens to skip the check, because `ALLFACES` walks
horizontals first and finds the housing before it looks up at the axle. That is a build-order accident, not
a licence — `BlockAxle.OnNeighbourBlockChange` breaks an unattached axle that loses its support.)

## The bullwheel — the wheel a station turns

The bullwheel is the **centre cell of a station's crossarm**, on both `drivestation.json` and
`tensionstation.json`, and a plain `pylonbase` now wants the plain sheave and nothing else. It was an
accepted alternative on any tower while it was decoration that happened to spin; accepting one on a plain
tower would now put a wheel joined to nothing on a tower that drives nothing. It is still not a mechanical
power node: no `entityBehaviors`, no `MPConsumer`, no network membership, no resistance, and no `class` at
all — it was a `BlockMPBase`, and with the intake gone the subclass was empty.

**It turns.** That is the whole reason it survived the trial. The review measured the wheel's silhouette
against the pylon head's and found them near-identical at any real distance, so a *still* wheel marked
nothing; what actually marked a drive tower was the scaffold. `BEBullwheel` polls the line's pooled drive
speed every 500 ms and `BullwheelRenderer` integrates it into an angle — revolutions per second, so a
stopped line is a stopped wheel.

**And it is now visibly driven rather than visibly spinning.** The complaint was that it *"floats with
nothing joining it"*: `driveboss` is a 3×3 stub topping out at y 16 under a rim whose resting bottom is
16.685, so the wheel balanced on a pinhead and was joined to nothing. Three elements fix it and they are the
same three that receive the drive. Two **bearing standards** rise out of the existing `sheavecheek` plates
at y 15 and flank the rim, and a **`hubaxle`** at y 24.7–26.7 — the rim's own rotation centre, so it *is*
the axle the rim turns about — runs the full width of the cell to meet the `layshaft` in the next cell east
with no seam. It is thinner than the rim's own hub bore (y 23.5–27.9, z 5.8–10.2) and buried inside both
bearing caps, so no face of it is ever coplanar with the turning mesh. `driveboss` stays: it is under the
rim, between the standards, and removing it would be a change with no visible effect.

**The visible train, drive station, bottom to top.** `drivehousing` (the intake, on the ground) → three
`driveshaft` lattice cells with the vertical shaft inside them → `drivehead`, the bevel gearbox on the
crossarm end → two `layshaft` cells → the bullwheel's hub. Every one of those is a block a player can point
at, and the shaft is one unbroken line at one height — 25.7 in the crossarm cell's own units — from the
gearbox to the wheel. The tension station mirrors it: `tensionweight` → three `tensionguide` cells →
`tensionhead`, a sheave whose top tangent is that same 25.7 and whose west tangent is the guide's own hanger
column → two `layshaft` cells as a tie rod → the return wheel's carriage. `ropeway:layshaft` is deliberately
one block used by both: on the drive it turns and on the tensioner it pulls, and at 16 pixels those are the
same forged bar on the same standards.

**Nothing in the drive train is within 8 units of the GOING strand or the cabin.** The shaft runs along local
X at 25.7 and the going strand runs along local Z at 8 — `SpanMath.AnchorOf`, the cell centre — so they cross
only at the wheel and are 17.7 units apart vertically.

**The RETURN strand is a different question and the answer is 1.56 units (2026-08-04).** The haul rope is a
loop: a second strand runs the whole length of the line `BEPylonBase.ReturnLift` = 2 × `BullwheelRenderer.WrapRadius`
= 21.22 units above the first, i.e. cell-local 29.22, band 28.26 … 30.18. The shaft train tops out at 26.7 —
`layshaft.shaft`, `bullwheel.hubaxle`, `tensionhead.tierod`, all the same bar — so the return strand passes
**1.56 units over the whole of it**, and over the hub axle it does so in the same column rather than a cell
along. Not a clash, and it is the tightest *vertical* gap the loop creates; what it means in practice is that
the shaft's height is now pinned from both sides and cannot be raised. The two genuinely tight clearances the
loop creates are LATERAL and are asserted rather than written down here:
`TheReturnStrandClearsTheBullwheelsOwnBearings` (0.74 units per side — the strand threads between the wheel's
own bearing caps, whose tops stand 0.24 units into its band), and the wheel brackets' 1.64, which is the
sheave throat's own number.

**And at a station the line runs THROUGH, the wheel is no longer level with the bar that drives it.** There
is no dead side to stand out on, and where the wheel rests the return strand runs 0.22 blocks above its axle
— 1.12 blocks of rope inside the swept rim, every revolution. So it rises `BullwheelRenderer.HoldDownRise` =
3 × `WrapRadius` − (`RimPivotY` − 0.5) = 0.883 blocks and becomes a **hold-down sheave** on the strand
nothing rides on, its groove tangent to that strand from below exactly as it is tangent to the going strand
from above at a terminal. The two brackets that carry it out to the rope at a terminal become the two struts
that carry it up here — one function, `BEPylonBase.BracketPath`, keyed off the same `BEBullwheel.WrapOffset`
the renderer's own matrix reads. The cost is `CullRadius` 2.0 → 2.75 and the unbroken-bar-at-one-height
property, which that tower loses in the same way and for the same reason a terminal already had.

**Why a plain `IRenderer` and not `MechBlockRenderer`.** `mods-dll/flywheelpower`'s
`FlywheelMechBlockRenderer` is an *instanced* renderer registered with `MechanicalPowerMod` and driven
per-device off `IMechanicalPowerRenderable` / `AngleRad`. It exists to draw hundreds of axles in one call,
and every device it draws must be a node on a mechanical network. This wheel deliberately is not, so there
is no device to enumerate and no angle to read — adopting it would mean putting the wheel back on the
network purely in order to be drawn. Vanilla's `QuernTopRenderer` is the right precedent and is what this
copies: one renderer per drive tower, of which a line has one or two.

**It is visually distinct now.** `shapes/block/bullwheelrim.json` is an eight-spoke wheel standing above the
crossarm, just clear of the sheave's drive boss — 18 units across, a full block of extra height. It rested
*on* the boss until the rim centre went 25.2 → 25.7 to buy clearance over the cabin's slot; that lifts the
rim's resting bottom to 16.685 against a `driveboss` topping out at y 16, so it now floats 0.685 unit —
0.043 blocks — above it. Sub-pixel at any distance a player will see it from, and not worth code. It is a separate shape
from `bullwheel.json` so the chunk tesselator keeps drawing the static half with normal chunk lighting.
It is authored **above** the block cell: the sheave throat is where the cabin's hanger blade rides at every
tower, and a wheel authored down into it is a cabin that catches with nothing to say so. At a **terminal**
the renderer carries it one cell out along the dead side and `BullwheelRenderer.WrapDrop` down so the rope
wraps it, and there it does enter that cell's airspace — on the side of the tower nothing ever passes.
`TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach` is the assert, in both poses.

**Not fixed, and named — and it got worse before it gets better.** The wheel is still `HorizontalOrientable`,
so one placed while facing the wrong way validates the tower with its throat and station rails running
across the line, and now with its hub axle pointing at the braces rather than at the lay shaft. `layshaft`,
`drivehead` and `tensionhead` inherit it, so the count goes 2 → 5. The fix is unchanged and is still one fix
in one place: orient the crossarm cells from the footing below them, for all of them at once. It is worth
doing at five where it was not at two.

## Power may be supplied at ANY drive station on the line, and pools

Unchanged where it counts: `BEPylonBase.DriveSpeedOn` sums `TrueSpeed` over the line's **drive stations**,
**one term per mechanical network** (`RopewayPower.PoolSpeed`, keyed on `MechanicalNetwork.networkId`). That
is the whole of the pooling — addition does not care about order, and a station whose chunk unloads simply
stops contributing. `PoolSpeed` itself did not change; only the source of the `(networkId, speed)` pairs
moved, for the third time.

**It is now a WALK of `line.Towers` rather than a scan of a table**, which is the whole of the 2026-08-04
change here. A housing bound by proximity had no tower to be indexed under, so the pool had to be gathered
from a table of every loaded housing and each one asked which line it was on; a housing that is a cell of a
station is reached from the footing at a known offset. The tensioner's own question went the same way and
now asks the *same* table, which narrows the band it can lie in — but **"no tensioner" and `line.Truncated`
do NOT coincide exactly**, and an earlier version of this paragraph said they did. `HasTensioner` gates on
`StructureComplete`, which is fifteen `GetBlockRaw` reads, and `BlockAccessorRelaxed` hands back air for an
unloaded chunk, so a loaded footing whose own leg is three blocks away across an unloaded chunk boundary
reads incomplete while `MarkLoadedEnds` — which only inspects the two ends of the walked chain — sees nothing
wrong. Same three-block residue `DriveSpeedOn` carries. `TryPlaceCabin` therefore asks `line.Truncated`
**first** and answers with the honest "part of that line is not loaded" whenever it applies.

The per-network key is load-bearing rather than tidiness. `TrueSpeed` is `|Network.Speed × GearedRatio|`, so
two stations tapped off one axle run are two windows onto the same turning shaft. Summing per hookup let a
player buy speed with axles: each hookup also declares `HaulResistance` on that network, but the load only
*subtracts* from the settling speed while the sum *multiplies* what is read — three stubs off one maxed metal
mill read 5.6 blocks/s against a single hookup's 3.0. Adding load made the machine faster, which is the one
thing a load model must never do. First hookup seen on a network wins; they are all looking at one rope.

Two drive stations on **one** network are therefore not merely redundant, they are a **penalty**: each writes
the full `HaulResistance` onto that shared network every second and `MechanicalNetwork.updateNetwork` sums
resistances, so the settling speed drops while the pool counts the speed once. The second one makes the line
slower. The handbook says so now.

**The load is declared by the station, not by the housing.** `BEDriveHousing` used to carry a
`RegisterGameTickListener` purely because it had to re-answer "which line am I on" every second;
`BEPylonBase.OnServerTick1s` already ticks at 1 s and already knows, so the listener is a deletion rather
than a move. It early-returns on a footing with no intake cell, which is every tower but one or two per
line, so the scan of loaded entities `FindOn` does never runs on a plain tower.

Two mills on separate networks are a visibly faster cabin. Historically defensible too: intermediate drive
stations are real on long ropeways, because driving a continuous haul loop anywhere along its length drives
all of it.

## The cabin reads live network state — and the loaded window is smaller than this used to claim

The old design forbade the live read in as many words. It is now the whole point. What was written down to
make it safe was a **line-length cap**: `maxLineLength` 320 sitting inside "the server's default
`MaxChunkRadius` of 384", so a player anywhere on a line held every tower of it loaded, drives included.
**That arithmetic is wrong, and this is the corrected version.** `MaxChunkRadius` is a *cap on* the loaded
radius, not the loaded radius:

- `ServerConfig.cs:925` — `MaxChunkRadius = 12`, i.e. 12 × 32 = 384 blocks. `ServerMain.cs:789` and `:4267`
  only ever raise it, so 12 is a floor on the cap and never the answer on its own.
- `ServerMain.cs:2527` — `GetAllowedChunkRadius(client)` is
  `min(Config.MaxChunkRadius, ceil(client.WorldData.Viewdistance / 32))`. It returns the **uncapped**
  `ceil(viewdistance / 32)` for a singleplayer client, so in singleplayer the window is the view-distance
  slider and nothing else.
- `ServerSystemUnloadChunks.cs:597` and `:734` — the keep-loaded set is exactly the octagons out to that
  radius for every connected client; everything outside it unloads.
- `ClientSettings.cs:1958` — the shipped default `viewDistance` is **256**.

So the stock window is `min(12, ceil(256/32))` = **8 chunks = 256 blocks**, against a line that may be 320.
**A line can be longer than the window it is loaded in, and a drive at the far end of one can be dark.**
From the *middle* of a full-length line each end is 160 away and everything holds; from one **end** of one,
the far end is 320 away and outside the window. A drive station at that far end is not in `LoadedTowers`,
so it contributes nothing to `DriveSpeedOn`, and the line reads as having no drive at all — a cabin that
will not start, beside a message telling the player to build the thing they have already built. The band
this used to describe was wider: a free-standing housing could be up to eight blocks from its own footing
and a chunk boundary between them made the footing lit and the housing dark on a chain that was not
truncated. The intake is a cell of the tower now, so that gap is three blocks instead of eight — narrower,
and still not closed, because a chunk boundary can fall anywhere. Every view
distance below 256 makes the window smaller; a singleplayer slider above 320 makes the problem vanish.

What the cap does buy is a **shorter** line: at stock settings anything under about 256 blocks end to end
is inside one player's window wherever they stand on it, which is most lines anyone builds. That is a
likelihood, not a guarantee, and the docs used to state it as a guarantee. The same correction applies to
the truncated-line class (`KNOWN-ISSUES.md` R1–R4), which is mitigated by the cap and not closed by it.

The 384 figure was repeated in code and asset comments as well as here — `pylonbase.json`'s
`//maxLineLength` and `BEPylonBase.DriveSpeedOn` — and those are the places a future reader will trust. Anything that says a line fits inside the loaded window wants the same correction.

## Transmitting power along the cable: RECORDED, NOT BUILT

**The idea (BASIC's):** the haul rope already runs between two stations and is already turning. Let a line
carry *mechanical power* between them by linking the two stations' `MechanicalNetwork`s, so a mill at one end
drives machines at the other.

**Do not pick this up naively.** Four objections, in the order they bite:

1. **It is a balance problem, not a feature.** A line reaches 320 blocks with a handful of towers. Vanilla's
   transmission over that distance is 320 axles plus supports plus gears, and it is deliberately awful. A
   ropeway that transmits power is *strictly better than every vanilla option at range*, so it stops being a
   transport mod and becomes the only way anyone moves power. Nothing about the rest of the design survives
   that.
2. **Merging two `MechanicalNetwork`s is engine-deep.** Direction and handedness have to reconcile:
   `MechPowerPath` carries `invert` and `turnDir`, and joining is written as "a device joins a network",
   never "two networks join". Two networks meeting with **opposite handedness fight** — there is no defined
   resolution, and no vanilla block does it.
3. **It re-opens the chunk coupling this mod spent four fix rounds removing.** One end unloading would change
   the other end's behaviour, which is exactly the class `maxLineLength` 320 was reduced to *mitigate*
   (`KNOWN-ISSUES.md` R1–R4) — and, per the corrected arithmetic above, only mitigate: the stock loaded
   window is 256 blocks, so a full-length line already has an end that can go dark. The whole current design
   is "each end pulls its own weight and nothing has to be kept in sync", and it is the reason a dark end is
   a slow cabin rather than a corrupt one.
4. **The rope already drives the cabin.** A transmitting line also *moves its cabin*, every time anyone at
   the far end runs a quern, unless a clutch concept is invented — and a clutch is a second state machine on
   a mod that just deleted one.

**The tractable version, if it is ever wanted: a one-way transfer, not a merge.** A receiving station carries
its own **MP producer** that emits a lossy, capped fraction of what the sending station's consumer takes. No
network merge, so no handedness problem and no direction reconciliation; a dark far end is simply a producer
that stops producing, which is already how every drive on this line behaves. The loss and the cap are what
keep it a niche tool — a trickle to a remote outpost — rather than a replacement for axles. Objection 4
survives and still needs an answer (a transmitting line moving its own cabin may simply be *correct* and
worth saying out loud in the handbook).

## Gravity storage: shelved, not cancelled

It comes back as **its own standalone block**, usable by anything on the mechanical network rather than
welded to a ropeway — which is also the only shape in which it can be sized honestly. Rough sizing from the
arithmetic above: **about one cubic metre of stone per 8.5 m of lift** for a single uphill ropeway trip. A
device that stores a useful ropeway trip is therefore a structure, not a decoration, and that is the design
constraint the ropeway-welded version was hiding.

The taxonomy that motivated it still stands:

| device | capacity | decay | what it is for |
|---|---|---|---|
| **Flywheel** (`flywheelpower`, sibling mod — not a dependency, and not craftable in vanilla) | modest | yes — friction | smoothing a variable load, riding out a gust |
| **Raised mass** — a standalone block, unbuilt | large | none | persistent storage across days of calm |
| **Frictionless flywheel** — imbued, speculative | large | none | a battery |

The third row is why the second must stay expensive: *"as soon as you have a flywheel that can store even
more power and doesn't degrade with friction over time, then you just made a battery."* If frictionless
flywheels ever ship cheap, gravity storage is dead content.

## The cheapness constraint, correctly scoped — and now checked against arithmetic

`DECISIONS.md` §3 is about the **marginal** cost of a pylon and its span, because a chained route
multiplies it by every hop — not about the system's one-time cost. A windmill plus axles for the whole
ropeway is fine; a windmill per pylon would not be. The drive train itself is not metal-gated at all: a
wooden rotor, wooden axles and angled gears cost no metal.

This section used to say "unchanged and still satisfied" and give no number, and the argument had never
been run past the recipes. It has now (`docs/agentic/ingest/cablecar/RECIPE-LADDER.md`), and it was **not**
satisfied — a minimum line came to 61 ingots and a ten-tower route to 93, against an anvil at 10. After the
recipe pass, pricing a `metalplate` at the 2 ingots it is smithed from and a `metalbit` at 1/20:

| | marginal | once per line |
|---|---|---|
| **plain tower** | **1.85 ingots**, 1 rope, 2 planks, 3 stone, 8 posts | — |
| **span, 30 blocks** | **4 vanilla rope** + 0.1 ingot | — |
| **span, 48 blocks (max)** | 6 rope + 0.15 ingot | — |
| **drive station** | — | **15.9 ingots** |
| **tension station** | — | **12.7 ingots** |
| **cabin** | — | 0.53 ingot |

So a whole short line at a 30-block span is **≈31 ingots** — about three anvils, or one and a half water
wheels once the water wheel's own six construction stages are counted — and each hop after it is under two
ingots, plus rope by the span: 2 up to 16 blocks, 4 from 17 to 32, 6 from 33 to 48. Haul rope only comes four
to a craft, so that is a step function and not a rate; quoting "four rope a hop" undercharges the 33-to-48
band that QA's own 20-to-40-block spans reach. The once-per-line cost sits beside the drive that runs it, and
the per-pylon cost is small enough that a ten-tower route at the same span is only **≈46**.
