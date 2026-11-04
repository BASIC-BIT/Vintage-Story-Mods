# Future option: re-model cabin motion locally

**Status:** not done. Deliberately deferred at v0.1 — BASIC chose to ship with the known issues and keep
this on the shelf. Nothing depends on it; pick it up when the truncated-line bugs actually bite in play,
or when `maxLineLength` needs to exceed the server chunk radius.

## Why this exists

Three consecutive fix rounds on the cabin's motion produced roughly ten defects, each round's fix
spawning the next:

| round | fixed | review then found |
|---|---|---|
| 1 | truncation detection, dangling spans, dead netcode | 5 defects, incl. a **new** rider-teleport introduced by the fix |
| 2 | those 5 | 2 HIGH + 4 MED, one of which round 2 **created** (R1) |

That is not bad luck. It is the design saying the abstraction is wrong.

## The root cause

The cabin's position is a `Travelled` scalar measured along a `RopewayLine` whose identity, total
length, and canonical direction are all **derived from which chunks happen to be loaded**.
`RopewayLine.WalkChain` walks `modSystem.LoadedTowers`, so an unloaded tower silently terminates the
chain. Worse, the canonical-orientation flip (`RopewayLine.cs:107`) compares different endpoints
depending on load state, so **the same physical spot has two different `Travelled` values** based purely
on chunk residency. A rider crossing that boundary gets yanked.

Every piece of machinery below exists only to compensate for that instability:

`RopewayLine.Truncated` · `MinTravel` / `MaxTravel` · `MarkLoadedEnds` · `IsTruncated` ·
`RebaseTo` · `ParkAtNearestEnd` · the `Towers[0] != LineKey` re-canonicalisation check ·
`PickSurvivor` · the truncation refusals in `TryLink` / `ScanCandidates` / `SendCandidates` /
`CallTo` · lang keys `err-line-truncated`, `err-line-truncated-link`, `blockinfo-line-truncated`,
`cabin-held-truncated`.

## The proposal

A cabin does not need a global line. It needs three things, all local:

```
BlockPos currentTower;   // the tower I last departed
BlockPos targetTower;    // the tower I am heading to
double   t;              // 0..1 between them
```

Movement is `lerp(anchorOf(currentTower), anchorOf(targetTower), t)`. On arrival, ask `targetTower` for
the peer in its `Spans` that isn't `currentTower`:

- **one such peer** → that becomes the new `targetTower`, `currentTower` becomes the old target, `t = 0`.
- **no such peer** (the tower carries exactly one span) → genuine endpoint, reverse.
- **target unloaded** → hold at `currentTower` and retry next tick. A rider is itself a chunk-loading
  anchor, so this self-heals; an empty recall simply waits, which is correct rather than a deadlock.

Chunk state can no longer corrupt position, because position is never expressed in terms of the whole
chain. There is no truncation concept because there is no global chain to truncate.

## What this deletes

- `RopewayLine.Cumulative`, `TotalLength`, `AnchorIndexAt`, `PositionAt`, `DirectionAt`
- `Truncated`, `MinTravel`, `MaxTravel`, `MarkLoadedEnds`, `IsTruncated`
- `EntityRopewayCabin.Travelled`, `LineKey`, `RebaseTo`, `ParkAtNearestEnd`, the re-canonicalisation
  check, and the window clamps
- `RopewayLinkService.PickSurvivor` and the truncation refusals in the link/picker paths
- the four truncation lang keys
- **R1, R2, R3, R4 and R6 outright** — they are all statements about `Travelled` or the window

## What it keeps

`RopewayLine` stays, but only for **link-time** checks — `Contains` for cycle detection and chain length
for `maxLineLength`. Those run with a player standing there, so the surrounding chunks are loaded, and a
truncated result there is already handled correctly by refusing the link (F4). The cabin simply stops
depending on it at runtime.

## What it does not fix

- **E3**, the one-cabin-per-line guard, still needs persisted line identity. Arguably easier afterwards:
  ownership could live on the tower rather than being derived from a resolved line.
- **R5**'s "cabin's half degenerates to a single tower" case still needs a decision, though a local model
  makes it trivial — a cabin whose `currentTower` lost all spans just drops.

## Cost

Concentrated in `EntityRopewayCabin` and `RopewayLine`, and it is mostly deletion. The fiddly parts are
persistence (`currentTower` / `targetTower` / `t` replace `Travelled` in the entity's tree attributes —
old saves need a migration or a one-time reset to the nearest tower) and re-testing the link/unlink
interactions that currently funnel through `RebaseTo`.
