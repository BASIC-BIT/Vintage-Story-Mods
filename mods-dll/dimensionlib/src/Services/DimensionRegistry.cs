using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace DimensionLib.Services;

internal sealed class DimensionRegistry
{
    private readonly int _firstAllowedDimensionPlaneId;
    private readonly Dictionary<string, Dimension> _dimensionsById = new Dictionary<string, Dimension>(StringComparer.Ordinal);
    private readonly Dictionary<int, Dictionary<long, Dimension>> _dimensionsByChunkByPlaneId = new Dictionary<int, Dictionary<long, Dimension>>();
    private readonly HashSet<string> _orphanedDimensionIds = new HashSet<string>(StringComparer.Ordinal);

    public DimensionRegistry(int firstAllowedDimensionPlaneId)
    {
        _firstAllowedDimensionPlaneId = firstAllowedDimensionPlaneId;
    }

    public IReadOnlyCollection<Dimension> Dimensions => _dimensionsById.Values.ToArray();

    public IEnumerable<Dimension> Values => _dimensionsById.Values;

    public Dimension GetRequired(string dimensionId)
    {
        return _dimensionsById[dimensionId];
    }

    public DimensionLibResult<Dimension> Register(DimensionSpec spec)
    {
        var validation = DimensionSpecValidator.Validate(spec, _firstAllowedDimensionPlaneId);
        if (!validation.Success)
        {
            return DimensionLibResult<Dimension>.Fail(validation.Message, validation.ErrorCode);
        }

        if (_dimensionsById.TryGetValue(spec.DimensionId, out var existing))
        {
            if (!DimensionSpecValidator.SameClaim(existing, spec))
            {
                return DimensionLibResult<Dimension>.Fail($"Dimension id '{spec.DimensionId}' is already registered with different bounds or ownership.", "dimension-id-conflict");
            }

            var updated = spec.ToDimension();
            RemoveFromSpatialIndex(existing);
            _dimensionsById[updated.DimensionId] = updated;
            AddToSpatialIndex(updated);
            _orphanedDimensionIds.Remove(updated.DimensionId);
            return DimensionLibResult<Dimension>.Ok(updated, "Dimension already registered; metadata refreshed.");
        }

        var dimension = spec.ToDimension();
        var collision = _dimensionsById.Values.FirstOrDefault(other => DimensionSpecValidator.RegionsOverlap(other, dimension));
        if (collision != null)
        {
            return DimensionLibResult<Dimension>.Fail($"Dimension '{dimension.DimensionId}' overlaps existing dimension '{collision.DimensionId}'.", "dimension-overlap");
        }

        _dimensionsById.Add(dimension.DimensionId, dimension);
        AddToSpatialIndex(dimension);
        _orphanedDimensionIds.Remove(dimension.DimensionId);
        return DimensionLibResult<Dimension>.Ok(dimension, "Dimension registered.");
    }

    public DimensionLibResult<Dimension> Get(string dimensionId)
    {
        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return DimensionLibResult<Dimension>.Fail("Dimension id is required.", "missing-dimension-id");
        }

