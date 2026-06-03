using System;
using System.Collections.Generic;
using System.Threading;
using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionCavern;

public sealed class CavernDimensionGenerator : IDimensionGenerator
{
    private readonly ICoreServerAPI _api;
    private readonly int _wallId;
    private readonly int _lavaId;
    private readonly Dictionary<string, CachedSource> _sourcesByDimensionId = new Dictionary<string, CachedSource>(StringComparer.Ordinal);

    public CavernDimensionGenerator(ICoreServerAPI api, string generatorId)
    {
        _api = api;
        GeneratorId = generatorId;
        _wallId = ResolveBlockId("dimensioncavern:cavernrock", "rock-basalt", "rock-granite");
        _lavaId = ResolveBlockId("lava-still-7", "lava-still-6", "lava");
    }

    public string GeneratorId { get; }

    public IBlockVolumeSource CreateSource(Dimension dimension)
    {
        if (_sourcesByDimensionId.TryGetValue(dimension.DimensionId, out var cached) && ReferenceEquals(cached.Dimension, dimension))
        {
            return cached.Source;
        }

        var source = new CavernBlockSource(dimension, GeneratorId, _wallId, _lavaId, _api.WorldManager.MapSizeY);
        _sourcesByDimensionId[dimension.DimensionId] = new CachedSource(dimension, source);
        return source;
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

    private sealed class CachedSource
    {
        public CachedSource(Dimension dimension, IBlockVolumeSource source)
        {
            Dimension = dimension;
            Source = source;
        }

        public Dimension Dimension { get; }

        public IBlockVolumeSource Source { get; }
    }
}

internal sealed class CavernBlockSource : IBlockVolumeSource
{
    private readonly Dimension _dimension;
    private readonly int _mapSizeY;
    private readonly int _seed;
    private readonly CavernGenerationProfile _profile = CavernGenerationProfile.Default;
    private readonly int _wallId;
    private readonly int _roughId;
    private readonly int _lavaId;

    public CavernBlockSource(Dimension dimension, string sourceId, int wallId, int lavaId, int mapSizeY)
    {
        _dimension = dimension;
        SourceId = sourceId;
        _mapSizeY = mapSizeY;
        _seed = unchecked((int)(dimension.Seed ^ (dimension.Seed >> 32)));
        Bounds = new BlockVolumeBounds(
            dimension.ChunkSizeX * GlobalConstants.ChunkSize,
            _mapSizeY,
            dimension.ChunkSizeZ * GlobalConstants.ChunkSize);
        _wallId = wallId;
        _roughId = _wallId;
        _lavaId = lavaId;
    }

    public string SourceId { get; }

    public BlockVolumeBounds Bounds { get; }

    public void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token)
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
                var worldX = _dimension.MinBlockX + localX;
                var worldZ = _dimension.MinBlockZ + localZ;
                var floorY = ApplySpawnPlateau(
                    FloorHeight(worldX, worldZ),
                    localX,
                    localZ,
                    _dimension.SpawnY - 1,
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
                    for (var y = floorY + 1; y < ceilingY; y++)
                    {
                        writer.SetBlock(0, localPos.Set(localX, y, localZ));
                    }

                    AddSpikes(writer, localPos, localX, localZ, worldX, worldZ, floorY, ceilingY, hasLavaPool, distanceToSpawn);
                    if (hasLavaFall)
                    {
                        AddLavaFall(writer, localPos, localX, localZ, floorY, ceilingY);
                    }
                }

