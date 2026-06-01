using System;
using System.Threading;
using DimensionLib.Api;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Generation;

public sealed class NetherCavernDimensionGenerator : IDimensionGenerator
{
    private readonly ICoreServerAPI _api;

    public NetherCavernDimensionGenerator(ICoreServerAPI api)
    {
        _api = api;
    }

    public string GeneratorId => DimensionGeneratorIds.NetherCavern;

    public IBlockVolumeSource CreateSource(Dimension dimension)
    {
        return new NetherCavernBlockSource(_api, dimension);
    }
}

internal sealed class NetherCavernBlockSource : BuiltInBlockSource
{
    private readonly NetherCavernGenerationProfile _profile;
    private readonly int _wallId;
    private readonly int _roughId;
    private readonly int _lavaId;

    public NetherCavernBlockSource(ICoreServerAPI api, Dimension dimension, NetherCavernGenerationProfile profile = null)
        : base(api, dimension, DimensionGeneratorIds.NetherCavern)
    {
        _profile = profile ?? NetherCavernGenerationProfile.Default;
        _wallId = ResolveBlockId("dimensionlib:netherrock", "rock-basalt", "rock-granite");
        _roughId = _wallId;
        _lavaId = ResolveBlockId("lava-still-7", "lava-still-6", "lava");
    }

