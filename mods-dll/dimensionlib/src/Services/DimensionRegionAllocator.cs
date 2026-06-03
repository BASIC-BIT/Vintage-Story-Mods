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
                var candidate = ToDimensionAt(spec, x, z);
                if (!existing.Any(dimension => DimensionSpecValidator.RegionsOverlap(dimension, candidate)))
                {
                    spec.ChunkX = x;
                    spec.ChunkZ = z;
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
                var chunkX = SparseStartChunk + slotX * stride;
                var chunkZ = SparseStartChunk + slotZ * stride;
                var candidate = ToDimensionAt(spec, chunkX, chunkZ);
                if (!existing.Any(dimension => DimensionSpecValidator.RegionsOverlap(dimension, candidate)))
                {
                    spec.ChunkX = chunkX;
                    spec.ChunkZ = chunkZ;
                    return true;
                }
            }
        }

        return false;
    }

    private static Dimension ToDimensionAt(DimensionSpec spec, int chunkX, int chunkZ)
    {
        return new Dimension(
            spec.DimensionId,
            spec.OwnerModId,
            spec.DimensionPlaneId,
            chunkX,
            chunkZ,
            spec.ChunkSizeX,
            spec.ChunkSizeZ,
            spec.SpawnY,
            spec.GeneratorId,
            spec.VisualSettings?.Clone(),
            spec.Seed,
            spec.Kind,
            spec.AccessPolicy,
            spec.Mutability,
            spec.IsTransient);
    }
}
