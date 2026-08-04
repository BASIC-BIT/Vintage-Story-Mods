using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// The tensioner's block. It keeps the haul rope taut, which is a thing you build beside a station, so the
/// only rule it carries is that it has to be within reach of a tower - a tensioner standing in a field is
/// tensioning nothing. Everything after placement is proximity at lookup time
/// (<see cref="BETensionWeight.OnLine"/>); nothing is bound, so nothing can come unbound.
/// </summary>
public class BlockTensionWeight : Block
{
    /// <summary>How far from a tower footing the weight may be built. A build constraint, not geometry.</summary>
    public double TowerRadius => Attributes?["towerRadius"].AsDouble(8) ?? 8;

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        var modSystem = world?.Api?.ModLoader?.GetModSystem<RopewayModSystem>();

        if (NearestTower(modSystem, blockSel?.Position, TowerRadius) == null)
        {
            failureCode = "ropewaynotower";
            return false;
        }

        return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
    }

    /// <summary>
    /// The nearest loaded tower footing in the same dimension, inside the radius, or null. Pure apart from
    /// the tower table, and therefore unit-tested through <see cref="Nearest"/>.
    /// <para>
    /// <paramref name="accept"/> narrows the candidates without moving the scan. The drive housing passes one
    /// that demands the footing resolve to a real line, because <c>LoadedTowers</c> holds every footing a
    /// player has ever dropped whether or not it carries spans, and a bare one scouted a few blocks nearer
    /// than the line's own would otherwise take the housing off its line: the mill keeps turning, the cabin
    /// stops, and every panel on screen still says the drive is fine. Placement passes nothing - a housing
    /// may legitimately be built beside a footing before the line it will serve exists.
    /// </para>
    /// <para>
    /// Ties break on <see cref="RopewayLine.ComparePos"/> rather than on whichever entry the dictionary
    /// happens to yield first, which is chunk-load order. That used to decide only who paid the haul load and
    /// now decides which line MOVES, and <c>BEPylonBase.DriveSpeedOn</c> is evaluated independently on the
    /// client - so two machines that loaded their chunks in different orders would report different speeds
    /// for the same rope.
    /// </para>
    /// </summary>
    public static BlockPos NearestTower(RopewayModSystem modSystem, BlockPos pos, double radius, System.Func<BlockPos, bool> accept = null)
    {
        if (modSystem == null || pos == null) return null;

        BlockPos best = null;
        var bestDistance = double.MaxValue;

        foreach (var entry in modSystem.LoadedTowers)
        {
            var distance = Nearest(pos, entry.Key, radius);
            if (distance > bestDistance) continue;

            // best is null only while bestDistance is still MaxValue, which no in-range candidate can equal,
            // so out-of-range entries never reach the comparison.
            if (distance == bestDistance && (best == null || RopewayLine.ComparePos(entry.Key, best) >= 0)) continue;

            // Last, because it is the only test that walks a chain.
            if (accept != null && !accept(entry.Key)) continue;

            bestDistance = distance;
            best = entry.Key;
        }

        return best;
    }

    /// <summary>
    /// Whether <paramref name="pos"/> stands within <paramref name="radius"/> of any tower on the line.
    /// <para>
    /// The TENSIONER asks this and nothing else does (<see cref="BETensionWeight.OnLine"/>). The drive
    /// housing used to as well, and that was the bug: "any tower of this line in range" is true of EVERY
    /// line with a tower nearby, so two lines built close together both read one housing's full speed while
    /// only the nearer of them was ever charged the haul load. The housing now goes through
    /// <see cref="NearestTower"/> instead. The two questions differing is deliberate - a tensioner certifies
    /// that a line has one, which any weight in reach can do, while a drive has to be the drive of exactly
    /// one line or the load model hands out free speed.
    /// </para>
    /// </summary>
    public static bool NearAnyTower(BlockPos pos, RopewayLine line, double radius)
    {
        if (pos == null || line?.Towers == null) return false;

        foreach (var tower in line.Towers)
        {
            if (Nearest(pos, tower, radius) < double.MaxValue) return true;
        }

        return false;
    }

    /// <summary>
    /// Square distance from a weight to a tower, or <c>double.MaxValue</c> when the tower is out of range
    /// or in another dimension. Pure, and therefore tested - the dimension term is the one that has no
    /// visible symptom until somebody builds in a pocket dimension.
    /// </summary>
    public static double Nearest(BlockPos from, BlockPos tower, double radius)
    {
        if (from == null || tower == null || from.dimension != tower.dimension) return double.MaxValue;

        double dx = from.X - tower.X;
        double dy = from.Y - tower.Y;
        double dz = from.Z - tower.Z;
        var square = dx * dx + dy * dy + dz * dz;

        return square > radius * radius ? double.MaxValue : square;
    }
}
