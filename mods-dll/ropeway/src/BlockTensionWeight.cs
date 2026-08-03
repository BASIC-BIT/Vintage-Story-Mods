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
    /// </summary>
    public static BlockPos NearestTower(RopewayModSystem modSystem, BlockPos pos, double radius)
    {
        if (modSystem == null || pos == null) return null;

        BlockPos best = null;
        var bestDistance = double.MaxValue;

        foreach (var entry in modSystem.LoadedTowers)
        {
            var distance = Nearest(pos, entry.Key, radius);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = entry.Key;
            }
        }

        return best;
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
