using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace FlywheelPower;

public sealed class BlockFlywheelPart : Block
{
    public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        IWorldAccessor world = player?.Entity?.World ?? api.World;
        if (!TryGetPrincipal(world, blockSel.Position, out BlockPos principal))
        {
            return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
        }

        BlockSelection principalSel = blockSel.Clone();
        principalSel.Position = principal;
        return world.BlockAccessor.GetBlock(principal).OnGettingBroken(player, principalSel, itemslot, remainingResistance, dt, counter);
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (!TryGetPrincipal(world, pos, out BlockPos principal))
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        if (byPlayer != null && !world.Claims.TryAccess(byPlayer, principal, EnumBlockAccessFlags.BuildOrBreak))
        {
            return;
        }

        Block principalBlock = world.BlockAccessor.GetBlock(principal);
        if (principalBlock.Id == 0)
        {
            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
            return;
        }

        principalBlock.OnBlockBroken(world, principal, byPlayer, dropQuantityMultiplier);
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (!TryGetPrincipal(world, blockSel.Position, out BlockPos principal))
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        if (byPlayer != null && !world.Claims.TryAccess(byPlayer, principal, EnumBlockAccessFlags.Use))
        {
            return false;
        }

        BlockSelection principalSel = blockSel.Clone();
        principalSel.Position = principal;
        return world.BlockAccessor.GetBlock(principal).OnBlockInteractStart(world, byPlayer, principalSel);
    }

    public override Cuboidf GetParticleBreakBox(IBlockAccessor blockAccess, BlockPos pos, BlockFacing facing)
    {
        if (!TryGetPrincipal(blockAccess, pos, out BlockPos principal))
        {
            return base.GetParticleBreakBox(blockAccess, pos, facing);
        }

        return blockAccess.GetBlock(principal).GetParticleBreakBox(blockAccess, principal, facing);
    }

    public override int GetRandomColor(ICoreClientAPI capi, BlockPos pos, BlockFacing facing, int rndIndex = -1)
    {
        IBlockAccessor blockAccessor = capi.World.BlockAccessor;
        if (!TryGetPrincipal(blockAccessor, pos, out BlockPos principal))
        {
            return base.GetRandomColor(capi, pos, facing, rndIndex);
        }

        return blockAccessor.GetBlock(principal).GetRandomColor(capi, principal, facing, rndIndex);
    }

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        if (!TryGetPrincipal(world, pos, out BlockPos principal))
        {
            return base.OnPickBlock(world, pos);
        }

        return world.BlockAccessor.GetBlock(principal).OnPickBlock(world, principal);
    }

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
    {
        if (!TryGetPrincipal(world, pos, out BlockPos principal))
        {
            return base.GetPlacedBlockName(world, pos);
        }

        StringBuilder name = new();
        name.Append(world.BlockAccessor.GetBlock(principal).OnPickBlock(world, principal)?.GetName());
        foreach (BlockBehavior behavior in BlockBehaviors)
        {
            behavior.GetPlacedBlockName(name, world, pos);
        }

        return name.ToString().TrimEnd();
    }

    private static bool TryGetPrincipal(IWorldAccessor world, BlockPos pos, out BlockPos principal)
    {
        return TryGetPrincipal(world.BlockAccessor, pos, out principal);
    }

    private static bool TryGetPrincipal(IBlockAccessor blockAccessor, BlockPos pos, out BlockPos principal)
    {
        principal = null;
        if (blockAccessor.GetBlockEntity(pos) is not BEFlywheelPart part || part.Principal == null)
        {
            return false;
        }

        Block principalBlock = blockAccessor.GetBlock(part.Principal);
        if (!IsValidPrincipalBlock(principalBlock))
        {
            return false;
        }

        string rotation = principalBlock.Variant?["rotation"];
        if (rotation is not ("ns" or "we" or "ud")
            || !FlywheelMultiblock.IsPartPosition(
                part.Principal,
                FlywheelMultiblock.AxisForRotation(rotation),
                pos))
        {
            return false;
        }

        principal = part.Principal;
        return true;
    }

    internal static bool IsValidPrincipalBlock(Block block)
    {
        return block is BlockFlywheel
            || block is BlockFlywheelStand && block.Variant?["size"] != "compact";
    }
}
