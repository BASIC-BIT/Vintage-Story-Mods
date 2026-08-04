using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Ropeway;

/// <summary>
/// The line's mechanical power intake, standing within eight blocks of any tower rather than on one. It is
/// the machine an axle run reaches without a scaffold: two or three blocks from the mill, rather than the
/// sixteen it took to get power up to the crossarm.
/// <para>
/// It is NOT necessarily on the ground, which is what this comment claimed first and is wrong for every
/// windmill. A rotor refuses its first sail unless the disc it turns in is clear every way, so even a
/// three-sail hub stands four blocks up and a maxed wooden one six; the housing climbs to meet it and the
/// run stays level. That layout is legal only because the eight blocks are a SPHERE -
/// <see cref="BlockTensionWeight.Nearest"/> squares dy alongside dx and dz - and the water wheel is the one
/// drive that really does hook up at bank level. Worked through in docs/POWER-AND-STORAGE.md.
/// </para>
/// <para>
/// It replaces the bullwheel as the consumer. The wheel stayed on the crossarm as the thing you can SEE
/// turning; this is the thing you can REACH. That split is the whole design: the intake lives where axles
/// live, and the tell-tale lives where the rope is.
/// </para>
/// <para>
/// Bound to a line by PROXIMITY at lookup time, exactly as the tension weight is - same radius attribute,
/// same <see cref="BlockTensionWeight.NearestTower"/> helper, no second pattern. Nothing is persisted at
/// placement, so nothing can come unbound: break the tower a housing was built beside and it simply drives
/// whichever line is still in range.
/// </para>
/// <para>
/// No <c>side</c> variant, deliberately. The housing takes an axle on ANY horizontal face, so there is
/// nothing for an orientation to decide, and a block with no orientation cannot be placed 90 degrees out.
/// </para>
/// </summary>
public class BlockDriveHousing : BlockMPBase
{
    /// <summary>How far from a tower footing this may be built, and the radius it serves a line at.</summary>
    public double TowerRadius => Attributes?["towerRadius"].AsDouble(8) ?? 8;

    /// <summary>
    /// Horizontal faces only, and that is the fix rather than a simplification: the bullwheel accepted an
    /// axle from either end ALONG THE LINE, which at sheave height is the haul rope's own path and the cells
    /// the cabin's hanger travels through - the handbook was telling players to build an axle where the
    /// cabin flies. Anywhere off the crossarm there is no rope to build across - on the ground, or up beside
    /// a mill's hub - and horizontal faces are where vanilla axle runs already live.
    /// </summary>
    public override bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face, BlockMPBase forBlock)
    {
        return face?.IsHorizontal == true;
    }

    public override void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face)
    {
    }

    /// <summary>
    /// Refuses rather than sitting inert. A housing out in a field drives nothing, and a block that accepts
    /// an axle and silently does nothing is the worst thing this could ship as.
    /// <para>
    /// ANY footing in range, deliberately - not <see cref="BEDriveHousing"/>'s stricter "nearest footing that
    /// resolves to a line". Adding that predicate here was considered and rejected: it refuses the housing
    /// while the line is still being built, which is the order half of the world builds in, and a lone
    /// footing becomes a line the moment the next span is strung. The housing's own block-info panel says
    /// <c>ropeway:housing-orphan</c> until then, which is the honest report rather than a refusal. The two
    /// rules are allowed to differ because the placement rule only has to keep the block off a random hillside.
    /// </para>
    /// </summary>
    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode)
    {
        var modSystem = world?.Api?.ModLoader?.GetModSystem<RopewayModSystem>();

        if (BlockTensionWeight.NearestTower(modSystem, blockSel?.Position, TowerRadius) == null)
        {
            failureCode = "ropewaynodrivetower";
            return false;
        }

        return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
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
