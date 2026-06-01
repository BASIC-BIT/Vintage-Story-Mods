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

    public int ApplyBlocklightFloor(Dimension dimension, int level, IEnumerable<Vec2i> localChunks, DimensionLightPolicy policy)
    {
        level = ClampInt(level, 0, 31);
        if (level <= 0)
        {
            return 0;
        }

        var chunkSize = GlobalConstants.ChunkSize;
        var mapChunkSectionsY = Math.Max(1, _api.WorldManager.MapSizeY / chunkSize);
        var yRange = ResolveAmbientLightYRange(dimension, policy, chunkSize, mapChunkSectionsY);
        var encodedBlockLight = level << 5;
        var updated = 0;

        foreach (var localChunk in localChunks)
        {
            updated += ApplyFloorToChunkColumn(dimension, localChunk, yRange, chunkSize, (chunk, index) =>
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

    public int ApplySunlightFloor(Dimension dimension, int level, IEnumerable<Vec2i> localChunks, DimensionLightPolicy policy)
    {
        level = ClampInt(level, 0, 31);
        if (level <= 0)
        {
            return 0;
        }

        var chunkSize = GlobalConstants.ChunkSize;
        var mapChunkSectionsY = Math.Max(1, _api.WorldManager.MapSizeY / chunkSize);
        var yRange = ResolveAmbientLightYRange(dimension, policy, chunkSize, mapChunkSectionsY);
        var updated = 0;

        foreach (var localChunk in localChunks)
        {
            updated += ApplyFloorToChunkColumn(dimension, localChunk, yRange, chunkSize, (chunk, index) =>
            {
                if (chunk.Lighting.GetSunlight(index) >= level)
                {
                    return false;
                }

                chunk.Lighting.SetSunlight(index, level);
                return true;
            });
        }

        return updated;
    }

    private int ApplyFloorToChunkColumn(Dimension dimension, Vec2i localChunk, AmbientLightYRange yRange, int chunkSize, System.Func<IWorldChunk, int, bool> apply)
    {
        var chunkX = dimension.ChunkX + localChunk.X;
        var chunkZ = dimension.ChunkZ + localChunk.Y;
        _api.WorldManager.LoadChunkColumnForDimension(chunkX, chunkZ, dimension.DimensionPlaneId);
        var updated = 0;

        for (var chunkY = yRange.MinChunkY; chunkY <= yRange.MaxChunkY; chunkY++)
        {
            var chunk = _api.World.BlockAccessor.GetChunk(chunkX, chunkY + dimension.DimensionPlaneId * 1024, chunkZ);
            if (chunk == null)
            {
                continue;
            }

            chunk.Unpack();
            var sectionMinY = Math.Max(0, yRange.MinY - chunkY * chunkSize);
            var sectionMaxY = Math.Min(chunkSize - 1, yRange.MaxY - chunkY * chunkSize);
            var changed = false;
            for (var y = sectionMinY; y <= sectionMaxY; y++)
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

    private AmbientLightYRange ResolveAmbientLightYRange(Dimension dimension, DimensionLightPolicy policy, int chunkSize, int mapChunkSectionsY)
    {
        var mapMaxY = mapChunkSectionsY * chunkSize - 1;
        var minY = 0;
        var maxY = mapMaxY;

        if (policy != null && policy.MaxYOffset != int.MaxValue)
        {
            minY = ClampInt(dimension.SpawnY + policy.MinYOffset, 0, mapMaxY);
            maxY = ClampInt(dimension.SpawnY + policy.MaxYOffset, minY, mapMaxY);
        }

        return new AmbientLightYRange(
            ClampInt(minY / chunkSize, 0, mapChunkSectionsY - 1),
            ClampInt(maxY / chunkSize, 0, mapChunkSectionsY - 1),
            minY,
            maxY);
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private readonly struct AmbientLightYRange
    {
        public AmbientLightYRange(int minChunkY, int maxChunkY, int minY, int maxY)
        {
            MinChunkY = minChunkY;
            MaxChunkY = maxChunkY;
            MinY = minY;
            MaxY = maxY;
        }

        public int MinChunkY { get; }

        public int MaxChunkY { get; }

        public int MinY { get; }

        public int MaxY { get; }
    }
}
