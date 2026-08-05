using System;
using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// Runtime-only geometry of one ropeway line: the ordered tower chain and the cumulative distances
/// along it. Derived from the block entities, never persisted.
/// </summary>
public sealed class RopewayLine
{
    /// <summary>A corrupt self-referential span must terminate rather than hang the tick.</summary>
    public const int MaxTowersPerLine = 64;

    public BlockPos[] Towers;
    public Vec3d[] Anchors;
    public double[] Cumulative;
    public double TotalLength;

    /// <summary>
    /// The direction the path runs AT each anchor, which is the horizontal bisector of the two legs meeting
    /// there. This is the whole of the bend: <see cref="PositionAt"/> leaves the chord tangent to this and
    /// comes back to it, so a cabin, and anything else drawn from these two methods, turns through a tower
    /// instead of stepping round it. The two end anchors have one leg and take it, which is why a two-tower
    /// line is bent nowhere at all.
    /// <para>
    /// Scaled to the two legs' MEAN horizontal rate rather than left as a unit vector, and that is not
    /// decoration. <see cref="LegOf"/> hands back the horizontal part of a unit 3D leg, which is
    /// <c>cos(pitch)</c> long on a climbing span; the bend is <c>leg - tangent</c>, so a unit tangent would
    /// put a phantom bend into a perfectly STRAIGHT sloped span - 0.08 blocks of longitudinal wobble at a 30
    /// degree pitch, on a line with no corner in it. Matching the rate makes <c>leg - tangent</c> exactly
    /// zero wherever the bearing does not change, at any pitch, and on the level line the mod is measured on
    /// it is the plain unit bisector.
    /// </para>
    /// <para>
    /// Null at an anchor where the line DOUBLES BACK: two opposite legs have no bisector, every direction is
    /// equally between them, and the honest answer at a cusp is not to round it. A null tangent means "no
    /// bend here" to <see cref="PositionAt"/> and "fall through to the leg" to <see cref="DirectionAt"/>,
    /// which is exactly what both did before the bend existed.
    /// </para>
    /// </summary>
    public Vec3d[] Tangents;

    /// <summary>
    /// The walk ended on a tower it could not query, so this may be a prefix of the real line rather than
    /// the whole of it - a shorter <see cref="TotalLength"/> and possibly the opposite canonical
    /// orientation. See <see cref="MarkLoadedEnds"/>.
    /// </summary>
    public bool Truncated;

    /// <summary>
    /// The stretch of the line the loaded chunks can vouch for, as distances from <c>Towers[0]</c>. The whole
    /// line when nothing is truncated. Otherwise the unloaded end tower is excluded: it is not a proven
    /// endpoint - the real line may carry on into the chunk nobody can see - so reversing there is exactly
    /// the false-endpoint teleport. Running to the last loaded tower and holding is safe, and the window
    /// widens by itself when the chunk loads.
    /// </summary>
    public double MinTravel;

    /// <summary>See <see cref="MinTravel"/>.</summary>
    public double MaxTravel;

    /// <summary>Builds the cumulative-length table for an already-ordered tower chain. Pure.</summary>
    public static RopewayLine FromTowers(IReadOnlyList<BlockPos> towers)
    {
        if (towers == null || towers.Count < 2) return null;

        var line = new RopewayLine
        {
            Towers = new BlockPos[towers.Count],
            Anchors = new Vec3d[towers.Count],
            Cumulative = new double[towers.Count],
            Tangents = new Vec3d[towers.Count]
        };

        for (var i = 0; i < towers.Count; i++)
        {
            line.Towers[i] = towers[i];
            line.Anchors[i] = SpanMath.AnchorOf(towers[i]);
            if (i > 0)
            {
                line.Cumulative[i] = line.Cumulative[i - 1] + line.Anchors[i - 1].DistanceTo(line.Anchors[i]);
            }
        }

        // One pass, after the anchors exist and before anything asks where the path goes, so PositionAt stays
        // O(1) behind AnchorIndexAt rather than re-deriving a corner on every tick of every cabin.
        for (var i = 0; i < towers.Count; i++)
        {
            line.Tangents[i] = Bisect(i > 0 ? line.LegOf(i - 1) : null, i < towers.Count - 1 ? line.LegOf(i) : null);
        }

        line.TotalLength = line.Cumulative[towers.Count - 1];
        line.MaxTravel = line.TotalLength;
        return line;
    }

