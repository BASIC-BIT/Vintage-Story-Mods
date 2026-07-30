using System;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace FlywheelPower;

public sealed class BlockFlywheel : BlockMPBase
{
    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
    {
        return IsOrientedTo(face);
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        failureCode = "flywheelrequiresstand";
        return false;
    }

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
    }

    public override void AddExtraHeldItemInfoPostMaterial(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world)
    {
        base.AddExtraHeldItemInfoPostMaterial(inSlot, dsc, world);
        FlywheelPhysicalProfile profile = FlywheelPhysicalProperties.ForBlock(this);
        dsc.AppendLine(Lang.Get(
            "flywheelpower:blockinfo-physical",
            Math.Round(profile.RotatingMassKg),
            Math.Round(profile.EffectiveInertia, 3)));
    }

    public override void OnBlockRemoved(IWorldAccessor world, BlockPos pos)
    {
        base.OnBlockRemoved(world, pos);
        FlywheelMultiblock.RemoveParts(world, pos);
    }

    private bool IsOrientedTo(BlockFacing facing)
    {
        string rotation = Variant["rotation"];
        return rotation.IndexOf(facing.Code[0]) >= 0;
    }

}
