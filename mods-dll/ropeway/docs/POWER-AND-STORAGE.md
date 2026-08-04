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
trap: the rider could not get out. **That is no longer true.** The phase-1 emergency bail-out (hold sneak
for two seconds) means a rider can always leave, anywhere on the line, and a stopped cabin lets them step
straight out because `IsMoving` is false. Once nobody can be trapped, "the cabin stopped because the wind
stopped" is ordinary machine behaviour — and the entire apparatus built to prevent it (charge arithmetic,
capacity, `paidTo` and the `Fare` credit rule, `Quote`/`TripCost`/`WorstTripCost`, the link-time steepness
refusal, and the `NoStore` / `StoreUnreachable` / `NoPower` / `TooDear` refusal states with their strings,
block-info lines and tests) was dead weight protecting against a problem that had already been solved
somewhere else.

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

`ropeway:tensionweight` is a **tensioner**: a build requirement for a line, one block, standing within
eight blocks of any tower on it. It holds no charge, has no gauge and no capacity, is not a mechanical
power node, and is not bound to anything.

- **It is a build requirement, not a runtime state**, and the check is at **cabin placement**:
  *a line needs one tension weight to keep the rope taut, and will not take a cabin without it.* One
  sentence, told once, while the player is building — rather than a refusal state with a message, a toast
  and a wait attached. Break the weight afterwards and the cabin keeps running; the tower panel says the
  tensioner is missing.
- **Proximity at lookup time, never a persisted binding.** `BETensionWeight.OnLine` asks whether any loaded
  weight stands within its own `towerRadius` of any tower on the line. That deletes `AnchorTower`, `Bind`,
  the `UnlinkAll` re-bind, the orphan and spare block-info lines, `StoreAt`, and the one-weight-per-line
  placement refusal — with them go review findings **F4, F6 and F7**, which were all consequences of
  binding a block to one tower at placement.
- **The mass moved into the shape.** It used to be chunk mesh drawn by the block entity at a height that
  *was* the charge gauge. It is now a static element hanging near the bottom of its guide, on a rod up to
  the head beam, which is where a rope tensioner rests and what makes it read as hanging rather than as a
  rock sitting on a pad. The block entity draws nothing and exists only to register the block's position.

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

**Every drive housing declares the same load**, not a share of it. A share would have to be divided by how
many other drives are powered — a number that changes when somebody walks away and a chunk unloads, which is
the coupling this design refuses to have. Each drive pulls its own weight and their speeds add.

## The drive housing — where power enters (2026-08-03)

**The consumer is `ropeway:drivehousing`, a separate block standing within eight blocks of any tower on the
line — and nearer to that line's towers than to any other's.** Placement asks only for *a* tower in range;
which line it then drives is decided by the **single nearest footing that is on a line** — a bare unlinked
footing is skipped, however close it stands. The two are not the same rule and the player-facing text has to
say both. It carries the `MPConsumer`, declares the haul load, and the line's speed pools over housings. The
footing stays what it was: the controller, the multiblock owner, the span owner, the name holder. It is
usually on the ground and it does not have to be — see the sphere, below, which is what makes a windmill
hookup possible at all.

**How it binds to a line: proximity at lookup time**, within its own `towerRadius` (8, the tension weight's
number). Nothing is persisted at placement, so nothing can come unbound — break the tower a housing was
built beside and it drives the line of whichever footing **that is on a line** is nearest it next.
`BlockTensionWeight.NearestTower` is the shared helper and `Nearest` is the shared metric, so the tensioner
and the drive agree about how far "beside the line" reaches.

**They do not ask the same question, and an earlier version of this section said they did.** The tensioner
asks `NearAnyTower` — is any tower of this line in range — and the housing asked it too, which is true of
*every* line with a tower nearby. Its load declaration meanwhile asked for the single nearest footing
overall, so two lines whose towers passed within eight blocks of one housing both read its full pooled speed
while only one of them was ever charged `HaulResistance`: one mill hauling two cabins for the price of one,
which is precisely the free speed `PoolSpeed` exists to refuse. `BEDriveHousing.Serves` now routes through
one `ServingTower` accessor on `NearestTower`, so the speed and the load answer the same question by
construction. The asymmetry is the point rather than an oversight — a tensioner only has to *certify* that a
line has one, which any weight in reach can do, while a drive has to be the drive of exactly one line.
`RopewayPowerTests.AHousingDrivesTheOneLineItsNearestFootingIsOn` pins both halves.