    /// <summary>
    /// The horizontal part of a span's unit direction. NOT re-normalised: keeping the <c>cos(pitch)</c>
    /// foreshortening is what lets <see cref="Tangents"/> cancel this term exactly on a straight span, so
    /// only a change of BEARING ever bends anything. Null when the span is degenerate or purely vertical -
    /// there is no bearing to bend, and dividing by that length is the one place this arithmetic could
    /// produce a NaN and hand it to <c>Entity.Pos</c>.
    /// </summary>
    private Vec3d LegOf(int i)
    {
        var a = Anchors[i];
        var b = Anchors[i + 1];
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var dz = b.Z - a.Z;

        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length < 1e-9 || Math.Sqrt(dx * dx + dz * dz) < 1e-9) return null;

        return new Vec3d(dx / length, 0, dz / length);
    }

    /// <summary>
    /// The tangent stored at an anchor: the horizontal bisector of the two legs meeting there, which on a
    /// straight run comes back out as the leg itself and so bends nothing. Null when the two are opposite -
    /// the line doubles back and no direction is between them. See <see cref="Tangents"/> for why the result
    /// carries the legs' mean horizontal rate rather than being a unit vector. Pure.
    /// </summary>
    private static Vec3d Bisect(Vec3d into, Vec3d outOf)
    {
        if (into == null) return outOf;
        if (outOf == null) return into;

        var sum = into.Clone().Normalize().Add(outOf.Clone().Normalize());
        if (sum.Length() < 1e-6) return null;

        return sum.Normalize().Mul((into.Length() + outOf.Length()) / 2);
    }

    /// <summary>
    /// Position of a tower in the chain, or -1 when it is not on this line. The one place anything turns a
    /// tower into a distance along the line - <see cref="Cumulative"/> at that index - so calling the cabin
    /// to a tower never recomputes geometry that is already tabulated here.
    /// </summary>
    public int IndexOf(BlockPos tower)
    {
        if (Towers == null || tower == null) return -1;

        for (var i = 0; i < Towers.Length; i++)
        {
            if (tower.Equals(Towers[i])) return i;
        }

        return -1;
    }

    /// <summary>
    /// Whether a distance along the line lands on a tower rather than out in the middle of a span. Any tower
    /// can now be called to and parked at, so the cabin's "never resume from mid-span" recovery has to ask
    /// this - assuming the two ends are the only places a cabin can legitimately be standing would drag one
    /// off the interior tower it was called to on the very next tick.
    /// </summary>
    public bool IsAtTower(double travelled, double tolerance)
    {
        return TowerAt(travelled, tolerance) >= 0;
    }

    /// <summary>
    /// Index of the tower standing at a distance along the line, or -1 for a point out in a span. The
    /// inverse of <see cref="Cumulative"/>, and what lets the rider's stop key carry on from the tower a
    /// trip is already aimed at rather than from where the cabin happens to be.
    /// </summary>
    public int TowerAt(double travelled, double tolerance)
    {
        if (Cumulative == null) return -1;

        for (var i = 0; i < Cumulative.Length; i++)
        {
            if (Math.Abs(Cumulative[i] - travelled) <= tolerance) return i;
        }

        return -1;
    }

    /// <summary>Index of the span the given distance falls inside, clamped to the line.</summary>
    public int AnchorIndexAt(double travelled)
    {
        if (Cumulative == null || Cumulative.Length < 2) return 0;
        if (travelled <= 0) return 0;
        if (travelled >= TotalLength) return Cumulative.Length - 2;

        for (var i = 1; i < Cumulative.Length; i++)
        {
            if (travelled < Cumulative[i]) return i - 1;
        }

        return Cumulative.Length - 2;
    }

    /// <summary>
    /// The span a cabin standing at <paramref name="travelled"/> is about to travel THROUGH, given which way
    /// it is running. <see cref="AnchorIndexAt"/> has no direction term: standing exactly on tower k it
    /// answers k, the span k-&gt;k+1, which is the span ahead of an outbound cabin and the span BEHIND an
    /// inbound one - that is entering k-1-&gt;k. Only ever right by accident while the two ends were the sole
    /// place a cabin could stand and depart; "parked at an interior tower, running inbound" is what calling
    /// to any tower makes ordinary, and certifying the wrong span there is a rider driven through stone.
    /// </summary>
    public int SpanAheadOf(double travelled, bool outbound)
    {
        var index = AnchorIndexAt(travelled);

        // Exactly on a tower boundary is the only case that differs: mid-span, both directions traverse the
        // same span. index > 0 can only come from the loop, so Cumulative is real here.
        if (!outbound && index > 0 && travelled <= Cumulative[index]) index--;

        return index;
    }

    /// <summary>
    /// Where the line runs at a distance along it: the chord between two anchors, bent at each end so the
    /// path leaves and arrives on the tower's own <see cref="Tangents"/> rather than turning a corner in one
    /// step. The bend is a cubic Hermite half at each end, which collapses to <see cref="BendOffset"/>.
    /// <para>
    /// Three properties are load-bearing and all three are free from the shape of the formula rather than
    /// checked for. It is ZERO at every anchor, so <see cref="Cumulative"/>, <see cref="TowerAt"/>,
    /// <see cref="SpanAheadOf"/>, every call target and <c>DropGhostPassengers</c>'s footing arithmetic mean
    /// exactly what they meant before. It is zero again beyond <see cref="SpanMath.TrimForTowers"/> of each
    /// anchor, so no ray <see cref="SpanMath.IsSpanClear"/> casts sees a different path from the one it saw
    /// before the bend existed and the clearance code needs no change. And it is HORIZONTAL: Y stays the
    /// plain chord lerp, which is what keeps the vertical fit through a tower and <c>ClimbOn</c>'s meaning
    /// untouched.
    /// </para>
    /// <para>
    /// The containment argument is NOT a safety proof and an earlier version of this comment read as one. The
    /// trimmed stretch is skipped precisely BECAUSE the tower's own structure stands in it - it is ground
    /// nobody certified, not ground certified clear - and the bend swings up to 0.42 blocks laterally inside
    /// it, so the uncertified swept corridor at a corner tower widens from 1.0 to 1.42 blocks of half-width.
    /// What makes that safe is the measurement, not the containment: the swept cabin is compared against the
    /// posts directly, corner by corner, in
    /// <c>RopewayAssetContractTests.TheBentPathNeverDrivesTheCabinDeeperIntoAPostThanTheStraightOneDid</c>.
    /// The containment buys one thing only, and it is worth having on its own: nothing outside the trim moves,
    /// so no span that used to certify stops certifying.
    /// </para>
    /// </summary>
    public Vec3d PositionAt(double travelled)
    {
        if (Anchors == null || Anchors.Length == 0) return null;
        if (Anchors.Length == 1) return Anchors[0].Clone();
        if (travelled <= 0) return Anchors[0].Clone();
        if (travelled >= TotalLength) return Anchors[Anchors.Length - 1].Clone();

        var i = AnchorIndexAt(travelled);
        var segment = Cumulative[i + 1] - Cumulative[i];
        var t = segment <= 0 ? 0 : (travelled - Cumulative[i]) / segment;
        var a = Anchors[i];
        var b = Anchors[i + 1];
        var point = new Vec3d(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        var leg = LegOf(i);
        if (leg == null || Tangents == null) return point;

        // The same expression at both ends, ADDED at the anchor being approached and SUBTRACTED at the one
        // being left. That sign is not a case: "pointing at the anchor" reverses both the leg and the
        // tangent, and reversing both flips the sign of their difference.
        var window = SpanMath.TrimForTowers(segment);
        return point
            .Add(BendOffset(leg, Tangents[i + 1], Cumulative[i + 1] - travelled, window))
            .Sub(BendOffset(leg, Tangents[i], travelled - Cumulative[i], window));
    }

    /// <summary>
    /// How far the bend pushes the path off its chord, <paramref name="distance"/> blocks from an anchor
    /// whose tangent is <paramref name="bisector"/>, over a window of <paramref name="window"/> blocks.
    /// Pure, and the whole curve: a cubic Hermite from the chord point back with the leg as its tangent, to
    /// the anchor with the bisector as its tangent, both scaled by the window, reduces to
    /// <c>window * s(1-s)^2 * (leg - bisector)</c> with <c>s = distance / window</c> - one line, both sides,
    /// no sign case.
    /// <para>
    /// <c>s(1-s)^2</c> is zero at both ends and peaks at 4/27 one third of the way out, so the curve passes
    /// through the anchor exactly, rejoins the chord with no step, and reaches
    /// <c>4/27 * window * |leg - bisector|</c> at its furthest - 0.45 blocks at a right angle with the
    /// window at its full 4. The bend bows OUTWARD of the corner. That is forced rather than chosen: a curve
    /// that both passes through the vertex and is tangent to the bisector there cannot also cut inside it,
    /// and cutting inside is what an inscribed fillet does - measured at 0.659 blocks of post penetration at
    /// R = 4 against 0.034 for no bend at all, because the far half of a 4-block cabin swings out through the
    /// post while the origin misses the tower entirely.
    /// </para>
    /// </summary>
    public static Vec3d BendOffset(Vec3d leg, Vec3d bisector, double distance, double window)
    {
        var weight = BendWeight(distance, window);
        return weight == 0 || leg == null || bisector == null
            ? new Vec3d()
            : new Vec3d(weight * (leg.X - bisector.X), 0, weight * (leg.Z - bisector.Z));
    }

    /// <summary>The bend's scalar weight, <c>window * s(1-s)^2</c>, and zero outside the window. Pure.</summary>
    public static double BendWeight(double distance, double window)
    {
        if (window <= 0 || distance <= 0 || distance >= window) return 0;

        var s = distance / window;
        return window * s * (1 - s) * (1 - s);
    }

    /// <summary>
    /// <see cref="BendWeight"/> differentiated with respect to distance: <c>(1-s)(1-3s)</c>. Exactly 1 AT the
    /// anchor, which is what makes <see cref="DirectionAt"/> come out as the tangent there rather than near
    /// it, so the bounds are closed at both ends where <see cref="BendWeight"/>'s are open. Pure.
    /// </summary>
    public static double BendSlope(double distance, double window)
    {
        if (window <= 0 || distance < 0 || distance > window) return 0;

        var s = distance / window;
        return (1 - s) * (1 - 3 * s);
    }

    /// <summary>
    /// Which way the path itself is running at a distance along the line: the unit tangent of
    /// <see cref="PositionAt"/>, and nothing else. Pure; the rendered yaw is its main consumer
    /// (<c>EntityRopewayCabin.Place</c>) and <c>ClimbOn</c> reads the vertical component for the drive load.
    /// AT a tower this is that tower's <see cref="Tangents"/> entry - the corner's bisector - because the
    /// bend is built to arrive on it, so nothing here special-cases towers.
    /// <para>
    /// THE TOMBSTONE, ANSWERED. An "angle-station" law that held each tower's own cardinal passage axis
    /// across a window at the vertex was tried and REVERTED, and this comment used to end "do not re-attempt
    /// the yaw law without bending the path in whatever model claims it works". The path has now been bent
    /// and the law re-run on it, and the answer is still no. Held as a hard cardinal across the full 4-block
    /// window on the bent path it measures 1.000 blocks of post penetration at a 90 degree corner with the
    /// tower perfectly on the bisector, against 0.034 for the plain leg bearing it replaced; 1.000 against
    /// 0.033 at 45 degrees, and 0.740 against 0.000 at 30. Worse in three of nine measured cells and better
    /// in none. The mechanism was never about the origin stepping: a tower's facing is one of four cardinals
    /// and so is up to 45 degrees off the way the cabin is actually going, and holding it makes a 4-block
    /// cabin crab across its own passage whether the path under it is straight or curved.
    /// </para>
    /// <para>
    /// What DID change is that the yaw no longer steps. The bearing used to jump by the whole turn angle in
    /// one tick at the vertex, because the chord's direction jumped; it is now continuous through a tower and
    /// square to the passage as it passes, and that came from bending the PATH rather than from overriding
    /// the yaw. The safe law is the one this method now implements - the tangent of the bent path, which at a
    /// tower is the bisector and never the cardinal. The one term added on top of it is
    /// <c>EntityRopewayCabin.YawLead</c>, which cancels the client's own rotation easing rather than choosing
    /// a heading. See <c>docs/KNOWN-ISSUES.md</c> and
    /// <c>docs/agentic/ingest/cablecar/TURNING-SPEC.md</c>.
    /// </para>
    /// <para>
    /// SCOPED, and the scope is the whole of the claim. Across the nine cells that were measured - turns of
    /// 90, 45 and 30 degrees against a tower facing 0, 22.5 and 45 degrees off the bisector - this law is
    /// never worse than the plain leg bearing and is better twice. It is NOT never worse everywhere, and this
    /// sentence used to say so: past about 125 degrees of turn the bend is the deeper of the two, because a
    /// hairpin's bisector is nearly perpendicular to both legs and arriving on it points a 4-block cabin
    /// broadside across its own passage. Worst measured is a 164.6 degree corner 45 degrees off the bisector,
    /// 0.529 blocks of post straight against a full 1.000 bent. Nothing in <c>RopewayLinkService</c>
    /// constrains the angle between two spans, so those corners are buildable - what it does now is WARN when
    /// one is built (<c>TryLink</c>), and the acceptance test carries the hairpin as a pinned row so this
    /// paragraph cannot drift back to "anywhere".
    /// </para>
    /// <para>
    /// The vertical is deliberately still the chord's. <see cref="PositionAt"/> bends in plan only, so two
    /// spans of different pitch meeting at a tower really do kink there and reporting a smooth pitch would be
    /// a lie to <c>ClimbOn</c>. Horizontally - which is all the rendered yaw reads - the tangent is C1.
    /// </para>
    /// </summary>
    public Vec3d DirectionAt(double travelled)
    {
        if (Anchors == null || Anchors.Length < 2) return new Vec3d(0, 0, 1);

        var i = AnchorIndexAt(travelled);
        var dir = Anchors[i + 1].Clone().Sub(Anchors[i]);
        if (dir.Length() < 1e-9) return new Vec3d(0, 0, 1);
        dir.Normalize();

        var leg = LegOf(i);
        if (leg == null || Tangents == null) return dir;

        // PositionAt's two bend terms, differentiated. Both come out negative, for different reasons: the
        // term for the anchor ahead is added there but its distance FALLS as travelled rises, and the term
        // for the anchor behind is subtracted there while its distance rises.
        var window = SpanMath.TrimForTowers(Cumulative[i + 1] - Cumulative[i]);
        Steer(dir, BendSlope(Cumulative[i + 1] - travelled, window), leg, Tangents[i + 1]);
        Steer(dir, BendSlope(travelled - Cumulative[i], window), leg, Tangents[i]);

        return dir.Length() < 1e-9 ? new Vec3d(0, 0, 1) : dir.Normalize();
    }

    private static void Steer(Vec3d dir, double slope, Vec3d leg, Vec3d bisector)
    {
        if (slope == 0 || bisector == null) return;

        dir.X -= slope * (leg.X - bisector.X);
        dir.Z -= slope * (leg.Z - bisector.Z);
    }

    /// <summary>
    /// Walks the span chain from any member tower out to both ends. A tower carries at most two spans, so a
    /// line is a path and this is a walk rather than a search. Pure: <paramref name="peersOf"/> is the only
    /// world access, which is what makes it testable.
    /// </summary>
    public static List<BlockPos> WalkChain(BlockPos start, Func<BlockPos, IReadOnlyList<BlockPos>> peersOf)
    {
        var chain = new List<BlockPos>();
        if (start == null || peersOf == null) return chain;

        chain.Add(start);
        var seen = new HashSet<BlockPos> { start };

        var peers = peersOf(start);
        if (peers == null) return chain;

        if (peers.Count > 0) Extend(chain, seen, start, peers[0], peersOf, append: true);
        if (peers.Count > 1) Extend(chain, seen, start, peers[1], peersOf, append: false);

        // Canonical orientation. Travelled is measured from Towers[0], so the chain must not flip just
        // because the walk started from the other end of the line.
        if (chain.Count > 1 && ComparePos(chain[0], chain[chain.Count - 1]) > 0) chain.Reverse();

        return chain;
    }

    /// <summary>
    /// Records which of the chain's two ends <see cref="WalkChain"/> could actually query, as
    /// <see cref="Truncated"/> plus the <see cref="MinTravel"/>/<see cref="MaxTravel"/> window.
    /// <see cref="Extend"/> adds a tower before asking it for peers, so an unloaded tower joins the chain and
    /// then terminates the walk one hop past the loaded region - which means only the two ends can ever be
    /// the unloaded one, and an unloaded end is exactly the tower whose remaining peers nobody can see.
    /// Conservative on purpose: it cannot tell "unloaded and last" from "unloaded and there is more line
    /// behind it", so it treats both as unproven. Pure.
    /// </summary>
    public void MarkLoadedEnds(Func<BlockPos, bool> isLoaded)
    {
        if (Towers == null || Towers.Length < 2 || isLoaded == null) return;

        var startLoaded = isLoaded(Towers[0]);
        var endLoaded = isLoaded(Towers[Towers.Length - 1]);

        Truncated = !startLoaded || !endLoaded;
        MinTravel = startLoaded ? 0 : Cumulative[1];
        MaxTravel = endLoaded ? TotalLength : Cumulative[Cumulative.Length - 2];
    }

    /// <summary>
    /// Total order on positions, and the reason a chain does not flip depending on which end the walk
    /// started from. Public because any other choice between blocks must be made on this rather than on
    /// dictionary enumeration order, which is chunk-load order and can differ across restarts.
    /// </summary>
    public static int ComparePos(BlockPos a, BlockPos b)
    {
        if (a.X != b.X) return a.X.CompareTo(b.X);
        if (a.Z != b.Z) return a.Z.CompareTo(b.Z);
        if (a.Y != b.Y) return a.Y.CompareTo(b.Y);
        return a.dimension.CompareTo(b.dimension);
    }

    private static void Extend(
        List<BlockPos> chain,
        HashSet<BlockPos> seen,
        BlockPos previous,
        BlockPos current,
        Func<BlockPos, IReadOnlyList<BlockPos>> peersOf,
        bool append)
    {
        var steps = 0;
        while (current != null && seen.Add(current) && ++steps <= MaxTowersPerLine)
        {
            if (append) chain.Add(current);
            else chain.Insert(0, current);

            var peers = peersOf(current);
            BlockPos next = null;
            if (peers != null)
            {
                for (var i = 0; i < peers.Count; i++)
                {
                    if (peers[i] != null && !peers[i].Equals(previous))
                    {
                        next = peers[i];
                        break;
                    }
                }
            }

            previous = current;
            current = next;
        }
    }

    /// <summary>
    /// Cached line through any member tower. Never persisted - it is derived from the blocks, so it cannot
    /// desync from them. Returns null while the tower is unknown or the chain has fewer than two towers.
    /// </summary>
    public static RopewayLine GetOrBuild(RopewayModSystem modSystem, BlockPos anyTower)
    {
        if (modSystem == null || anyTower == null) return null;
        if (modSystem.LineCache.TryGetValue(anyTower, out var cached)) return cached;
        if (!modSystem.LoadedTowers.ContainsKey(anyTower)) return null;

        var towers = WalkChain(anyTower, pos => modSystem.LoadedTowers.TryGetValue(pos, out var be) ? be.Spans : null);
        var line = FromTowers(towers);
        if (line == null) return null;

        // Still a real line - the cabin's "line is gone for good" test wants it - but with the unproven end
        // fenced off so nobody parks or reverses on a false endpoint.
        line.MarkLoadedEnds(pos => modSystem.LoadedTowers.ContainsKey(pos));

        foreach (var tower in line.Towers) modSystem.LineCache[tower] = line;
        return line;
    }
}
