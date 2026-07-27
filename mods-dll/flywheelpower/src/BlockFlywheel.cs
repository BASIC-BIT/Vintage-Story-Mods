using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class BlockFlywheel : BlockMPBase
{
    private const string PartBlockCode = "flywheelpart";

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
            if (!PlaceFlywheel(world, byPlayer, itemstack, blockSel, block, axis, ref failureCode))
            {
                return false;
            }

            ConnectToAxisNeighbors(world, blockSel.Position, connectorFaces, axis);
            return true;
        }

        EnumAxis fallbackAxis = blockSel.Face.Axis;
        Block fallbackBlock = world.GetBlock(CodeWithVariant("rotation", RotationForAxis(fallbackAxis)));
        if (PlaceFlywheel(world, byPlayer, itemstack, blockSel, fallbackBlock, fallbackAxis, ref failureCode))
        {
            WasPlaced(world, blockSel.Position, null);
            return true;
        }

        return false;
    }

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        base.OnBlockRemoved(world, pos);
        RemovePartBlocks(world, pos);
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

    private bool PlaceFlywheel(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, Block block, EnumAxis axis, ref string failureCode)
    {
        if (!CanPlaceFlywheel(world, byPlayer, blockSel, block, axis, ref failureCode))
        {
            return false;
        }

        if (!block.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
        {
            return false;
        }

        PlacePartBlocks(world, blockSel.Position, axis);
        return true;
    }

    private bool CanPlaceFlywheel(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, Block block, EnumAxis axis, ref string failureCode)
    {
        if (block == null || !block.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode))
        {
            return false;
        }

        if (!FlywheelGroundSupport.HasFullSizeFoundation(world, blockSel.Position, axis))
        {
            failureCode = "flywheelrequiresfoundation";
            return false;
        }

        Block partBlock = GetPartBlock(world);
        if (partBlock == null)
        {
            failureCode = "flywheelrequiresclearance";
            return false;
        }

        BlockSelection partSel = blockSel.Clone();
        foreach (BlockPos partPos in GetFootprintPartPositions(blockSel.Position, axis))
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

    private void PlacePartBlocks(IWorldAccessor world, BlockPos center, EnumAxis axis)
    {
        Block partBlock = GetPartBlock(world);
        if (partBlock == null)
        {
            return;
        }

        foreach (BlockPos partPos in GetFootprintPartPositions(center, axis))
        {
            world.BlockAccessor.SetBlock(partBlock.BlockId, partPos);
            if (world.BlockAccessor.GetBlockEntity(partPos) is BEFlywheelPart part)
            {
                part.Principal = center.Copy();
                part.MarkDirty(true);
            }
        }
    }

    private void RemovePartBlocks(IWorldAccessor world, BlockPos center)
    {
        foreach (EnumAxis axis in new[] { EnumAxis.X, EnumAxis.Y, EnumAxis.Z })
        {
            foreach (BlockPos partPos in GetFootprintPartPositions(center, axis))
            {
                if (world.BlockAccessor.GetBlockEntity(partPos) is BEFlywheelPart part && part.Principal != null && part.Principal.Equals(center))
                {
                    part.Principal = null;
                    world.BlockAccessor.SetBlock(0, partPos);
                }
            }
        }
    }

    private Block GetPartBlock(IWorldAccessor world)
    {
        return world.GetBlock(AssetLocation.Create(PartBlockCode, Code.Domain));
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

    private static BlockPos[] GetFootprintPartPositions(BlockPos center, EnumAxis axis)
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
