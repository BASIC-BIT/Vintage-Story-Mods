using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FlywheelPower;

internal static class FlywheelGroundSupport
{
    internal static bool HasFullSizeFoundation(IWorldAccessor world, BlockPos center, EnumAxis axis)
    {
        foreach (BlockPos supportPos in GetFullSizeSupportPositions(center, axis))
        {
            if (!HasSolidTop(world, supportPos))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool HasCompactFoundation(IWorldAccessor world, BlockPos center)
    {
        return HasSolidTop(world, center.DownCopy());
    }

    internal static IEnumerable<BlockPos> GetFullSizeSupportPositions(BlockPos center, EnumAxis axis)
    {
        if (axis == EnumAxis.Y)
        {
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    yield return new BlockPos(center.X + x, center.Y - 1, center.Z + z, center.dimension);
                }
            }

            yield break;
        }

        for (int offset = -1; offset <= 1; offset++)
        {
            yield return axis == EnumAxis.X
                ? new BlockPos(center.X, center.Y - 2, center.Z + offset, center.dimension)
                : new BlockPos(center.X + offset, center.Y - 2, center.Z, center.dimension);
        }
    }

    private static bool HasSolidTop(IWorldAccessor world, BlockPos pos)
    {
        Block block = world.BlockAccessor.GetBlock(pos);
        return block != null && block.SideSolid[BlockFacing.UP.Index];
    }
}
