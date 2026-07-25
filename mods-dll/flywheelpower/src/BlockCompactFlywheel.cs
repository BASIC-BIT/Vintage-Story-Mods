using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class BlockCompactFlywheel : BlockMPBase
{
    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
    {
        return IsOrientedTo(face);
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        BlockFacing[] connectorFaces = GetConnectableFaces(world, blockSel.Position);
        if (connectorFaces.Length > 0)
        {
            EnumAxis axis = ChooseAxis(connectorFaces, blockSel.Face.Axis);
            Block block = world.GetBlock(CodeWithVariant("rotation", RotationForAxis(axis)));
            if (!block.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
            {
                return false;
            }

            ConnectToAxisNeighbors(world, blockSel.Position, connectorFaces, axis);
            return true;
        }

        EnumAxis fallbackAxis = blockSel.Face.Axis;
        Block fallbackBlock = world.GetBlock(CodeWithVariant("rotation", RotationForAxis(fallbackAxis)));
        if (fallbackBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
        {
            WasPlaced(world, blockSel.Position, null);
            return true;
        }

        return false;
    }

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
    }

    private bool IsOrientedTo(BlockFacing facing)
    {
        string rotation = Variant["rotation"];
        return rotation.IndexOf(facing.Code[0]) >= 0;
    }

    private static string RotationForAxis(EnumAxis axis)
    {
        if (axis == EnumAxis.X)
        {
            return "we";
        }

        if (axis == EnumAxis.Y)
        {
            return "ud";
        }

        return "ns";
    }

    private void ConnectToAxisNeighbors(IWorldAccessor world, BlockPos pos, BlockFacing[] connectorFaces, EnumAxis axis)
    {
        foreach (BlockFacing face in connectorFaces)
        {
            if (face.Axis != axis)
            {
                continue;
            }

            BlockPos neighborPos = pos.AddCopy(face);
            if (world.BlockAccessor.GetBlock(neighborPos) is IMechanicalPowerBlock neighbor)
            {
                neighbor.DidConnectAt(world, neighborPos, face.Opposite);
                WasPlaced(world, pos, face);
            }
        }
    }

    private BlockFacing[] GetConnectableFaces(IWorldAccessor world, BlockPos ownPos)
    {
        BlockFacing[] faces = new BlockFacing[6];
        int count = 0;

        foreach (BlockFacing face in BlockFacing.ALLFACES)
        {
            BlockPos neighborPos = ownPos.AddCopy(face);
            if (world.BlockAccessor.GetBlock(neighborPos) is IMechanicalPowerBlock neighbor && neighbor.HasMechPowerConnectorAt(world, neighborPos, face.Opposite, this))
            {
                faces[count++] = face;
            }
        }

        BlockFacing[] result = new BlockFacing[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = faces[i];
        }

        return result;
    }

    private static EnumAxis ChooseAxis(BlockFacing[] connectorFaces, EnumAxis fallbackAxis)
    {
        int x = CountAxis(connectorFaces, EnumAxis.X);
        int y = CountAxis(connectorFaces, EnumAxis.Y);
        int z = CountAxis(connectorFaces, EnumAxis.Z);

        if (CountForAxis(fallbackAxis, x, y, z) > 0 && CountForAxis(fallbackAxis, x, y, z) >= x && CountForAxis(fallbackAxis, x, y, z) >= y && CountForAxis(fallbackAxis, x, y, z) >= z)
        {
            return fallbackAxis;
        }

        if (y >= x && y >= z)
        {
            return EnumAxis.Y;
        }

        if (z >= x)
        {
            return EnumAxis.Z;
        }

        return EnumAxis.X;
    }

    private static int CountAxis(BlockFacing[] faces, EnumAxis axis)
    {
        int count = 0;
        foreach (BlockFacing face in faces)
        {
            if (face.Axis == axis)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountForAxis(EnumAxis axis, int x, int y, int z)
    {
        return axis switch
        {
            EnumAxis.X => x,
            EnumAxis.Y => y,
            _ => z
        };
    }
}
