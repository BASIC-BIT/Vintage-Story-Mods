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

**Every powered tower declares the same load**, not a share of it. A share would have to be divided by how
many other towers are powered — a number that changes when somebody walks away and a chunk unloads, which is
the coupling this design refuses to have. Each drive pulls its own weight and their speeds add.

## Power may be supplied at ANY tower, and pools

Unchanged, and now simpler: `BEPylonBase.DriveSpeedOn` sums `TrueSpeed` over the line's loaded towers, **one
term per mechanical network** (`RopewayPower.PoolSpeed`, keyed on `MechanicalNetwork.networkId`). That is the
whole of the pooling — addition does not care about order, and a tower whose chunk unloads simply stops
contributing.

The per-network key is load-bearing rather than tidiness. `TrueSpeed` is `|Network.Speed × GearedRatio|`, so
two footings tapped off one axle run are two windows onto the same turning shaft. Summing per tower let a
player buy speed with axles: each hookup also declares `HaulResistance` on that network, but the load only
*subtracts* from the settling speed while the sum *multiplies* what is read — three stubs off one maxed metal
mill read 5.6 blocks/s against a single hookup's 3.0. Adding load made the machine faster, which is the one
thing a load model must never do. First hookup seen on a network wins; they are all looking at one rope.

Two mills on separate towers are a visibly faster cabin. Historically defensible too: intermediate drive
stations are real on long ropeways, because driving a continuous haul loop anywhere along its length drives
all of it.

## The cabin reads live network state — and what makes that safe

The old design forbade this in as many words. It is now the whole point, and the guarantee that replaces the
store is the **line-length cap**: `maxLineLength` is 320 blocks, inside the server's default
`MaxChunkRadius` of 384 (`ServerConfig.cs:925`, only ever raised by `ServerMain.cs:789`). Chain length is an
upper bound on the straight-line distance between any two towers, so **a player anywhere on a line holds
every tower of it loaded — drives included**. The same argument that closes the truncated-line failure class
(`KNOWN-ISSUES.md` R1–R4) also keeps a rider's drive loaded.

Raise that cap, or run a server configured below `MaxChunkRadius` 10, and a drive can go dark under a rider.
Nothing corrupts — the cabin slows or stops — but it stops for a reason the player cannot see. The note lives
on `pylonbase.json`'s `//maxLineLength`, where the old no-live-read rule used to be written down.

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
