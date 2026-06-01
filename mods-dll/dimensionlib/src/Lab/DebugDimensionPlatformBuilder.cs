using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Lab;

internal sealed class DebugDimensionPlatformBuilder
{
    private readonly ICoreServerAPI _api;

    public DebugDimensionPlatformBuilder(ICoreServerAPI api)
    {
        _api = api;
    }

    public void Fill(Dimension dimension)
    {
        var blockId = ResolveBlockId("rock-granite", "cobblestone-granite", "soil-medium-normal");
        var markerBlockId = ResolveBlockId("soil-medium-normal", "cobblestone-granite", "rock-granite");
        if (blockId == 0 || markerBlockId == 0)
        {
            _api.Logger.Warning("[DimensionLib] Debug platform block lookup resolved to air. floor={0}, marker={1}", blockId, markerBlockId);
        }

        var accessor = _api.World.BlockAccessor;
        var pos = new BlockPos(dimension.DimensionPlaneId);
        var floorY = dimension.SpawnY - 1;

        for (var x = dimension.MinBlockX; x <= dimension.MaxBlockX; x++)
        {
            for (var z = dimension.MinBlockZ; z <= dimension.MaxBlockZ; z++)
            {
                var isAxisMarker = x == (int)dimension.SpawnX || z == (int)dimension.SpawnZ;
                pos.Set(x, floorY, z);
                accessor.SetBlock(isAxisMarker ? markerBlockId : blockId, pos);
            }
        }

        for (var x = (int)dimension.SpawnX - 2; x <= (int)dimension.SpawnX + 2; x++)
        {
            for (var y = dimension.SpawnY; y <= dimension.SpawnY + 4; y++)
            {
                for (var z = (int)dimension.SpawnZ - 2; z <= (int)dimension.SpawnZ + 2; z++)
                {
                    pos.Set(x, y, z);
                    accessor.SetBlock(0, pos);
                }
            }
        }
    }

    public void LogSample(Dimension dimension)
    {
        var samplePos = new BlockPos((int)dimension.SpawnX, dimension.SpawnY - 1, (int)dimension.SpawnZ, dimension.DimensionPlaneId);
        var sampleBlockId = _api.World.BlockAccessor.GetBlockId(samplePos);
        _api.Logger.Notification("[DimensionLib] Debug platform sample at {0}/{1}/{2} dim {3}: blockId={4}", samplePos.X, samplePos.Y, samplePos.Z, samplePos.dimension, sampleBlockId);
    }

    private int ResolveBlockId(params string[] codes)
    {
        foreach (var code in codes)
        {
            var block = _api.World.GetBlock(new AssetLocation(code));
            if (block != null && block.BlockId != 0)
            {
                return block.BlockId;
            }
        }

        return 0;
    }
}
