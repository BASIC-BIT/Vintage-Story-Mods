# Power and energy storage — design direction

**Status:** direction agreed, not built. Spans two mods (`ropeway` and `flywheelpower`), so if
`flywheelpower` grows a docs home this belongs there as much as here.

## The decision

Mechanical power is **required** to run a ropeway. The earlier draft recommendation — power as a speed
bonus — was argued primarily from vanilla's weather: the `still` wind pattern (strength avg 0, no
`strengthNoise`, weight 1 of 4.4) means dead calm roughly **23% of play time**, in blocks averaging six
in-game hours, and no windmill of any size makes torque then. A departure gate would have refused to move
a quarter of the time.

BASIC's answer is better than weakening the requirement: **buffer the calm with storage.** A ropeway then
becomes a reason to build energy storage rather than just another consumer of it, and the weather
problem turns into a design opportunity.

## The storage taxonomy

Three devices that are not interchangeable, and the differences are what make them worth having:

| device | capacity | decay | what it is actually for |
|---|---|---|---|
| **Flywheel** (`flywheelpower`) | modest | yes — friction | smoothing a variable load, riding out a gust or a lull |
| **Raised mass** (tension weight, or a stone block hoisted up a tower) | large | none | persistent storage across days of calm |
| **Frictionless flywheel** — imbued, speculative | large | none | a battery |

The third row is the load-bearing observation, in BASIC's words: *"as soon as you have a flywheel that
can store even more power and doesn't degrade with friction over time, then you just made a battery."*
That is a strong argument for the imbuement being **expensive and late** — it does not improve a
flywheel, it collapses two distinct mechanics into one and deletes the reason raised mass exists.
If frictionless flywheels ever ship cheap, gravity storage is dead content.

## What this means for the ropeway

- The station's **tension weight** is not just ropeway machinery — it is a concrete instance of "raised
  mass as energy storage". Build it as a concrete thing now. Do **not** generalise it into a shared
  gravity-battery abstraction until there is a second real caller (a standalone storage block would be
  that caller; the ropeway alone is not).
- **A flywheel on the line is the intended answer to dead calm.** Hook a flywheel into the drive
  network and the ropeway keeps running through a lull. That is a cross-mod interaction worth making
  legible to the player — the handbook should say it in as many words, since nothing else will teach it.
- Design so `flywheelpower` is **optional**, not a hard dependency: the ropeway must work with any
  mechanical power source, with a flywheel being the thing that makes it *pleasant*.

## The cheapness constraint, correctly scoped

An earlier draft of this doc claimed requiring mechanical power conflicts with `DECISIONS.md` §3
("keep the recipe reachable early, do not gate this behind late-game metal"). **It does not.** BASIC
clarified: §3 is about the **marginal** cost of a pylon and its rope span, because a chained route
multiplies that cost by every hop. It was never about the system's one-time cost.

So the budgets are separate and both are satisfiable:

- **Per pylon, per span — must stay cheap.** This is what scales with route length. Wood, minimal metal.
- **Per line — a drive may be a real investment.** It is paid once no matter how long the route grows.

A windmill plus axles for the whole ropeway is fine. A windmill *per pylon* would not be.

## Power may be supplied at ANY tower on the line

BASIC: *"mechanical power can be supplied to any of the gantries across a chained route. Then it will be
transferred across the cable accordingly."*

**Decided.** Any tower's `ropeway:pylonbase` can take mechanical power; there is no designated Drive
Station. Contributions from multiple powered towers **pool** for the line, so adding a second windmill
somewhere along a long route helps rather than being ignored.

This is also more historically defensible than it might sound: **intermediate drive stations are real**
on long ropeways, exactly because driving a continuous haul loop anywhere along its length drives the
whole loop. Worth a handbook line — it is flavour the player will accept immediately once told.

Practical consequences:

1. **Put the drive where the resource is.** Wind on a ridge, water in the valley — you power the tower
   that suits the terrain, not the one the mod nominates. This is the real win, and it is why the
   pooling matters more than the realism.
2. **The store stays physical, and singular.** Recommend ONE tension-weight block placed somewhere on
   the line, wound by any powered tower on it. Keep the stored energy visible as a raised mass rather
   than as abstract per-line state — the whole appeal of gravity storage is that you can look at it and
   see how much you have left.
3. `AnchorOf` and the line walk already treat towers uniformly, so "any tower" costs no extra machinery
   here — it is cheaper than a designated drive end would have been.

## Why the store is what makes pooling safe

Pooling power across towers has a trap, and it is the one that already cost this mod three fix rounds:
**a tower in an unloaded chunk contributes nothing.** If the cabin drew live power from the line, a
windmill at the far end going out of render would slow or stop a cabin mid-span — the truncated-line
failure class, wearing a new hat.

The wound store removes it structurally. Drives charge the store *whenever their chunks are loaded*; the
cabin draws from the **store**, never from live power. An unloaded windmill simply stops contributing
charge; it cannot strand anyone, because the energy for the trip was already banked — and under the
option-B design the trip's cost is deducted at departure, so a journey that starts always finishes.

This is the strongest argument for storage over direct drive, and it is worth more than the realism
argument: **it decouples movement from chunk-load state entirely.** Any future design that has the cabin
read live network speed re-opens the whole class.

## Also queued

**Cargo in the cabin**, the way boats carry it. Independent of power, small, and it turns the ropeway
from a personal lift into freight infrastructure — which is what real material ropeways were for.