        return _dimensionsById.TryGetValue(dimensionId.Trim(), out var dimension)
            ? DimensionLibResult<Dimension>.Ok(dimension)
            : DimensionLibResult<Dimension>.Fail($"Dimension '{dimensionId}' is not registered.", "unknown-dimension");
    }

    public DimensionLibResult<Dimension> GetAt(BlockPos pos)
    {
        if (pos == null)
        {
            return DimensionLibResult<Dimension>.Fail("Position is required.", "missing-position");
        }

        return TryGetIndexedAt(pos, out var dimension)
            ? DimensionLibResult<Dimension>.Ok(dimension)
            : DimensionLibResult<Dimension>.Fail("No DimensionLib dimension contains that position.", "no-dimension-at-position");
    }

    public bool TryGet(string dimensionId, out Dimension dimension)
    {
        dimension = null;
        return !string.IsNullOrWhiteSpace(dimensionId) && _dimensionsById.TryGetValue(dimensionId.Trim(), out dimension);
    }

    public bool TryGetAt(BlockPos pos, out Dimension dimension)
    {
        dimension = null;
        if (pos == null)
        {
            return false;
        }

        return TryGetIndexedAt(pos, out dimension);
    }

    public bool IsOrphaned(string dimensionId)
    {
        return !string.IsNullOrWhiteSpace(dimensionId) && _orphanedDimensionIds.Contains(dimensionId.Trim());
    }

    public void MarkOrphaned(string dimensionId)
    {
        if (!string.IsNullOrWhiteSpace(dimensionId))
        {
            _orphanedDimensionIds.Add(dimensionId.Trim());
        }
    }

    public void RemoveOrphaned(string dimensionId)
    {
        if (!string.IsNullOrWhiteSpace(dimensionId))
        {
            _orphanedDimensionIds.Remove(dimensionId.Trim());
        }
    }

    public void Remove(string dimensionId)
    {
        if (string.IsNullOrWhiteSpace(dimensionId))
        {
            return;
        }

        dimensionId = dimensionId.Trim();
        if (_dimensionsById.TryGetValue(dimensionId, out var existing))
        {
            RemoveFromSpatialIndex(existing);
        }

        _dimensionsById.Remove(dimensionId);
        _orphanedDimensionIds.Remove(dimensionId);
    }

    public void Load(Dimension dimension, bool isOrphaned)
    {
        if (_dimensionsById.TryGetValue(dimension.DimensionId, out var existing))
        {
            RemoveFromSpatialIndex(existing);
        }

        _dimensionsById[dimension.DimensionId] = dimension;
        AddToSpatialIndex(dimension);
        if (isOrphaned)
        {
            _orphanedDimensionIds.Add(dimension.DimensionId);
        }
    }

    private bool TryGetIndexedAt(BlockPos pos, out Dimension dimension)
    {
        dimension = null;
        if (pos == null || !_dimensionsByChunkByPlaneId.TryGetValue(pos.dimension, out var dimensionsByChunk))
        {
            return false;
        }

        var chunkX = FloorDiv(pos.X, GlobalConstants.ChunkSize);
        var chunkZ = FloorDiv(pos.Z, GlobalConstants.ChunkSize);
        if (!dimensionsByChunk.TryGetValue(ChunkKey(chunkX, chunkZ), out var candidate) || !candidate.ContainsBlock(pos))
        {
            return false;
        }

        dimension = candidate;
        return true;
    }

    private void AddToSpatialIndex(Dimension dimension)
    {
        if (!_dimensionsByChunkByPlaneId.TryGetValue(dimension.DimensionPlaneId, out var dimensionsByChunk))
        {
            dimensionsByChunk = new Dictionary<long, Dimension>();
            _dimensionsByChunkByPlaneId[dimension.DimensionPlaneId] = dimensionsByChunk;
        }

        for (var chunkX = dimension.ChunkX; chunkX < dimension.ChunkX + dimension.ChunkSizeX; chunkX++)
        {
            for (var chunkZ = dimension.ChunkZ; chunkZ < dimension.ChunkZ + dimension.ChunkSizeZ; chunkZ++)
            {
                dimensionsByChunk[ChunkKey(chunkX, chunkZ)] = dimension;
            }
        }
    }

    private void RemoveFromSpatialIndex(Dimension dimension)
    {
        if (!_dimensionsByChunkByPlaneId.TryGetValue(dimension.DimensionPlaneId, out var dimensionsByChunk))
        {
            return;
        }

        for (var chunkX = dimension.ChunkX; chunkX < dimension.ChunkX + dimension.ChunkSizeX; chunkX++)
        {
            for (var chunkZ = dimension.ChunkZ; chunkZ < dimension.ChunkZ + dimension.ChunkSizeZ; chunkZ++)
            {
                dimensionsByChunk.Remove(ChunkKey(chunkX, chunkZ));
            }
        }

        if (dimensionsByChunk.Count == 0)
        {
            _dimensionsByChunkByPlaneId.Remove(dimension.DimensionPlaneId);
        }
    }

    private static int FloorDiv(int value, int divisor)
    {
        return value >= 0 ? value / divisor : (value - divisor + 1) / divisor;
    }

    private static long ChunkKey(int chunkX, int chunkZ)
    {
        return ((long)chunkX << 32) | (uint)chunkZ;
    }
}