**The eight blocks are a SPHERE, and that is now load-bearing rather than incidental.**
`BlockTensionWeight.Nearest` squares `dy` alongside `dx` and `dz` and compares against `radius * radius`
with the boundary inclusive, so height counts exactly as much as distance across the ground and a housing
may sit *above* a footing as readily as beside it. That was inherited from the tensioner, where it never
mattered; it matters here, because a windmill rotor cannot stand at ground level (below) and the only way
the axle run into the housing stays horizontal is for the housing to climb with the hub. `LoadedTowers` is
keyed by the ground-placed footing, so on flat ground the `dy` in that expression is exactly the height
above the tower's own first block. The envelope, since the handbook now quotes parts of it: **+6 with up to
5 blocks of horizontal offset**, +7 with up to 3, +8 only directly above the footing (`64 <= 64`, exactly on
the boundary), nothing at all at +9 or higher. A raised housing must also miss the tower's own fifteen
multiblock cells, which in practice means offsetting along the passage axis rather than along the crossarm.

**Placement refuses rather than sitting inert.** No tower inside 8 blocks →
`placefailure-ropewaynodrivetower`.

**It takes an axle on any HORIZONTAL face**, and that is the fix rather than a simplification. The trial's
bullwheel accepted power from either end *along the line*, which at sheave height is the haul rope's own
path and the cells the cabin's hanger travels through: the handbook was telling players to build an axle
where the cabin flies. Anywhere off the crossarm there is no rope to build across — on the ground, or up
beside a mill's hub, which for a five-sail rotor is two clear blocks above the rope line — and horizontal
faces are where vanilla axle runs already live. `BlockMPBase.WasPlaced(world, pos, null)` probes `BlockFacing.HORIZONTALS` and nothing else,
which is now exactly and only the set this block connects on — so the "documented entry the auto-connect
cannot see" failure the crossarm hookup carried does not exist here either.

**What horizontal-only costs, stated once: one angled gear, and only where the housing sits below the
rotor.** A `woodenaxle-ud` column cannot terminate on the housing's roof, so a descent is two gears and
N−1 axles rather than one gear and N. The water wheel and the raised-housing windmill pay nothing, because
their runs never leave the horizontal. Worth it, on the same argument the rule exists for.

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
keyed by and therefore exactly the `dy` the eight-block sphere squares. Against the ground *surface* the
same hub is one cell higher again — the footing itself is a block, and it stands on the ground rather than
in it.

Three real hookups follow from that.

**Water wheel — three blocks, on the ground, nothing to climb, and it is the expensive one.** The wheel
drives an axle out of *either* end of its own axis and its hub sits at most one block above the water
surface, so a housing on the bank at hub height takes a straight `woodenaxle-we` run of two or three and
nothing else. **It is not the easy example the review nominated.** `BEBehaviorMPWaterWheel.CheckWater`
counts a ring cell only when `flowSpeed > requiresMinFlowSpeed` (1.5), and only `rapidwater` declares one
(2) — plain `water` has no `flowSpeed` key at all, so **ordinary water never turns a wheel in 1.22.1**.
Rapid water is worldgen-only, ~5% in mountain-side rivulets, and vanilla's own handbook says players cannot
place it. The block is a six-stage `RightClickConstructable` costing 32 support beams, 96 planks, 12 resin
and 8 nails-and-strips, on top of a craft needing two iron 4-way hubs. One size, 3m, one cell, with 8 ring
cells that must be non-solid.

**Wooden windmill — three blocks, and the housing climbs with the mill.** Four clear blocks under the hub
for three sails, six for a maxed five, which is +4 and +6 above the footing. Put the rotor's axle along the
tower's passage axis so the sail disc can never contain a
tower cell, and stand the housing at hub height:

