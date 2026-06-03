using System;
using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace DimensionLib.Protection;

internal sealed class DimensionAccessService
{
    private readonly PolicyProviderRegistry _policyProviders;
    private readonly System.Func<string, bool> _isDimensionOrphaned;

    public DimensionAccessService(PolicyProviderRegistry policyProviders, System.Func<string, bool> isDimensionOrphaned)
    {
        _policyProviders = policyProviders;
        _isDimensionOrphaned = isDimensionOrphaned;
    }

    public bool CanEnter(IServerPlayer player, Dimension dimension, out string reason)
    {
        reason = string.Empty;
        if (player?.Entity == null)
        {
            reason = "Online player is required.";
            return false;
        }

        if (player.HasPrivilege(Privilege.root))
        {
            return true;
        }

        if (_isDimensionOrphaned(dimension.DimensionId))
        {
            reason = "DimensionLib dimension is orphaned.";
            return false;
        }

        if (dimension.AccessPolicy == DimensionAccessPolicy.AdminOnly)
        {
            reason = "DimensionLib dimension is admin-only.";
            return false;
        }

        if (_policyProviders.TryGet(dimension, out var provider))
        {
            return provider.CanEnter(player, dimension, out reason);
        }

        if (dimension.AccessPolicy == DimensionAccessPolicy.OwnerOnly)
        {
            reason = "DimensionLib dimension requires an owner policy provider.";
            return false;
        }

        return true;
    }

    public bool CanUseBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSel, out string reason)
    {
        reason = string.Empty;
        if (player?.Entity == null)
        {
            reason = "Online player is required.";
            return false;
        }

        if (player.HasPrivilege(Privilege.root))
        {
            return true;
        }

        var hasProvider = _policyProviders.TryGet(dimension, out var provider);
        if (hasProvider && !provider.CanUseBlock(player, dimension, blockSel, out reason))
        {
            return false;
        }

        if (!CanEnter(player, dimension, out reason))
        {
            return false;
        }

        if (dimension.Mutability == DimensionMutability.ReadOnly)
        {
            reason = "DimensionLib dimension is read-only.";
            return false;
        }

        return true;
    }

    public bool CanMutateBlock(IServerPlayer player, Dimension dimension, BlockSelection blockSel, DimensionBlockMutationKind mutationKind, out string reason)
    {
        reason = string.Empty;
        if (player?.Entity == null)
        {
            reason = "Online player is required.";
            return false;
        }

        if (player.HasPrivilege(Privilege.root))
        {
            return true;
        }

        var hasProvider = _policyProviders.TryGet(dimension, out var provider);
        if (hasProvider && !provider.CanMutateBlock(player, dimension, blockSel, mutationKind, out reason))
        {
            return false;
        }

        if (!CanEnter(player, dimension, out reason))
        {
            return false;
        }

        if (dimension.Mutability == DimensionMutability.ReadOnly)
        {
            reason = "DimensionLib dimension is read-only.";
            return false;
        }

        return true;
    }
}
