using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class BlockSlipTransmission : BlockTransmission
{
    private const string SourceSection = "source";
    private const string LoadSection = "load";

    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
    {
        return face == GetExternalFacing();
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        foreach (BlockFacing sourceFacing in GetPlacementFacings(world, blockSel))
        {
            if (TryPlacePair(world, byPlayer, itemstack, blockSel, sourceFacing, ref failureCode))
            {
                return true;
            }
        }

        failureCode = "notenoughspace";
        return false;
    }

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
    }

    public override MechanicalNetwork GetNetwork(IWorldAccessor world, BlockPos pos)
    {
        return world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSlipTransmission>()?.Network;
    }

    public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
    {
        world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSlipTransmission>()?.CheckEngaged();
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (IsLoadSection() && TryGetPairedPos(pos, out BlockPos sourcePos))
        {
            Block sourceBlock = world.BlockAccessor.GetBlock(sourcePos);
            if (sourceBlock is BlockSlipTransmission)
            {
                sourceBlock.OnBlockBroken(world, sourcePos, byPlayer, dropQuantityMultiplier);
                return;
            }
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        base.OnBlockRemoved(world, pos);

        if (!TryGetPairedPos(pos, out BlockPos pairedPos))
        {
            return;
        }

        if (world.BlockAccessor.GetBlock(pairedPos) is BlockSlipTransmission pairedBlock && pairedBlock.Variant["facing"] == Variant["facing"])
        {
            world.BlockAccessor.SetBlock(0, pairedPos);
        }
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer?.Entity?.Controls.Sneak != true)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        if (world.Side != EnumAppSide.Server)
        {
            return true;
        }

        BEBehaviorMPSlipTransmission behavior = GetSourceBehavior(world, blockSel.Position);
        if (behavior == null)
        {
            return true;
        }

        float ratio = behavior.CycleRatio();
        if (byPlayer is IServerPlayer serverPlayer)
        {
            serverPlayer.SendMessage(GlobalConstants.GeneralChatGroup, Lang.Get("flywheelpower:sliptransmission-ratio-set", ratio), EnumChatType.Notification);
        }

        return true;
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        Block sourceBlock = world.GetBlock(new AssetLocation(Code.Domain, $"{FirstCodePart()}-{Variant["facing"]}-{SourceSection}"));
        return sourceBlock == null ? base.OnPickBlock(world, pos) : new ItemStack(sourceBlock);
    }

    private bool TryPlacePair(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, BlockFacing sourceFacing, ref string failureCode)
    {
        Block sourceBlock = GetBlockFor(world, sourceFacing, SourceSection);
        Block loadBlock = GetBlockFor(world, sourceFacing, LoadSection);
        if (sourceBlock == null || loadBlock == null)
        {
            failureCode = "notenoughspace";
            return false;
        }

        BlockPos loadPos = blockSel.Position.AddCopy(sourceFacing.Opposite);
        if (!CanPlaceHalf(world, byPlayer, itemstack, blockSel, sourceBlock, blockSel.Position, ref failureCode)
            || !CanPlaceHalf(world, byPlayer, itemstack, blockSel, loadBlock, loadPos, ref failureCode))
        {
            failureCode = "notenoughspace";
            return false;
        }

        if (!sourceBlock.DoPlaceBlock(world, byPlayer, blockSel, itemstack))
        {
            return false;
        }

        world.BlockAccessor.SetBlock(loadBlock.BlockId, loadPos);
        ConnectExternal(world, blockSel.Position, sourceBlock as BlockSlipTransmission, sourceFacing);
        ConnectExternal(world, loadPos, loadBlock as BlockSlipTransmission, sourceFacing.Opposite);
        return true;
    }

    private static bool CanPlaceHalf(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection originalSel, Block block, BlockPos pos, ref string failureCode)
    {
        BlockSelection selection = originalSel.Clone();
        selection.Position = pos;
        return block.CanPlaceBlock(world, byPlayer, selection, ref failureCode);
    }

    private static void ConnectExternal(IWorldAccessor world, BlockPos pos, BlockSlipTransmission block, BlockFacing externalFacing)
    {
        if (block == null)
        {
            return;
        }

        BlockPos neighborPos = pos.AddCopy(externalFacing);
        if (world.BlockAccessor.GetBlock(neighborPos) is IMechanicalPowerBlock neighbor
            && neighbor.HasMechPowerConnectorAt(world, neighborPos, externalFacing.Opposite, block))
        {
            neighbor.DidConnectAt(world, neighborPos, externalFacing.Opposite);
            block.WasPlaced(world, pos, externalFacing);
        }
    }

    private Block GetBlockFor(IWorldAccessor world, BlockFacing sourceFacing, string section)
    {
        return world.GetBlock(new AssetLocation(Code.Domain, $"{FirstCodePart()}-{sourceFacing.Code}-{section}"));
    }

    private BlockFacing[] GetPlacementFacings(IWorldAccessor world, BlockSelection blockSel)
    {
        List<BlockFacing> facings = new();
        foreach (BlockFacing face in BlockFacing.ALLFACES)
        {
            BlockPos neighborPos = blockSel.Position.AddCopy(face);
            if (world.BlockAccessor.GetBlock(neighborPos) is IMechanicalPowerBlock neighbor
                && neighbor.HasMechPowerConnectorAt(world, neighborPos, face.Opposite, this))
            {
                AddFacing(facings, face);
            }
        }

        if (blockSel.Face != null)
        {
            AddFacing(facings, blockSel.Face.Opposite);
        }

        foreach (BlockFacing face in BlockFacing.ALLFACES)
        {
            AddFacing(facings, face);
        }

        return facings.ToArray();
    }

    private static void AddFacing(List<BlockFacing> facings, BlockFacing facing)
    {
        if (facing != null && !facings.Contains(facing))
        {
            facings.Add(facing);
        }
    }

    private BEBehaviorMPSlipTransmission GetSourceBehavior(IWorldAccessor world, BlockPos pos)
    {
        BEBehaviorMPSlipTransmission behavior = world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPSlipTransmission>();
        if (behavior?.IsSource == true)
        {
            return behavior;
        }

        return behavior?.GetPairedBehavior()?.IsSource == true ? behavior.GetPairedBehavior() : null;
    }

    private bool TryGetPairedPos(BlockPos pos, out BlockPos pairedPos)
    {
        BlockFacing facing = GetSourceFacing();
        pairedPos = IsLoadSection() ? pos.AddCopy(facing) : pos.AddCopy(facing.Opposite);
        return true;
    }

    private bool IsLoadSection()
    {
        return Variant["section"] == LoadSection;
    }

    private BlockFacing GetExternalFacing()
    {
        BlockFacing sourceFacing = GetSourceFacing();
        return IsLoadSection() ? sourceFacing.Opposite : sourceFacing;
    }

    private BlockFacing GetSourceFacing()
    {
        return BlockFacing.FromCode(Variant["facing"]) ?? BlockFacing.NORTH;
    }
}