| what | where, footing-relative | count |
|---|---|---|
| `windmillrotor-wood-*` | hub at +6, disc in the plane square to its axle | — |
| `woodenaxle-{ns,we}` | 2 cells at hub height, on the axle axis | 2 |
| `ropeway:drivehousing` | dy 6, dz 2 → `36 + 4 = 40 ≤ 64` | 1 |
| | | **3 between the mill and the line** |

The housing, whatever holds it up and every axle between it and the hub sit **on the axle axis**, which
`obstructed` never scans. Nothing in the drive train can obstruct the mill it drives — that is why this
layout closes rather than fighting itself.

**Maxed metal rotor — the column is back, and shorter.** Eleven clear blocks every way puts the hub at +11
above the footing, which gives `121 > 64` before any
horizontal term, so no housing can meet it at its own height and power has to come down: `angledgears` at
the hub, 3 × `woodenaxle-ud`, a second `angledgears` beside a housing at dy 7 / dz 2 (`53 ≤ 64`). **Five
vanilla blocks**, plus a wall beside the `ud` column. A four-block version exists — housing at dy 8
straight above the footing, exactly on the radius boundary, hanging over the crossarm — and it is legal and
should not be what the handbook draws.

**It was sixteen, and it is three for the drives that can meet the housing at their own height.** The trial
put the intake on the bullwheel, four blocks above the footing, so *every* drive paid a five-log support column, four
vertical axles, a four-block run back across the crossarm and three angled gears. A water wheel or a wooden
rotor now pays none of that. **The maxed metal rotor still pays some of it, and this is the claim to keep
honest:** it needs eleven clear blocks under its hub whatever the intake does, so it descends through two gears and three
vertical axles either way — the drop is 11 → 7 instead of the old 11 → 4, which is one or two fewer
`woodenaxle-ud` and the same two gears. What the ground housing **deleted** is the scaffold the *tower* had
to carry: nothing is built on the crossarm and nothing climbs the tower. What it **reduced** is the descent
from a mill that has to stand high to turn at all. Rendered at
`docs/agentic/ingest/cablecar/renders/drive/` — compare `textured/isometric.png` against the sixteen-block
version the review looked at. **That render is stale in one respect and is kept anyway:** its scene
(`renders/scenes/drive/manifest.json`) stands the rotor, both axles and the housing at footing height,
which is the ground-level windmill the clearance rule below rules out. It is right about what the *drive
train* costs — the count this section is making — and wrong about how high the whole assembly sits. Read it
for the three blocks, not for the altitude.

**The build-order dead end went with it for the gearless layouts only.**
`BlockAngledGears.TryPlaceBlock` refuses to sit beside an axle that fails `IsAttachedToBlock`, which is what
forced one legal build order and dead-ended the documented one. There is no angled gear in the water-wheel
build or in a wooden-rotor build whose housing rides up to hub height, and
`BEBehaviorMPAxle.IsAttachedToBlock` passes a ground-level `we` axle on the block *below* it — the ground.
The metal descent
has the rule back: a `woodenaxle-ud` column needs a horizontally adjacent solid side, and the tower's own
blocks are all `sidesolid: all false`, so it cannot lean on the tower it serves. Build the wall first. (A
bottom gear placed beside an already-built housing happens to skip the check, because `ALLFACES` walks
horizontals first and finds the housing before it looks up at the axle. That is a build-order accident, not
a licence — `BlockAxle.OnNeighbourBlockChange` breaks an unattached axle that loses its support.)

## The bullwheel — decoration that turns

