using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>Pure span arithmetic. No engine state, so it is the one thing that is unit-tested.</summary>
public static class SpanMath
{
    /// <summary>
    /// Half-width of the corridor the cabin needs, in blocks. 1 gives the 3-wide passage the tower's
    /// posts leave open.
    /// </summary>
    public const int ClearanceRadius = 1;

    /// <summary>
    /// Rows below the rope line that must also be clear ON A LEVEL SPAN. The cabin hangs 2.25 blocks under the
    /// rope and its body runs anchor-3.5..anchor (jaw closed on the rope), so certifying only the rope line lets
    /// a rise two blocks under the rope drag a seated rider through solid stone - riders have no block collision
    /// to stop it.
    /// <para>
    /// 3 rows covers it exactly, and only just. The anchor is a block centre, so the bottom ray runs down the
    /// centre of the row spanning anchor-3.5..anchor-2.5 - the cabin's floor lands on that row's bottom face.
    /// </para>
    /// <para>
    /// NO LONGER THE LOOP BOUND, and that is the fix rather than a tidy-up: <see cref="IsSpanClear"/> now lays
    /// its ladder on <see cref="ClearanceRows"/>, which is this pair of numbers at zero pitch and something else at
    /// every other pitch. Held fixed, they were a level-line assumption - the cabin hangs PLUMB and stays LEVEL
    /// while <c>up</c> leans back with the pitch, so at 30 degrees the cabin's floor reaches 4.031 blocks below
    /// the path against the 3.5 these certify, and 0.531 blocks of ground under a seated rider went unchecked.
    /// They stay as the level case the derivation has to reproduce, which
    /// <c>RopewayMathTests.ClearanceCoversTheCabinBodyAndNotJustTheRopeLine</c> is what pins.
    /// </para>
    /// </summary>
    public const int ClearanceBelow = 3;

    /// <summary>
    /// Rows ABOVE the rope line that must also be clear ON A LEVEL SPAN, for the return strand. The haul rope is
    /// a loop with two strands stacked <c>BEPylonBase.ReturnLift</c> = 1.3263 blocks apart, so the upper one
    /// occupies 1.2663 to 1.3863 above the anchor - and the anchor is a block centre, so the top row spans
    /// exactly 0.5 to 1.5.
    /// <para>
    /// ONE row covers it exactly and with room at both ends: 0.7663 blocks of that row below the strand and
    /// 0.1137 above it. Two "for margin" would refuse spans over nothing, because 2*rho lands in one row and
    /// the arithmetic says which. Rays per span 12 -> 15. See <see cref="ClearanceBelow"/> for why this is the
    /// level case rather than the loop bound.
    /// </para>
    /// </summary>
    public const int ClearanceAbove = 1;

    /// <summary>
    /// Half the cabin's length along its own direction of travel, in blocks, read off the roof slab
    /// (<c>shapes/entity/cabin.json</c>, x -32..32). It is here rather than only in the shape because the
    /// corridor and the tower fit both turn on it, and
    /// <c>RopewayAssetContractTests.TheCabinIsBuiltAlongTheTravelAxis</c> pins the two together.
    /// </summary>
    public const double CabinHalfLength = 2.0;

    /// <summary>
    /// Half the cabin's height, in blocks: roof top +1.25, floor bottom -1.25 about its own origin. Same shape,
    /// same reason as <see cref="CabinHalfLength"/>.
    /// </summary>
    public const double CabinHalfHeight = 1.25;

