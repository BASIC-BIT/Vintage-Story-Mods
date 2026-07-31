using System.Collections.Generic;
using Vintagestory.API.MathTools;

namespace Ropeway.Tests;

public class RopewayMathTests
{
    [Theory]
    [InlineData(0.1, 1, 0)]
    [InlineData(1.0, 1, 1)]
    [InlineData(47.9, 48, 47)]
    [InlineData(48.0, 48, 48)]
    public void RopeCostRoundsUpAndRefundRoundsDown(double span, int expectedCost, int expectedRefund)
    {
        Assert.Equal(expectedCost, SpanMath.RopeCost(span));
        Assert.Equal(expectedRefund, SpanMath.RopeRefund(span));
    }

    [Fact]
    public void ChargeIsNeverLessThanRefundAndLosesAtMostOneRope()
    {
        for (var span = 0.05; span < 60; span += 0.05)
        {
            var cost = SpanMath.RopeCost(span);
            var refund = SpanMath.RopeRefund(span);

            Assert.True(cost >= refund, $"span {span}: cost {cost} < refund {refund}");
            Assert.True(cost - refund <= 1, $"span {span}: loss {cost - refund} > 1");
        }
    }

    [Fact]
    public void ZeroLengthSpanIsFree()
    {
        Assert.Equal(0, SpanMath.RopeCost(0));
        Assert.Equal(0, SpanMath.RopeRefund(0));
        Assert.Equal(0, SpanMath.RopeCost(-3));
    }

    [Theory]
    // The shipped rate. A full 48-block span has to stay inside what an early-game player can gather.
    [InlineData(48.0, 0.25, 12, 12)]
    [InlineData(30.0, 0.25, 8, 7)]
    [InlineData(4.0, 0.25, 1, 1)]
    [InlineData(0.4, 0.25, 1, 0)]
    public void RopeCostScalesWithRopePerBlock(double span, double rate, int expectedCost, int expectedRefund)
    {
        Assert.Equal(expectedCost, SpanMath.RopeCost(span, rate));
        Assert.Equal(expectedRefund, SpanMath.RopeRefund(span, rate));
        Assert.True(SpanMath.RopeCost(span, rate) >= SpanMath.RopeRefund(span, rate));
    }

    [Fact]
    public void SpanCheckSkipsTheTowerVolumeAtBothEndsButNeverMoreThanHalf()
    {
        // A tower's posts are player-chosen logs, invisible to the block filter, and reach 3.6 blocks out
        // from the sheave. Without the trim, every ray leaving the sheave exits through the tower's own
        // post and any span off the tower's axis is silently refused.
        Assert.Equal(SpanMath.TowerClearance, SpanMath.TrimForTowers(48));
        Assert.Equal(SpanMath.TowerClearance, SpanMath.TrimForTowers(9));

        // Short spans keep at least a token middle rather than skipping the check entirely.
        Assert.True(SpanMath.TrimForTowers(5) * 2 < 5);
        Assert.True(SpanMath.TrimForTowers(5) > 0);
        Assert.Equal(0, SpanMath.TrimForTowers(1));
        Assert.Equal(0, SpanMath.TrimForTowers(0.5));
    }

    [Fact]
    public void ClearanceCoversTheCabinBodyAndNotJustTheRopeLine()
    {
        // Cabin body is anchor-3.25..anchor+0.19; the sampled rows are anchor-ClearanceBelow-0.5..anchor+0.5.
        Assert.True(SpanMath.ClearanceBelow + 0.5 >= 3.25);
        Assert.Equal(1, SpanMath.ClearanceRadius);
    }

    [Fact]
    public void ConsumptionSpreadsAcrossStacksInOrder()
    {
        // 48 rope at a stack size of 16 is exactly three stacks - the normal case, not an edge case.
        var plan = SpanMath.PlanConsumption(new List<int> { 16, 16, 16 }, 48);

        Assert.NotNull(plan);
        Assert.Equal(new[] { 16, 16, 16 }, plan);
    }

