using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class BlockFlywheelStand : Block
{
    public override bool TryPlaceBlock(
        IWorldAccessor world,
        IPlayer byPlayer,
        ItemStack itemstack,
        BlockSelection blockSel,
        ref string failureCode)
    {
        bool compact = IsCompact;
        EnumAxis axis = ResolvePlacementAxis(
            blockSel.Face.Axis,
            byPlayer.Entity?.Pos.Yaw ?? 0f,
            byPlayer.Entity?.Controls?.Sneak == true);
        BlockSelection placementSel = blockSel.Clone();
        placementSel.Position = ResolvePlacementPosition(blockSel.Position, compact, blockSel.Face.Axis);
        string rotation = FlywheelMultiblock.RotationForAxis(axis);
        Block orientedBlock = world.GetBlock(CodeWithVariant("rotation", rotation));
        if (orientedBlock == null || !orientedBlock.CanPlaceBlock(world, byPlayer, placementSel, ref failureCode))
        {
            return false;
        }

        if (!(compact
                ? FlywheelGroundSupport.HasCompactFoundation(world, placementSel.Position)
                : FlywheelGroundSupport.HasFullSizeFoundation(world, placementSel.Position, axis)))
        {
            failureCode = "flywheelrequiresfoundation";
            return false;
        }

        if (!compact && !FlywheelMultiblock.HasClearance(world, byPlayer, placementSel, axis, ref failureCode))
        {
            return false;
        }

        if (!orientedBlock.DoPlaceBlock(world, byPlayer, placementSel, itemstack))
        {
            return false;
        }

        if (!compact)
        {
            FlywheelMultiblock.PlaceParts(world, placementSel.Position, axis);
        }

        return true;
    }

    internal static BlockPos ResolvePlacementPosition(
        BlockPos selectedPosition,
        bool compact,
        EnumAxis selectedFaceAxis)
    {
        return !compact && selectedFaceAxis == EnumAxis.Y
            ? selectedPosition.UpCopy()
            : selectedPosition.Copy();
    }

    internal static EnumAxis ResolvePlacementAxis(
        EnumAxis selectedFaceAxis,
        float playerYaw,
        bool verticalPlacement)
    {
        if (selectedFaceAxis != EnumAxis.Y)
        {
            return selectedFaceAxis;
        }

        return verticalPlacement
            ? EnumAxis.Y
            : BlockFacing.HorizontalFromAngle(playerYaw).Axis;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        Block heldBlock = slot?.Itemstack?.Block;
        bool heldCompact = heldBlock is BlockCompactFlywheel;
        if (heldBlock is not BlockFlywheel && !heldCompact)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        if (heldCompact != IsCompact)
        {
            if (world.Side == EnumAppSide.Client)
            {
                (world.Api as ICoreClientAPI)?.TriggerIngameError(
                    this,
                    "flywheelstandmismatch",
                    Lang.Get(IsCompact
                        ? "flywheelpower:error-compactstand"
                        : "flywheelpower:error-fullstand"));
            }

            return true;
        }

        if (world.Side == EnumAppSide.Server)
        {
            string rotation = Variant["rotation"];
            Block installed = world.GetBlock(heldBlock.CodeWithVariant("rotation", rotation));
            if (installed == null)
            {
                return true;
            }

            ItemStack assemblyStack = slot.Itemstack.Clone();
            world.BlockAccessor.SetBlock(installed.BlockId, blockSel.Position, assemblyStack);
            if (!IsCompact)
            {
                FlywheelMultiblock.PlaceParts(world, blockSel.Position, FlywheelMultiblock.AxisForRotation(rotation));
            }

            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            if (installed is BlockMPBase mechanicalBlock)
            {
                mechanicalBlock.WasPlaced(world, blockSel.Position, null);
            }
        }

        return true;
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        base.OnBlockRemoved(world, pos);
        if (!IsCompact)
        {
            FlywheelMultiblock.RemoveParts(world, pos);
        }
    }

    private bool IsCompact => Variant?["size"] == "compact";
}
