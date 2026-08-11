using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FlywheelPower;

internal static class FlywheelMultiblock
{
    private const string PartBlockCode = "flywheelpart";

    internal static bool HasClearance(
        IWorldAccessor world,
        IPlayer byPlayer,
        BlockSelection blockSel,
        EnumAxis axis,
        ref string failureCode)
    {
        Block partBlock = GetPartBlock(world);
        if (partBlock == null)
        {
            failureCode = "flywheelrequiresclearance";
            return false;
        }

        BlockSelection partSel = blockSel.Clone();
        foreach (BlockPos partPos in GetPartPositions(blockSel.Position, axis))
        {
            partSel.Position = partPos;
            if (!partBlock.CanPlaceBlock(world, byPlayer, partSel, ref failureCode))
            {
                failureCode = "flywheelrequiresclearance";
                return false;
            }
        }

        return true;
    }

    internal static void PlaceParts(IWorldAccessor world, BlockPos center, EnumAxis axis)
    {
        Block partBlock = GetPartBlock(world);
        if (partBlock == null)
        {
            return;
        }

        foreach (BlockPos partPos in GetPartPositions(center, axis))
        {
            world.BlockAccessor.SetBlock(partBlock.BlockId, partPos);
            if (world.BlockAccessor.GetBlockEntity(partPos) is BEFlywheelPart part)
            {
                part.Principal = center.Copy();
                part.MarkDirty(true);
            }
        }
    }

    internal static void RemoveParts(IWorldAccessor world, BlockPos center)
    {
        foreach (EnumAxis axis in new[] { EnumAxis.X, EnumAxis.Y, EnumAxis.Z })
        {
            foreach (BlockPos partPos in GetPartPositions(center, axis))
            {
                if (world.BlockAccessor.GetBlockEntity(partPos) is BEFlywheelPart part
                    && part.Principal != null
                    && part.Principal.Equals(center))
                {
                    part.Principal = null;
                    world.BlockAccessor.SetBlock(0, partPos);
                }
            }
        }
    }

    internal static BlockPos[] GetPartPositions(BlockPos center, EnumAxis axis)
    {
        BlockPos[] positions = new BlockPos[8];
        int count = 0;

        for (int a = -1; a <= 1; a++)
        {
            for (int b = -1; b <= 1; b++)
            {
                if (a == 0 && b == 0)
                {
                    continue;
                }

                positions[count++] = OffsetPosition(center, axis, a, b);
            }
        }

        return positions;
    }

    internal static EnumAxis AxisForRotation(string rotation)
    {
        return rotation switch
        {
            "we" => EnumAxis.X,
            "ud" => EnumAxis.Y,
            _ => EnumAxis.Z
        };
    }

    internal static string RotationForAxis(EnumAxis axis)
    {
        return axis switch
        {
            EnumAxis.X => "we",
            EnumAxis.Y => "ud",
            _ => "ns"
        };
    }

    internal static string RotateRotation(string rotation, int angle)
    {
        int normalizedAngle = ((angle % 360) + 360) % 360;
        if (normalizedAngle != 90 && normalizedAngle != 270)
        {
            return rotation;
        }

        return rotation switch
        {
            "ns" => "we",
            "we" => "ns",
            _ => rotation
        };
    }

    private static Block GetPartBlock(IWorldAccessor world)
    {
        return world.GetBlock(AssetLocation.Create(PartBlockCode, "flywheelpower"));
    }

    private static BlockPos OffsetPosition(BlockPos center, EnumAxis axis, int a, int b)
    {
        BlockPos pos = new(center.dimension);
        switch (axis)
        {
            case EnumAxis.X:
                pos.Set(center.X, center.Y + a, center.Z + b);
                break;
            case EnumAxis.Y:
                pos.Set(center.X + a, center.Y, center.Z + b);
                break;
            default:
                pos.Set(center.X + a, center.Y + b, center.Z);
                break;
        }

        return pos;
    }
}