    [Fact]
    public void ConsumptionStopsOnceTheQuantityIsMet()
    {
        var plan = SpanMath.PlanConsumption(new List<int> { 16, 16, 16, 4 }, 20);

        Assert.NotNull(plan);
        Assert.Equal(new[] { 16, 4, 0, 0 }, plan);
        Assert.Equal(20, Sum(plan));
    }

    [Fact]
    public void ShortInventoryConsumesNothing()
    {
        // 341 available, 342 wanted: the caller must be able to bail without having touched a single stack.
        var stacks = new List<int> { 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 5 };
        Assert.Equal(341, Sum(stacks.ToArray()));

        Assert.Null(SpanMath.PlanConsumption(stacks, 342));

        var exact = SpanMath.PlanConsumption(stacks, 340);
        Assert.NotNull(exact);
        Assert.Equal(340, Sum(exact));
    }

    [Fact]
    public void EmptyInventoryOnlySatisfiesAZeroRequest()
    {
        Assert.NotNull(SpanMath.PlanConsumption(new List<int>(), 0));
        Assert.Null(SpanMath.PlanConsumption(new List<int>(), 1));
        Assert.Null(SpanMath.PlanConsumption(null, 1));
    }

    [Fact]
    public void PositionAtHitsEveryAnchorExactly()
    {
        var line = Line((0, 64, 0), (10, 64, 0), (10, 64, 24));

        Assert.NotNull(line);
        Assert.Equal(34, line.TotalLength, 6);

        for (var i = 0; i < line.Anchors.Length; i++)
        {
            var p = line.PositionAt(line.Cumulative[i]);
            Assert.Equal(line.Anchors[i].X, p.X, 6);
            Assert.Equal(line.Anchors[i].Y, p.Y, 6);
            Assert.Equal(line.Anchors[i].Z, p.Z, 6);
        }
    }

    [Fact]
    public void PositionAtIsMonotonicAndClampsOutsideTheLine()
    {
        var line = Line((0, 64, 0), (10, 64, 0), (10, 64, 24));

        var previous = -1.0;
        for (var i = 0; i <= 100; i++)
        {
            var p = line.PositionAt(line.TotalLength * i / 100.0);
            var d = line.Anchors[0].DistanceTo(p);
            Assert.True(d >= previous - 1e-6, $"sample {i} moved backwards");
            previous = d;
        }

        Assert.Equal(line.Anchors[0].X, line.PositionAt(-5).X, 6);
        Assert.Equal(line.Anchors[^1].X, line.PositionAt(line.TotalLength + 5).X, 6);
    }

    [Fact]
    public void ChainWalkResolvesTheSameLineFromAnyMember()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(10, 64, 0);
        var c = new BlockPos(20, 64, 0);

        var spans = new Dictionary<BlockPos, List<BlockPos>>
        {
            [a] = new() { b },
            [b] = new() { a, c },
            [c] = new() { b }
        };

        IReadOnlyList<BlockPos> Peers(BlockPos p) => spans.TryGetValue(p, out var v) ? v : new List<BlockPos>();

