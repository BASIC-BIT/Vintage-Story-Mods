using System;
using System.Threading;
using DimensionLib.Api;
using DimensionLib.Generation.Noise;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Generation;

internal abstract class BuiltInBlockSource : IBlockVolumeSource
{
    protected readonly ICoreServerAPI Api;
    protected readonly Dimension Dimension;
    protected readonly int MapSizeY;
    protected readonly int Seed;

    protected BuiltInBlockSource(ICoreServerAPI api, Dimension dimension, string sourceId)
    {
        Api = api;
        Dimension = dimension;
        SourceId = sourceId;
        MapSizeY = api.WorldManager.MapSizeY;
        Seed = unchecked((int)(dimension.Seed ^ (dimension.Seed >> 32)));
        Bounds = new BlockVolumeBounds(
            dimension.ChunkSizeX * GlobalConstants.ChunkSize,
            MapSizeY,
            dimension.ChunkSizeZ * GlobalConstants.ChunkSize);
    }

    public string SourceId { get; }

    public BlockVolumeBounds Bounds { get; }

    public abstract void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token);

    protected int ResolveBlockId(params string[] codes)
    {
        foreach (var code in codes)
        {
            var block = Api.World.GetBlock(new AssetLocation(code));
            if (block != null && block.BlockId != 0)
            {
                return block.BlockId;
            }
        }

        return 0;
    }

    protected int ApplySpawnPlateau(int height, int localX, int localZ, int floorY, int radius, int blendDistance = 6, double dropPerBlock = 2.5)
    {
        var worldX = Dimension.MinBlockX + localX;
        var worldZ = Dimension.MinBlockZ + localZ;
        var dx = worldX - Dimension.SpawnX;
        var dz = worldZ - Dimension.SpawnZ;
        var distance = Math.Sqrt(dx * dx + dz * dz);

        if (distance <= radius)
        {
            return floorY;
        }

        if (distance <= radius + blendDistance)
        {
            var target = floorY - (int)((distance - radius) * dropPerBlock);
            return Math.Max(height, target);
        }

        return height;
    }

    protected void ClearSpawnBubbleAndMark(IChunkColumnWriter writer, int localChunkX, int localChunkZ, int markerBlockId, int markerRadius = 3)
    {
        var chunkSize = GlobalConstants.ChunkSize;
        var chunkMinX = localChunkX * chunkSize;
        var chunkMinZ = localChunkZ * chunkSize;
        var chunkMaxX = chunkMinX + chunkSize - 1;
        var chunkMaxZ = chunkMinZ + chunkSize - 1;
        var centerX = (int)Math.Round(Dimension.SpawnX - Dimension.MinBlockX);
        var centerZ = (int)Math.Round(Dimension.SpawnZ - Dimension.MinBlockZ);
        var minX = Math.Max(chunkMinX, centerX - markerRadius);
        var maxX = Math.Min(chunkMaxX, centerX + markerRadius);
        var minZ = Math.Max(chunkMinZ, centerZ - markerRadius);
        var maxZ = Math.Min(chunkMaxZ, centerZ + markerRadius);

        if (minX > maxX || minZ > maxZ)
        {
            return;
        }

        var localPos = new BlockPos(0);
        for (var x = minX; x <= maxX; x++)
        {
            for (var z = minZ; z <= maxZ; z++)
            {
                var dx = Math.Abs(x - centerX);
                var dz = Math.Abs(z - centerZ);
                var isMarker = markerBlockId != 0 && (dx == markerRadius || dz == markerRadius || x == centerX || z == centerZ);
                writer.SetBlock(isMarker ? markerBlockId : 0, localPos.Set(x, Dimension.SpawnY - 1, z));

                for (var y = Dimension.SpawnY; y <= Dimension.SpawnY + 5; y++)
                {
                    writer.SetBlock(0, localPos.Set(x, y, z));
                }
            }
        }
    }

    protected static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    protected static double FractalNoise(double x, double z, int seed, int octaves, double persistence)
    {
        return ValueNoise2D.Fractal(x, z, seed, octaves, persistence);
    }
}
