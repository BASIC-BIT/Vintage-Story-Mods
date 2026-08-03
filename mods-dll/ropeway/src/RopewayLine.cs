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
            Cumulative = new double[towers.Count]
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

        line.TotalLength = line.Cumulative[towers.Count - 1];
        line.MaxTravel = line.TotalLength;
        return line;
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
        return new Vec3d(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    /// <summary>
    /// Which way the cabin points at a distance along the line: the bearing of the span it is on, and
    /// nothing else. Pure; the rendered yaw is its only consumer (<c>EntityRopewayCabin.Place</c>).
    /// <para>
    /// An "angle-station" law that held each tower's own passage axis across the vertex was tried and
    /// REVERTED - it is a regression, measured, not a matter of taste. <see cref="PositionAt"/> swings the
    /// cabin's ORIGIN onto the outgoing leg at the vertex, so a cabin holding the incoming axis crab-walks
    /// and its 4-block tail sweeps into the post on the outside of the bend: post penetration went 0.033 -&gt;
    /// 1.000 blocks at a 45 degree corner and 0.000 -&gt; 0.331 at 30 degrees against this plain bearing. What
    /// actually reduced penetration was the 5-wide passage. See <c>docs/KNOWN-ISSUES.md</c>; do not
    /// re-attempt the yaw law without bending the path in whatever model claims it works.
    /// </para>
    /// <para>
    /// The narrow half of it is fine and did ship, but NOT here: <c>EntityRopewayCabin.SquareTo</c> squares
    /// the cabin to a tower's passage only while it is STOPPED there, where the origin does not move and
    /// there is nothing to crab away from. This method is the bearing for a cabin in motion and must stay
    /// the plain leg - a passing cabin's yaw comes from here and from nowhere else.
    /// </para>
    /// </summary>
    public Vec3d DirectionAt(double travelled)
    {
        if (Anchors == null || Anchors.Length < 2) return new Vec3d(0, 0, 1);

        var i = AnchorIndexAt(travelled);
        var dir = Anchors[i + 1].Clone().Sub(Anchors[i]);
        return dir.Length() < 1e-9 ? new Vec3d(0, 0, 1) : dir.Normalize();
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
    /// Total order on positions. Public because it is also what makes every OTHER choice between blocks
    /// deterministic - which of two merged tension weights is the live one, which peer an orphaned weight
    /// re-binds to - rather than dictionary enumeration order, which is chunk-load order and can differ
    /// across restarts.
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

    /// <summary>Lowest of a set of positions in <see cref="ComparePos"/> order, or null when there are none.</summary>
    public static BlockPos Lowest(IReadOnlyList<BlockPos> positions)
    {
        BlockPos best = null;
        for (var i = 0; positions != null && i < positions.Count; i++)
        {
            if (positions[i] != null && (best == null || ComparePos(positions[i], best) < 0)) best = positions[i];
        }

        return best;
    }

    /// <summary>
    /// The chain two towers WOULD form if they were linked, without linking them. Rules that have to refuse
    /// a link before the rope is spent - the store's capacity against the dearest trip on the result - need
    /// the merged geometry, and this is the only place it can be ordered: the two towers each carry at most
    /// one span (<see cref="RopewayLinkService.TryLink"/> refuses a full one), so each is an END of its own
    /// chain and the merge is simply chain-from followed by chain-to. A tower with no line of its own is a
    /// chain of one. Pure, and therefore tested.
    /// </summary>
    public static RopewayLine Preview(RopewayLine lineFrom, BlockPos from, RopewayLine lineTo, BlockPos to)
    {
        var towers = new List<BlockPos>();
        towers.AddRange(JoiningAt(lineFrom, from, last: true));
        towers.AddRange(JoiningAt(lineTo, to, last: false));
        return FromTowers(towers);
    }

    /// <summary>One side of a <see cref="Preview"/>, oriented so the joining tower is the end that joins.</summary>
    private static IReadOnlyList<BlockPos> JoiningAt(RopewayLine line, BlockPos tower, bool last)
    {
        if (line?.Towers == null || line.IndexOf(tower) < 0) return new[] { tower };

        var chain = new List<BlockPos>(line.Towers);
        if (chain[chain.Count - 1].Equals(tower) != last) chain.Reverse();
        return chain;
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
