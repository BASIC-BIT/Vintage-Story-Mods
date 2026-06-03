using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;
using Vintagestory.API.MathTools;

namespace DimensionLib.Services;

internal sealed class PreparedDimensionTracker
{
    private readonly HashSet<string> _preparedDimensionIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<long>> _preparedChunkKeysByDimensionId = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);

    public bool IsDimensionPrepared(string dimensionId)
    {
        return !string.IsNullOrWhiteSpace(dimensionId) && _preparedDimensionIds.Contains(dimensionId.Trim());
    }

    public void MarkDimensionPrepared(string dimensionId)
    {
        if (!string.IsNullOrWhiteSpace(dimensionId))
        {
            _preparedDimensionIds.Add(dimensionId.Trim());
        }
    }

    public void UnmarkDimension(string dimensionId)
    {
        if (!string.IsNullOrWhiteSpace(dimensionId))
        {
            _preparedDimensionIds.Remove(dimensionId.Trim());
        }
    }

    public void RemoveDimension(string dimensionId)
    {
        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return;
        }

        dimensionId = dimensionId.Trim();
        _preparedDimensionIds.Remove(dimensionId);
        _preparedChunkKeysByDimensionId.Remove(dimensionId);
    }

    public void MarkAllChunksPrepared(Dimension dimension)
    {
        for (var localChunkX = 0; localChunkX < dimension.ChunkSizeX; localChunkX++)
        {
            for (var localChunkZ = 0; localChunkZ < dimension.ChunkSizeZ; localChunkZ++)
            {
                MarkChunkPrepared(dimension.DimensionId, localChunkX, localChunkZ);
            }
        }
    }

    public void MarkChunkPrepared(string dimensionId, int localChunkX, int localChunkZ)
    {
        if (!_preparedChunkKeysByDimensionId.TryGetValue(dimensionId, out var preparedChunks))
        {
            preparedChunks = new HashSet<long>();
            _preparedChunkKeysByDimensionId[dimensionId] = preparedChunks;
        }

        preparedChunks.Add(ChunkKey(localChunkX, localChunkZ));
    }

    public bool IsChunkPrepared(string dimensionId, int localChunkX, int localChunkZ)
    {
        return _preparedChunkKeysByDimensionId.TryGetValue(dimensionId, out var preparedChunks) && preparedChunks.Contains(ChunkKey(localChunkX, localChunkZ));
    }

    public int GetPreparedChunkCount(Dimension dimension)
    {
        return _preparedChunkKeysByDimensionId.TryGetValue(dimension.DimensionId, out var preparedChunks) ? preparedChunks.Count : 0;
    }

    public List<long> GetPreparedChunkKeys(Dimension dimension)
    {
        return _preparedChunkKeysByDimensionId.TryGetValue(dimension.DimensionId, out var preparedChunks)
            ? preparedChunks.OrderBy(key => key).ToList()
            : new List<long>();
    }

    public void LoadPreparedChunks(Dimension dimension, IEnumerable<long> preparedChunkKeys)
    {
        if (dimension == null || string.IsNullOrWhiteSpace(dimension.DimensionId) || preparedChunkKeys == null)
        {
            return;
        }

        var preparedChunks = new HashSet<long>();
        foreach (var key in preparedChunkKeys)
        {
            DecodeChunkKey(key, out var localChunkX, out var localChunkZ);
            if (localChunkX >= 0 && localChunkX < dimension.ChunkSizeX && localChunkZ >= 0 && localChunkZ < dimension.ChunkSizeZ)
            {
                preparedChunks.Add(key);
            }
        }

        if (preparedChunks.Count == 0)
        {
            return;
        }

        _preparedChunkKeysByDimensionId[dimension.DimensionId] = preparedChunks;
        _preparedDimensionIds.Add(dimension.DimensionId);
    }

    public bool TryGetPartialPreparedLocalChunks(Dimension dimension, out Vec2i[] chunks)
    {
        chunks = null;
        if (!_preparedChunkKeysByDimensionId.TryGetValue(dimension.DimensionId, out var preparedChunks) || preparedChunks.Count <= 0 || preparedChunks.Count >= dimension.ChunkSizeX * dimension.ChunkSizeZ)
        {
            return false;
        }

        chunks = DecodeChunkKeys(preparedChunks).ToArray();
        return true;
    }

    public IEnumerable<Vec2i> GetPreparedLocalChunks(Dimension dimension)
    {
        if (_preparedChunkKeysByDimensionId.TryGetValue(dimension.DimensionId, out var preparedChunks) && preparedChunks.Count > 0)
        {
            foreach (var key in preparedChunks.OrderBy(key => key))
            {
                DecodeChunkKey(key, out var localChunkX, out var localChunkZ);
                yield return new Vec2i(localChunkX, localChunkZ);
            }

            yield break;
        }

        if (!IsDimensionPrepared(dimension.DimensionId))
        {
            yield break;
        }

        for (var localChunkX = 0; localChunkX < dimension.ChunkSizeX; localChunkX++)
        {
            for (var localChunkZ = 0; localChunkZ < dimension.ChunkSizeZ; localChunkZ++)
            {
                yield return new Vec2i(localChunkX, localChunkZ);
            }
        }
    }

    private static IEnumerable<Vec2i> DecodeChunkKeys(IEnumerable<long> keys)
    {
        foreach (var key in keys)
        {
            DecodeChunkKey(key, out var localChunkX, out var localChunkZ);
            yield return new Vec2i(localChunkX, localChunkZ);
        }
    }

    private static long ChunkKey(int localChunkX, int localChunkZ)
    {
        return ((long)localChunkX << 32) | (uint)localChunkZ;
    }

    private static void DecodeChunkKey(long key, out int localChunkX, out int localChunkZ)
    {
        localChunkX = (int)(key >> 32);
        localChunkZ = (int)key;
    }
}
