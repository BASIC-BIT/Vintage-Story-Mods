using System;
using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;

namespace DimensionLib.Services;

internal static class DimensionRegionAllocator
{
    private const int SparseStartChunk = 1024;
    private const int SparseStrideChunks = 1024;
    private const int SparseSlotsPerAxis = 1024;

    public static bool TryAssignAvailableRegion(DimensionSpec spec, IEnumerable<Dimension> existingDimensions, int maxChunkCoordinate = 512)
    {
        var existing = existingDimensions?.ToArray() ?? Array.Empty<Dimension>();
        var step = Math.Max(spec.ChunkSizeX, spec.ChunkSizeZ) + 1;
        for (var z = 0; z <= maxChunkCoordinate; z += step)
        {
            for (var x = 0; x <= maxChunkCoordinate; x += step)
            {
                spec.ChunkX = x;
                spec.ChunkZ = z;
                var candidate = spec.ToDimension();
                if (!existing.Any(dimension => DimensionSpecValidator.RegionsOverlap(dimension, candidate)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool TryAssignSparseRegion(DimensionSpec spec, IEnumerable<Dimension> existingDimensions)
    {
        var existing = existingDimensions?.ToArray() ?? Array.Empty<Dimension>();
        var stride = Math.Max(SparseStrideChunks, Math.Max(spec.ChunkSizeX, spec.ChunkSizeZ) + 1);
        for (var slotZ = 0; slotZ < SparseSlotsPerAxis; slotZ++)
        {
            for (var slotX = 0; slotX < SparseSlotsPerAxis; slotX++)
            {
                spec.ChunkX = SparseStartChunk + slotX * stride;
                spec.ChunkZ = SparseStartChunk + slotZ * stride;
                var candidate = spec.ToDimension();
                if (!existing.Any(dimension => DimensionSpecValidator.RegionsOverlap(dimension, candidate)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