    public override void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token)
    {
        var chunkSize = GlobalConstants.ChunkSize;
        var baseX = localChunkX * chunkSize;
        var baseZ = localChunkZ * chunkSize;
        var localPos = new BlockPos(0);

        for (var lx = 0; lx < chunkSize; lx++)
        {
            for (var lz = 0; lz < chunkSize; lz++)
            {
                token.ThrowIfCancellationRequested();

                var localX = baseX + lx;
                var localZ = baseZ + lz;
                var worldX = Dimension.MinBlockX + localX;
                var worldZ = Dimension.MinBlockZ + localZ;
                var floorY = ApplySpawnPlateau(
                    FloorHeight(worldX, worldZ),
                    localX,
                    localZ,
                    Dimension.SpawnY - 1,
                    _profile.SpawnPlateauRadius,
                    _profile.SpawnPlateauBlendDistance,
                    _profile.SpawnPlateauDropPerBlock);
                var ceilingY = CeilingHeight(worldX, worldZ, floorY);
                var hasLavaPool = IsLavaPool(localX, localZ, worldX, worldZ, floorY);
                var distanceToSpawn = DistanceToSpawn(localX, localZ);
                var columnStrength = ColumnStrength(worldX, worldZ, distanceToSpawn);
                var isColumn = !hasLavaPool && columnStrength >= _profile.ColumnThreshold;
                var hasLavaFall = !isColumn && IsLavaFall(localX, localZ, worldX, worldZ, distanceToSpawn);
                var lavaBottomY = hasLavaPool ? Math.Max(1, floorY - LavaDepth(worldX, worldZ)) : 0;
                var solidFloorTop = hasLavaPool ? lavaBottomY - 1 : floorY;

                for (var y = 1; y <= solidFloorTop; y++)
                {
                    var blockId = y >= solidFloorTop - _profile.SurfaceRoughDepth ? _roughId : _wallId;
                    writer.SetBlock(blockId, localPos.Set(localX, y, localZ));
                }

                if (hasLavaPool)
                {
                    for (var y = lavaBottomY; y <= floorY; y++)
                    {
                        writer.SetBlock(_lavaId, localPos.Set(localX, y, localZ));
                    }
                }

                for (var y = floorY + 1; y < ceilingY; y++)
                {
                    writer.SetBlock(0, localPos.Set(localX, y, localZ));
                }

                if (isColumn)
                {
                    for (var y = floorY + 1; y < ceilingY; y++)
                    {
                        var blockId = y <= floorY + _profile.ColumnRoughDepth || y >= ceilingY - _profile.CeilingSpikeRoughDepth || columnStrength < _profile.ColumnSolidThreshold ? _roughId : _wallId;
                        writer.SetBlock(blockId, localPos.Set(localX, y, localZ));
                    }
                }
                else
                {
                    AddSpikes(writer, localPos, localX, localZ, worldX, worldZ, floorY, ceilingY, hasLavaPool, distanceToSpawn);
                    if (hasLavaFall)
                    {
                        AddLavaFall(writer, localPos, localX, localZ, floorY, ceilingY);
                    }
                }

                var ceilingTop = Math.Min(MapSizeY - 1, ceilingY + _profile.CeilingCoverDepth);
                for (var y = ceilingY; y <= ceilingTop; y++)
                {
                    var blockId = y <= ceilingY + _profile.CeilingRoughDepth ? _roughId : _wallId;
                    writer.SetBlock(blockId, localPos.Set(localX, y, localZ));
                }
            }
        }

        ClearSpawnBubbleAndMark(writer, localChunkX, localChunkZ, _roughId, _profile.SpawnMarkerRadius);
    }

    private int FloorHeight(int worldX, int worldZ)
    {
        var basin = FractalNoise(worldX / _profile.FloorBasinScale, worldZ / _profile.FloorBasinScale, Seed + _profile.FloorBasinSeedOffset, 4, _profile.FloorBasinPersistence);
        var rough = FractalNoise(worldX / _profile.FloorRoughScale, worldZ / _profile.FloorRoughScale, Seed + _profile.FloorRoughSeedOffset, 3, _profile.FloorRoughPersistence);
        var shelves = Math.Abs(FractalNoise(worldX / _profile.FloorShelfScale, worldZ / _profile.FloorShelfScale, Seed + _profile.FloorShelfSeedOffset, 3, _profile.FloorShelfPersistence) - 0.5) * 2.0;
        return ClampInt(_profile.FloorBaseY + (int)(basin * _profile.FloorBasinAmplitude) + (int)(rough * _profile.FloorRoughAmplitude) + (int)(shelves * _profile.FloorShelfAmplitude), _profile.FloorMinY, Dimension.SpawnY - _profile.FloorSpawnClearance);
    }

    private int CeilingHeight(int worldX, int worldZ, int floorY)
    {
        var dome = FractalNoise(worldX / _profile.CeilingDomeScale, worldZ / _profile.CeilingDomeScale, Seed + _profile.CeilingDomeSeedOffset, 4, _profile.CeilingDomePersistence);
        var teeth = Math.Abs(FractalNoise(worldX / _profile.CeilingTeethScale, worldZ / _profile.CeilingTeethScale, Seed + _profile.CeilingTeethSeedOffset, 3, _profile.CeilingTeethPersistence) - 0.5) * 2.0;
        var squeeze = Math.Max(0.0, FractalNoise(worldX / _profile.CeilingSqueezeScale, worldZ / _profile.CeilingSqueezeScale, Seed + _profile.CeilingSqueezeSeedOffset, 4, _profile.CeilingSqueezePersistence) - _profile.CeilingSqueezeThreshold) / _profile.CeilingSqueezeNormalizer;
        var ceilingY = _profile.CeilingBaseY + (int)(dome * _profile.CeilingDomeAmplitude) - (int)(teeth * _profile.CeilingTeethAmplitude) - (int)(squeeze * squeeze * _profile.CeilingSqueezeAmplitude);
        return ClampInt(Math.Max(ceilingY, floorY + _profile.CeilingMinimumOpenHeight), floorY + _profile.CeilingClampMinimumHeight, Math.Min(MapSizeY - _profile.CeilingMapTopClearance, Dimension.SpawnY + _profile.CeilingSpawnMaxOffset));
    }

    private bool IsLavaPool(int localX, int localZ, int worldX, int worldZ, int floorY)
    {
        if (_lavaId == 0 || floorY > _profile.LavaMaxFloorY)
        {
            return false;
        }

        var dx = Dimension.MinBlockX + localX - Dimension.SpawnX;
        var dz = Dimension.MinBlockZ + localZ - Dimension.SpawnZ;
        if (Math.Sqrt(dx * dx + dz * dz) < _profile.LavaSpawnClearRadius)
        {
            return false;
        }

        var poolNoise = FractalNoise(worldX / _profile.LavaPoolScale, worldZ / _profile.LavaPoolScale, Seed + _profile.LavaPoolSeedOffset, 3, _profile.LavaPoolPersistence);
        return poolNoise >= _profile.LavaPoolThreshold;
    }

    private int LavaDepth(int worldX, int worldZ)
    {
        return _profile.LavaDepthBase + (int)(FractalNoise(worldX / _profile.LavaDepthScale, worldZ / _profile.LavaDepthScale, Seed + _profile.LavaDepthSeedOffset, 2, _profile.LavaDepthPersistence) * _profile.LavaDepthAmplitude);
    }

    private void AddSpikes(IChunkColumnWriter writer, BlockPos localPos, int localX, int localZ, int worldX, int worldZ, int floorY, int ceilingY, bool hasLavaPool, double distanceToSpawn)
    {
        if (distanceToSpawn < _profile.FloorSpikeSpawnClearRadius || ceilingY - floorY < _profile.SpikeMinimumOpenHeight)
        {
            return;
        }

        var spikeNoise = FractalNoise(worldX / _profile.FloorSpikeScale, worldZ / _profile.FloorSpikeScale, Seed + _profile.FloorSpikeSeedOffset, 2, _profile.FloorSpikePersistence);
        if (!hasLavaPool && spikeNoise > _profile.FloorSpikeThreshold)
        {
            var height = Math.Min(ceilingY - floorY - _profile.FloorSpikeHeadroom, _profile.FloorSpikeBaseHeight + (int)((spikeNoise - _profile.FloorSpikeThreshold) / _profile.FloorSpikeThresholdRange * _profile.FloorSpikeMaxExtraHeight));
            for (var y = floorY + 1; y <= floorY + height; y++)
            {
                writer.SetBlock(y <= floorY + _profile.SurfaceRoughDepth ? _roughId : _wallId, localPos.Set(localX, y, localZ));
            }
        }

        var ceilingSpikeNoise = FractalNoise(worldX / _profile.CeilingSpikeScale, worldZ / _profile.CeilingSpikeScale, Seed + _profile.CeilingSpikeSeedOffset, 2, _profile.CeilingSpikePersistence);
        if (ceilingSpikeNoise > _profile.CeilingSpikeThreshold)
        {
            var depth = Math.Min(ceilingY - floorY - _profile.CeilingSpikeHeadroom, _profile.CeilingSpikeBaseDepth + (int)((ceilingSpikeNoise - _profile.CeilingSpikeThreshold) / _profile.CeilingSpikeThresholdRange * _profile.CeilingSpikeMaxExtraDepth));
            for (var y = ceilingY - depth; y < ceilingY; y++)
            {
                writer.SetBlock(y >= ceilingY - _profile.CeilingSpikeRoughDepth ? _roughId : _wallId, localPos.Set(localX, y, localZ));
            }
        }
    }

    private void AddLavaFall(IChunkColumnWriter writer, BlockPos localPos, int localX, int localZ, int floorY, int ceilingY)
    {
        if (_lavaId == 0 || ceilingY - floorY < _profile.LavaFallMinimumOpenHeight)
        {
            return;
        }

        for (var y = floorY + 1; y < ceilingY; y++)
        {
            writer.SetBlock(_lavaId, localPos.Set(localX, y, localZ));
        }
    }

    private bool IsLavaFall(int localX, int localZ, int worldX, int worldZ, double distanceToSpawn)
    {
        if (_lavaId == 0 || distanceToSpawn < _profile.LavaFallSpawnClearRadius)
        {
            return false;
        }

        var fissure = Math.Abs(FractalNoise(worldX / _profile.LavaFallFissureScale, worldZ / _profile.LavaFallFissureScale, Seed + _profile.LavaFallFissureSeedOffset, 3, _profile.LavaFallFissurePersistence) - 0.5) * 2.0;
        var source = FractalNoise(worldX / _profile.LavaFallSourceScale, worldZ / _profile.LavaFallSourceScale, Seed + _profile.LavaFallSourceSeedOffset, 3, _profile.LavaFallSourcePersistence);
        return fissure > _profile.LavaFallFissureThreshold && source > _profile.LavaFallSourceThreshold;
    }

    private double ColumnStrength(int worldX, int worldZ, double distanceToSpawn)
    {
        if (distanceToSpawn < _profile.ColumnSpawnClearRadius)
        {
            return 0.0;
        }

        var broad = FractalNoise(worldX / _profile.ColumnBroadScale, worldZ / _profile.ColumnBroadScale, Seed + _profile.ColumnBroadSeedOffset, 4, _profile.ColumnBroadPersistence);
        var vein = Math.Abs(FractalNoise(worldX / _profile.ColumnVeinScale, worldZ / _profile.ColumnVeinScale, Seed + _profile.ColumnVeinSeedOffset, 3, _profile.ColumnVeinPersistence) - 0.5) * 2.0;
        var detail = FractalNoise(worldX / _profile.ColumnDetailScale, worldZ / _profile.ColumnDetailScale, Seed + _profile.ColumnDetailSeedOffset, 2, _profile.ColumnDetailPersistence);
        return broad * _profile.ColumnBroadWeight + vein * _profile.ColumnVeinWeight + detail * _profile.ColumnDetailWeight;
    }

    private double DistanceToSpawn(int localX, int localZ)
    {
        var dx = Dimension.MinBlockX + localX - Dimension.SpawnX;
        var dz = Dimension.MinBlockZ + localZ - Dimension.SpawnZ;
        return Math.Sqrt(dx * dx + dz * dz);
    }
}