    /// <summary>
    /// The steepest span a tower can pass the cabin through, as a TANGENT - rise over horizontal run. Above it
    /// the cabin's roof drives through the crossarm cells on the way out of a tower and its floor through the
    /// footing's plinth on the way down out of one.
    /// <para>
    /// DERIVED, NOT CHOSEN, and it is the whole of what the tower has to give. The archway is 3.5 blocks tall -
    /// plinth top at anchor-4.0, crossarm cells' underside at anchor-0.5 - the cabin is 2.5 tall and hangs
    /// centred in it, so there is exactly 0.5 of slack over the roof and 0.5 under the floor. The cabin hangs
    /// plumb and stays LEVEL (<c>EntityRopewayCabin.Place</c> writes yaw and nothing else), so on a climbing
    /// span its roof rises with the rope while the crossarm does not, and it still overlaps the one-cell-deep
    /// crossarm row until it is <see cref="CabinHalfLength"/> + 0.5 = 2.5 blocks of plan past the tower:
    /// <c>0.5 / 2.5 = 0.2</c>, i.e. 11.31 degrees. The floor mirrors it on the descending side at 0.5 / 2.4375
    /// = 11.59 degrees, so this is the binding one.
    /// </para>
    /// <para>
    /// NOTHING IN THE CABIN CAN RAISE IT. Trading roof height for floor height moves both limits at once and the
    /// best split is WORSE (9.6 degrees), because the drawn station rail hangs 0.75 under the rope, follows the
    /// rope's pitch, and is already only 0.25 over the roof - it grazes the roof's tail from tan 0.125 (7.13
    /// degrees), which is 2.2 units of decoration rather than the tower's own blocks and is why this number is
    /// the crossarm's and not the rail's. Shortening the cabin to 3 blocks buys 14.0 degrees; thinning both
    /// slabs buys 14.4. The only lever with real travel in it is the archway, i.e.
    /// <see cref="SheaveHeight"/> and <c>hangDrop</c> together: each extra cell of tower adds 0.5 of slack per
    /// side and 0.2 to this tangent (5 -> 21.8 degrees, 6 -> 31.0, 8 -> 45.0). That is a multiblock change and
    /// it is not this one. <c>RopewayAssetContractTests.TheCabinFitsThroughTheTowerAtEveryPitch</c> re-derives
    /// every number in this comment off the shipped shapes and fails if any of them moves.
    /// </para>
    /// </summary>
    public const double PassablePitchTan = 0.2;

    /// <summary>
    /// Length of each end of a span that the tower's own structure occupies and that is therefore not
    /// checked. The posts are player-chosen logs and planks, so <see cref="RopewayBlockFilter"/> cannot
    /// tell them from terrain; without this the <see cref="ClearanceBelow"/> rays leave the sheave, drop
    /// three blocks and run straight into the tower's own posts, and every span is silently refused.
    /// The posts stand at x = +/-3, y = 0..3 above the footing; their far corners sit
    /// sqrt(3.5^2 + 0.5^2) = 3.54 blocks horizontally from the sheave column, so 4 still clears them on any
    /// bearing - but the margin is 0.46 now, not 1.45. Widening the passage again needs this raised too.
    /// </summary>
    public const double TowerClearance = 4.0;

    /// <summary>
    /// Cells from the ground-placed controller up to the sheave block at the top of its crossarm -
    /// <c>ropeway:pylonhead</c> on a plain tower, <c>ropeway:bullwheel</c> on a station. The one number that
    /// turns a tower's canonical position into its geometry, which is why it lives next to
    /// <see cref="AnchorOf"/>. It is the same for all three footings because all three carry one offset
    /// list; <c>RopewayAssetContractTests.AllThreeFootingsShareOneCellList</c> is what pins that.
    /// <para>
    /// Forced by the cabin, not chosen: the cabin body runs 1.25 below its origin to 1.25 above it, the
    /// origin hangs <c>hangDrop</c> = 2.25 below the sheave, and the footing occupies the ground cell the
    /// cabin passes over. Floor above the footing needs <c>SheaveHeight + 0.5 - 2.25 - 1.25 &gt; 0.5</c>
    /// and roof under the crossarm needs <c>SheaveHeight + 0.5 - 2.25 + 1.25 &lt; SheaveHeight</c>;
    /// together those admit only 4. See <c>RopewayAssetContractTests.TheCabinFitsThroughTheTower</c>.
    /// </para>
    /// </summary>
    public const int SheaveHeight = 4;

    private const double Epsilon = 1e-6;

    /// <summary>Haul rope charged for a span of the given length. Always rounds up.</summary>
    public static int RopeCost(double span, double ropePerBlock = 1.0)
    {
        if (double.IsNaN(span) || span <= 0 || ropePerBlock <= 0) return 0;
        return (int)Math.Ceiling(span * ropePerBlock - Epsilon);
    }

    /// <summary>Haul rope handed back when a span is removed. Always rounds down, so a span never pays for itself.</summary>
    public static int RopeRefund(double span, double ropePerBlock = 1.0)
    {
        if (double.IsNaN(span) || span <= 0 || ropePerBlock <= 0) return 0;
        return (int)Math.Floor(span * ropePerBlock + Epsilon);
    }

    /// <summary>
    /// How much to cut off each end of a span before checking it, so the tower structures at the ends do
    /// not block their own line. Never more than half, so a very short span keeps at least a token check.
    /// </summary>
    public static double TrimForTowers(double length)
    {
        var trim = Math.Min(TowerClearance, (length - 1) / 2);
        return trim > 0 ? trim : 0;
    }