        Assert.Equal(new[] { a, b, c }, RopewayLine.WalkChain(a, Peers));
        Assert.Equal(new[] { a, b, c }, RopewayLine.WalkChain(b, Peers));
        Assert.Equal(new[] { a, b, c }, RopewayLine.WalkChain(c, Peers));
    }

    /// <summary>
    /// Extend appends a tower before asking it for peers, so an unloaded tower joins the chain and then
    /// terminates the walk. The result is a shorter line that looks perfectly valid, and driving on it
    /// reverses the cabin at a false endpoint. Only the two ends can ever be the unloaded one.
    /// </summary>
    [Fact]
    public void ChainIsTruncatedWhenAnEndTowerIsNotLoaded()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(10, 64, 0);
        var c = new BlockPos(20, 64, 0);
        var d = new BlockPos(30, 64, 0);

        var spans = new Dictionary<BlockPos, List<BlockPos>>
        {
            [a] = new() { b },
            [b] = new() { a, c },
            [c] = new() { b, d },
            [d] = new() { c }
        };

        // D is unloaded: it answers no peers, so the walk stops on it.
        var loaded = new HashSet<BlockPos> { a, b, c };
        // null, not an empty list: that is exactly what the LoadedTowers lookup yields for an unloaded tower.
        IReadOnlyList<BlockPos> Peers(BlockPos p) => loaded.Contains(p) && spans.TryGetValue(p, out var v) ? v : null!;

        var chain = RopewayLine.WalkChain(a, Peers);

        Assert.Equal(new[] { a, b, c, d }, chain);
        Assert.True(Marked(chain, loaded).Truncated);

        // Everything loaded: the same chain, not truncated.
        loaded.Add(d);
        Assert.False(Marked(RopewayLine.WalkChain(a, Peers), loaded).Truncated);

        // The other end, from either walk start.
        loaded.Remove(a);
        Assert.True(Marked(RopewayLine.WalkChain(d, Peers), loaded).Truncated);
        Assert.True(Marked(RopewayLine.WalkChain(c, Peers), loaded).Truncated);

        // An interior tower always answered peersOf, so it is loaded by construction - a chain whose ends
        // are both loaded is whole.
        loaded.Add(a);
        Assert.False(Marked(RopewayLine.WalkChain(b, Peers), loaded).Truncated);
    }

    /// <summary>
    /// The rule that replaced "hold on any truncated line": a cabin may use everything up to the last tower
    /// the walk could actually query, and reversing is only allowed at an end that is loaded. Getting the
    /// window wrong either strands the cabin (too narrow) or reverses it at a false endpoint (too wide).
    /// </summary>
    [Fact]
    public void TheTravelWindowStopsAtTheLastLoadedTower()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(10, 64, 0);
        var c = new BlockPos(20, 64, 0);
        var d = new BlockPos(30, 64, 0);
        var chain = new[] { a, b, c, d };

        // Whole line: the window is the line, so both ends are proven endpoints and the cabin reverses.
        var whole = Marked(chain, new HashSet<BlockPos> { a, b, c, d });
        Assert.False(whole.Truncated);
        Assert.Equal(0, whole.MinTravel);
        Assert.Equal(whole.TotalLength, whole.MaxTravel);

        // Far end unloaded: run to C and hold. D is where the line might carry on, so it is not an endpoint.
        var farEnd = Marked(chain, new HashSet<BlockPos> { a, b, c });
        Assert.True(farEnd.Truncated);
        Assert.Equal(0, farEnd.MinTravel);
        Assert.Equal(20, farEnd.MaxTravel);
        Assert.True(farEnd.MaxTravel < farEnd.TotalLength);

        // Near end unloaded: the window starts at B instead.
        var nearEnd = Marked(chain, new HashSet<BlockPos> { b, c, d });
        Assert.True(nearEnd.Truncated);
        Assert.Equal(10, nearEnd.MinTravel);
        Assert.Equal(nearEnd.TotalLength, nearEnd.MaxTravel);

        // Both ends unloaded - reachable by resolving from an interior tower - still leaves a usable window.
        var both = Marked(chain, new HashSet<BlockPos> { b, c });
        Assert.Equal(10, both.MinTravel);
        Assert.Equal(20, both.MaxTravel);
    }

    /// <summary>
    /// Breaking a mid-line tower splits the line in two. Taking whichever half resolves first teleports the
    /// cabin to the far end of the wrong one, which is a hundreds-of-blocks move on a long line.
    /// </summary>
    [Fact]
    public void TheSurvivingHalfIsTheOneHoldingTheCabin()
    {
        var left = RopewayLine.FromTowers(new List<BlockPos> { new(0, 64, 0), new(10, 64, 0) });
        var right = RopewayLine.FromTowers(new List<BlockPos> { new(30, 64, 0), new(40, 64, 0) });

        // LineKey is the cabin's canonical Towers[0], so it names exactly one half.
        Assert.Same(right, RopewayLinkService.PickSurvivor(new[] { left, right }, new BlockPos(30, 64, 0)));
        Assert.Same(left, RopewayLinkService.PickSurvivor(new[] { left, right }, new BlockPos(10, 64, 0)));

        // A half that did not resolve (its chunk is gone too) must not shadow the one that did.
        Assert.Same(right, RopewayLinkService.PickSurvivor(new[] { null, right }, new BlockPos(40, 64, 0)));

        // Key on neither half - only reachable when the broken tower was the key itself, and then there is
        // one half anyway. First non-null, never null when something survived.
        Assert.Same(left, RopewayLinkService.PickSurvivor(new[] { left, right }, new BlockPos(99, 64, 0)));
        Assert.Null(RopewayLinkService.PickSurvivor(new RopewayLine[] { null!, null! }, new BlockPos(0, 64, 0)));
        Assert.Null(RopewayLinkService.PickSurvivor(null, new BlockPos(0, 64, 0)));
    }

    private static RopewayLine Marked(IReadOnlyList<BlockPos> chain, HashSet<BlockPos> loaded)
    {
        var line = RopewayLine.FromTowers(chain);
        line.MarkLoadedEnds(loaded.Contains);
        return line;
    }

    [Fact]
    public void SelfReferentialSpanTerminates()
    {
        var a = new BlockPos(0, 64, 0);
        var spans = new Dictionary<BlockPos, List<BlockPos>> { [a] = new() { a } };

        var chain = RopewayLine.WalkChain(a, p => spans.TryGetValue(p, out var v) ? v : new List<BlockPos>());

        Assert.Single(chain);
    }

    /// <summary>
    /// Rotating a mesh's local +Z by CableAngles must land exactly on the span direction, or cables point
    /// somewhere other than the tower they connect to. The rotations are applied here by hand rather than
    /// through the helper, so this checks the sign and order convention instead of restating it.
    /// </summary>
    [Theory]
    [InlineData(5, 0, 0)]     // due east
    [InlineData(-5, 0, 0)]    // due west
    [InlineData(0, 0, 7)]     // due south
    [InlineData(0, 0, -7)]    // due north
    [InlineData(0, 4, 0)]     // straight up - the degenerate case where horizontal distance is 0
    [InlineData(0, -4, 0)]    // straight down
    [InlineData(-4, 2, 6)]    // arbitrary climbing span
    [InlineData(9, -3, -2)]   // arbitrary descending span
    public void CableAnglesAimLocalZAlongTheSpan(double dx, double dy, double dz)
    {
        SpanMath.CableAngles(dx, dy, dz, out var radX, out var radY);

        // (0,0,1) rotated about X: (x, y*cos - z*sin, y*sin + z*cos)
        var y1 = -Math.Sin(radX);
        var z1 = Math.Cos(radX);

        // then about Y: (x*cos + z*sin, y, -x*sin + z*cos), with x still 0
        var x2 = z1 * Math.Sin(radY);
        var y2 = y1;
        var z2 = z1 * Math.Cos(radY);

        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);

        Assert.Equal(dx / length, x2, 5);
        Assert.Equal(dy / length, y2, 5);
        Assert.Equal(dz / length, z2, 5);
    }

    private static RopewayLine Line(params (int X, int Y, int Z)[] towers)
    {
        var positions = new List<BlockPos>();
        foreach (var t in towers) positions.Add(new BlockPos(t.X, t.Y, t.Z));
        return RopewayLine.FromTowers(positions);
    }

    private static int Sum(int[] values)
    {
        var total = 0;
        foreach (var v in values) total += v;
        return total;
    }
}
