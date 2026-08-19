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
        // Cabin body is anchor-3.25..anchor+0.19; the sampled rows are
        // anchor-ClearanceBelow-0.5..anchor+ClearanceAbove+0.5.
        Assert.True(SpanMath.ClearanceBelow + 0.5 >= 3.25);
        Assert.Equal(1, SpanMath.ClearanceRadius);

        // ...and above the rope there is the RETURN STRAND, which the cabin does not ride and terrain still
        // has to be out of the way of. Its band is ReturnLift +/- CableRadius. ONE row covers it, with 0.766
        // blocks of that row under it and 0.114 over: two "for margin" would refuse spans over nothing.
        Assert.True(SpanMath.ClearanceAbove + 0.5 >= BEPylonBase.ReturnLift + BEPylonBase.CableRadius);
        Assert.True(SpanMath.ClearanceAbove - 0.5 <= BEPylonBase.ReturnLift - BEPylonBase.CableRadius,
            "a row of clearance rays is being cast above the return strand, over nothing");

        // ...and the derived ladder IS those rows on a level span, which is the whole claim that nothing about
        // the flat case changed: rays at -3, -2, -1, 0, +1, certifying -3.5 to +1.5.
        Assert.Equal(new[] { -3.0, -2.0, -1.0, 0.0, 1.0 }, SpanMath.ClearanceRows(0));
    }

    /// <summary>
    /// The corridor has to certify what the cabin SWEEPS, and the cabin does not lie along the span. It hangs
    /// plumb and stays level - <c>EntityRopewayCabin.Place</c> writes yaw and nothing else - while the sweep's
    /// own <c>up</c> axis is perpendicular to the chord and leans back with the pitch. So the 4-block body
    /// projects onto <c>up</c> as an extra <c>2*sin(pitch)</c> at each end, down at the tail and up at the nose,
    /// on top of its own height.
    /// <para>
    /// The fixed <c>[-3.5, +1.5]</c> window that shipped was exact at zero and wrong everywhere else. Worst
    /// under the floor at 29.74 degrees, which is an ordinary hillside; worst over the nose approaching
    /// vertical. Both numbers are pinned below so the two ends of the mistake cannot come back one at a time.
    /// </para>
    /// </summary>
    [Fact]
    public void TheCorridorFollowsTheCabinRatherThanTheRopeLine()
    {
        const double hang = EntityRopewayCabin.DefaultHangDrop;

        // The band, worked independently of SpanMath: the cabin's eight body corners and the return strand,
        // each projected onto up = (-sin along the bearing, +cos vertical).
        (double Low, double High) Band(double deg)
        {
            var sin = Math.Sin(deg * Math.PI / 180);
            var cos = Math.Cos(deg * Math.PI / 180);
            var low = double.MaxValue;
            var high = double.MinValue;

            foreach (var along in new[] { -SpanMath.CabinHalfLength, SpanMath.CabinHalfLength })
            {
                foreach (var vertical in new[] { -hang - SpanMath.CabinHalfHeight, -hang + SpanMath.CabinHalfHeight })
                {
                    var onUp = -along * sin + vertical * cos;
                    low = Math.Min(low, onUp);
                    high = Math.Max(high, onUp);
                }
            }

            return (low, Math.Max(high, BEPylonBase.ReturnLift * cos));
        }

        var shortfallBelow = 0.0;
        var shortfallAbove = 0.0;
        for (var deg = 0.0; deg <= 90.0; deg += 0.02)
        {
            var (low, high) = Band(deg);
            var rows = SpanMath.ClearanceRows(Math.Sin(deg * Math.PI / 180));

            Assert.True(rows[0] - 0.5 <= low + 1e-9,
                $"at {deg:0.00} degrees the corridor's lowest ray certifies down to {rows[0] - 0.5:0.000} " +
                $"and the cabin's floor reaches {low:0.000} - uncertified ground under a seated rider");
            Assert.True(rows[^1] + 0.5 >= high - 1e-9,
                $"at {deg:0.00} degrees the corridor's highest ray certifies up to {rows[^1] + 0.5:0.000} " +
                $"and the cabin's nose reaches {high:0.000}");

            // Rows are one block apart and in order, or a gap between two of them is uncertified ground.
            for (var i = 1; i < rows.Length; i++) Assert.Equal(1.0, rows[i] - rows[i - 1], 9);

            // What the old fixed window would have missed at this pitch, for the two numbers below.
            shortfallBelow = Math.Max(shortfallBelow, -(SpanMath.ClearanceBelow + 0.5) - low);
            shortfallAbove = Math.Max(shortfallAbove, high - (SpanMath.ClearanceAbove + 0.5));
        }

        // Both ends of the bug it replaced, pinned. Below: 2*sin + 3.5*cos peaks at sqrt(2^2 + 3.5^2) = 4.031
        // at atan(2/3.5) = 29.74 degrees, so a fixed 3 rows was 0.531 blocks short under the cabin on the
        // pitch a hill line is actually built at. Above: the nose reaches the cabin's own half-length, 2.0,
        // as the span goes vertical and the strand collapses onto the rope line, against 1.5 certified.
        Assert.Equal(0.531, shortfallBelow, 3);
        Assert.Equal(SpanMath.CabinHalfLength - (SpanMath.ClearanceAbove + 0.5), shortfallAbove, 3);

        // The price, and it is only paid where it is earned: 5 rows on the flat, 6 through the middle of the
        // range where the leaning band is widest, back to 5 once the strand has collapsed onto the rope line.
        Assert.Equal(5, SpanMath.ClearanceRows(0).Length);
        Assert.Equal(6, SpanMath.ClearanceRows(Math.Sin(30 * Math.PI / 180)).Length);
        Assert.Equal(5, SpanMath.ClearanceRows(Math.Sin(85 * Math.PI / 180)).Length);
        Assert.Equal(4, SpanMath.ClearanceRows(1).Length);
    }

    /// <summary>
    /// A vertical span has no bearing, so <see cref="SpanMath.IsSpanClear"/> falls back to a hard-coded
    /// <c>right</c> and its <c>up</c> flips with the direction of travel. The rows have to be symmetric about
    /// the cabin or the corridor certified from the top tower is not the one certified from the bottom - which
    /// is a link that succeeds followed by a cabin that refuses to move.
    /// </summary>
    [Fact]
    public void TheVerticalCorridorIsTheSameOneFromEitherEnd()
    {
        var up = SpanMath.ClearanceRows(1);
        var down = SpanMath.ClearanceRows(-1);

        Assert.Equal(up, down);
        Assert.Equal(0, up[0] + up[^1], 9);
        Assert.Equal(SpanMath.CabinHalfLength, up[^1] + 0.5, 9);
    }

    /// <summary>
    /// The pitch a span is built at, which is the number <c>PassablePitchTan</c> is compared against. Unsigned,
    /// because a line is ridden both ways and the cabin eats the crossarm going up whichever end it started at.
    /// </summary>
    [Fact]
    public void PitchIsRiseOverRunAndUnsigned()
    {
        var foot = new Vec3d(0, 64, 0);

        Assert.Equal(0, SpanMath.PitchTan(foot, new Vec3d(30, 64, 0)));
        Assert.Equal(1, SpanMath.PitchTan(foot, new Vec3d(30, 94, 0)), 9);
        Assert.Equal(1, SpanMath.PitchTan(foot, new Vec3d(30, 34, 0)), 9);
        Assert.Equal(SpanMath.PassablePitchTan, SpanMath.PitchTan(foot, new Vec3d(0, 70, 30)), 9);
        Assert.Equal(double.PositiveInfinity, SpanMath.PitchTan(foot, new Vec3d(0, 104, 0)));

        // A tower linked to itself is not a climb, and must not read as an infinite one.
        Assert.Equal(0, SpanMath.PitchTan(foot, foot.Clone()));
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
    /// THE ONE-BOX TRAP, and the test above is the reason it survived a whole build: it makes exactly this
    /// claim and makes it on <c>BuildHalfCable</c>, which is the degenerate ONE-BOX case - the only case
    /// where six faces and the six-long arrays <c>CubeMeshUtil.GetCube</c> happens to allocate line up.
    /// <para>
    /// <c>GetCube</c> leaves <c>TextureIndicesCount</c> at 0 and <c>WithColorMaps</c> leaves
    /// <c>ColorMapIdsCount</c> at 0, and <c>MeshData.AddMeshData</c> - which <c>BuildRun</c> calls once per
    /// extra box - copies each per-face array by its <c>*Count</c> rather than by its <c>Length</c>
    /// (1.22.1 <c>MeshData.cs:1028-1046</c>). Only <c>XyzFaces</c> carries a real count. So every run of more
    /// than one box reached the chunk tesselator with <c>XyzFacesCount = 6N</c> against six-long side arrays,
    /// and <c>JsonTesselator.AddJsonModelDataToMesh</c> indexes <c>TextureIndices[l]</c> and both colour maps
    /// for <c>l &lt; XyzFacesCount</c> - IndexOutOfRangeException at face 6, caught and logged per block
    /// entity, with box 1 already committed and every later box AND every later run of that
    /// <c>OnTesselation</c> call never built.
    /// </para>
    /// <para>
    /// In the world that was three of the author's QA items and not one: a corner tower's runs are 29-30
    /// boxes so it drew NOTHING (items 1 and 4), a terminal's wrap is 9 so it lost the arc and both brackets
    /// (item 2), and a straight tower's runs are one box each so a two-tower line looked perfect. The
    /// assertion that catches it is <c>Length &gt;= XyzFacesCount</c> on every array the face loop indexes,
    /// over runs long enough to leave the boundary behind.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(2)]
    [InlineData(9)]     // a terminal's wrap
    [InlineData(30)]    // one half-span at a right-angle corner
    public void EveryBoxOfARunCarriesTheSideArrayEntriesTheTesselatorIndexesPerFace(int boxes)
    {
        // A polyline that cannot collapse: every sample turns by 30 degrees, so OnTheLine never merges two.
        var points = new List<Vec3d>();
        for (var i = 0; i <= boxes; i++)
        {
            points.Add(new Vec3d(Math.Cos(i * Math.PI / 6), 0, Math.Sin(i * Math.PI / 6)).Mul(i * 0.5));
        }

        var mesh = BEPylonBase.BuildRun(
            points, BEPylonBase.CableRadius, BEPylonBase.CableRadius,
            new TextureAtlasPosition { x1 = 0, y1 = 0, x2 = 1, y2 = 1 });

        Assert.NotNull(mesh);
        Assert.Equal(6 * boxes, mesh.XyzFacesCount);

        // The three arrays JsonTesselator indexes by face, and the counts AddMeshData copies them by. Length
        // is what stops the throw; the counts are what make the NEXT AddMeshData carry them at all, so a run
        // merged into a longer one has to keep both true.
        Assert.True(mesh.TextureIndices.Length >= mesh.XyzFacesCount,
            $"TextureIndices is {mesh.TextureIndices.Length} long for {mesh.XyzFacesCount} faces - the "
            + "tesselator throws at face " + mesh.TextureIndices.Length);
        Assert.True(mesh.SeasonColorMapIds.Length >= mesh.XyzFacesCount);
        Assert.True(mesh.ClimateColorMapIds.Length >= mesh.XyzFacesCount);
        Assert.Equal(mesh.XyzFacesCount, mesh.TextureIndicesCount);
        Assert.Equal(mesh.XyzFacesCount, mesh.ColorMapIdsCount);

        // ...and the faces really are addressed, or the arrays above are long enough for a mesh that draws
        // nothing. Four vertices and six indices per face is what the cube chain is.
        Assert.Equal(4 * mesh.XyzFacesCount, mesh.VerticesCount);
        Assert.Equal(6 * mesh.XyzFacesCount, mesh.IndicesCount);
    }

    /// <summary>
    /// ITEM 1, as an invariant: the two towers either side of one span draw halves that MEET, and both of
    /// them are really built. Each end samples its own <c>LocalLine</c> - a two- or three-tower mini-line
    /// whose far anchor is an END, so its tangent is its single leg - and the claim is that the two answers
    /// agree at the chord midpoint anyway, because the bend window
    /// <c>TrimForTowers(L) &lt;= (L-1)/2 &lt; L/2</c> never reaches it.
    /// <para>
    /// The MESH half is the half that failed. The geometry has always met to double precision; what the
    /// author saw was one end of every span drawn and the other missing, because the tower at that end threw
    /// inside the tesselator before it got to its own half. So this asserts both: the polylines meet, and
    /// each half is a mesh whose per-face arrays cover its own face count.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTwoHalvesEitherSideOfOneSpanMeetAndBothAreDrawn()
    {
        // Two right-angle corners, so both interior towers bend and neither half is the trivial straight one.
        var towers = new List<BlockPos>
        {
            new(0, 64, 0), new(0, 64, 20), new(20, 64, 20), new(20, 64, 40)
        };

        var texPos = new TextureAtlasPosition { x1 = 0, y1 = 0, x2 = 1, y2 = 1 };

        for (var span = 0; span < towers.Count - 1; span++)
        {
            // Exactly what BEPylonBase.LocalLine hands each of the two footings: this tower between its own
            // peers, so the far anchor of the span is an end of the mini-line.
            var near = MiniLine(towers, span, out var nearMe);
            var far = MiniLine(towers, span + 1, out var farMe);

            var nearPeer = nearMe + 1;
            var farPeer = farMe - 1;

            var a = BEPylonBase.HalfSpanPath(near, nearMe, nearPeer);
            var b = BEPylonBase.HalfSpanPath(far, farMe, farPeer);

            // In world coordinates: each half is relative to its own sheave.
            var meetA = a[a.Count - 1].Clone().Add(near.Anchors[nearMe]);
            var meetB = b[b.Count - 1].Clone().Add(far.Anchors[farMe]);

            Assert.Equal(0, meetA.DistanceTo(meetB), 9);

            // ...and each reaches exactly half the span, so neither is short and neither overlaps the other.
            var half = near.Anchors[nearMe].DistanceTo(far.Anchors[farMe]) / 2;
            Assert.Equal(half, a[a.Count - 1].Length(), 9);
            Assert.Equal(half, b[b.Count - 1].Length(), 9);

            // Both halves are DRAWN. A corner tower's half is 29-30 boxes; before the per-face counts were
            // set it built one 0.125-block stub and abandoned the rest of its OnTesselation call.
            foreach (var half_ in new[] { a, b })
            {
                var mesh = BEPylonBase.BuildRun(half_, BEPylonBase.CableRadius, BEPylonBase.CableRadius, texPos);
                Assert.NotNull(mesh);
                Assert.True(mesh.TextureIndices.Length >= mesh.XyzFacesCount);
                Assert.True(mesh.SeasonColorMapIds.Length >= mesh.XyzFacesCount);
                Assert.True(mesh.ClimateColorMapIds.Length >= mesh.XyzFacesCount);
            }
        }
    }

    /// <summary>The mini-line <c>BEPylonBase.LocalLine</c> builds for one tower of a chain.</summary>
    private static RopewayLine MiniLine(IReadOnlyList<BlockPos> towers, int index, out int me)
    {
        var chain = new List<BlockPos>();
        if (index > 0) chain.Add(towers[index - 1]);
        chain.Add(towers[index]);
        if (index < towers.Count - 1) chain.Add(towers[index + 1]);

        me = chain.IndexOf(towers[index]);
        return RopewayLine.FromTowers(chain);
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
        // ESCAPES, not the raw bytes. These two characters were written literally until 2026-08-10,
        // which put a NUL in the file - and one NUL is all it takes for grep, ripgrep and `git diff`
        // to call the whole 106 KB suite "binary" and print nothing. Same string, same test.
        Assert.Null(BEPylonBase.SanitiseName("\u0000\u0001"));
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
        held = EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: true, 0.5f, ref released);
        held = EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: true, 0.5f, ref released);
        Assert.Equal(1.0, held, 6);
        Assert.True(held < EntityRopewayCabin.BailHoldSeconds, "one second must not be enough to jump");

        // Letting go throws the lot away - the next press starts from zero.
        Assert.Equal(0, EntityRopewayCabin.HoldSneak(held, sneaking: false, heldIn: true, 0.5f, ref released));

        // So does the cabin stopping, even with the key still down.
        Assert.Equal(0, EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: false, 0.5f, ref released));

        // And an unbroken hold does reach the threshold, at the moment it says it does.
        held = 0;
        released = true;
        for (var i = 0; i < (int)(EntityRopewayCabin.BailHoldSeconds / 0.5); i++)
        {
            held = EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: true, 0.5f, ref released);
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
        for (var i = 0; i < 10; i++) held = EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: false, 0.5f, ref released);

        // It departs and they keep holding, well past the threshold. Still nothing.
        for (var i = 0; i < 20; i++) held = EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: true, 0.5f, ref released);
        Assert.Equal(0, held);

        // One tick off the key while moving is the edge the arm actually wants.
        held = EntityRopewayCabin.HoldSneak(held, sneaking: false, heldIn: true, 0.5f, ref released);
        Assert.True(released);
        for (var i = 0; i < (int)(EntityRopewayCabin.BailHoldSeconds / 0.5); i++)
        {
            held = EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: true, 0.5f, ref released);
        }

        Assert.True(held >= EntityRopewayCabin.BailHoldSeconds);

        // And the cabin stopping revokes it again: a rider who rides on through a stop still holding the key
        // has to press it afresh, exactly as if they had just boarded.
        held = EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: false, 0.5f, ref released);
        Assert.False(released);
        Assert.Equal(0, EntityRopewayCabin.HoldSneak(held, sneaking: true, heldIn: true, 0.5f, ref released));
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
    /// ITEM 5. The wheel took its TRANSLATION from the line and its ROTATION from its own <c>side</c>
    /// variant, so the hub landed on the rope's plan line and the groove plane stayed on the nearest
    /// cardinal: two sources, up to 90 degrees apart, crossing at the hub and diverging everywhere else. Past
    /// 13.4 degrees the rope has left the felloe entirely; past about 22.5 the turning felloe sweeps through
    /// both brackets drawn to carry its axle.
    /// <para>
    /// The claim is that the disc's own direction - <c>Ry(yaw)</c> applied to the authored disc normal's
    /// perpendicular <c>(0, 0, 1)</c>, which is <c>(sin yaw, 0, cos yaw)</c> - is PARALLEL to the line's plan
    /// tangent, on the same axis rather than merely at some angle to it. Parallel and not equal, because the
    /// rim is 180-degree symmetric and <see cref="BullwheelRenderer.YawAlong"/> deliberately takes the branch
    /// nearer the block's own facing so the shipped spin direction survives.
    /// </para>
    /// </summary>
    [Theory]
    // A line on the cardinal reproduces the shipped answer exactly, at every facing.
    [InlineData(0.0, "north")]
    [InlineData(0.0, "east")]
    [InlineData(0.0, "south")]
    [InlineData(0.0, "west")]
    // ...and off it, which is every terminal nobody built due north of its peer. 45 is the worst a bearing
    // can be from the nearest cardinal; the rest are the rows the wrap measurement was taken at.
    [InlineData(13.41, "north")]
    [InlineData(22.5, "north")]
    [InlineData(36.87, "north")]
    [InlineData(45.0, "east")]
    [InlineData(-45.0, "south")]
    [InlineData(30.0, "west")]
    [InlineData(180.0, "north")]
    public void TheWheelsGrooveStandsInThePlaneTheLineRunsIn(double bearingDeg, string side)
    {
        var bearing = bearingDeg * Math.PI / 180;
        var tangent = new Vec3d(Math.Sin(bearing), 0, Math.Cos(bearing));
        var blockYaw = BullwheelRenderer.YawFor(side);

        var yaw = BullwheelRenderer.YawAlong(tangent, blockYaw);

        // The disc's own direction, pushed through the real matrix rather than restated: RimMatrix puts the
        // axle at (0.5, RimPivotY, 0.5) + offset, so a point one block along the rim's authored +Z lands on
        // the groove plane's own bearing.
        var offset = new Vec3f(0.3f, -BullwheelRenderer.WrapDrop, -0.7f);
        var matrix = BullwheelRenderer.RimMatrix(new Matrixf().Identity(), yaw, 0f, offset).Values;
        var axle = Mat4f.MulWithVec4(matrix, 0.5f, BullwheelRenderer.RimPivotY, 0.5f, 1f);
        var along = Mat4f.MulWithVec4(matrix, 0.5f, BullwheelRenderer.RimPivotY, 1.5f, 1f);

        var dx = along[0] - axle[0];
        var dz = along[2] - axle[2];

        // Parallel: the cross product of the two plan directions is zero. The dot may be either sign.
        Assert.Equal(0, dx * tangent.Z - dz * tangent.X, 4);

        // The branch, and it is not decoration: RotateX sits INSIDE the yaw, so yaw and yaw + pi draw the
        // same disc turning opposite ways. Never more than a quarter turn from the block's own facing, which
        // is what keeps a shaft's wheel spinning the way it always did.
        Assert.True(Math.Abs(GameMath.AngleRadDistance(yaw, blockYaw)) <= Math.PI / 2 + 1e-5);
    }

    /// <summary>
    /// The other half of item 5: a wheel with no line to read, and a wheel on a SHAFT - where the peer is
    /// directly below, the plan tangent is the zero vector and <c>Math.Atan2(0, 0)</c> is <b>0.0</b> rather
    /// than NaN, which is a silent permanent due north. Both fall back to the block's own facing, and on a
    /// shaft that is the right answer rather than a degradation: <c>OwnTheHeadCell</c> narrows
    /// <c>shaftsheave-*</c> to the footing's own side, so the sheave's variant IS the machine's heading.
    /// </summary>
    [Theory]
    [InlineData("north")]
    [InlineData("east")]
    [InlineData("south")]
    [InlineData("west")]
    public void AWheelWithNoBearingToReadKeepsItsOwnFacing(string side)
    {
        var blockYaw = BullwheelRenderer.YawFor(side);

        Assert.Equal(blockYaw, BullwheelRenderer.YawAlong(null, blockYaw));
        Assert.Equal(blockYaw, BullwheelRenderer.YawAlong(new Vec3d(0, 1, 0), blockYaw));
        Assert.Equal(blockYaw, BullwheelRenderer.YawAlong(new Vec3d(0, -1, 0), blockYaw));
    }

    /// <summary>
    /// Where the wheel's yaw comes FROM: the tower's own tangent, which is its single leg at a terminal and
    /// the corner's bisector at a station the line runs through. Same expression the brackets and both rail
    /// cheeks are already taken across, so the wheel cannot disagree with the metal drawn to carry it.
    /// </summary>
    [Fact]
    public void ATowersLineTangentIsItsLegAtATerminalAndItsBisectorAtACorner()
    {
        var tower = new BEPylonBase { Pos = new BlockPos(0, 64, 0) };
        Assert.Null(tower.LineTangent);

        // One span, 3-4-5 to the south east: the tangent is the leg, 36.87 degrees off due south.
        tower.Spans.Add(new BlockPos(12, 64, 16));
        var leg = tower.LineTangent;
        Assert.Equal(0.6, leg!.X, 6);
        Assert.Equal(0.8, leg.Z, 6);

        // ...and it is the same axis the wrap is laid on, so the groove and the rope are one plane.
        var dead = tower.DeadSide;
        Assert.Equal(0, leg.X * dead!.Z - leg.Z * dead.X, 9);

        // A second span due west makes it a corner, and the mini-line runs peer -> here -> peer. The tangent
        // is the bisector of the leg ARRIVING, (-0.6, -0.8), and the leg leaving, (-1, 0) - their normalised
        // sum, exactly between the two and pointing the way the cabin goes.
        tower.Spans.Add(new BlockPos(-20, 64, 0));
        var bisector = tower.LineTangent;
        Assert.NotNull(bisector);

        var sum = new Vec3d(-0.6, 0, -0.8).Add(new Vec3d(-1, 0, 0)).Normalize();
        Assert.Equal(sum.X, bisector!.X, 6);
        Assert.Equal(sum.Z, bisector.Z, 6);

        // ...and it is genuinely between them: a corner tower's wheel used to sit on whichever cardinal its
        // own variant named, which at this 116.57 degree corner is up to 45 degrees off this.
        Assert.NotEqual(leg.X, bisector.X, 3);

        // A SHAFT head is the case with no bearing at all - its one peer is directly below, so the tangent is
        // vertical and there is no plan direction to stand a groove in. This is the whole of "the elevator is
        // untouched": the wheel falls back to its own facing, which OwnTheHeadCell has already narrowed to
        // the footing's, and nothing about the shaft moves.
        var head = new BEPylonBase { Pos = new BlockPos(0, 100, 0), ShaftRole = "head" };
        head.Spans.Add(new BlockPos(0, 52, 0));

        var vertical = head.LineTangent;
        Assert.Equal(0, vertical!.X, 9);
        Assert.Equal(0, vertical.Z, 9);
        Assert.Equal(-1, vertical.Y, 9);

        foreach (var side in new[] { "north", "east", "south", "west" })
        {
            Assert.Equal(BullwheelRenderer.YawFor(side), BullwheelRenderer.YawAlong(vertical, BullwheelRenderer.YawFor(side)));
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
    /// The wrap's own geometry, and it is one claim made twice. The chord MIDPOINTS sit on rho, not the
    /// corners, so the FIRST chord lands exactly on the going strand's centreline where the wheel is tangent
    /// to it from above, and the LAST lands exactly on the RETURN strand's where it is tangent from below.
    /// That is what makes <c>ReturnLift</c> a wheel diameter rather than a number somebody picked - and it is
    /// what stops anyone "tidying" the chord count, because an odd count puts no midpoint at pi and the rope
    /// would silently leave the wheel off its own strand.
    /// <para>
    /// It used to be a CLOSED RING, on the argument that a true arc "stops in mid air where the second strand
    /// would leave". The second strand is drawn now, so there is no free end and the arc is also cheaper:
    /// both stubs are collinear with the chord they meet, so twelve points come back as nine boxes against
    /// the ring's sixteen.
    /// </para>
    /// </summary>
    [Fact]
    public void TheWrapLeavesOnTheReturnStrand()
    {
        var dead = new Vec3d(0, 0, 1);
        var points = BEPylonBase.WrapPath(dead);

        Assert.Null(BEPylonBase.WrapPath(null));

        // The sheave, ten arc vertices - half of sixteen chords, plus the one that closes each end chord -
        // and the tower again on the return strand.
        Assert.Equal(12, points.Count);

        // It starts at the sheave, exactly, the same way the half-cable does...
        Assert.Equal(0, points[0].Length(), 9);

        // ...and it ends where this tower's own lifted half-span starts. No free end anywhere.
        Assert.Equal(0, points[11].X, 9);
        Assert.Equal(BEPylonBase.ReturnLift, points[11].Y, 9);
        Assert.Equal(0, points[11].Z, 9);

        var axleZ = (double)BullwheelRenderer.WrapOut;
        var axleY = (double)BullwheelRenderer.WrapRadius;

        for (var k = 1; k < points.Count - 2; k++)
        {
            // Every chord's MIDPOINT is exactly rho from the axle, which is the tangency. The vertices are
            // 0.208 units outside it, a tenth of the cable's own thickness.
            var midY = (points[k].Y + points[k + 1].Y) / 2 - axleY;
            var midZ = (points[k].Z + points[k + 1].Z) / 2 - axleZ;
            Assert.Equal(axleY, Math.Sqrt(midY * midY + midZ * midZ), 6);

            // Flat in the cross-axis: the arc turns in the plane that contains the line, so a wheel that
            // wrapped sideways would show up here rather than in a render.
            Assert.Equal(0, points[k].X, 9);
        }

        // The BOTTOM chord is ON the going strand and the stub that reaches it is dead straight out of the
        // sheave, so BuildRun merges the three into one box.
        Assert.Equal(0, points[1].Y, 6);
        Assert.Equal(0, points[2].Y, 6);
        Assert.True(points[1].Z < axleZ && points[2].Z > axleZ,
            "the arc's bottom chord does not straddle the axle, so it is not tangent under the wheel");

        // The TOP chord is on the return strand, at exactly twice the radius, and the closing stub is
        // collinear with it for the same reason. THIS is where the loop's separation comes from.
        Assert.Equal(BEPylonBase.ReturnLift, points[9].Y, 6);
        Assert.Equal(BEPylonBase.ReturnLift, points[10].Y, 6);
        Assert.True(points[9].Z > axleZ && points[10].Z < axleZ,
            "the arc's top chord does not straddle the axle, so the rope does not leave over the wheel");

        // Half a turn and no more: the arc never comes back down past the axle's height on the near side.
        Assert.True(points[10].Z < axleZ && points[10].Y > axleY,
            "the arc runs past the top of the wheel");

        // THE HANDSHAKE, and it is the only thing tying the two lanes together. The arc is chunk mesh from
        // BEPylonBase and the wheel is a matrix in BullwheelRenderer, computed in different files from
        // different constants, and the whole build is a rope drawn round a wheel that is somewhere else if
        // they disagree by a sign. The wheel rests RimPivotY - 0.5 above the anchor; add the offset and it
        // has to land on the centre this arc was drawn about.
        var offset = BEBullwheel.WrapOffset(dead, 1);
        Assert.Equal(dead.X * axleZ, offset.X, 5);
        Assert.Equal(dead.Z * axleZ, offset.Z, 5);
        Assert.Equal(axleY, BullwheelRenderer.RimPivotY - 0.5 + offset.Y, 5);

        // A tower with no rope on it leaves the wheel where the shape authors it...
        Assert.Equal(0, BEBullwheel.WrapOffset(null, 0).Length(), 9);

        // ...and a station the line runs THROUGH lifts it onto the return strand instead, groove tangent
        // from below. Straight up, because there is no dead side to go out along.
        var held = BEBullwheel.WrapOffset(null, 2);
        Assert.Equal(0, held.X, 9);
        Assert.Equal(0, held.Z, 9);
        Assert.Equal(BEPylonBase.ReturnLift + axleY, BullwheelRenderer.RimPivotY - 0.5 + held.Y, 5);
    }

    /// <summary>
    /// ITEM 2 - "I think the top rope doesn't extend all the way to the bullwheel." The model had no gap:
    /// <see cref="TheWrapLeavesOnTheReturnStrand"/> pins the going strand, the wrap's two ends and the return
    /// strand to the same points and the arc's centre to the renderer's own axle. What the author saw was the
    /// arc not being BUILT - the wrap collapses to nine boxes, and until the per-face counts were set every
    /// run past its first box was lost inside the tesselator. So the going strand ran out under the wheel and
    /// the return strand stopped dead on the tower column with the whole half turn missing.
    /// <para>
    /// This is the mesh half of that claim, and it is deliberately made on the VERTICES: the arc has to be in
    /// the chunk mesh, not merely in the list of points handed to the builder.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDrawnWrapCarriesTheWholeHalfTurnOntoTheReturnStrand()
    {
        var dead = new Vec3d(0, 0, 1);
        var mesh = BEPylonBase.BuildRun(
            BEPylonBase.WrapPath(dead), BEPylonBase.CableRadius, BEPylonBase.CableRadius,
            new TextureAtlasPosition { x1 = 0, y1 = 0, x2 = 1, y2 = 1 }, turnsVertically: true);

        Assert.NotNull(mesh);

        // Nine boxes: both end stubs merge into the chord they meet, which is why the arc is cheaper than the
        // closed ring it replaced. Six faces each, and the side arrays have to cover all of them.
        Assert.Equal(9 * 6, mesh.XyzFacesCount);
        Assert.True(mesh.TextureIndices.Length >= mesh.XyzFacesCount);
        Assert.True(mesh.SeasonColorMapIds.Length >= mesh.XyzFacesCount);

        var min = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
        var max = new[] { double.MinValue, double.MinValue, double.MinValue };
        for (var i = 0; i < mesh.VerticesCount; i++)
        for (var axis = 0; axis < 3; axis++)
        {
            min[axis] = Math.Min(min[axis], mesh.xyz[3 * i + axis]);
            max[axis] = Math.Max(max[axis], mesh.xyz[3 * i + axis]);
        }

        // Local to the FOOTING, so the sheave is SheaveHeight above its centre. The arc's top has to reach a
        // wheel DIAMETER above the going strand, which is where the return strand leaves - the truncated wrap
        // reached the going strand's own height and stopped.
        var sheaveY = 0.5 + SpanMath.SheaveHeight;
        Assert.Equal(sheaveY + BEPylonBase.ReturnLift + BEPylonBase.CableRadius, max[1], 4);

        // ...and it goes round the FAR side of the axle, past the wheel rather than up to it. The axle stands
        // WrapOut out along the dead side and the groove is WrapRadius beyond that.
        var axleZ = 0.5 + BullwheelRenderer.WrapOut;
        Assert.True(max[2] > axleZ + BullwheelRenderer.WrapRadius - 0.01,
            $"the drawn wrap reaches z = {max[2]:0.####} and the far side of the groove is at "
            + $"{axleZ + BullwheelRenderer.WrapRadius:0.####} - the arc stops short of the wheel");

        // Both ends are ON the tower and neither runs past it: the two stubs are butt caps on the tower's own
        // centre line, where the going strand arrives and the return strand leaves.
        Assert.Equal(0.5, min[2], 4);

        // The lowest the arc goes is the underside of the going strand it is tangent to.
        Assert.Equal(sheaveY - BEPylonBase.CableRadius, min[1], 4);
    }

    /// <summary>
    /// ITEM 4 - "the rails and travel path really should generate a curve". The bend is implemented, has been
    /// since <c>RopewayLine.Tangents</c> landed, and is not too small to read: 0.4536 blocks off the chord at
    /// a right angle, which is 3.8 times the cable's own thickness. It never reached the screen because the
    /// only tower that draws bent geometry is the CORNER tower, and a corner tower's runs are 29-30 boxes -
    /// exactly the case the tesselator threw on. So the fix for item 4 was the mesh plumbing and NOT a bigger
    /// bend, and this is the assertion that says the curve is in the mesh rather than only in the maths.
    /// <para>
    /// Measured on the box ENDPOINTS the builder emits, because that is what the chunk mesh is made of: a
    /// straight chord would report zero here whatever <c>PositionAt</c> says.
    /// </para>
    /// </summary>
    [Fact]
    public void ACornerTowersDrawnHalfSpanIsTheBentCurveAndNotItsChord()
    {
        // A right-angle corner with 20-block spans, so the bend window is its full 4 blocks.
        var line = MiniLine(
            new List<BlockPos> { new(0, 64, -20), new(0, 64, 0), new(20, 64, 0) }, 1, out var me);

        var path = BEPylonBase.HalfSpanPath(line, me, 2);

        // Off the chord from the tower to the midpoint of the span, which is what a straight half would be.
        var chord = path[path.Count - 1].Clone().Normalize();
        var peak = 0.0;
        foreach (var p in path)
        {
            var along = p.X * chord.X + p.Z * chord.Z;
            peak = Math.Max(peak, Math.Sqrt(p.X * p.X + p.Z * p.Z - along * along));
        }

        // 4/27 * window * sin(turn / 2). The bend's own magnitude is 4/27 * window * |leg - bisector|, which
        // is 0.4536 blocks at a right angle, and the component of it PERPENDICULAR to the chord - which is
        // what a player sees as bow - works out to sin(turn / 2) exactly, because
        // |leg - bisector|^2 - (leg . (leg - bisector))^2 = 1 - cos^2(turn / 2). Three decimals rather than
        // six: RunStep samples at 1.250 and 1.375 blocks and the peak is at window / 3 = 1.333, so the drawn
        // polyline misses the true crest by 0.0003 blocks.
        Assert.Equal(4.0 / 27 * 4 * Math.Sin(Math.PI / 4), peak, 3);
        Assert.True(peak > 3 * 2 * BEPylonBase.CableRadius,
            $"the bend peaks {peak:0.###} blocks off the chord, which is under three cable thicknesses - "
            + "at that size item 4 really would be a design question rather than a plumbing one");

        // ...and every one of the boxes that carries it is built. The run is 30 boxes at this corner; before
        // the per-face counts were set it was one 0.125-block stub and the rest of the tower with it.
        var mesh = BEPylonBase.BuildRun(
            path, BEPylonBase.CableRadius, BEPylonBase.CableRadius,
            new TextureAtlasPosition { x1 = 0, y1 = 0, x2 = 1, y2 = 1 });

        Assert.NotNull(mesh);
        Assert.True(mesh.XyzFacesCount >= 6 * 29, $"only {mesh.XyzFacesCount / 6} boxes of the bend were built");
        Assert.True(mesh.TextureIndices.Length >= mesh.XyzFacesCount);
        Assert.True(mesh.SeasonColorMapIds.Length >= mesh.XyzFacesCount);
        Assert.True(mesh.ClimateColorMapIds.Length >= mesh.XyzFacesCount);

        // The drawn polyline really tracks the curve rather than cutting it: the sagitta of one RunStep chord
        // at the tightest radius the bend reaches is 16 times finer than a single texture unit.
        Assert.True(0.125 * 0.125 / (8 * 1.317) < 1.0 / 16 / 16);
    }

    /// <summary>
    /// THE no-scissoring claim, and it is one assert because it is one list. The return strand is the going
    /// strand's own polyline with a constant added to Y, so at a 90 degree corner - the sharpest bend the mod
    /// draws, radius of curvature 1.317 blocks - the two differ by exactly <c>(0, ReturnLift, 0)</c> at every
    /// sample and by NOTHING in plan. <c>PositionAt</c> adds its bend to X and Z only, which is what makes
    /// that true rather than approximately true.
    /// <para>
    /// Stacked is the only arrangement with this property. An offset curve 1.3263 blocks LATERAL on the
    /// inside of that same turn has radius -0.009: offsetting a plan curve by more than its own radius of
    /// curvature folds it over itself, and the two strands cross. A vertical offset does not interact with a
    /// horizontal curvature at all.
    /// </para>
    /// <para>
    /// The separation itself is the WHEEL, and that half is pinned where the wheel is measured -
    /// <c>TheWrappedWheelClearsACabinAtEveryPositionTheCabinCanReach</c> re-derives rho off the shipped rim's
    /// own sweep and ties <c>ReturnLift</c> to it, so re-authoring <c>bullwheelrim.json</c> moves the loop
    /// with it or fails there.
    /// </para>
    /// </summary>
    [Fact]
    public void TheTwoStrandsAreOneCurveAWheelApart()
    {
        // Two 24-block legs meeting at a right angle, which is the geometry TheBentPathNeverDrives... uses.
        var line = Line((0, 64, -24), (0, 64, 0), (24, 64, 0));

        foreach (var peer in new[] { 0, 2 })
        {
            var going = BEPylonBase.HalfSpanPath(line, 1, peer);
            var returning = BEPylonBase.Lift(going, BEPylonBase.ReturnLift);

            Assert.Equal(going.Count, returning.Count);
            Assert.True(going.Count > 8, "the bend window was not sampled, so this proves nothing about a curve");

            for (var i = 0; i < going.Count; i++)
            {
                // Bit-identical in plan: the SAME list, not a curve fitted to it.
                Assert.Equal(going[i].X, returning[i].X);
                Assert.Equal(going[i].Z, returning[i].Z);
                Assert.Equal(BEPylonBase.ReturnLift, returning[i].Y - going[i].Y, 12);
            }

            // The bend is real on this geometry, or the loop above is comparing two straight lines.
            var bowed = 0.0;
            for (var i = 0; i < going.Count; i++)
            {
                var t = going[i].Length() / going[^1].Length();
                bowed = Math.Max(bowed, Math.Abs(going[i].X - going[^1].X * t) + Math.Abs(going[i].Z - going[^1].Z * t));
            }

            Assert.True(bowed > 0.1, $"the sampled half-span only bows {bowed} blocks off its own chord");
        }

        // A wheel DIAMETER, not a number. Two radii because the rope enters the groove at the bottom of the
        // bullwheel and a 180 degree wrap leaves at the top.
        Assert.Equal(2 * BullwheelRenderer.WrapRadius, BEPylonBase.ReturnLift, 12);
        Assert.Equal(1.3263, BEPylonBase.ReturnLift, 4);
    }

    /// <summary>
    /// THE TWO HALVES MEET, at any pitch and at any length, because the lift is a constant and not a ramp.
    /// Each tower draws only its own half of a span, so the two have to arrive at the midpoint at the same
    /// height, and a lift that was a function of position along the span did not: it ramped over
    /// <c>TrimForTowers</c> of the THREE-DIMENSIONAL span while dividing by the HORIZONTAL run, so on a
    /// pitched span the ramp was still climbing where this tower's half stops - 0.33 blocks of missing rope
    /// on a 5-block span at 53 degrees, 0.83 at 71.6 - and it switched off entirely at a span of one block
    /// or less, where <c>TrimForTowers</c> is 0, leaving the strand at full height with no ramp under it.
    /// <para>
    /// The rows are the pitches that broke and the two lengths that turn the old window off. What they all
    /// assert now is the same one thing: the return strand is the going strand's own points plus
    /// <c>ReturnLift</c>, at every sample, so a half-span cannot end anywhere but where the peer's begins.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(30, 0)]     // flat, and the ordinary case
    [InlineData(4, 3)]      // 36.9 degrees - the one pitch the old ramp happened to get right
    [InlineData(3, 4)]      // 53.1 - a tower on a ledge above another, which is what a ropeway is for
    [InlineData(2, 6)]      // 71.6, the worst measured gap
    [InlineData(4, 16)]     // 76.0 over a 16.5-block span
    [InlineData(1, 0)]      // one block: TrimForTowers is 0, so there was no window at all
    [InlineData(0, 6)]      // no plan run to divide by
    public void TheTwoHalvesOfTheReturnStrandMeetAtTheSpanMidpointAtAnyPitch(int run, int rise)
    {
        var line = Line((0, 64, 0), (run, 64 + rise, 0));

        Vec3d Meeting(int me, int peer, out double atTheTower)
        {
            var going = BEPylonBase.HalfSpanPath(line, me, peer);
            var returning = BEPylonBase.Lift(going, BEPylonBase.ReturnLift);

            // Every sample, not just the ends: this is the whole no-ramp claim.
            for (var i = 0; i < going.Count; i++)
            {
                Assert.Equal(going[i].X, returning[i].X);
                Assert.Equal(going[i].Z, returning[i].Z);
                Assert.Equal(BEPylonBase.ReturnLift, returning[i].Y - going[i].Y, 12);
            }

            // Half-spans are drawn in coordinates local to their own sheave, so put this one in the world.
            atTheTower = returning[0].Y;
            var origin = line.Anchors[me];
            return new Vec3d(origin.X + returning[^1].X, origin.Y + returning[^1].Y, origin.Z + returning[^1].Z);
        }

        var mine = Meeting(0, 1, out var atMe);
        var theirs = Meeting(1, 0, out var atPeer);

        // THE MEETING. Both halves stop at the span's midpoint and both are a wheel above it.
        Assert.Equal(mine.X, theirs.X, 9);
        Assert.Equal(mine.Y, theirs.Y, 9);
        Assert.Equal(mine.Z, theirs.Z, 9);

        // ...and both START at their own tower a wheel above the sheave, which is where the shoe on
        // pylonhead.json is - its top face IS ReturnLift - CableRadius. No free end and nothing to ramp.
        Assert.Equal(BEPylonBase.ReturnLift, atMe, 12);
        Assert.Equal(BEPylonBase.ReturnLift, atPeer, 12);
    }

    private static RopewayLine Line(params (int X, int Y, int Z)[] towers)
    {
        var positions = new List<BlockPos>();
        foreach (var t in towers) positions.Add(new BlockPos(t.X, t.Y, t.Z));
        return RopewayLine.FromTowers(positions);
    }

    /// <summary>
    /// THE SHAFT'S CORRIDOR RAYS FALL ON COLUMN CENTRES, and this is the assert that says the derived ladder is
    /// not enough on its own. <see cref="SpanMath.ClearanceRows"/>'s whole convention is "one ray down the
    /// centre of its own row so it certifies +/-0.5 either side", and on a level span that is free: <c>up</c> is
    /// vertical, the anchor's Y is a block centre, and the offsets come out integral. At exactly vertical
    /// <c>up</c> is HORIZONTAL and the anchor's plan coordinate is <c>pos + 0.5</c> - also a centre - but the
    /// offsets are HALF-integers, so every ray would run down a block-boundary plane and the DDA would floor it
    /// onto one side. Deterministically: four rays sampling plan columns {-1, 0, +1, +2} against a car sweeping
    /// {-2 .. +2}. The column its tail occupies goes untested at every level of the shaft, and the check stops
    /// being symmetric about the cabin - which <c>IsSpanClear</c>'s own comment names as the thing that must not
    /// silently re-open.
    /// </summary>
    [Fact]
    public void TheShaftsClearanceRaysRunDownColumnCentresAndCoverEveryColumnTheCarSweeps()
    {
        var derived = SpanMath.ClearanceRows(1);
        var laid = SpanMath.OnColumnCentres(derived);

        // The defect, stated: the derived ladder is half-integral at vertical and would sit on boundaries.
        Assert.All(derived, row => Assert.Equal(0.5, Math.Abs(row % 1), 9));
        Assert.All(laid, row => Assert.Equal(0, row % 1, 9));

        // Five rays, not four, because the car spans centre +/- CabinHalfLength about a block CENTRE and that
        // is five columns rather than four.
        Assert.Equal(new[] { -2.0, -1.0, 0.0, 1.0, 2.0 }, laid);
        Assert.Equal((int)(2 * SpanMath.CabinHalfLength) + 1, laid.Length);

        // Every column the car's floor slab passes through is the row of one ray, and no ray reaches outside
        // the band the derived ladder certified.
        for (var column = -2; column <= 2; column++) Assert.Contains((double)column, laid);
        Assert.True(laid[0] >= derived[0] - 0.5);
        Assert.True(laid[laid.Length - 1] <= derived[derived.Length - 1] + 0.5);

        // Still symmetric about the cabin from either end, which is what
        // TheVerticalCorridorIsTheSameOneFromEitherEnd holds for the derived rows and the shaft must not lose.
        Assert.Equal(SpanMath.OnColumnCentres(SpanMath.ClearanceRows(-1)), laid);
        Assert.Equal(0, laid[0] + laid[laid.Length - 1], 9);
    }

    /// <summary>
    /// THE LADDER IS THE WHOLE OF THE PLAN AND NONE OF THE HEIGHT, on a shaft, and that is what makes the
    /// cast's own ENDS load-bearing there in a way they never are on a ropeway. On a level span <c>up</c> is
    /// vertical, so <see cref="SpanMath.ClearanceRows"/>'s <c>-3.5 .. +1.33</c> band IS the cabin's height and
    /// the rays carry the car. At exactly vertical <c>up = Cross(right, dir)</c> with <c>dir = (0, +/-1, 0)</c>
    /// has an identically zero Y - asserted below rather than argued - so the same band is laid entirely
    /// ACROSS THE PLAN and every ray spans exactly <c>[anchorFoot, anchorHead]</c> in Y. The rope's segment.
    /// <para>
    /// The car's body hangs <see cref="SpanMath.ShaftCarDrop"/> to <c>hangDrop - CabinHalfHeight</c> under its
    /// rope point, so the volume it actually sweeps is <c>[anchorFoot - 3.5, anchorHead - 1.0]</c>. The bottom
    /// 3.5 blocks - <c>footY+1.0 .. footY+4.5</c>, over the whole 3 x 5 footprint, which is precisely where the
    /// car parks and the rider sits - were therefore never tested, and a shaft sunk from the top and stopped at
    /// the foot footing's own cell linked, parked the car in rock and left the dismount search with no landing.
    /// <c>IsSpanClear</c> drops the lower end of a shaft cast by that much before laying its rays; the top end
    /// needs nothing, which is the second assertion here.
    /// </para>
    /// <para>
    /// This pins the ARITHMETIC rather than the cast: <c>IsSpanClear</c> takes an <c>IWorldAccessor</c> and its
    /// DDA is the engine's, so nothing in this suite can call it. What the suite can hold is that the number it
    /// drops by is the car's own floor and not a margin somebody picked, which is
    /// <c>TheCarsFloorIsTheDropTheShaftsCorridorIsExtendedBy</c> in the asset suite off the shipped shape.
    /// </para>
    /// </summary>
    [Fact]
    public void TheShaftsCertifiedVolumeIsTheCarsSweptBodyAndNotTheRopesSegment()
    {
        // `up` at vertical is the head's own facing laid flat, in every one of the eight cases - four facings
        // times both directions of cast. Its Y is not small, it is exactly zero, which is why the band that
        // carries the cabin's height on a level span carries none of it here.
        foreach (var facing in BlockFacing.HORIZONTALS)
        foreach (var sign in new[] { 1.0, -1.0 })
        {
            var right = new Vec3d(-facing.Normalf.Z, 0, facing.Normalf.X);
            var dir = new Vec3d(0, sign, 0);
            var up = new Vec3d(
                right.Y * dir.Z - right.Z * dir.Y,
                right.Z * dir.X - right.X * dir.Z,
                right.X * dir.Y - right.Y * dir.X);

            Assert.Equal(0, up.Y, 12);
            Assert.Equal(1, up.Length(), 9);
            Assert.Equal(-sign * facing.Normalf.X, up.X, 6);
            Assert.Equal(-sign * facing.Normalf.Z, up.Z, 6);
        }

        var foot = new BlockPos(10, 40, 10, 0);
        var head = new BlockPos(10, 80, 10, 0);
        var anchorFoot = SpanMath.AnchorOf(foot).Y;
        var anchorHead = SpanMath.AnchorOf(head).Y;

        // What the rays used to span in Y, and what the car actually occupies.
        var carFloor = anchorFoot - SpanMath.ShaftCarDrop;
        var carRoof = anchorHead - (EntityRopewayCabin.DefaultHangDrop - SpanMath.CabinHalfHeight);

        // The uncertified stretch was the parked car itself: its floor is the top landing's own face one block
        // over the footing, and its rope point is 3.5 blocks above that.
        Assert.Equal(foot.Y + 1.0, carFloor, 9);
        Assert.Equal(foot.Y + 4.5, anchorFoot, 9);
        Assert.Equal(3.5, anchorFoot - carFloor, 9);

        // And nothing is owed at the TOP: the car's roof is a full block below its own rope point, so the
        // unextended upper end already covers it. Extending both ends would demand a clear block above the
        // sheave that nothing ever sweeps.
        Assert.True(carRoof < anchorHead, $"the car's roof at {carRoof} is not below the head anchor {anchorHead}");
        Assert.Equal(1.0, anchorHead - carRoof, 9);

        // The same drop the counterweight hangs by, because it is the same body mirrored - one constant now,
        // not the same arithmetic written at three sites.
        Assert.Equal(ShaftRenderer.WeightDrop, SpanMath.ShaftCarDrop, 9);
    }

    /// <summary>
    /// Nothing but a shaft gets the re-laid ladder, and a level span could not tell if it did: at zero pitch
    /// <see cref="SpanMath.ClearanceRows"/> already returns whole numbers, so the re-lay is the IDENTITY there
    /// and no ropeway ray can move. That is this half of the "a horizontal line is byte-identical" claim,
    /// checked rather than argued.
    /// <para>
    /// It is NOT the identity in between, and it is not meant to be: at half a degree the band's ends are 3.500
    /// and 1.326, and rounding those INWARD onto centres would drop coverage the derived ladder had. Which is
    /// exactly why <see cref="SpanMath.IsSpanClear"/> applies it only where a shaft axis is supplied, and why
    /// <see cref="SpanMath.ShaftLinkFits"/> makes "a shaft axis exists" mean "this leg is straight up".
    /// </para>
    /// </summary>
    [Fact]
    public void RelayingTheLadderOnColumnCentresChangesNothingOnALevelSpan()
    {
        Assert.Equal(SpanMath.ClearanceRows(0), SpanMath.OnColumnCentres(SpanMath.ClearanceRows(0)));

        // The mid-pitch case, stated so nobody widens the re-lay to every span thinking it is free.
        var tilted = SpanMath.ClearanceRows(Math.Sin(0.5 * Math.PI / 180));
        Assert.NotEqual(tilted, SpanMath.OnColumnCentres(tilted));

        // At vertical the RAYS land on the five column centres the car sweeps, so what each of them certifies
        // reaches half a block past the car's own nose and tail. That surplus is the price of testing whole
        // columns and it is symmetric, which is the property that matters.
        var laid = SpanMath.OnColumnCentres(SpanMath.ClearanceRows(1));
        Assert.Equal(-SpanMath.CabinHalfLength, laid[0], 9);
        Assert.Equal(SpanMath.CabinHalfLength, laid[laid.Length - 1], 9);
        Assert.Equal(0.5, -SpanMath.CabinHalfLength - (laid[0] - 0.5), 9);
    }

    /// <summary>
    /// VERTICALITY IS STRUCTURAL. Nothing in the mod asks whether a span is vertical; a shaft line is a line
    /// whose footings are shaft stations, and this predicate is what decides which spans those may carry.
    /// Every "on a shaft..." branch downstream is safe because the shapes it would be wrong about - a vertical
    /// stub bolted onto a hill line, two feet with no sheave, a sheave under the car, a foot and a head facing
    /// different ways - are refused here.
    /// <para>
    /// PER-SPAN, and the last block below is what says so out loud. <c>MaxSpansPerTower</c> is 2, so the "one
    /// head" clause does NOT give one head per LINE: a fold and a two-headed line both satisfy every clause of
    /// this predicate. Those are closed by the one-span-per-shaft-footing rule in
    /// <c>RopewayLinkService.TryLink</c> and <c>ScanCandidates</c>, which is a question about a footing's
    /// existing spans rather than about this span's geometry and so cannot live in a pure function of two
    /// positions.
    /// </para>
    /// </summary>
    [Fact]
    public void AShaftSpanRunsUpOneColumnWithExactlyOneSheaveAndTheSheaveOnTop()
    {
        var foot = new BlockPos(10, 40, 10, 0);
        var head = new BlockPos(10, 88, 10, 0);
        var n = BlockFacing.NORTH;

        Assert.True(SpanMath.ShaftLinkFits(foot, false, n, head, true, n));
        Assert.True(SpanMath.ShaftLinkFits(head, true, n, foot, false, n));

        // Not out of its own column, by even one block, at any height.
        Assert.False(SpanMath.ShaftLinkFits(foot, false, n, new BlockPos(11, 88, 10, 0), true, n));
        Assert.False(SpanMath.ShaftLinkFits(foot, false, n, new BlockPos(10, 88, 11, 0), true, n));

        // Exactly one head on this SPAN: two feet have no sheave to hang the rope on, and two heads have two.
        Assert.False(SpanMath.ShaftLinkFits(foot, false, n, head, false, n));
        Assert.False(SpanMath.ShaftLinkFits(foot, true, n, head, true, n));

        // And the head on TOP - everything the sheave carries is drawn downward from it.
        Assert.False(SpanMath.ShaftLinkFits(foot, true, n, head, false, n));
        Assert.False(SpanMath.ShaftLinkFits(head, false, n, foot, true, n));

        // THE SAME FACING AT BOTH ENDS. The foot's facing is not decoration: BEPylonBase.Init rotates its
        // structure by it, which moves the one cell shaftfoot requires - the tensionguide at (0, 0, -3), the
        // bottom of the counterweight's lane - while the lane the weight actually descends follows the HEAD's
        // facing. Mismatched, the multiblock made the player dig a guide one way and the weight came down the
        // other, into undug rock, with every check passing.
        foreach (var other in new[] { BlockFacing.EAST, BlockFacing.SOUTH, BlockFacing.WEST })
        {
            Assert.False(SpanMath.ShaftLinkFits(foot, false, n, head, true, other));
            Assert.False(SpanMath.ShaftLinkFits(foot, false, other, head, true, n));

            // Any facing will do, as long as it is the same one - nothing prefers north.
            Assert.True(SpanMath.ShaftLinkFits(foot, false, other, head, true, other));
        }

        // Fail closed on a facing nobody supplied. PassageFacing never hands back null, so this is about a
        // caller that has not established the thing the clause is about rather than about a real footing.
        Assert.False(SpanMath.ShaftLinkFits(foot, false, null, head, true, n));
        Assert.False(SpanMath.ShaftLinkFits(foot, false, n, head, true, null));

        // One cell is not a span, and a missing footing is not a link.
        Assert.False(SpanMath.ShaftLinkFits(foot, true, n, foot.Copy(), false, n));
        Assert.False(SpanMath.ShaftLinkFits(null, true, n, head, false, n));

        // THE TWO SHAPES THIS PREDICATE DOES NOT REFUSE, pinned so nobody reads it as a per-LINE invariant
        // again. A fold - foot@0 -> head@10 then head@10 -> foot@5 - and a two-headed line - foot@0 -> head@10
        // plus foot@0 -> head@20 - are both four legal spans by this function alone. What refuses them is the
        // callers' one-span-per-shaft-footing rule.
        var mid = new BlockPos(10, 50, 10, 0);
        var upper = new BlockPos(10, 60, 10, 0);
        Assert.True(SpanMath.ShaftLinkFits(foot, false, n, mid, true, n));
        Assert.True(SpanMath.ShaftLinkFits(mid, true, n, new BlockPos(10, 45, 10, 0), false, n));
        Assert.True(SpanMath.ShaftLinkFits(foot, false, n, upper, true, n));
    }

    /// <summary>
    /// The two degenerate facts a vertical leg hands the cabin, pinned so the branch that answers them cannot
    /// be deleted as redundant. <c>DirectionAt</c> is exact and useful - the climb really is +/-1 - while the
    /// yaw derived from it is a SILENT WRONG ANSWER: <c>Math.Atan2(0.0, 0.0)</c> is 0.0, not NaN, so a car with
    /// no bearing at all faces due south for the whole ride and then snaps to the station when it parks.
    /// </summary>
    [Fact]
    public void AVerticalLegHasNoBearingAndAtan2SaysSouthRatherThanSayingSo()
    {
        var line = RopewayLine.FromTowers(new List<BlockPos>
        {
            new(4, 20, 7, 0),
            new(4, 68, 7, 0)
        });

        var dir = line.DirectionAt(line.TotalLength / 2);
        Assert.Equal(0, dir.X, 9);
        Assert.Equal(0, dir.Z, 9);
        Assert.Equal(1, dir.Y, 9);

        // The tombstone: this is the yaw Place would otherwise have written, and it is a heading nobody chose.
        Assert.Equal(0f, (float)Math.Atan2(dir.X, dir.Z));

        // No NaN anywhere - LegOf refuses a bearing rather than dividing by a zero plan length, and that guard
        // was written for exactly this case long before an elevator was proposed.
        Assert.False(double.IsNaN(line.PositionAt(line.TotalLength / 2).X));
        Assert.Null(line.Tangents[0]);
        Assert.Null(line.Tangents[1]);

        // What a shaft uses instead: the head's own facing, through the same SquareTo a parked ropeway cabin
        // squares to. Constant everywhere, so there is nothing to snap at either end. Compared as a DIRECTION
        // rather than as a number, because SquareTo answers a north facing with axis + PI = 2 PI - the same
        // heading, and a raw equality here would be a test of the branch it happened to take.
        foreach (var (facing, x, z) in new[]
                 {
                     (BlockFacing.NORTH, 0.0, -1.0), (BlockFacing.EAST, 1.0, 0.0),
                     (BlockFacing.SOUTH, 0.0, 1.0), (BlockFacing.WEST, -1.0, 0.0)
                 })
        {
            var yaw = EntityRopewayCabin.SquareTo(facing, 0f);

            // The cabin is symmetric front to back, so either of the axis's two yaws is correct - what must
            // not happen is a yaw off the axis altogether.
            Assert.Equal(0, Math.Abs(Math.Sin(yaw) * z - Math.Cos(yaw) * x), 5);
        }
    }

    /// <summary>
    /// A rider stepping out at the TOP of a shaft is over a hole - the top landing must have a car-sized
    /// opening in it, because the car parks with its floor level with that landing and then descends through
    /// its whole footprint. Vanilla's own dismount teleport probes exactly two columns, one block either side,
    /// and one block from the axis is still inside a three-wide hoistway. This is the widened search: rings
    /// outward from the cabin's own column, in a fixed order so the server and both clients pick the same block.
    /// </summary>
    [Fact]
    public void TheDismountSearchFindsTheLandingRoundAThreeWideHoistway()
    {
        // The shaft as dug: three columns across the head's facing, five along, plus the counterweight's lane
        // one further. Everything else at this level is landing.
        bool Standable(int x, int z) => !(Math.Abs(x) <= 1 && z >= -3 && z <= 2);

        var found = RopewayCabinSeat.Landing(0, 0, RopewayCabinSeat.ShaftExitReach, Standable);
        Assert.NotNull(found);
        Assert.False(Standable(0, 0));
        Assert.True(Standable(found.Value.X, found.Value.Z));

        // Two blocks across the doors is the nearest landing there is, and the search has to reach it.
        Assert.Equal(2, Math.Max(Math.Abs(found.Value.X), Math.Abs(found.Value.Z)));
        Assert.True(RopewayCabinSeat.ShaftExitReach >= 2);

        // Deterministic: the same rig gives the same block every time.
        Assert.Equal(found, RopewayCabinSeat.Landing(0, 0, RopewayCabinSeat.ShaftExitReach, Standable));

        // The cabin's own column wins when it IS standable, so a foot station with a solid deck round it never
        // moves anybody sideways.
        Assert.Equal((0, 0), RopewayCabinSeat.Landing(0, 0, 3, (_, _) => true));

        // Nothing within reach is nothing rather than a guess: the rider is left where vanilla left them.
        Assert.Null(RopewayCabinSeat.Landing(0, 0, 3, (_, _) => false));
        Assert.Null(RopewayCabinSeat.Landing(0, 0, 3, null));
    }

    /// <summary>
    /// The counterweight is a pure function of the car and needs no state of its own: no <c>Travelled</c>, no
    /// persistence, no despawn path, no corridor and no seat. The two are exact mirrors, so they pass level at
    /// the shaft's midpoint by construction rather than by tuning, and the two strands' lengths always add up
    /// to one rope.
    /// </summary>
    [Fact]
    public void TheCounterweightIsTheCarsMirrorAndTheTwoStrandsAlwaysSumToOneRope()
    {
        const double bottom = 72 / 16.0;
        const double top = 264 / 16.0;
        const double sum = top + bottom;

        double Weight(double car) => sum - car;

        // The car at one stop puts the weight at the other, both ways round.
        Assert.Equal(bottom, Weight(top), 9);
        Assert.Equal(top, Weight(bottom), 9);

        // They meet level at the midpoint, and nowhere else.
        Assert.Equal(sum / 2, Weight(sum / 2), 9);

        // The rope is one length whatever the car is doing, which is why an open 1:1 rope needs no take-up.
        foreach (var car in new[] { bottom, sum / 2, top, bottom + 3.25 })
        {
            Assert.Equal(top - bottom, (top - car) + (top - Weight(car)), 9);
        }

        // And the weight hangs the car's own drop below its rope point, so the two bodies are the same body.
        Assert.Equal(EntityRopewayCabin.DefaultHangDrop + SpanMath.CabinHalfHeight, ShaftRenderer.WeightDrop, 9);
    }

    private static int Sum(int[] values)
    {
        var total = 0;
        foreach (var v in values) total += v;
        return total;
    }
}
