using System;
using DimensionLib.Api;

namespace DimensionLib.Services;

internal static class DimensionSpecValidator
{
    public static DimensionLibResult Validate(DimensionSpec spec, int firstAllowedDimensionPlaneId)
    {
        if (spec == null)
        {
            return DimensionLibResult.Fail("Dimension spec is required.", "missing-dimension-spec");
        }

        spec.DimensionId = spec.DimensionId?.Trim();
        spec.OwnerModId = spec.OwnerModId?.Trim();
        spec.GeneratorId = spec.GeneratorId?.Trim();
        spec.VisualProfileId = spec.VisualProfileId?.Trim();
        spec.MinimumSceneLight = ClampFloat(spec.MinimumSceneLight, 0f, 0.8f);

        if (string.IsNullOrWhiteSpace(spec.DimensionId))
        {
            return DimensionLibResult.Fail("Dimension id is required.", "missing-dimension-id");
        }

        if (string.IsNullOrWhiteSpace(spec.OwnerModId))
        {
            return DimensionLibResult.Fail("Owner mod id is required.", "missing-owner-mod-id");
        }

        if (spec.DimensionPlaneId < firstAllowedDimensionPlaneId)
        {
            return DimensionLibResult.Fail("DimensionLib dimensions must not use dimension planes 0, 1, or 2.", "reserved-dimension-plane");
        }

        if (spec.ChunkSizeX <= 0 || spec.ChunkSizeZ <= 0)
        {
            return DimensionLibResult.Fail("Dimension region chunk sizes must be positive.", "invalid-size");
        }

        return DimensionLibResult.Ok();
    }

    public static bool RegionsOverlap(Dimension left, Dimension right)
    {
        return left.DimensionPlaneId == right.DimensionPlaneId &&
            left.ChunkX < right.ChunkX + right.ChunkSizeX &&
            left.ChunkX + left.ChunkSizeX > right.ChunkX &&
            left.ChunkZ < right.ChunkZ + right.ChunkSizeZ &&
            left.ChunkZ + left.ChunkSizeZ > right.ChunkZ;
    }

    public static bool SameClaim(Dimension existing, DimensionSpec spec)
    {
        return string.Equals(existing.OwnerModId, spec.OwnerModId, StringComparison.Ordinal) &&
            existing.DimensionPlaneId == spec.DimensionPlaneId &&
            existing.ChunkX == spec.ChunkX &&
            existing.ChunkZ == spec.ChunkZ &&
            existing.ChunkSizeX == spec.ChunkSizeX &&
            existing.ChunkSizeZ == spec.ChunkSizeZ &&
            string.Equals(existing.GeneratorId, spec.GeneratorId, StringComparison.Ordinal) &&
            existing.Seed == spec.Seed;
    }

    private static float ClampFloat(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