    /// <summary>Centre of a block. Dimension-encoded Y, the same space Vec3d world positions and raycasts use.</summary>
    public static Vec3d CentreOf(BlockPos pos)
    {
        return pos == null ? null : new Vec3d(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5);
    }

    /// <summary>
    /// Where the haul rope actually runs for a tower whose canonical position is its ground-placed footing:
    /// the centre of the sheave block, <see cref="SheaveHeight"/> cells up. Every spatial consumer -
    /// clearance sweeps, span length, rope cost, picker distances, the drawn cable and the cabin's own
    /// position - goes through here, so the offset belongs in this one function and nowhere else. A
    /// per-caller offset is how the cable and the cabin end up at different heights.
    /// </summary>
    public static Vec3d AnchorOf(BlockPos pos)
    {
        var centre = CentreOf(pos);
        if (centre != null) centre.Y += SheaveHeight;
        return centre;
    }

    /// <summary>
    /// The pitch-about-X and yaw-about-Y that aim a mesh's local +Z straight down a span of the given delta,
    /// applied in that order. Pure and therefore unit-tested: a flipped sign or a swapped order gives cables
    /// pointing off into the sky, which compiles, renders, and is only visible by standing in the world.
    /// </summary>
    public static void CableAngles(double dx, double dy, double dz, out float radX, out float radY)
    {
        var horizontal = Math.Sqrt(dx * dx + dz * dz);
        radX = -(float)Math.Atan2(dy, horizontal);
        radY = (float)Math.Atan2(dx, dz);
    }