The bullwheel survives as an **accepted alternative to the pylon head** on the crossarm's centre cell
(`pylonbase.json`'s `multiblockStructure` matches either for block number 1), so a drive tower is still the
same sixteen cells. It is **purely cosmetic**: no `entityBehaviors`, no `MPConsumer`, no network membership,
no resistance. Its block has no `class` at all — it was a `BlockMPBase`, and with the intake gone the
subclass was empty.

**It turns.** That is the whole reason it survived. The review measured the wheel's silhouette against the
pylon head's and found them near-identical at any real distance, so a *still* wheel marked nothing; what
actually marked a drive tower was the scaffold. `BEBullwheel` polls the line's pooled drive speed every 500
ms and `BullwheelRenderer` integrates it into an angle — revolutions per second, so a stopped line is a
stopped wheel.

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
It is authored **above** the block cell, never in it: the sheave throat is where the cabin's hanger blade
rides at every tower, and a wheel authored down into it is a cabin that catches with nothing to say so.
`TheTurningWheelStaysAboveTheCellTheCabinPassesThrough` is the assert.

**Not fixed, and named:** the wheel is still `HorizontalOrientable`, so one placed while facing the wrong
way validates the tower with its throat and station rails running across the line. The pylon head has had
exactly the same looseness since the pattern was written; the fix is to orient the crossarm's centre cell
from the footing below it for **both** blocks in one place, and a private rule on the decorative half would
leave the bug and add a rule.

## Power may be supplied ANYWHERE beside the line, and pools

Unchanged where it counts: `BEPylonBase.DriveSpeedOn` sums `TrueSpeed` over the loaded **drive housings**
standing beside the line, **one term per mechanical network** (`RopewayPower.PoolSpeed`, keyed on
`MechanicalNetwork.networkId`). That is the whole of the pooling — addition does not care about order, and a
housing whose chunk unloads simply stops contributing. `PoolSpeed` itself did not change; only the source of
the `(networkId, speed)` pairs moved, for the second time.

It is scanned over the housing table rather than walked from the towers, because a housing has no tower to
be indexed under — the one it answers to is resolved from its own position at lookup time. Same shape as
`BETensionWeight.OnLine`, same reason. Not the same predicate: the tensioner takes any tower of the line in
range and the housing takes the single nearest footing **that is on a line**, per "The drive housing — where
power enters" above, which states the same rule.

The per-network key is load-bearing rather than tidiness. `TrueSpeed` is `|Network.Speed × GearedRatio|`, so
two housings tapped off one axle run are two windows onto the same turning shaft. Summing per hookup let a
player buy speed with axles: each hookup also declares `HaulResistance` on that network, but the load only
*subtracts* from the settling speed while the sum *multiplies* what is read — three stubs off one maxed metal
mill read 5.6 blocks/s against a single hookup's 3.0. Adding load made the machine faster, which is the one
thing a load model must never do. First hookup seen on a network wins; they are all looking at one rope.

Two housings on **one** network are therefore not merely redundant, they are a **penalty**: each writes the
full `HaulResistance` onto that shared network every second and `MechanicalNetwork.updateNetwork` sums
resistances, so the settling speed drops while the pool counts the speed once. The second one makes the line
slower. The handbook says so now.

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
the far end is 320 away and outside the window. A housing beside that far end is not in `LoadedHousings`,
so it contributes nothing to `DriveSpeedOn`, and the line reads as having no drive at all — a cabin that
will not start, beside a message telling the player to build the thing they have already built. Every view
distance below 256 makes the window smaller; a singleplayer slider above 320 makes the problem vanish.

What the cap does buy is a **shorter** line: at stock settings anything under about 256 blocks end to end
is inside one player's window wherever they stand on it, which is most lines anyone builds. That is a
likelihood, not a guarantee, and the docs used to state it as a guarantee. The same correction applies to
the truncated-line class (`KNOWN-ISSUES.md` R1–R4), which is mitigated by the cap and not closed by it.

The 384 figure was repeated in code and asset comments as well as here — `pylonbase.json`'s
`//maxLineLength`, `BEPylonBase.DriveSpeedOn`, `BETensionWeight` — and those are the places a future reader
will trust. Anything that says a line fits inside the loaded window wants the same correction.

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

## The cheapness constraint, correctly scoped

Unchanged and still satisfied. `DECISIONS.md` §3 is about the **marginal** cost of a pylon and its span,
because a chained route multiplies it by every hop — not about the system's one-time cost. A windmill plus
axles for the whole ropeway is fine; a windmill per pylon would not be. Nothing here is metal-gated: a
wooden rotor, wooden axles and angled gears cost no metal at all.
