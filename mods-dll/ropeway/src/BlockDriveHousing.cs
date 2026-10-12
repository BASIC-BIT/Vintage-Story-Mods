using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Ropeway;

/// <summary>
/// The line's mechanical intake, and cell [3,0,0] of a <c>ropeway:drivestation</c> - the foot of the
/// station's machine leg, on the ground, where axle runs already live.
/// <para>
/// It stood FREE for one round, within an eight-block sphere of any tower, and this class carried the
/// placement refusal that kept it there. Both are gone: a housing that is not a cell of a station simply
/// drives nothing, which needs no rule and cannot be got wrong halfway through a build. The honest cost is
/// that the intake no longer climbs to meet a windmill's hub - a mill four to eleven blocks up now runs an
/// axle column down the drive leg, which <c>driveshaft</c>'s <c>sidesolid</c> is what makes possible.
/// See docs/POWER-AND-STORAGE.md.
/// </para>
/// <para>
/// No <c>side</c> variant, deliberately. The housing takes an axle on ANY horizontal face, so there is
/// nothing for an orientation to decide, and a block with no orientation cannot be placed 90 degrees out.
/// </para>
/// </summary>
public class BlockDriveHousing : BlockMPBase
{
    /// <summary>
    /// Horizontal faces only, and that is the fix rather than a simplification: the bullwheel accepted an
    /// axle from either end ALONG THE LINE, which at sheave height is the haul rope's own path and the cells
    /// the cabin's hanger travels through - the handbook was telling players to build an axle where the
    /// cabin flies. Down at the foot of the leg there is no rope to build across, and horizontal faces are
    /// where vanilla axle runs already live.
    /// </summary>
    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
    {
        return face?.IsHorizontal == true;
    }

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
    }

    /// <summary>
    /// Nothing in the engine calls <see cref="BlockMPBase.WasPlaced"/> for you - every vanilla mechanical
    /// block makes this call itself, and omitting it ships a housing that accepts an axle and does nothing
    /// until the player breaks and replaces the axle. A null facing makes vanilla probe
    /// <c>BlockFacing.HORIZONTALS</c>, which is exactly and only the set this block connects on, so unlike
    /// the bullwheel's old hookup there is no documented entry it cannot see.
    /// </summary>
    public override void OnBlockPlaced(IWorldAccessor world, BlockPos blockPos, ItemStack byItemStack = null)
    {
        base.OnBlockPlaced(world, blockPos, byItemStack);
        WasPlaced(world, blockPos, null);
    }
}
