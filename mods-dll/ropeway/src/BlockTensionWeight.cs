using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Ropeway;

/// <summary>
/// The store's block. Bound to a tower at placement time, and through that tower to a line: any powered
/// tower on that line winds it, and the cabin spends from it.
/// <para>
/// Binding by PROXIMITY at placement rather than by a link interaction is the lazy half of "one per line":
/// a weight is a thing you build next to your station, the tower is already the mod's one canonical
/// position, and a placement that finds no tower refuses with a reason instead of standing there inert.
/// </para>
/// </summary>
public class BlockTensionWeight : Block
{
    /// <summary>How far from a tower footing the weight may be built. A build constraint, not geometry.</summary>
    public double TowerRadius => Attributes?["towerRadius"].AsDouble(8) ?? 8;

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        var modSystem = world?.Api?.ModLoader?.GetModSystem<RopewayModSystem>();
        var tower = NearestTower(modSystem, blockSel?.Position, TowerRadius);

        if (tower == null)
        {
            failureCode = "ropewaynotower";
            return false;
        }

        // One store per line, and the check has to run BEFORE the block goes down or the player pays for a
        // weight that would never be wound. A line that cannot be resolved yet - a tower with no spans -
        // still gets the check, against that tower alone.
        var line = RopewayLine.GetOrBuild(modSystem, tower);
        var existing = line != null ? BETensionWeight.StoreOn(modSystem, line) : BETensionWeight.StoreAt(modSystem, tower);
        if (existing != null)
        {
            failureCode = "ropewayweightexists";
            return false;
        }

        if (!base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode)) return false;

        (world.BlockAccessor.GetBlockEntity(blockSel.Position) as BETensionWeight)?.Bind(tower);
        return true;
    }

    /// <summary>
    /// The tower footing this weight should serve: the nearest loaded one in the same dimension, inside
    /// the radius. Pure apart from the tower table, and therefore unit-tested through
    /// <see cref="Nearest"/>: picking the wrong tower binds the weight to the wrong LINE, which is a store
    /// that fills up while the cabin next to it refuses to move.
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
