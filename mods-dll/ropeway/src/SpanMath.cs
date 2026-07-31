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
    /// Rows below the rope line that must also be clear. The cabin hangs 2 blocks under the rope and its
    /// body runs anchor-3.25..anchor+0.19, so certifying only the rope line lets a rise two blocks under
    /// the rope drag a seated rider through solid stone - riders have no block collision to stop it.
    /// </summary>
    public const int ClearanceBelow = 3;

    /// <summary>
    /// Length of each end of a span that the tower's own structure occupies and that is therefore not
    /// checked. The posts are player-chosen logs and planks, so <see cref="RopewayBlockFilter"/> cannot
    /// tell them from terrain; without this every ray leaving the sheave exits through the tower's own
    /// post and any span more than ~20 degrees off the tower's axis is silently refused. The far corner
    /// of the tower envelope is (2, -3, 3) relative to the head, 3.6 blocks out, so 4 clears it.
    /// </summary>
    public const double TowerClearance = 4.0;

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

    /// <summary>Centre of the sheave block. Dimension-encoded Y, the same space Vec3d world positions and raycasts use.</summary>
    public static Vec3d AnchorOf(BlockPos pos)
    {
        return pos == null ? null : new Vec3d(pos.X + 0.5, pos.InternalY + 0.5, pos.Z + 0.5);
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
    /// True when the corridor the cabin sweeps between the two anchors is clear: 3 wide, and from the rope
    /// line down to the bottom of the cabin. Parallel block-only ray casts through the engine's own DDA -
    /// a zero-width ray cannot certify a 3-wide cabin, and hand-rolling a voxel walk when
    /// <c>IWorldAccessor.InteresectionTester</c> already exists would be silly. Main thread only.
    /// Fails closed - a rope through a mountain is a bug report, a refused build is an annoyance.
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

            for (var i = -ClearanceRadius; i <= ClearanceRadius; i++)
            {
                for (var j = -ClearanceBelow; j <= 0; j++)
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
