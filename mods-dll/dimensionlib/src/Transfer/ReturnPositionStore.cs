using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Transfer;

internal sealed class ReturnPositionStore
{
    private const string ReturnPositionModDataKey = "dimensionlib:return-position";

    private readonly ILogger _logger;
    private readonly System.Collections.Generic.Dictionary<string, EntityPos> _returnPositionsByPlayerUid = new System.Collections.Generic.Dictionary<string, EntityPos>(StringComparer.Ordinal);

    public ReturnPositionStore(ILogger logger)
    {
        _logger = logger;
    }

    public bool ShouldRecord(IServerPlayer player, System.Func<BlockPos, bool> isInsideDimensionLibDimension)
    {
        if (player?.Entity?.Pos == null)
        {
            return false;
        }

        return !isInsideDimensionLibDimension(player.Entity.Pos.AsBlockPos) || !TryGet(player, out _);
    }

    public void Record(IServerPlayer player)
    {
        var returnPos = player.Entity.Pos.Copy();
        _returnPositionsByPlayerUid[player.PlayerUID] = returnPos;
        player.SetModData(ReturnPositionModDataKey, returnPos);
    }

    public bool TryGet(IServerPlayer player, out EntityPos returnPos)
    {
        returnPos = null;
        if (player == null)
        {
            return false;
        }

        if (_returnPositionsByPlayerUid.TryGetValue(player.PlayerUID, out returnPos))
        {
            return true;
        }

        try
        {
            returnPos = player.GetModData<EntityPos>(ReturnPositionModDataKey);
        }
        catch (Exception ex)
        {
            _logger.Warning("[DimensionLib] Failed to load return point for player '{0}': {1}", player.PlayerUID, ex.Message);
        }

        if (returnPos == null)
        {
            return false;
        }

        _returnPositionsByPlayerUid[player.PlayerUID] = returnPos.Copy();
        return true;
    }

    public void Clear(IServerPlayer player)
    {
        if (player == null)
        {
            return;
        }

        _returnPositionsByPlayerUid.Remove(player.PlayerUID);
        player.RemoveModdata(ReturnPositionModDataKey);
    }
}
