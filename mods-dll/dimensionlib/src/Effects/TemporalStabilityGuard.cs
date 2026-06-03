using System;
using DimensionLib.Api;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Effects;

internal sealed class TemporalStabilityGuard
{
    private readonly ICoreServerAPI _api;
    private readonly Func<BlockPos, bool> _isGuardedPosition;

    public TemporalStabilityGuard(ICoreServerAPI api, Func<BlockPos, bool> isGuardedPosition)
    {
        _api = api;
        _isGuardedPosition = isGuardedPosition;
    }

    public void Tick(float dt)
    {
        foreach (var player in _api.World.AllOnlinePlayers)
        {
            var entity = player.Entity;
            if (entity?.Pos == null || !_isGuardedPosition(entity.Pos.AsBlockPos))
            {
                continue;
            }

            if (entity.WatchedAttributes != null)
            {
                entity.WatchedAttributes.SetDouble("temporalStability", 1.0);
                entity.WatchedAttributes.MarkPathDirty("temporalStability");
            }
        }
    }
}
