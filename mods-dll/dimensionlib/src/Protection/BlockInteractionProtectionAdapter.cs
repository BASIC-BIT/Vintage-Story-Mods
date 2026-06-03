using System;
using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace DimensionLib.Protection;

internal sealed class BlockInteractionProtectionAdapter
{
    private readonly DimensionAccessService _accessService;
    private readonly System.Func<BlockSelection, Dimension> _resolveDimension;

    public BlockInteractionProtectionAdapter(DimensionAccessService accessService, System.Func<BlockSelection, Dimension> resolveDimension)
    {
        _accessService = accessService;
        _resolveDimension = resolveDimension;
    }

    public bool OnCanPlaceOrBreakBlock(IServerPlayer byPlayer, BlockSelection blockSel, out string claimant)
    {
        claimant = null;
        if (CanMutateBlock(byPlayer, blockSel, DimensionBlockMutationKind.Place, out var reason))
        {
            return true;
        }

        claimant = string.IsNullOrWhiteSpace(reason) ? "DimensionLib" : reason;
        return false;
    }

    public bool OnCanUseBlock(IServerPlayer byPlayer, BlockSelection blockSel)
    {
        return CanUseBlock(byPlayer, blockSel, out _);
    }

    public void OnBreakBlock(IServerPlayer byPlayer, BlockSelection blockSel, ref float dropQuantityMultiplier, ref EnumHandling handling)
    {
        if (CanMutateBlock(byPlayer, blockSel, DimensionBlockMutationKind.Break, out _))
        {
            return;
        }

        dropQuantityMultiplier = 0;
        handling = EnumHandling.PreventDefault;
    }

    private bool CanUseBlock(IServerPlayer player, BlockSelection blockSel, out string reason)
    {
        reason = string.Empty;
        var dimension = _resolveDimension(blockSel);
        if (dimension == null)
        {
            return true;
        }

        return _accessService.CanUseBlock(player, dimension, blockSel, out reason);
    }

    private bool CanMutateBlock(IServerPlayer player, BlockSelection blockSel, DimensionBlockMutationKind mutationKind, out string reason)
    {
        reason = string.Empty;
        var dimension = _resolveDimension(blockSel);
        if (dimension == null)
        {
            return true;
        }

        return _accessService.CanMutateBlock(player, dimension, blockSel, mutationKind, out reason);
    }
}