                var ceilingTop = Math.Min(_mapSizeY - 1, ceilingY + _profile.CeilingCoverDepth);
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
        var basin = FractalNoise(worldX / _profile.FloorBasinScale, worldZ / _profile.FloorBasinScale, _seed + _profile.FloorBasinSeedOffset, 4, _profile.FloorBasinPersistence);
        var rough = FractalNoise(worldX / _profile.FloorRoughScale, worldZ / _profile.FloorRoughScale, _seed + _profile.FloorRoughSeedOffset, 3, _profile.FloorRoughPersistence);
        var shelves = Math.Abs(FractalNoise(worldX / _profile.FloorShelfScale, worldZ / _profile.FloorShelfScale, _seed + _profile.FloorShelfSeedOffset, 3, _profile.FloorShelfPersistence) - 0.5) * 2.0;
        return ClampInt(_profile.FloorBaseY + (int)(basin * _profile.FloorBasinAmplitude) + (int)(rough * _profile.FloorRoughAmplitude) + (int)(shelves * _profile.FloorShelfAmplitude), _profile.FloorMinY, _dimension.SpawnY - _profile.FloorSpawnClearance);
    }

    private int CeilingHeight(int worldX, int worldZ, int floorY)
    {
        var dome = FractalNoise(worldX / _profile.CeilingDomeScale, worldZ / _profile.CeilingDomeScale, _seed + _profile.CeilingDomeSeedOffset, 4, _profile.CeilingDomePersistence);
        var teeth = Math.Abs(FractalNoise(worldX / _profile.CeilingTeethScale, worldZ / _profile.CeilingTeethScale, _seed + _profile.CeilingTeethSeedOffset, 3, _profile.CeilingTeethPersistence) - 0.5) * 2.0;
        var squeeze = Math.Max(0.0, FractalNoise(worldX / _profile.CeilingSqueezeScale, worldZ / _profile.CeilingSqueezeScale, _seed + _profile.CeilingSqueezeSeedOffset, 4, _profile.CeilingSqueezePersistence) - _profile.CeilingSqueezeThreshold) / _profile.CeilingSqueezeNormalizer;
        var ceilingY = _profile.CeilingBaseY + (int)(dome * _profile.CeilingDomeAmplitude) - (int)(teeth * _profile.CeilingTeethAmplitude) - (int)(squeeze * squeeze * _profile.CeilingSqueezeAmplitude);
        return ClampInt(Math.Max(ceilingY, floorY + _profile.CeilingMinimumOpenHeight), floorY + _profile.CeilingClampMinimumHeight, Math.Min(_mapSizeY - _profile.CeilingMapTopClearance, _dimension.SpawnY + _profile.CeilingSpawnMaxOffset));
    }

    private bool IsLavaPool(int localX, int localZ, int worldX, int worldZ, int floorY)
    {
        if (_lavaId == 0 || floorY > _profile.LavaMaxFloorY)
        {
            return false;
        }

        if (DistanceToSpawn(localX, localZ) < _profile.LavaSpawnClearRadius)
        {
            return false;
        }

        var poolNoise = FractalNoise(worldX / _profile.LavaPoolScale, worldZ / _profile.LavaPoolScale, _seed + _profile.LavaPoolSeedOffset, 3, _profile.LavaPoolPersistence);
        return poolNoise >= _profile.LavaPoolThreshold;
    }

    private int LavaDepth(int worldX, int worldZ)
    {
        return _profile.LavaDepthBase + (int)(FractalNoise(worldX / _profile.LavaDepthScale, worldZ / _profile.LavaDepthScale, _seed + _profile.LavaDepthSeedOffset, 2, _profile.LavaDepthPersistence) * _profile.LavaDepthAmplitude);
    }

    private void AddSpikes(IChunkColumnWriter writer, BlockPos localPos, int localX, int localZ, int worldX, int worldZ, int floorY, int ceilingY, bool hasLavaPool, double distanceToSpawn)
    {
        if (distanceToSpawn < _profile.FloorSpikeSpawnClearRadius || ceilingY - floorY < _profile.SpikeMinimumOpenHeight)
        {
            return;
        }

        var spikeNoise = FractalNoise(worldX / _profile.FloorSpikeScale, worldZ / _profile.FloorSpikeScale, _seed + _profile.FloorSpikeSeedOffset, 2, _profile.FloorSpikePersistence);
        if (!hasLavaPool && spikeNoise > _profile.FloorSpikeThreshold)
        {
            var height = Math.Min(ceilingY - floorY - _profile.FloorSpikeHeadroom, _profile.FloorSpikeBaseHeight + (int)((spikeNoise - _profile.FloorSpikeThreshold) / _profile.FloorSpikeThresholdRange * _profile.FloorSpikeMaxExtraHeight));
            for (var y = floorY + 1; y <= floorY + height; y++)
            {
                writer.SetBlock(y <= floorY + _profile.SurfaceRoughDepth ? _roughId : _wallId, localPos.Set(localX, y, localZ));
            }
        }

        var ceilingSpikeNoise = FractalNoise(worldX / _profile.CeilingSpikeScale, worldZ / _profile.CeilingSpikeScale, _seed + _profile.CeilingSpikeSeedOffset, 2, _profile.CeilingSpikePersistence);
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

        var fissure = Math.Abs(FractalNoise(worldX / _profile.LavaFallFissureScale, worldZ / _profile.LavaFallFissureScale, _seed + _profile.LavaFallFissureSeedOffset, 3, _profile.LavaFallFissurePersistence) - 0.5) * 2.0;
        var source = FractalNoise(worldX / _profile.LavaFallSourceScale, worldZ / _profile.LavaFallSourceScale, _seed + _profile.LavaFallSourceSeedOffset, 3, _profile.LavaFallSourcePersistence);
        return fissure > _profile.LavaFallFissureThreshold && source > _profile.LavaFallSourceThreshold;
    }

    private double ColumnStrength(int worldX, int worldZ, double distanceToSpawn)
    {
        if (distanceToSpawn < _profile.ColumnSpawnClearRadius)
        {
            return 0.0;
        }

        var broad = FractalNoise(worldX / _profile.ColumnBroadScale, worldZ / _profile.ColumnBroadScale, _seed + _profile.ColumnBroadSeedOffset, 4, _profile.ColumnBroadPersistence);
        var vein = Math.Abs(FractalNoise(worldX / _profile.ColumnVeinScale, worldZ / _profile.ColumnVeinScale, _seed + _profile.ColumnVeinSeedOffset, 3, _profile.ColumnVeinPersistence) - 0.5) * 2.0;
        var detail = FractalNoise(worldX / _profile.ColumnDetailScale, worldZ / _profile.ColumnDetailScale, _seed + _profile.ColumnDetailSeedOffset, 2, _profile.ColumnDetailPersistence);
        return broad * _profile.ColumnBroadWeight + vein * _profile.ColumnVeinWeight + detail * _profile.ColumnDetailWeight;
    }

    private double DistanceToSpawn(int localX, int localZ)
    {
        var dx = _dimension.MinBlockX + localX - _dimension.SpawnX;
        var dz = _dimension.MinBlockZ + localZ - _dimension.SpawnZ;
        return Math.Sqrt(dx * dx + dz * dz);
    }

    private int ApplySpawnPlateau(int height, int localX, int localZ, int floorY, int radius, int blendDistance, double dropPerBlock)
    {
        var worldX = _dimension.MinBlockX + localX;
        var worldZ = _dimension.MinBlockZ + localZ;
        var dx = worldX - _dimension.SpawnX;
        var dz = worldZ - _dimension.SpawnZ;
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

    private void ClearSpawnBubbleAndMark(IChunkColumnWriter writer, int localChunkX, int localChunkZ, int markerBlockId, int markerRadius)
    {
        var chunkSize = GlobalConstants.ChunkSize;
        var chunkMinX = localChunkX * chunkSize;
        var chunkMinZ = localChunkZ * chunkSize;
        var chunkMaxX = chunkMinX + chunkSize - 1;
        var chunkMaxZ = chunkMinZ + chunkSize - 1;
        var centerX = (int)Math.Round(_dimension.SpawnX - _dimension.MinBlockX);
        var centerZ = (int)Math.Round(_dimension.SpawnZ - _dimension.MinBlockZ);
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
                writer.SetBlock(isMarker ? markerBlockId : 0, localPos.Set(x, _dimension.SpawnY - 1, z));

                for (var y = _dimension.SpawnY; y <= _dimension.SpawnY + 5; y++)
                {
                    writer.SetBlock(0, localPos.Set(x, y, z));
                }
            }
        }
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private static double FractalNoise(double x, double z, int seed, int octaves, double persistence)
    {
        return ValueNoise2D.Fractal(x, z, seed, octaves, persistence);
    }
}
