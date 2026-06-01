using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;
using Vintagestory.API.MathTools;

namespace DimensionLib.Services;

internal sealed class DimensionRegistry
{
    private readonly int _firstAllowedDimensionPlaneId;
    private readonly Dictionary<string, Dimension> _dimensionsById = new Dictionary<string, Dimension>(StringComparer.Ordinal);
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
            _dimensionsById[updated.DimensionId] = updated;
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

        var dimension = _dimensionsById.Values.FirstOrDefault(candidate => candidate.ContainsBlock(pos));
        return dimension != null
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

        dimension = _dimensionsById.Values.FirstOrDefault(candidate => candidate.ContainsBlock(pos));
        return dimension != null;
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
        _dimensionsById.Remove(dimensionId);
        _orphanedDimensionIds.Remove(dimensionId);
    }

    public void Load(Dimension dimension, bool isOrphaned)
    {
        _dimensionsById[dimension.DimensionId] = dimension;
        if (isOrphaned)
        {
            _orphanedDimensionIds.Add(dimension.DimensionId);
        }
    }
}