    /// <summary>
    /// How steeply a span climbs, as a TANGENT - rise over horizontal run, unsigned, because a span is ridden
    /// both ways. <see cref="double.PositiveInfinity"/> for a purely vertical span, which is buildable. Pure.
    /// </summary>
    public static double PitchTan(Vec3d from, Vec3d to)
    {
        if (from == null || to == null) return 0;

        var plan = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Z - from.Z) * (to.Z - from.Z));
        var rise = Math.Abs(to.Y - from.Y);
        if (plan >= Epsilon) return rise / plan;
        return rise < Epsilon ? 0 : double.PositiveInfinity;
    }

    /// <summary>
    /// The rows <see cref="IsSpanClear"/> casts along, as offsets on its own <c>up</c> axis in blocks off the
    /// rope line: one block apart, covering the band the cabin and the return strand sweep out on a span of the
    /// given pitch (<paramref name="pitchSin"/> is the unit direction's Y). Pure, and the reason the sweep is
    /// not a fixed ladder.
    /// <para>
    /// <c>up</c> is the PATH's vertical - perpendicular to the chord - so it leans back by the pitch, while the
    /// cabin hangs plumb and stays level. The cabin's own length therefore projects onto <c>up</c>:
    /// <c>hangDrop +/- CabinHalfHeight</c> vertically becomes <c>(hangDrop +/- 1.25)*cos</c>, and each end of
    /// the 4-block body reaches a further <c>2.00*sin</c> - down at the tail, up at the nose. The return strand
    /// sits <c>ReturnLift</c> straight up from the rope, so it projects as <c>ReturnLift*cos</c> and collapses
    /// onto the rope line as the span goes vertical.
    /// </para>
    /// <para>
    /// The fixed <c>[-ClearanceBelow-0.5, +ClearanceAbove+0.5] = [-3.5, +1.5]</c> this replaced was exactly
    /// right at zero pitch and wrong at every other: worst under the cabin at 29.74 degrees, where the floor
    /// reaches 4.031 below the path and 0.531 blocks of ground under a seated rider went uncertified; worst over
    /// its nose at 89, where it reaches 1.977 against 1.5. Both are the same mistake and this is the one place
    /// to fix it. At zero pitch this returns [-3.5, +1.3263] and the ladder built from it IS the old one.
    /// </para>
    /// </summary>
    public static double[] ClearanceRows(double pitchSin)
    {
        var sin = Math.Min(1, Math.Abs(pitchSin));
        var cos = Math.Sqrt(Math.Max(0, 1 - sin * sin));

        var low = -(CabinHalfLength * sin + (EntityRopewayCabin.DefaultHangDrop + CabinHalfHeight) * cos);
        var high = Math.Max(
            CabinHalfLength * sin - (EntityRopewayCabin.DefaultHangDrop - CabinHalfHeight) * cos,
            BEPylonBase.ReturnLift * cos);

        // One block per row, each ray down the centre of its own row so it certifies +/-0.5 either side.
        // Ceiling, so the ladder always covers the band and never leaves a sliver of it between two rays; the
        // surplus goes at the top, over the strand, where the rope has just come from, rather than under the
        // floor. The Epsilon keeps a band that is an exact multiple of a block from buying a row it does not
        // need - at zero pitch it is 4.8263 and rounds to the 5 rows that shipped.
        var rows = Math.Max(1, (int)Math.Ceiling(high - low - Epsilon));
        var offsets = new double[rows];
        for (var i = 0; i < rows; i++) offsets[i] = low + 0.5 + i;
        return offsets;
    }

    /// <summary>
    /// Lang key for the eight-point compass bearing from one tower to another. This is what an unnamed tower
    /// is called, so it has to be a bearing a player can act on rather than a placeholder. Returns whole lang
    /// keys rather than a bare code so the shipped-lang-key test can see every one of them. Pure.
    /// </summary>
    public static string CompassKey(double dx, double dz)
    {
        // Two towers on the same column have no bearing, and -0.0 would otherwise send atan2 due south.
        if (dx == 0 && dz == 0) return "ropeway:dir-n";

        // -dz because north is -Z, and atan2(east, north) puts 0 at north and grows clockwise.
        var octant = ((int)Math.Round(Math.Atan2(dx, -dz) / (Math.PI / 4)) + 8) % 8;
        return octant switch
        {
            0 => "ropeway:dir-n",
            1 => "ropeway:dir-ne",
            2 => "ropeway:dir-e",
            3 => "ropeway:dir-se",
            4 => "ropeway:dir-s",
            5 => "ropeway:dir-sw",
            6 => "ropeway:dir-w",
            _ => "ropeway:dir-nw"
        };
    }

    /// <summary>
    /// The widest error between a corner tower's crossarm and the corner's own bisector that still lets the
    /// cabin through without clipping a post, in degrees, for a corner that deflects the line by
    /// <paramref name="turnDeg"/>. Pure.
    /// <para>
    /// A FIT TO THREE MEASURED POINTS, not a derivation, and it is stated that way on purpose. The rig in
    /// <c>docs/agentic/ingest/cablecar/TURNING-SPEC.md</c> §2.5 swept the tower facing at each corner angle
    /// and found the widest error keeping penetration under the cabin's own 0.0625-block wall thickness:
    /// <b>+/-1.0 degree at a 90 degree turn, +/-23.2 at 45, +/-30.8 at 30</b>. Half the shortfall from a right
    /// angle reproduces all three to within a degree (0 / 22.5 / 30) and errs on the warning side, which is
    /// the right side for something that only ever prints a chat line. Past 90 degrees it is zero: a corner
    /// that sharp is dirty at every facing, which the acceptance test's hairpin row measures.
    /// </para>
    /// <para>
    /// This is NOT a closed form waiting to be found. The cabin fits the passage at any yaw - its 2.463-block
    /// half-diagonal against post inner faces at 2.5 - so what a facing error costs is not the rotation but
    /// where the cabin's ORIGIN is, twenty blocks out, which no local geometry knows.
    /// </para>
    /// </summary>
    public static double CornerTolerance(double turnDeg)
    {
        return Math.Max(0, (90 - turnDeg) / 2);
    }

    /// <summary>
    /// How far a bearing is off a facing's AXIS rather than off the facing itself, in degrees, folded into
    /// [0, 90]. The passage runs through the tower both ways and the cabin is symmetric front to back, so a
    /// north-facing crossarm and a south-facing one are the same crossarm - measuring against the facing
    /// would report 180 degrees of error for a tower that is exactly right. Pure.
    /// </summary>
    public static double AxisError(double bearingRad, BlockFacing axis)
    {
        if (axis == null) return 90;

        // Folded modulo PI rather than through GameMath.AngleRadDistance, which is float: a due-north axis is
        // atan2(0, -1) = PI exactly in double and 1.4e-5 degrees off it in float, and this is compared against
        // a tolerance that is legitimately zero at a right angle.
        var off = (bearingRad - Math.Atan2(axis.Normalf.X, axis.Normalf.Z)) % Math.PI;
        if (off < 0) off += Math.PI;

        // 180 / PI in double, not GameMath.RAD2DEG, which is a float and puts a perpendicular tower at 89.99995.
        return Math.Min(off, Math.PI - off) * (180 / Math.PI);
    }

    /// <summary>
    /// How much to take out of each candidate slot to reach <paramref name="quantity"/>, or null when the
    /// stacks do not add up. Null means the caller mutates nothing - a short inventory must never be
    /// partially drained.
    /// </summary>
    public static int[] PlanConsumption(IReadOnlyList<int> stackSizes, int quantity)
    {
        if (stackSizes == null) return null;

        var takes = new int[stackSizes.Count];
        if (quantity <= 0) return takes;

        var remaining = quantity;
        for (var i = 0; i < stackSizes.Count && remaining > 0; i++)
        {
            var take = Math.Min(Math.Max(0, stackSizes[i]), remaining);
            takes[i] = take;
            remaining -= take;
        }

        return remaining > 0 ? null : takes;
    }

    /// <summary>
    /// Blocks that stop a span. Air, foliage and our own tower parts pass - a straight line of towers must
    /// not block itself, and refusing to build through a fern is infuriating.
    /// </summary>
    public static readonly BlockFilter RopewayBlockFilter = (pos, block) =>
    {
        if (block == null || block.Id == 0) return false;
        if (block.BlockMaterial == EnumBlockMaterial.Leaves || block.BlockMaterial == EnumBlockMaterial.Plant) return false;
        if (block.Code?.Domain == "ropeway") return false;
        return block.SideSolid.Any || block.CollisionBoxes is { Length: > 0 };
    };

    /// <summary>
    /// True when the corridor the rope needs between the two anchors is clear: 3 wide, and from the return
    /// strand's own row down to the bottom of the cabin AT THIS SPAN'S PITCH - see <see cref="ClearanceRows"/> for
    /// why that is not a fixed pair of rows. Parallel block-only ray casts through the engine's own DDA -
    /// a zero-width ray cannot certify a 3-wide cabin, and hand-rolling a voxel walk when
    /// <c>IWorldAccessor.InteresectionTester</c> already exists would be silly. Main thread only.
    /// Fails closed - a rope through a mountain is a bug report, a refused build is an annoyance.
    /// <para>
    /// The band is symmetric about the cabin, which also closes a direction-dependence in the near-vertical
    /// branch below: it hard-codes <c>right = (1,0,0)</c>, so <c>up = Cross(right, dir)</c> flips sign with the
    /// direction of travel. Against the old fixed and ASYMMETRIC row window that made a link clicked from the
    /// top tower certify <c>Z-1..Z+3</c> and the ride check <c>Z-3..Z+1</c> - a link that succeeded and a cabin
    /// that then refused to move. Anything that makes these rows asymmetric again re-opens it silently.
    /// </para>
    /// </summary>
    public static bool IsSpanClear(IWorldAccessor world, Vec3d from, Vec3d to, out BlockPos firstBlocker)
    {
        firstBlocker = null;
        if (world == null || from == null || to == null) return false;

        try
        {
            var dir = to.Clone().Sub(from);
            var length = dir.Length();
            if (length < Epsilon) return true;
            dir.Normalize();

            var trim = TrimForTowers(length);
            if (trim > 0)
            {
                from = from.Clone().Add(dir.X * trim, dir.Y * trim, dir.Z * trim);
                to = to.Clone().Add(-dir.X * trim, -dir.Y * trim, -dir.Z * trim);
            }

            var right = Math.Abs(dir.Y) > 0.999
                ? new Vec3d(1, 0, 0)
                : new Vec3d(-dir.Z, 0, dir.X).Normalize();
            var up = Cross(right, dir).Normalize();

            // The rows the cabin and the strand actually occupy at THIS pitch, rather than a fixed ladder that
            // was only ever the level-span answer.
            var rows = ClearanceRows(dir.Y);

            for (var i = -ClearanceRadius; i <= ClearanceRadius; i++)
            {
                foreach (var j in rows)
                {
                    var offset = new Vec3d(
                        right.X * i + up.X * j,
                        right.Y * i + up.Y * j,
                        right.Z * i + up.Z * j);

                    var hit = world.InteresectionTester.GetSelectedBlock(
                        from.Clone().Add(offset), to.Clone().Add(offset), RopewayBlockFilter);

                    if (hit?.Block != null && hit.Block.Id != 0)
                    {
                        firstBlocker = hit.Position;
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception)
        {
            firstBlocker = null;
            return false;
        }
    }

    private static Vec3d Cross(Vec3d a, Vec3d b)
    {
        return new Vec3d(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);
    }
}
