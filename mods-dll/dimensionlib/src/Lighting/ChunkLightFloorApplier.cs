using System;
using System.Collections.Generic;
using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Lighting;

internal sealed class ChunkLightFloorApplier
{
    private readonly ICoreServerAPI _api;

    public ChunkLightFloorApplier(ICoreServerAPI api)
    {
        _api = api;
    }

    public int ApplyBlocklightFloor(Dimension dimension, int level, IEnumerable<Vec2i> localChunks)
    {
        level = ClampInt(level, 0, 31);
        if (level <= 0)
        {
            return 0;
        }

        var chunkSize = GlobalConstants.ChunkSize;
        var mapChunkSectionsY = Math.Max(1, _api.WorldManager.MapSizeY / chunkSize);
        var encodedBlockLight = level << 5;
        var updated = 0;

        foreach (var localChunk in localChunks)
        {
            updated += ApplyFloorToChunkColumn(dimension, localChunk, chunkSize, mapChunkSectionsY, (chunk, index) =>
            {
                if (chunk.Lighting.GetBlocklight(index) >= level)
                {
                    return false;
                }

                chunk.Lighting.SetBlocklight(index, encodedBlockLight);
                return true;
            });
        }

        return updated;
    }

    private int ApplyFloorToChunkColumn(Dimension dimension, Vec2i localChunk, int chunkSize, int mapChunkSectionsY, System.Func<IWorldChunk, int, bool> apply)
    {
        var chunkX = dimension.ChunkX + localChunk.X;
        var chunkZ = dimension.ChunkZ + localChunk.Y;
        _api.WorldManager.LoadChunkColumnForDimension(chunkX, chunkZ, dimension.DimensionPlaneId);
        var updated = 0;

        for (var chunkY = 0; chunkY < mapChunkSectionsY; chunkY++)
        {
            var chunk = _api.World.BlockAccessor.GetChunk(chunkX, chunkY + dimension.DimensionPlaneId * 1024, chunkZ);
            if (chunk == null)
            {
                continue;
            }

            chunk.Unpack();
            var changed = false;
            for (var y = 0; y < chunkSize; y++)
            {
                for (var z = 0; z < chunkSize; z++)
                {
                    var indexBase = (y * chunkSize + z) * chunkSize;
                    for (var x = 0; x < chunkSize; x++)
                    {
                        var index = indexBase + x;
                        if (chunk.Data[index] != 0 || !apply(chunk, index))
                        {
                            continue;
                        }

                        updated++;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                chunk.MarkModified();
            }
        }

        return updated;
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

}
