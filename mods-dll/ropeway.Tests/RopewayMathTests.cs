using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
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
        // A tower's posts are player-chosen logs, invisible to the block filter, and stand under the sheave
        // at up to 2.55 blocks horizontally. Without the trim, the clearance rays leave the sheave, drop
        // three blocks and run straight into the tower's own posts, and every span is silently refused.
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

    /// <summary>
    /// The cabin never goes backwards, and stops at the ends. Asserted as the PROJECTION ONTO EACH SPAN'S OWN
    /// CHORD, strictly increasing, which is the property that actually holds and has teeth.
    /// <para>
    /// Two weaker versions were here first and both are worth naming. Distance from <c>Anchors[0]</c> is what
    /// the straight path used and is a false negative on a curve - a step round the outside of a corner
    /// really can shorten the straight-line distance to a tower twenty blocks behind without the cabin having
    /// reversed. Its replacement, <c>step . DirectionAt</c>, is close to a tautology: the step is a finite
    /// difference of <c>PositionAt</c> and the heading is the analytic tangent of that same function, so it
    /// reduces to "the tangent is not zero" and cannot see a loop or a backward excursion. The chord
    /// projection can: its derivative is <c>1 - BendSlope * (1 - cos(turn/2))</c>, worst at the anchor where
    /// <c>BendSlope</c> is 1, so it is bounded below by <c>cos(turn/2)</c> and a bend that overshot its own
    /// window or reversed its sign would drive it negative.
    /// </para>
    /// </summary>
    [Fact]
    public void PositionAtOnlyEverMovesForwardAndClampsOutsideTheLine()
    {
        // Two right angles, the second doubling the line back across itself, so the assert is exercised where
        // distance-from-the-start is not a usable proxy. The 6-block span also trims to 2.5, i.e. a window
        // below the full TowerClearance.
        var line = Line((0, 64, 0), (10, 64, 0), (10, 64, 24), (4, 64, 24));

        for (var i = 0; i < line.Anchors.Length - 1; i++)
        {
            var chord = line.Anchors[i + 1].Clone().Sub(line.Anchors[i]).Normalize();
            var span = line.Cumulative[i + 1] - line.Cumulative[i];
            var previous = double.NegativeInfinity;

            for (var k = 0; k <= 2000; k++)
            {
                var t = line.Cumulative[i] + span * k / 2000.0;
                var along = line.PositionAt(t).Sub(line.Anchors[i]).Dot(chord);

                Assert.True(along > previous, $"span {i} sample {k} at {t:0.###} went backwards along its chord");
                previous = along;
            }
        }

        Assert.Equal(line.Anchors[0].X, line.PositionAt(-5).X, 6);
        Assert.Equal(line.Anchors[^1].X, line.PositionAt(line.TotalLength + 5).X, 6);
    }

    /// <summary>
    /// The bend's first requirement: it passes through every tower EXACTLY, on that corner's bisector. The
    /// first half is what keeps Cumulative, TowerAt, SpanAheadOf, every call target and the ghost-drop's
    /// footing arithmetic meaning what they meant before there was a curve at all; the second is the whole
    /// point of the curve, because a cabin arriving on the bisector is a cabin square to the passage.
    /// </summary>
    [Fact]
    public void TheBendPassesThroughEveryTowerOnTheBisector()
    {
        var line = Line((0, 64, 0), (0, 64, 40), (40, 64, 40), (40, 64, 0));

        for (var i = 0; i < line.Anchors.Length; i++)
        {
            var p = line.PositionAt(line.Cumulative[i]);
            Assert.Equal(line.Anchors[i].X, p.X, 9);
            Assert.Equal(line.Anchors[i].Y, p.Y, 9);
            Assert.Equal(line.Anchors[i].Z, p.Z, 9);
        }

        // Both corners are 90 degrees between cardinals, so the bisectors are the two diagonals; the ends
        // keep their single leg. Read off the anchors rather than typed, and compared as a BEARING - the
        // vertical is still the chord's, by design.
        for (var i = 0; i < line.Anchors.Length; i++)
        {
            var into = i > 0 ? Bearing(line.Anchors[i - 1], line.Anchors[i]) : (double?)null;
            var outOf = i < line.Anchors.Length - 1 ? Bearing(line.Anchors[i], line.Anchors[i + 1]) : (double?)null;
            var expected = into == null ? outOf!.Value
                : outOf == null ? into.Value
                : into.Value + GameMath.AngleRadDistance((float)into.Value, (float)outOf.Value) / 2;

            var dir = line.DirectionAt(line.Cumulative[i]);
            Assert.Equal(0, GameMath.AngleRadDistance((float)expected, (float)Math.Atan2(dir.X, dir.Z)), 6);
        }

        // And the heading does not step at a tower the way the plain leg bearing used to: sampled either
        // side of the middle corner it turns by a hair, not by the whole 90 degrees.
        var before = line.DirectionAt(line.Cumulative[2] - 0.01);
        var after = line.DirectionAt(line.Cumulative[2] + 0.01);
        var turned = Math.Abs(GameMath.AngleRadDistance(
            (float)Math.Atan2(before.X, before.Z), (float)Math.Atan2(after.X, after.Z)));
        Assert.True(turned < 0.05, $"the heading stepped by {turned * GameMath.RAD2DEG:0.##} degrees at the tower");
    }

    /// <summary>
    /// The bend's second requirement, and the whole clearance argument in one assert: it lives entirely
    /// inside the stretch at each end of a span that <see cref="SpanMath.IsSpanClear"/> already trims away
    /// before it casts a single ray. So the curve cannot wander into ground nobody certified, and
    /// <c>IsSpanClear</c>, <c>ClearanceRadius</c>, <c>ClearanceBelow</c> and <c>TrimForTowers</c> all stay
    /// exactly as they were. Widen the window past the trim and this is what fails.
    /// </summary>
    [Fact]
    public void TheBendStaysInsideTheStretchNoClearanceRayVisits()
    {
        // A 90 degree corner on a long span (trims to the full 4) and a 6-block one (trims to 2.5).
        var line = Line((0, 64, 0), (40, 64, 0), (40, 64, 6), (46, 64, 6));
        var peak = 0.0;

        for (var i = 0; i < line.Anchors.Length - 1; i++)
        {
            var segment = line.Cumulative[i + 1] - line.Cumulative[i];
            var trim = SpanMath.TrimForTowers(segment);
            Assert.True(trim > 0);

            for (var k = 0; k <= 4000; k++)
            {
                var along = segment * k / 4000.0;
                var offset = Perpendicular(line.Anchors[i], line.Anchors[i + 1], line.PositionAt(line.Cumulative[i] + along));

                if (along < trim || along > segment - trim)
                {
                    if (i == 0) peak = Math.Max(peak, offset);
                    continue;
                }

                Assert.Equal(0, offset, 12);
            }
        }

        // And it bows by as much as it is meant to, which is what stops a right window hiding a wrong curve.
        // The first span is 40 blocks, so its window is the full TowerClearance of 4 and its far anchor is a
        // right angle: the furthest the path leaves that chord is 4/27 * 4 * sin(45 deg) = 0.419 blocks.
        Assert.Equal(4.0 / 27 * SpanMath.TowerClearance * Math.Sin(Math.PI / 4), peak, 4);
        Assert.Equal(0.419, peak, 3);
    }

    /// <summary>
    /// The three shapes of line that have no corner to bend, each of which is a way to produce a NaN or a
    /// jump if the bend is written as though every anchor had two legs.
    /// </summary>
    [Fact]
    public void ALineWithNoCornerToTurnIsLeftDeadStraight()
    {
        // 1. Two towers: no interior anchor at all, so the path is the chord and the heading is constant.
        var pair = Line((0, 64, 0), (30, 64, 40));
        for (var i = 0; i <= 100; i++)
        {
            var t = pair.TotalLength * i / 100.0;
            var p = pair.PositionAt(t);
            Assert.Equal(0, Perpendicular(pair.Anchors[0], pair.Anchors[1], p), 12);
            Assert.Equal(0.6, pair.DirectionAt(t).X, 9);
        }

        // 2. Straight through an interior tower: a bisector that IS the leg must bend nothing, and that has
        // to hold on a CLIMBING line too - the tangent carries the legs' horizontal rate for this reason.
        //
        // Compared in ALL THREE COMPONENTS against the chord point, and that is the whole strength of this
        // case. It used to measure only the PERPENDICULAR distance off the line, which is blind to both of
        // the properties it is here to pin: dropping the mean-horizontal-rate scaling from Bisect puts a
        // phantom bend into a straight sloped span, but the wobble is LONGITUDINAL and slides the cabin
        // along the line rather than off it, so the perpendicular reads 0.00e+00 either way; and a vertical
        // term in BendOffset - the bend is horizontal by construction - is invisible to a horizontal
        // measurement for the same reason. Both mutations left this test green. Three asserts kill both.
        var straight = Line((0, 64, 0), (20, 70, 0), (40, 76, 0));
        var from = straight.Anchors[0];
        var to = straight.Anchors[2];
        for (var i = 0; i <= 200; i++)
        {
            var t = straight.TotalLength * i / 200.0;
            var u = t / straight.TotalLength;
            var p = straight.PositionAt(t);

            Assert.Equal(from.X + (to.X - from.X) * u, p.X, 9);
            Assert.Equal(from.Y + (to.Y - from.Y) * u, p.Y, 9);
            Assert.Equal(from.Z + (to.Z - from.Z) * u, p.Z, 9);
        }

        // 3. Doubling back: the two legs are opposite, no direction is between them, and the cusp is left as
        // a cusp. Finite everywhere is the whole assert - a normalised zero here is a cabin at NaN.
        var hairpin = Line((0, 64, 0), (40, 64, 0), (10, 64, 0));
        for (var i = 0; i <= 400; i++)
        {
            var t = hairpin.TotalLength * i / 400.0;
            var p = hairpin.PositionAt(t);
            var d = hairpin.DirectionAt(t);

            Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y) && double.IsFinite(p.Z));
            Assert.True(double.IsFinite(d.X) && double.IsFinite(d.Y) && double.IsFinite(d.Z));
            Assert.Equal(0, p.Z - hairpin.Anchors[0].Z, 9);
            Assert.Equal(1, d.Length(), 9);
        }

        Assert.Null(hairpin.Tangents[1]);
    }

    /// <summary>
    /// The two pure halves of the link-time corner warning, and the case it exists for. Psi - the angle
    /// between a corner's bisector and the tower's own crossarm - is what decides whether a cabin clears the
    /// posts, nothing in the mod ever constrains it, and before this nothing told the player it mattered.
    /// </summary>
    [Fact]
    public void ACornerTellsAPlayerWhichWayItsTowerWantsToFace()
    {
        // The fit against the three measured points: +/-1.0 degree at a 90 degree turn, +/-23.2 at 45,
        // +/-30.8 at 30. Half the shortfall from a right angle, and never negative.
        Assert.Equal(0, SpanMath.CornerTolerance(90), 9);
        Assert.Equal(22.5, SpanMath.CornerTolerance(45), 9);
        Assert.Equal(30, SpanMath.CornerTolerance(30), 9);
        Assert.Equal(45, SpanMath.CornerTolerance(0), 9);
        Assert.Equal(0, SpanMath.CornerTolerance(165), 9);

        // An AXIS, not a facing: the passage runs through the tower both ways and the cabin is symmetric
        // front to back, so a south-facing crossarm carries a due-north bisector exactly. Measuring against
        // the facing would call that tower 180 degrees wrong.
        Assert.Equal(0, SpanMath.AxisError(0, BlockFacing.NORTH), 6);
        Assert.Equal(0, SpanMath.AxisError(0, BlockFacing.SOUTH), 6);
        Assert.Equal(90, SpanMath.AxisError(0, BlockFacing.EAST), 6);
        Assert.Equal(45, SpanMath.AxisError(Math.PI / 4, BlockFacing.NORTH), 6);
        Assert.Equal(90, SpanMath.AxisError(0, null), 6);

        // THE PHOTOGRAPH, in two lines. A right angle between two CARDINAL legs has a diagonal bisector, so
        // the best of four cardinals is 45 degrees off against a tolerance of 0 - no facing carries it, and
        // the warning has to say so rather than name one.
        var cardinal = Line((0, 64, -60), (0, 64, 0), (60, 64, 0));
        var bisector = Bearing(new Vec3d(), cardinal.DirectionAt(cardinal.Cumulative[1]));
        Assert.Equal(45, SpanMath.AxisError(bisector, BlockFacing.NORTH), 6);
        Assert.True(SpanMath.AxisError(bisector, BlockFacing.EAST) > SpanMath.CornerTolerance(90));

        // ...and the same right angle between two DIAGONAL legs has a CARDINAL bisector - here due east,
        // since the two legs come in from the south-west and leave to the north-east - which a tower can face
        // exactly. That is the corner KNOWN-ISSUES had recorded as impossible under any yaw law.
        var diagonal = Line((-60, 64, -60), (0, 64, 0), (60, 64, -60));
        var clean = Bearing(new Vec3d(), diagonal.DirectionAt(diagonal.Cumulative[1]));
        Assert.Equal(0, SpanMath.AxisError(clean, BlockFacing.EAST), 6);
        Assert.True(SpanMath.AxisError(clean, BlockFacing.EAST) <= SpanMath.CornerTolerance(90));
    }

    /// <summary>
    /// A tower whose next-door chunk has not landed yet is the end of a TRUNCATED chain, so it has one leg
    /// and no bend - and when the chunk does land it grows a second leg and a bend appears under a cabin
    /// that never moved. That is only safe because the two are the same everywhere the cabin is allowed to
    /// be: <c>MarkLoadedEnds</c> fences it at the last PROVEN tower, the bend is zero at every anchor, and
    /// the stretch beyond that fence is the only ground the new curve touches. This is that, measured.
    /// </summary>
    [Fact]
    public void AChunkLandingBehindTheLastProvenTowerMovesNothingTheCabinCanReach()
    {
        var towers = new List<BlockPos>
        {
            new(0, 64, 0), new(0, 64, 20), new(0, 64, 40), new(30, 64, 40)
        };

        var whole = RopewayLine.FromTowers(towers)!;
        var partial = RopewayLine.FromTowers(towers.GetRange(0, 3))!;
        partial.MarkLoadedEnds(pos => !pos.Equals(towers[2]));

        // The unproven end really is fenced off, and the fence lands on a tower.
        Assert.True(partial.Truncated);
        Assert.Equal(partial.Cumulative[1], partial.MaxTravel, 9);

        // Everything the cabin may occupy is byte-for-byte the same line before and after the chunk lands.
        for (var i = 0; i <= 1000; i++)
        {
            var t = partial.MaxTravel * i / 1000.0;
            var a = partial.PositionAt(t);
            var b = whole.PositionAt(t);

            Assert.Equal(a.X, b.X, 12);
            Assert.Equal(a.Y, b.Y, 12);
            Assert.Equal(a.Z, b.Z, 12);
        }

        // And the corner the partial chain could not see IS genuinely bent once it can, or the agreement
        // above would only be saying that neither line bends anywhere.
        var inside = whole.Cumulative[2] - 2;
        Assert.True(partial.PositionAt(inside).DistanceTo(whole.PositionAt(inside)) > 0.1,
            "the chunk landing changed nothing at all, so this proves nothing");
    }

    private static double Bearing(Vec3d from, Vec3d to)
    {
        return Math.Atan2(to.X - from.X, to.Z - from.Z);
    }

    /// <summary>How far a point sits off the straight line through two anchors, horizontally.</summary>
    private static double Perpendicular(Vec3d from, Vec3d to, Vec3d point)
    {
        var dx = to.X - from.X;
        var dz = to.Z - from.Z;
        var length = Math.Sqrt(dx * dx + dz * dz);

        return Math.Abs((point.X - from.X) * dz - (point.Z - from.Z) * dx) / length;
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
    /// The reload teleport, as the one pure fact underneath it: <b>a truncated chain's Towers[0] is not
    /// evidence of anything.</b> WalkChain picks canonical orientation by comparing the two ends the WALK
    /// reached, so a chain that is only a PREFIX of the line - which is every chain at world load, while the
    /// tower chunks are still registering one column at a time - can sort the opposite way from the whole
    /// line and reverse. Towers[0] then stops equalling the cabin's LineKey, and ServerTick used to read
    /// that as "the chain re-canonicalised under us" and rewrite LineKey, Travelled and Pos from it. Both
    /// guards - ServerTick's re-base branch and RebaseTo - now refuse while Truncated, and this is the input
    /// they refuse on: the diagnosis's geometry, a line whose first hop goes back on itself.
    /// <para>
    /// Pure, and therefore only half the story: it pins the DATA, not the tick ordering that feeds it. That
    /// the partial chains really do arrive this way during a world load, that the cabin sits still through
    /// them, and that it is still where it was left afterwards - mid-span included - is the in-game reload
    /// check in docs/QA-SCRIPT.md.
    /// </para>
    /// </summary>
    [Fact]
    public void APartialChainCanCanonicaliseTheOppositeWayFromTheWholeLine()
    {
        var t0 = new BlockPos(0, 64, 0);
        var t1 = new BlockPos(-10, 64, 0);
        var t2 = new BlockPos(30, 64, 0);

        var spans = new Dictionary<BlockPos, List<BlockPos>>
        {
            [t0] = new() { t1 },
            [t1] = new() { t0, t2 },
            [t2] = new() { t1 }
        };

        IReadOnlyList<BlockPos> Peers(BlockPos p, HashSet<BlockPos> loaded) =>
            loaded.Contains(p) && spans.TryGetValue(p, out var v) ? v : null!;

        // Whole line: the walk's two ends are T0 and T2, T0 sorts first, so nothing reverses and Towers[0]
        // is T0 - which is the cabin's LineKey. The tick's re-base branch does not fire.
        var all = new HashSet<BlockPos> { t0, t1, t2 };
        var whole = Marked(RopewayLine.WalkChain(t0, p => Peers(p, all)), all);
        Assert.False(whole.Truncated);
        Assert.Equal(t0, whole.Towers[0]);
        Assert.Equal(50, whole.TotalLength, 6);

        // Mid-load: only T0's column is registered, so the walk reaches T1 and stops on it. NOW the two ends
        // are T0 and T1, T1 sorts first, and the prefix reverses - Towers[0] is a tower the cabin was never
        // keyed to. Nothing about the line changed; the chunks are simply not all in yet.
        var partial = new HashSet<BlockPos> { t0 };
        var prefix = Marked(RopewayLine.WalkChain(t0, p => Peers(p, partial)), partial);
        Assert.True(prefix.Truncated);
        Assert.Equal(t1, prefix.Towers[0]);
        Assert.NotEqual(whole.Towers[0], prefix.Towers[0]);

        // And that mismatch used to be acted on, which is the teleport. The cabin was parked at T2, x 30,
        // Travelled 50. The prefix is 10 long with a window of 10..10, so ParkAtNearestEnd sends it to 10 -
        // which on this chain is T0's own anchor, 30 blocks back down the line.
        Assert.Equal(10, prefix.TotalLength, 6);
        Assert.Equal(10, prefix.MinTravel, 6);
        Assert.Equal(10, prefix.MaxTravel, 6);
        Assert.Equal(30, whole.PositionAt(whole.TotalLength).X - prefix.PositionAt(prefix.MaxTravel).X, 6);
        Assert.Equal(whole.PositionAt(0).X, prefix.PositionAt(prefix.MaxTravel).X, 6);
    }

    /// <summary>
    /// The "the world is not ready" invariant, as the only part of it a pure test can hold: WHICH states
    /// count. There are three ways to be in one - no line at all, a truncated chain measuring from a
    /// different tower, and a truncated chain that no longer reaches the cabin - and they used to be three
    /// separate branches, one of which called <c>Hold</c> and so cleared the <c>departed</c> flag the other
    /// two were carefully preserving. One tick later the cabin was mid-span with <c>departed == false</c>,
    /// which is the mid-span park, which is the reload teleport wearing a different hat.
    /// <para>
    /// The recovery itself - stand still, write nothing else - is one block at one call site and is checked
    /// in game by QA 18/18b. What this pins is that the question is asked in one place and in this order.
    /// </para>
    /// </summary>
    [Fact]
    public void TheWorldIsNotReadyUntilTheChainCanVouchForWhereTheCabinIs()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(10, 64, 0);
        var c = new BlockPos(20, 64, 0);
        var d = new BlockPos(30, 64, 0);
        var chain = new[] { a, b, c, d };

        // No line resolved at all: the tower's own chunk is not in, or the line was cut.
        Assert.True(EntityRopewayCabin.NotReady(null, a, 0));

        // A whole chain is proof of everything, including for a Travelled that is off the end of it - that
        // is stale state for the clamp to deal with, not a chunk anyone is waiting for.
        var whole = Marked(chain, new HashSet<BlockPos> { a, b, c, d });
        Assert.False(EntityRopewayCabin.NotReady(whole, a, 0));
        Assert.False(EntityRopewayCabin.NotReady(whole, a, 15));
        Assert.False(EntityRopewayCabin.NotReady(whole, a, 99));

        // Truncated, and measuring from a tower that is not the cabin's key. THE reload teleport.
        var farEnd = Marked(chain, new HashSet<BlockPos> { a, b, c });
        Assert.True(farEnd.Truncated);
        Assert.True(EntityRopewayCabin.NotReady(farEnd, b, 0));
        Assert.True(EntityRopewayCabin.NotReady(farEnd, null, 0));

        // Same base, inside the window the loaded towers can vouch for: ready, and this is the mid-span
        // resume working - the cabin carries on from exactly where the save left it.
        Assert.False(EntityRopewayCabin.NotReady(farEnd, a, 0));
        Assert.False(EntityRopewayCabin.NotReady(farEnd, a, 15));
        Assert.False(EntityRopewayCabin.NotReady(farEnd, a, farEnd.MaxTravel));

        // Same base, PAST that window - the third branch, the one that used to Hold. A cabin saved between
        // the last two towers of a long line is here on every load, because MaxTravel collapses to the
        // second-to-last tower the moment the far end is out.
        Assert.True(EntityRopewayCabin.NotReady(farEnd, a, farEnd.MaxTravel + 0.001));
        Assert.True(EntityRopewayCabin.NotReady(farEnd, a, 25));

        // And the same at the near end, where the window starts short of zero rather than ending early.
        var nearEnd = Marked(chain, new HashSet<BlockPos> { b, c, d });
        Assert.Equal(10, nearEnd.MinTravel);
        Assert.True(EntityRopewayCabin.NotReady(nearEnd, a, 0));
        Assert.False(EntityRopewayCabin.NotReady(nearEnd, a, 10));
    }

    /// <summary>
    /// The other half of "a truncated chain's Towers[0] is not evidence of anything", and the case that made
    /// the guard worse than the bug. Refusing to re-key on ANY truncated line strands a cabin forever
    /// whenever the tower it is keyed to is the one that was just broken - which is <c>UnlinkAll</c>'s
    /// ordinary case, not an exotic one, because <c>LineKey</c> is always an end tower and
    /// <c>PickSurvivor</c> falls back to the first survivor. The tower leaves <c>LoadedTowers</c> a moment
    /// later, <c>ResolveLine</c> returns null for good, and the <c>DropAndDie</c> backstop cannot fire
    /// because it needs <c>LoadedTowers</c> to contain <c>LineKey</c>: an uncollectable cabin in mid-air
    /// with its item destroyed, and nothing in the mod ever repairs it.
    /// </summary>
    [Fact]
    public void ARebaseWaitsForChunksOnlyWhileTheOldKeyIsStillOnTheChain()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(10, 64, 0);
        var c = new BlockPos(20, 64, 0);
        var d = new BlockPos(30, 64, 0);

        // A whole chain never waits, whatever the key is - re-keying is exactly what it is for.
        var whole = Marked(new[] { a, b, c, d }, new HashSet<BlockPos> { a, b, c, d });
        Assert.False(EntityRopewayCabin.RebaseMustWait(whole, a));
        Assert.False(EntityRopewayCabin.RebaseMustWait(whole, new BlockPos(99, 64, 0)));

        // Truncated, and the cabin's key is still on it: hold. Towers[0] here is whichever end the walk
        // reached, so re-keying onto it would put Travelled on a scale that means somewhere else, and the
        // tick re-bases for real once the chunk lands. Nothing is lost by waiting.
        var truncated = Marked(new[] { a, b, c, d }, new HashSet<BlockPos> { a, b, c });
        Assert.True(truncated.Truncated);
        Assert.True(EntityRopewayCabin.RebaseMustWait(truncated, a));
        Assert.True(EntityRopewayCabin.RebaseMustWait(truncated, c));

        // The key has LEFT the world: break tower A of A-B-C-D and the survivor is B-C-D, still truncated
        // because D's chunk is out. Waiting for a tower that no longer exists is waiting forever, so this
        // one must re-key onto the survivor - inside its loaded window, which ParkAtNearestEnd guarantees.
        var survivor = Marked(new[] { b, c, d }, new HashSet<BlockPos> { b, c });
        Assert.True(survivor.Truncated);
        Assert.False(EntityRopewayCabin.RebaseMustWait(survivor, a));

        // A key that was never set at all is in the same position, and re-keying is its repair.
        Assert.False(EntityRopewayCabin.RebaseMustWait(survivor, null));
        Assert.False(EntityRopewayCabin.RebaseMustWait(null, a));
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

    /// <summary>
    /// Calling the cabin to a tower is that tower's own cumulative distance, whichever tower it is - the
    /// end-only version could only ever pick 0 or TotalLength, which made every middle tower on a line
    /// scenery. Direction is that target against where the cabin stands, which is what CallTo writes to
    /// Outbound.
    /// </summary>
    [Fact]
    public void ACallTargetsTheClickedTowerAndNotTheNearestEnd()
    {
        var line = FourTowerLine(out var a, out var b, out var c, out var d);

        // Forward past one interior tower, from the near end.
        Assert.Equal(CabinCall.Called, EntityRopewayCabin.PlanCall(line, c, 0, out var target));
        Assert.Equal(20, target);
        Assert.True(target > 0, "a call to a tower ahead of the cabin must run outbound");

        // Backward to the other interior tower, from the far end.
        Assert.Equal(CabinCall.Called, EntityRopewayCabin.PlanCall(line, b, 30, out target));
        Assert.Equal(10, target);
        Assert.True(target < 30, "a call to a tower behind the cabin must run inbound");

        // The two ends still work, and still mean the ends.
        Assert.Equal(CabinCall.Called, EntityRopewayCabin.PlanCall(line, a, 10, out target));
        Assert.Equal(0, target);
        Assert.Equal(CabinCall.Called, EntityRopewayCabin.PlanCall(line, d, 10, out target));
        Assert.Equal(line.TotalLength, target);

        // A tower that is not on this line at all is not a destination.
        Assert.Equal(CabinCall.Unreachable, EntityRopewayCabin.PlanCall(line, new BlockPos(99, 64, 0), 0, out _));
        Assert.Equal(CabinCall.Unreachable, EntityRopewayCabin.PlanCall(null, a, 0, out _));
    }

    /// <summary>
    /// "It is already here" is not "it cannot come": reporting the first as failure is what made a click on
    /// the tower the cabin is parked at look like a broken call.
    /// </summary>
    [Fact]
    public void CallingTheCabinToWhereItAlreadyStandsIsNotATrip()
    {
        var line = FourTowerLine(out _, out _, out var c, out _);

        Assert.Equal(CabinCall.AlreadyHere, EntityRopewayCabin.PlanCall(line, c, 20, out _));
        Assert.Equal(CabinCall.AlreadyHere, EntityRopewayCabin.PlanCall(line, c, 20.4, out _));

        // Half a metre out is a real trip - it is the resolution the arrival test itself works at.
        Assert.Equal(CabinCall.Called, EntityRopewayCabin.PlanCall(line, c, 20.5, out _));
    }

    /// <summary>
    /// Neither end of the trip may be outside the loaded window. Past it the cabin cannot be proven to be
    /// where its Travelled says, and the target cannot be proven to still be on this line - both of which
    /// end in the false-endpoint teleport the window exists to prevent.
    /// </summary>
    [Fact]
    public void ACallIsRefusedWhenEitherEndIsOutsideTheLoadedWindow()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(10, 64, 0);
        var c = new BlockPos(20, 64, 0);
        var d = new BlockPos(30, 64, 0);

        // D unloaded: the window is 0..20, so C is still a station and D is not.
        var line = Marked(new[] { a, b, c, d }, new HashSet<BlockPos> { a, b, c });

        Assert.Equal(CabinCall.Called, EntityRopewayCabin.PlanCall(line, c, 0, out var target));
        Assert.Equal(20, target);

        Assert.Equal(CabinCall.Unreachable, EntityRopewayCabin.PlanCall(line, d, 0, out _));

        // And a cabin sitting in the unloaded stretch cannot be summoned out of it either.
        Assert.Equal(CabinCall.Unreachable, EntityRopewayCabin.PlanCall(line, a, 30, out _));
    }

    /// <summary>
    /// The halt. A called cabin stops at its destination rather than running on to the end, and an inverted
    /// comparison here is a cabin that sails straight through the station it was called to.
    /// </summary>
    [Fact]
    public void ACalledCabinHaltsAtItsDestinationAndNotBefore()
    {
        // Outbound: not yet at 20, then on it, then past it.
        Assert.False(EntityRopewayCabin.Reached(19.9, 20, outbound: true));
        Assert.True(EntityRopewayCabin.Reached(20, 20, outbound: true));
        Assert.True(EntityRopewayCabin.Reached(20.1, 20, outbound: true));

        // Inbound the comparison flips, or the cabin halts the instant it is called.
        Assert.False(EntityRopewayCabin.Reached(10.1, 10, outbound: false));
        Assert.True(EntityRopewayCabin.Reached(10, 10, outbound: false));
        Assert.True(EntityRopewayCabin.Reached(9.9, 10, outbound: false));

        // Walk a call from tower A to tower C at one tick of travel, and check it stops on C rather than
        // carrying on to D. Same arithmetic the tick runs, without the tick.
        var line = FourTowerLine(out _, out _, out var c, out _);
        Assert.Equal(CabinCall.Called, EntityRopewayCabin.PlanCall(line, c, 0, out var destination));

        var travelled = 0.0;
        var steps = 0;
        while (!EntityRopewayCabin.Reached(travelled, destination, outbound: true) && ++steps < 1000)
        {
            travelled += RopewayPower.CabinSpeed(0.36) * 0.1;
        }

        Assert.True(steps < 1000, "the cabin never reached the tower it was called to");
        Assert.Equal(destination, Math.Min(travelled, destination), 6);
        Assert.True(travelled < line.TotalLength, "the cabin ran past its destination toward the end of the line");
    }

    /// <summary>
    /// The other half of the halt: a cabin standing at an interior tower is parked at a station, not stopped
    /// mid-span. The tick's "never resume from mid-span" recovery reads this, and reading it as "is it at an
    /// end" instead teleports a just-called cabin off its destination on the very next tick.
    /// </summary>
    [Fact]
    public void EveryTowerCountsAsParkedAndTheSpansBetweenThemDoNot()
    {
        var line = FourTowerLine(out _, out _, out _, out _);

        foreach (var cumulative in line.Cumulative)
        {
            Assert.True(line.IsAtTower(cumulative, EntityRopewayCabin.ArrivalTolerance), $"tower at {cumulative} is not a park");
        }

        Assert.False(line.IsAtTower(15, EntityRopewayCabin.ArrivalTolerance));
        Assert.False(line.IsAtTower(20 - 2 * EntityRopewayCabin.ArrivalTolerance, EntityRopewayCabin.ArrivalTolerance));
    }

    /// <summary>
    /// The clearance gate asks which span the cabin is about to travel through, and the answer depends on
    /// which way it is running. Standing on tower 2 of [0,10,20,30] an outbound cabin enters 20-&gt;30 and an
    /// inbound one enters 10-&gt;20. Answering 20-&gt;30 for both is what certified the wrong span - harmless
    /// while the cabin could only ever stand and depart at an endpoint, a rider driven through stone the
    /// moment calling to an interior tower made "parked inside the line, running inbound" ordinary.
    /// </summary>
    [Fact]
    public void TheSpanAheadIsTheOneTheCabinEntersAndNotTheOneItStandsOn()
    {
        var line = FourTowerLine(out _, out _, out _, out _);

        // At an interior tower the two directions are different spans. This is the whole defect.
        Assert.Equal(2, line.SpanAheadOf(20, outbound: true));
        Assert.Equal(1, line.SpanAheadOf(20, outbound: false));
        Assert.Equal(1, line.SpanAheadOf(10, outbound: true));
        Assert.Equal(0, line.SpanAheadOf(10, outbound: false));

        // Mid-span there is one span and it is travelled both ways, so direction cannot change the answer.
        Assert.Equal(1, line.SpanAheadOf(15, outbound: true));
        Assert.Equal(1, line.SpanAheadOf(15, outbound: false));
        Assert.Equal(0, line.SpanAheadOf(0.1, outbound: false));
        Assert.Equal(2, line.SpanAheadOf(29.9, outbound: true));

        // An end tower touches exactly one span whichever flag it is asked with, so neither direction can
        // index off the line.
        Assert.Equal(0, line.SpanAheadOf(0, outbound: true));
        Assert.Equal(0, line.SpanAheadOf(0, outbound: false));
        Assert.Equal(2, line.SpanAheadOf(30, outbound: false));
        Assert.Equal(2, line.SpanAheadOf(30, outbound: true));

        // Outside the line, and a line too short to have a span.
        Assert.Equal(0, line.SpanAheadOf(-5, outbound: false));
        Assert.Equal(2, line.SpanAheadOf(99, outbound: true));
        Assert.Equal(0, RopewayLine.FromTowers(new List<BlockPos> { new(0, 64, 0), new(10, 64, 0) })
            .SpanAheadOf(10, outbound: false));
    }

    /// <summary>
    /// A blocked span holds the cabin exactly where it stands and writes nothing to Travelled, which is only
    /// safe because where it stands is a boundary of the span being refused rather than a point inside it.
    /// The previous recovery wrote <c>Cumulative[segment + 1]</c> for an inbound cabin, which - with the
    /// direction-blind index feeding it - was a full span FORWARD across the span just proven blocked.
    /// </summary>
    [Fact]
    public void HoldingWhereItStandsNeverPutsTheCabinThroughTheRefusedSpan()
    {
        var line = FourTowerLine(out _, out _, out _, out _);

        foreach (var travelled in line.Cumulative)
        {
            foreach (var outbound in new[] { true, false })
            {
                var span = line.SpanAheadOf(travelled, outbound);
                var start = line.Cumulative[span];
                var end = line.Cumulative[span + 1];

                Assert.True(
                    travelled == start || travelled == end,
                    $"at {travelled} running {(outbound ? "outbound" : "inbound")}: span {start}..{end} does not touch the cabin");
            }
        }

        // And at an interior tower it is the span the cabin moves INTO: it begins where the cabin stands
        // when outbound and ends where the cabin stands when inbound.
        for (var i = 1; i < line.Cumulative.Length - 1; i++)
        {
            Assert.Equal(line.Cumulative[i], line.Cumulative[line.SpanAheadOf(line.Cumulative[i], outbound: true)]);
            Assert.Equal(line.Cumulative[i], line.Cumulative[line.SpanAheadOf(line.Cumulative[i], outbound: false) + 1]);
        }
    }

    /// <summary>
    /// Same rule on geometry that is not round numbers - the boundary case is an exact comparison against a
    /// cumulative sum of distances, not against tidy multiples of ten - and a hair off a tower is mid-span,
    /// not a boundary, in both directions.
    /// </summary>
    [Fact]
    public void TheSpanAheadIsBoundaryExactOnUnevenGeometry()
    {
        // Cumulative 0, 7, 26, 49.
        var line = Line((0, 64, 0), (7, 64, 0), (7, 64, 19), (30, 64, 19));

        Assert.Equal(1, line.SpanAheadOf(line.Cumulative[1], outbound: true));
        Assert.Equal(0, line.SpanAheadOf(line.Cumulative[1], outbound: false));
        Assert.Equal(2, line.SpanAheadOf(line.Cumulative[2], outbound: true));
        Assert.Equal(1, line.SpanAheadOf(line.Cumulative[2], outbound: false));

        // Just past tower 1 inbound is still inside span 1, running back down toward tower 1.
        Assert.Equal(1, line.SpanAheadOf(line.Cumulative[1] + 0.001, outbound: false));
        Assert.Equal(0, line.SpanAheadOf(line.Cumulative[1] - 0.001, outbound: false));
    }

    /// <summary>
    /// The rider's whole control surface. One key, stepping the requested stop along the chain in the
    /// direction of travel and wrapping at the ends - and the wrap is load bearing, because it is the only
    /// way a rider who boarded at an interior station on a cabin pointing the wrong way can ask to go the
    /// other way. Every candidate goes through PlanCall, so the tower the cabin is standing on and anything
    /// outside the loaded window are skipped rather than offered and then refused.
    /// </summary>
    [Fact]
    public void TheStopKeyStepsAlongTheLineAndWrapsBackTheOtherWay()
    {
        var line = FourTowerLine(out _, out _, out _, out _);

        int Press(double travelled, int requested, bool outbound) =>
            EntityRopewayCabin.NextStop(line, travelled, requested, outbound,
                i => EntityRopewayCabin.PlanCall(line, line.Towers[i], travelled, out _) == CabinCall.Called);

        // Parked at tower 0 and pointing outward: one press per tower, out to the far end.
        Assert.Equal(1, Press(0, -1, outbound: true));
        Assert.Equal(2, Press(0, 1, outbound: true));
        Assert.Equal(3, Press(0, 2, outbound: true));

        // Past the far end it wraps - skipping tower 0, which is where the cabin is standing.
        Assert.Equal(1, Press(0, 3, outbound: true));

        // Mid-span and running inbound, the first press is the tower behind, not the one it just left.
        Assert.Equal(2, Press(25, -1, outbound: false));

        // The direction escape: parked at interior tower 2 with the cabin pointing back down the line, a
        // rider who wants tower 3 keeps pressing and the selection comes round the other way.
        Assert.Equal(1, Press(20, -1, outbound: false));
        Assert.Equal(0, Press(20, 1, outbound: false));
        Assert.Equal(3, Press(20, 0, outbound: false));

        Assert.Equal(-1, EntityRopewayCabin.NextStop(null, 0, -1, true, _ => true));
    }

    /// <summary>A tower past the loaded end is never offered, the same rule a call from the ground obeys.</summary>
    [Fact]
    public void TheStopKeyNeverOffersATowerOutsideTheLoadedWindow()
    {
        var a = new BlockPos(0, 64, 0);
        var b = new BlockPos(10, 64, 0);
        var c = new BlockPos(20, 64, 0);
        var d = new BlockPos(30, 64, 0);
        var line = Marked(new[] { a, b, c, d }, new HashSet<BlockPos> { a, b, c });

        int Press(int requested) =>
            EntityRopewayCabin.NextStop(line, 0, requested, outbound: true,
                i => EntityRopewayCabin.PlanCall(line, line.Towers[i], 0, out _) == CabinCall.Called);

        Assert.Equal(1, Press(-1));
        Assert.Equal(2, Press(1));

        // Tower 3 is unproven and tower 0 is where the cabin stands, so the cycle is just 1 and 2.
        Assert.Equal(1, Press(2));
    }

    /// <summary>
    /// The striping BASIC saw. ScaleCubeMesh multiplies the cube's UVs by the axis scale
    /// (CubeMeshUtil.cs:230-251), so a long cable leaves them running well past 1 - and MeshData.SetTexPos
    /// maps u through <c>x1 + u * (x2 - x1)</c>, which lands everything past 1 OUTSIDE this texture's
    /// sub-region of the block atlas, sampling whatever sprites happen to sit next to it.
    /// </summary>
    [Fact]
    public void TheCableSamplesOnlyItsOwnCornerOfTheAtlas()
    {
        // A deliberately small sub-region in the middle of the atlas, so an out-of-range UV cannot land
        // inside it by luck. Half of a 48-block span, which is the longest the mod allows.
        var texPos = new TextureAtlasPosition { x1 = 0.25f, y1 = 0.5f, x2 = 0.3f, y2 = 0.55f };
        var mesh = BEPylonBase.BuildHalfCable(24, 0, 0, texPos);

        Assert.NotNull(mesh);
        for (var i = 0; i < mesh.Uv.Length; i += 2)
        {
            Assert.InRange(mesh.Uv[i], texPos.x1, texPos.x2);
            Assert.InRange(mesh.Uv[i + 1], texPos.y1, texPos.y2);
        }
    }

    private static RopewayLine FourTowerLine(out BlockPos a, out BlockPos b, out BlockPos c, out BlockPos d)
    {
        a = new BlockPos(0, 64, 0);
        b = new BlockPos(10, 64, 0);
        c = new BlockPos(20, 64, 0);
        d = new BlockPos(30, 64, 0);
        return RopewayLine.FromTowers(new List<BlockPos> { a, b, c, d });
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

    /// <summary>
    /// The cable's failure mode is silence: <c>CubeMeshUtil.GetCube</c> hands back a mesh whose XyzFaces is
    /// <c>Array.Empty</c>, and the chunk tesselator emits geometry only inside
    /// <c>for (l = 0; l &lt; sourceMesh.XyzFacesCount; l++)</c> (JsonTesselator.cs:709), so
    /// <c>mesher.AddMeshData</c> copies zero vertices with no exception and no log line. Nothing but a test
    /// or standing in the world tells you the cable is gone.
    /// </summary>
    [Fact]
    public void TheCableMeshIsCentredAndCarriesTheFaceCountTheTesselatorLoopsOver()
    {
        // Half of an 8-block span due east.
        var mesh = BEPylonBase.BuildHalfCable(4, 0, 0, new TextureAtlasPosition { x1 = 0, y1 = 0, x2 = 1, y2 = 1 });

        Assert.NotNull(mesh);
        Assert.Equal(6, mesh.XyzFacesCount);

        // The tesselator indexes both colour maps once per face (JsonTesselator.cs:834); zero-length arrays
        // trade an invisible cable for an IndexOutOfRangeException.
        Assert.True(mesh.SeasonColorMapIds.Length >= 6);
        Assert.True(mesh.ClimateColorMapIds.Length >= 6);

        // ScaleCubeMesh puts the box corner-at-origin, so without the centring translate the rotate-about-
        // origin below swings the cable out by half its own box on every axis.
        var min = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
        var max = new[] { double.MinValue, double.MinValue, double.MinValue };
        for (var i = 0; i < mesh.VerticesCount; i++)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                min[axis] = Math.Min(min[axis], mesh.xyz[3 * i + axis]);
                max[axis] = Math.Max(max[axis], mesh.xyz[3 * i + axis]);
            }
        }

        // Local to the FOOTING that draws it: it leaves the sheave, SheaveHeight above the footing's centre,
        // and runs to the midpoint of the span, 4 blocks east. That vertical offset is what makes the drawn
        // cable meet SpanMath.AnchorOf - without it the cable hangs at the footing and the cabin at the rope.
        Assert.Equal(0.5, min[0], 4);
        Assert.Equal(4.5, max[0], 4);
        Assert.Equal(0.5 + SpanMath.SheaveHeight - 0.06, min[1], 4);
        Assert.Equal(0.5 + SpanMath.SheaveHeight + 0.06, max[1], 4);
        Assert.Equal(0.5 - 0.06, min[2], 4);
        Assert.Equal(0.5 + 0.06, max[2], 4);
    }

    /// <summary>
    /// The one conversion from a tower's canonical position - its ground-placed footing - to the height its
    /// rope actually runs at. Every spatial consumer goes through it, so an offset applied anywhere else as
    /// well is a cable and a cabin at different heights.
    /// </summary>
    [Fact]
    public void TheAnchorIsTheSheaveAndNotTheFootingItIsKeyedBy()
    {
        var footing = new BlockPos(10, 64, -3);

        var centre = SpanMath.CentreOf(footing);
        var anchor = SpanMath.AnchorOf(footing);

        Assert.Equal(10.5, centre.X, 6);
        Assert.Equal(64.5, centre.Y, 6);
        Assert.Equal(-2.5, centre.Z, 6);

        // Purely vertical, so it survives the tower's rotation and needs no facing term at any caller.
        Assert.Equal(centre.X, anchor.X, 6);
        Assert.Equal(centre.Z, anchor.Z, 6);
        Assert.Equal(centre.Y + SpanMath.SheaveHeight, anchor.Y, 6);

        // A span is therefore the same length measured footing to footing or sheave to sheave, which is why
        // the cable mesh can take raw footing deltas and only shift its own origin.
        var peer = new BlockPos(40, 70, -3);
        Assert.Equal(
            SpanMath.CentreOf(footing).DistanceTo(SpanMath.CentreOf(peer)),
            SpanMath.AnchorOf(footing).DistanceTo(SpanMath.AnchorOf(peer)),
            6);

        Assert.Null(SpanMath.AnchorOf(null));
        Assert.Null(SpanMath.CentreOf(null));
    }

    [Fact]
    public void ACableToNowhereIsNotDrawn()
    {
        Assert.Null(BEPylonBase.BuildHalfCable(0, 0, 0, new TextureAtlasPosition()));
    }

    /// <summary>
    /// Tower names arrive from a client packet, so this is a trust boundary: control characters corrupt the
    /// chat and GUI text renderers, and an unbounded string is persisted on the tower and re-sent to every
    /// client in range.
    /// </summary>
    [Fact]
    public void TowerNamesAreSanitised()
    {
        Assert.Equal("Summit Station", BEPylonBase.SanitiseName("  Summit Station  "));

        // Control characters out, every flavour of whitespace collapsed to one plain space.
        Assert.Equal("Summit Station", BEPylonBase.SanitiseName("Summit\tStation"));
        Assert.Equal("Summit Station", BEPylonBase.SanitiseName("Summit\r\n   Station"));
        Assert.DoesNotContain("\n", BEPylonBase.SanitiseName("a\nb"));

        // Nothing readable left is null, not an empty label the GUI then has to special-case.
        Assert.Null(BEPylonBase.SanitiseName(null));
        Assert.Null(BEPylonBase.SanitiseName(""));
        Assert.Null(BEPylonBase.SanitiseName("   \t\r\n "));
        Assert.Null(BEPylonBase.SanitiseName(" "));
    }

    /// <summary>
    /// A tower name reaches two VTML-rendered surfaces - GetBlockInfo's rich-text block-info panel and the
    /// span-linked / span-cut chat lines, which HudDialogChat composes with AddRichtext - and it is visible
    /// to everyone who looks at the tower, not only whoever set it. 24 characters is enough for
    /// &lt;font color="red"&gt; or a short &lt;a href&gt;, so no tag may survive the sanitiser.
    /// </summary>
    [Theory]
    [InlineData("<font color=\"red\">Hot")]
    [InlineData("<a href=\"x\">click</a>")]
    [InlineData("<strong>Summit")]
    [InlineData("Summit</br>")]
    public void TowerNamesCannotCarryVtml(string payload)
    {
        var name = BEPylonBase.SanitiseName(payload);

        Assert.NotNull(name);
        Assert.DoesNotContain("<", name);
        Assert.DoesNotContain(">", name);
    }

    [Fact]
    public void ANameThatIsNothingButMarkupIsNoName()
    {
        Assert.Null(BEPylonBase.SanitiseName("<>"));
        Assert.Null(BEPylonBase.SanitiseName("< >"));
    }

    [Fact]
    public void TowerNamesAreCappedWithoutSplittingACharacter()
    {
        var long_ = BEPylonBase.SanitiseName(new string('x', 200));
        Assert.Equal(BEPylonBase.MaxNameLength, long_!.Length);

        // A cut that lands mid-word must not leave trailing padding either.
        Assert.Equal(BEPylonBase.MaxNameLength - 1, BEPylonBase.SanitiseName(new string('x', 23) + "   yyy")!.Length);

        // Cutting a fixed number of chars can land between the halves of a surrogate pair, which renders as
        // a replacement glyph rather than the character the player typed.
        var emoji = BEPylonBase.SanitiseName(new string('x', BEPylonBase.MaxNameLength - 1) + "\U0001F6A1");
        Assert.Equal(BEPylonBase.MaxNameLength - 1, emoji!.Length);
        Assert.DoesNotContain(emoji, c => char.IsHighSurrogate(c));
    }

    /// <summary>
    /// An unnamed tower is called by its bearing, so a wrong octant sends a player walking the wrong way.
    /// North is -Z and east is +X, and the boundaries are half-octants either side of each direction.
    /// </summary>
    [Theory]
    [InlineData(0, -10, "ropeway:dir-n")]
    [InlineData(10, -10, "ropeway:dir-ne")]
    [InlineData(10, 0, "ropeway:dir-e")]
    [InlineData(10, 10, "ropeway:dir-se")]
    [InlineData(0, 10, "ropeway:dir-s")]
    [InlineData(-10, 10, "ropeway:dir-sw")]
    [InlineData(-10, 0, "ropeway:dir-w")]
    [InlineData(-10, -10, "ropeway:dir-nw")]
    // Just off due north on either side is still north; the whole point is that it never reads "unnamed".
    [InlineData(1, -10, "ropeway:dir-n")]
    [InlineData(-1, -10, "ropeway:dir-n")]
    [InlineData(0, 0, "ropeway:dir-n")]
    public void CompassKeyNamesTheBearing(double dx, double dz, string expected)
    {
        Assert.Equal(expected, SpanMath.CompassKey(dx, dz));
    }

    /// <summary>
    /// A cabin standing at a tower sits square to that tower's passage, and turns the SHORT way to get there.
    /// The passage axis has two yaws because the cabin is symmetric front-to-back, so picking the wrong one is
    /// not wrong to look at - it is a cabin that spins the long way round on arrival, past the axis and back.
    /// </summary>
    [Theory]
    // Facing north, arriving from the south: already nose-on, nothing to do.
    [InlineData("north", Math.PI, Math.PI)]
    // Facing north, arriving from the north: the OTHER yaw of the same axis, not a half turn back to it.
    [InlineData("north", 0, 0)]
    [InlineData("east", Math.PI / 2, Math.PI / 2)]
    [InlineData("east", -Math.PI / 2, -Math.PI / 2)]
    // A leg off the axis snaps onto the nearer of the two, and only ever onto the axis.
    [InlineData("north", Math.PI * 0.75, Math.PI)]
    [InlineData("north", Math.PI * 0.25, 0)]
    public void ACabinStoppedAtATowerSquaresUpTheShortWay(string facing, double leg, double expected)
    {
        var yaw = EntityRopewayCabin.SquareTo(BlockFacing.FromCode(facing), (float)leg);

        Assert.Equal(0, GameMath.AngleRadDistance((float)expected, yaw), 4);

        // And whatever the leg was, the turn is never more than a quarter. Anything larger means the two
        // yaws of the axis were mixed up, which is the long way round.
        Assert.True(Math.Abs(GameMath.AngleRadDistance((float)leg, yaw)) <= Math.PI / 2 + 1e-4);
    }

    /// <summary>A tower that cannot be asked leaves the cabin on its plain leg bearing rather than due north.</summary>
    [Fact]
    public void WithNoPassageToSquareToTheCabinKeepsTheLegBearing()
    {
        Assert.Equal(1.234f, EntityRopewayCabin.SquareTo(null, 1.234f));
    }

    /// <summary>
    /// The bail-out hold has to be UNBROKEN and it only ever runs while there is something to bail out of.
    /// A hold that survived letting go is a rider ejected by two unrelated taps of sneak; one that survived
    /// the cabin stopping is a rider who steps out at a tower and finds themselves armed on the next ride.
    /// </summary>
    [Fact]
    public void TheBailOutHoldOnlyCountsWhileSneakIsHeldAndTheCabinMoves()
    {
        var held = 0.0;

        // The rider let go once while the cabin moved, so the press that follows is a real one.
        var released = true;

        // Two ticks of holding while moving accumulate.
        held = EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: true, 0.5f, ref released);
        held = EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: true, 0.5f, ref released);
        Assert.Equal(1.0, held, 6);
        Assert.True(held < EntityRopewayCabin.BailHoldSeconds, "one second must not be enough to jump");

        // Letting go throws the lot away - the next press starts from zero.
        Assert.Equal(0, EntityRopewayCabin.HoldSneak(held, sneaking: false, moving: true, 0.5f, ref released));

        // So does the cabin stopping, even with the key still down.
        Assert.Equal(0, EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: false, 0.5f, ref released));

        // And an unbroken hold does reach the threshold, at the moment it says it does.
        held = 0;
        released = true;
        for (var i = 0; i < (int)(EntityRopewayCabin.BailHoldSeconds / 0.5); i++)
        {
            held = EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: true, 0.5f, ref released);
        }

        Assert.True(held >= EntityRopewayCabin.BailHoldSeconds);
    }

    /// <summary>
    /// THE ACCIDENT CASE, and it is the one that has to stay shut. Boarding copies the player's live control
    /// flags into the seat, so a rider who crouch-walked aboard is "sneaking" from tick one without ever
    /// pressing anything after they sat down - and no false-&gt;true edge means the refusal that advertises
    /// the bail-out never fired either. A level-triggered hold ejects them from a moving cabin having pressed
    /// nothing and read nothing. Nothing may accumulate until they have been seen to let go while moving.
    /// </summary>
    [Fact]
    public void SneakInheritedFromBoardingNeverArmsTheBailOut()
    {
        var held = 0.0;
        var released = false;

        // The cabin sits at the tower through the boarding grace, key already down.
        for (var i = 0; i < 10; i++) held = EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: false, 0.5f, ref released);

        // It departs and they keep holding, well past the threshold. Still nothing.
        for (var i = 0; i < 20; i++) held = EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: true, 0.5f, ref released);
        Assert.Equal(0, held);

        // One tick off the key while moving is the edge the arm actually wants.
        held = EntityRopewayCabin.HoldSneak(held, sneaking: false, moving: true, 0.5f, ref released);
        Assert.True(released);
        for (var i = 0; i < (int)(EntityRopewayCabin.BailHoldSeconds / 0.5); i++)
        {
            held = EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: true, 0.5f, ref released);
        }

        Assert.True(held >= EntityRopewayCabin.BailHoldSeconds);

        // And the cabin stopping revokes it again: a rider who rides on through a stop still holding the key
        // has to press it afresh, exactly as if they had just boarded.
        held = EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: false, 0.5f, ref released);
        Assert.False(released);
        Assert.Equal(0, EntityRopewayCabin.HoldSneak(held, sneaking: true, moving: true, 0.5f, ref released));
    }

    /// <summary>
    /// The one new decision in the cabin's state machine, and the only control the mod can take away from a
    /// rider. Every row, because each of the three terms is there for a different failure and dropping any of
    /// them looks harmless from the code.
    /// </summary>
    [Theory]
    // Nothing turning, whole line loaded, never left a station: the one refusal there is.
    [InlineData(false, 0.0, false, false)]
    // A drive is turning, so of course it may go.
    [InlineData(false, 1.2, false, true)]
    // Already left and stalled mid-span. Refusing here takes the stop key off a rider at the exact moment
    // the wind drops, and the load it would save is pinned by the departure already.
    [InlineData(true, 0.0, false, true)]
    [InlineData(true, 1.2, false, true)]
    // Truncated: the zero is a chunk that has not landed rather than a drive nobody built. Refusing here
    // told a player whose only housing stands beside the far end of a 320-block line to go and build the
    // drive they were standing next to.
    [InlineData(false, 0.0, true, true)]
    [InlineData(false, 1.2, true, true)]
    [InlineData(true, 0.0, true, true)]
    [InlineData(true, 1.2, true, true)]
    public void ACabinOnlyRefusesToStartWhenTheWholeLineIsLoadedAndNothingIsTurning(
        bool departed, double lineSpeed, bool truncated, bool expected)
    {
        Assert.Equal(expected, EntityRopewayCabin.MayStart(departed, lineSpeed, truncated));
    }

    /// <summary>
    /// The rim has to TURN rather than orbit, and the guard for that cannot be the constant the chain is
    /// built from: asserting <c>RimPivotY</c> against the shape pins two INPUTS and leaves the matrix free,
    /// so putting the return translate back to -0.5 on its own restores the orbit with every other test
    /// still green. The axle is the chain's fixed point or the wheel is swinging round something else, and
    /// that has to hold at every angle on every facing.
    /// <para>
    /// The axle probe ALONE is very nearly worthless, which is what an earlier version of this test was: the
    /// pivot is the fixed point of <c>T(c) * A * B * T(-c)</c> for any pair of rotations about <c>c</c>, in
    /// any order and about any axes, so both ways <c>BullwheelRenderer.RimMatrix</c> can be wrong - the two
    /// rotations swapped, or RotateZ where RotateX belongs - leave all three of its numbers untouched. Both
    /// edits were applied and the whole suite stayed green. The felloe probe below is what sees them.
    /// </para>
    /// </summary>
    [Fact]
    public void TheRimTurnsOnItsOwnAxleAtEveryAngleAndEveryFacing()
    {
        // The four poses a wheel is ever in: over the tower at a station the line runs through, and carried
        // out along each of the two horizontal axes at a terminal. The offset rows are what stop it going in
        // the wrong translate - put it in BOTH and the pair no longer cancels, so the wheel lands right and
        // spins about the cell it left; put it in the SECOND only and it orbits; put it inside the yaw and
        // the horizontal half swings round with the facing, which is the one a north-facing test never sees.
        var poses = new[]
        {
            new Vec3f(),
            new Vec3f(0, -BullwheelRenderer.WrapDrop, BullwheelRenderer.WrapOut),
            new Vec3f(-BullwheelRenderer.WrapOut, -BullwheelRenderer.WrapDrop, 0)
        };

        foreach (var side in new[] { "north", "east", "south", "west", null })
        foreach (var turns in new[] { 0.0, 0.25, 0.5, 0.75 })
        foreach (var offset in poses)
        {
            var yaw = BullwheelRenderer.YawFor(side);
            var theta = (float)(turns * GameMath.TWOPI);
            var matrix = BullwheelRenderer.RimMatrix(new Matrixf().Identity(), yaw, theta, offset).Values;

            var axle = Mat4f.MulWithVec4(matrix, 0.5f, BullwheelRenderer.RimPivotY, 0.5f, 1f);

            // Tolerances rather than decimal places: these are single-precision sines and cosines, and the
            // pivot itself sits on a 4-dp midpoint that rounds two ways. A wheel that orbits misses by a
            // whole block, four orders above this.
            Assert.Equal(0.5 + offset.X, axle[0], 1e-4);
            Assert.Equal(BullwheelRenderer.RimPivotY + offset.Y, axle[1], 1e-4);
            Assert.Equal(0.5 + offset.Z, axle[2], 1e-4);

            // One point on the felloe, 0.6 up the axle's own vertical, against the closed form of the chain
            // the renderer is meant to be: Ry(yaw) applied to Rx(theta) applied to (0, 0.6, 0), written out
            // from the rotation definitions rather than composed from Matrixf - composing it would agree
            // with whatever the renderer does and prove nothing. All three components, because every wrong
            // chain puts the same 0.6 on a DIFFERENT axis and changes nothing else.
            //
            // Which row catches what, because it is not one row: west (yaw 90) at a quarter turn separates
            // the shipped chain, +0.6 on x, from both wrong ones, +0.6 on z. It cannot separate those two
            // from EACH OTHER - at yaw 90 they are literally the same matrix, since Ry(90) carries z to x and
            // so Ry(90)*Rz(t) == Rx(t)*Ry(90). North (yaw 0) at a quarter turn is the row that does: the
            // swapped chain agrees with the shipped one there, and RotateZ puts the 0.6 on -x.
            var felloe = Mat4f.MulWithVec4(matrix, 0.5f, BullwheelRenderer.RimPivotY + 0.6f, 0.5f, 1f);
            Assert.Equal(0.5 + offset.X + 0.6 * Math.Sin(theta) * Math.Sin(yaw), felloe[0], 1e-4);
            Assert.Equal(BullwheelRenderer.RimPivotY + offset.Y + 0.6 * Math.Cos(theta), felloe[1], 1e-4);
            Assert.Equal(0.5 + offset.Z + 0.6 * Math.Sin(theta) * Math.Cos(yaw), felloe[2], 1e-4);
        }
    }

    /// <summary>
    /// The wrap is drawn at a tower carrying exactly ONE span and nowhere else, and that conditional is not
    /// a wart. <c>STATION-DESIGN</c> §1 allows a station that is not an end tower, and at such a tower there
    /// is no dead side: a ring dropped to the rope on either side has its underside below a passing cabin's
    /// grip for a block of travel, every trip, in both directions.
    /// <para>
    /// <c>Spans.Count</c> and deliberately not <c>IsEndpoint</c> - gating on <c>StructureComplete</c> would
    /// make the wheel jump a block sideways the moment somebody broke a brace, and jump back when they put
    /// it in. And the wheel's own facing is not in it anywhere, which is why a bullwheel placed a quarter
    /// turn out still wraps correctly: the wrap is keyed on the footing's spans.
    /// </para>
    /// </summary>
    [Fact]
    public void TheWrapIsOnlyDrawnWhereNoCabinPasses()
    {
        var tower = new BEPylonBase { Pos = new BlockPos(0, 64, 0) };
        Assert.Null(tower.DeadSide);

        // One span, in from the north: the dead side points the other way, along the line and away from it.
        tower.Spans.Add(new BlockPos(0, 64, -20));
        Assert.Equal(0, tower.DeadSide!.X, 9);
        Assert.Equal(0, tower.DeadSide.Y, 9);
        Assert.Equal(1, tower.DeadSide.Z, 9);

        // Two spans: a cabin passes, and there is no side of this tower nothing runs over.
        tower.Spans.Add(new BlockPos(20, 64, 0));
        Assert.Null(tower.DeadSide);

        // A pitched span is still a horizontal dead side - the wheel drops to the rope's own centreline at
        // the tower, and the arriving rope's climb is a kink at the sheave throat, which is where a sheave
        // puts one.
        tower.Spans.Clear();
        tower.Spans.Add(new BlockPos(-30, 20, -40));
        Assert.Equal(0.6, tower.DeadSide!.X, 9);
        Assert.Equal(0, tower.DeadSide.Y, 9);
        Assert.Equal(0.8, tower.DeadSide.Z, 9);
    }

    /// <summary>
    /// The wrap's own geometry, which is three claims and all of them are about one number. The chord
    /// MIDPOINTS sit on rho, not the corners, so the bottom of the ring lands exactly on the rope's own
    /// centreline where the wheel is tangent to it; the ring closes on itself, so there is no free end
    /// anywhere; and the straight stub out of the sheave is collinear with the bottom chord, which is why
    /// <c>BuildRun</c> hands back sixteen boxes rather than seventeen and the joint phase alternates all the
    /// way round instead of putting two full-depth boxes in one plane where it meets itself.
    /// </summary>
    [Fact]
    public void TheWrapIsAClosedRingBeddedOnTheRopesOwnCentreline()
    {
        var dead = new Vec3d(0, 0, 1);
        var points = BEPylonBase.WrapPath(dead);

        Assert.Null(BEPylonBase.WrapPath(null));
        Assert.Equal(18, points.Count);

        // It starts at the sheave, exactly, the same way the half-cable does.
        Assert.Equal(0, points[0].Length(), 9);

        // Closed: last vertex is first vertex.
        Assert.Equal(points[1].X, points[17].X, 9);
        Assert.Equal(points[1].Y, points[17].Y, 9);
        Assert.Equal(points[1].Z, points[17].Z, 9);

        var axleZ = (double)BullwheelRenderer.WrapOut;
        var axleY = (double)BullwheelRenderer.WrapRadius;

        for (var k = 1; k < points.Count - 1; k++)
        {
            // Every chord's MIDPOINT is exactly rho from the axle, which is the tangency. The vertices are
            // 0.208 units outside it, a tenth of the cable's own thickness.
            var midY = (points[k].Y + points[k + 1].Y) / 2 - axleY;
            var midZ = (points[k].Z + points[k + 1].Z) / 2 - axleZ;
            Assert.Equal(axleY, Math.Sqrt(midY * midY + midZ * midZ), 6);

            // Flat in the cross-axis: the ring turns in the plane that contains the line, so a wheel that
            // wrapped sideways would show up here rather than in a render.
            Assert.Equal(0, points[k].X, 9);
        }

        // The bottom of the ring is ON the rope, which is what the whole drop is for, and the stub that
        // reaches it is dead straight out of the sheave.
        Assert.Equal(0, points[1].Y, 6);
        Assert.Equal(0, points[2].Y, 6);
        Assert.True(points[1].Z < axleZ && points[2].Z > axleZ,
            "the ring's bottom chord does not straddle the axle, so it is not tangent under the wheel");

        // THE HANDSHAKE, and it is the only thing tying the two lanes together. The ring is chunk mesh from
        // BEPylonBase and the wheel is a matrix in BullwheelRenderer, computed in different files from
        // different constants, and the whole build is a rope drawn round a wheel that is somewhere else if
        // they disagree by a sign. The wheel rests RimPivotY - 0.5 above the anchor; add the offset and it
        // has to land on the centre this ring was drawn about.
        var offset = BEBullwheel.WrapOffset(dead);
        Assert.Equal(dead.X * axleZ, offset.X, 5);
        Assert.Equal(dead.Z * axleZ, offset.Z, 5);
        Assert.Equal(axleY, BullwheelRenderer.RimPivotY - 0.5 + offset.Y, 5);

        // ...and no dead side is no offset, which is the wheel a cabin passes under.
        Assert.Equal(0, BEBullwheel.WrapOffset(null).Length(), 9);
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
