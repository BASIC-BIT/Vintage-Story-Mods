# Ropeway v0.1 — known issues

State at v0.1 ship: build green, 43 ropeway tests passing. Nothing here blocks a first in-game session.
Everything below was found by reading code, not by playing — none of it has been observed in game yet.

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

## Deliberate behaviour worth knowing

- **A rider unseated mid-span by an explosion now falls.** The pre-fix behaviour put them at a tower,
  but that was a rider teleport (F3). Falling was chosen over teleporting; it is not an accident.
- **The cable is one mesh per block entity**, so a long span disappears when its own chunk leaves the
  view frustum even while the cable is still on screen. Per-chunk segments are the fix if it reads badly.
- **The cable is straight, not sagging.** The cabin travels the straight chord and `IsSpanClear`
  certifies a straight corridor; a drawn catenary would be a cable that lies about where the cabin goes.
- **Span ends are not clearance-checked.** `TrimForTowers` skips 4 blocks at each end so a tower's own
  posts don't block its own line, so an obstruction inside those end zones goes undetected.
- **Metal cost is higher than intended.** Two 5-wide brace gantries per tower is roughly 2.5 iron
  plates, above the "don't gate this behind a bunch of metal" target in `DECISIONS.md`.
