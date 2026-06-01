using System;
using System.Threading;
using DimensionLib.Api;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Generation;

public sealed class OverworldOppositeDimensionGenerator : IDimensionGenerator
{
    private readonly ICoreServerAPI _api;

    public OverworldOppositeDimensionGenerator(ICoreServerAPI api)
    {
        _api = api;
    }

    public string GeneratorId => DimensionGeneratorIds.OverworldOpposite;

    public IBlockVolumeSource CreateSource(Dimension dimension)
    {
        return new OverworldOppositeBlockSource(_api, dimension);
    }
}

internal sealed class OverworldOppositeBlockSource : BuiltInBlockSource
{
    private readonly int _rockId;
    private readonly int _soilId;
    private readonly int _surfaceId;
    private readonly int _waterId;

    public OverworldOppositeBlockSource(ICoreServerAPI api, Dimension dimension)
        : base(api, dimension, DimensionGeneratorIds.OverworldOpposite)
    {
        _rockId = ResolveBlockId("rock-granite", "cobblestone-granite", "soil-medium-normal");
        _soilId = ResolveBlockId("soil-medium-normal", "soil-low-normal", "rock-granite");
        _surfaceId = ResolveBlockId("soil-medium-normal", "soil-low-normal", "rock-granite");
        _waterId = ResolveBlockId("water-still-7", "water-still-6", "water");
    }

    public override void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token)
    {
        var chunkSize = GlobalConstants.ChunkSize;
        var baseX = localChunkX * chunkSize;
        var baseZ = localChunkZ * chunkSize;
        var localPos = new BlockPos(0);
        var waterY = 84;

        for (var lx = 0; lx < chunkSize; lx++)
        {
            for (var lz = 0; lz < chunkSize; lz++)
            {
                token.ThrowIfCancellationRequested();

                var localX = baseX + lx;
                var localZ = baseZ + lz;
                var worldX = Dimension.MinBlockX + localX;
                var worldZ = Dimension.MinBlockZ + localZ;
                var height = TerrainHeight(worldX, worldZ);
                height = ApplySpawnPlateau(height, localX, localZ, Dimension.SpawnY - 1, 5);

                for (var y = 1; y <= height; y++)
                {
                    var blockId = y == height ? _surfaceId : y >= height - 4 ? _soilId : _rockId;
                    writer.SetBlock(blockId, localPos.Set(localX, y, localZ));
                }

                if (_waterId == 0 || height >= waterY)
                {
                    continue;
                }

                for (var y = height + 1; y <= waterY; y++)
                {
                    writer.SetBlock(_waterId, localPos.Set(localX, y, localZ));
                }
            }
        }

        ClearSpawnBubbleAndMark(writer, localChunkX, localChunkZ, _soilId);
    }

    private int TerrainHeight(int worldX, int worldZ)
    {
        var continent = FractalNoise(worldX / 180.0, worldZ / 180.0, Seed + 11, 4, 0.52);
        var hills = FractalNoise(worldX / 48.0, worldZ / 48.0, Seed + 37, 4, 0.55);
        var ridges = Math.Abs(FractalNoise(worldX / 28.0, worldZ / 28.0, Seed + 71, 3, 0.6) - 0.5) * 2.0;
        var height = 66 + (int)(continent * 22.0) + (int)(hills * 18.0) + (int)(ridges * 8.0);
        return ClampInt(height, 58, MapSizeY - 24);
    }
}
