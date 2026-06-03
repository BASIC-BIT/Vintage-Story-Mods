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
        NormalizeVisualSettings(spec.VisualSettings);

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

    private static void NormalizeVisualSettings(DimensionVisualSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        settings.Sky.Color.Red = Clamp01(settings.Sky.Color.Red);
        settings.Sky.Color.Green = Clamp01(settings.Sky.Color.Green);
        settings.Sky.Color.Blue = Clamp01(settings.Sky.Color.Blue);
        settings.Sky.Color.Alpha = Clamp01(settings.Sky.Color.Alpha);
        settings.Fog.Color.Value.Red = Clamp01(settings.Fog.Color.Value.Red);
        settings.Fog.Color.Value.Green = Clamp01(settings.Fog.Color.Value.Green);
        settings.Fog.Color.Value.Blue = Clamp01(settings.Fog.Color.Value.Blue);
        settings.Fog.Color.Weight = Clamp01(settings.Fog.Color.Weight);
        settings.Ambient.Color.Value.Red = Clamp01(settings.Ambient.Color.Value.Red);
        settings.Ambient.Color.Value.Green = Clamp01(settings.Ambient.Color.Value.Green);
        settings.Ambient.Color.Value.Blue = Clamp01(settings.Ambient.Color.Value.Blue);
        settings.Ambient.Color.Weight = Clamp01(settings.Ambient.Color.Weight);
        settings.Fog.Density.Value = Math.Max(0f, settings.Fog.Density.Value);
        settings.Fog.Density.Weight = Clamp01(settings.Fog.Density.Weight);
        settings.Fog.FlatDensity.Value = Math.Max(0f, settings.Fog.FlatDensity.Value);
        settings.Fog.FlatDensity.Weight = Clamp01(settings.Fog.FlatDensity.Weight);
        settings.Clouds.Density.Value = Clamp01(settings.Clouds.Density.Value);
        settings.Clouds.Density.Weight = Clamp01(settings.Clouds.Density.Weight);
        settings.Clouds.Brightness.Value = Clamp01(settings.Clouds.Brightness.Value);
        settings.Clouds.Brightness.Weight = Clamp01(settings.Clouds.Brightness.Weight);
        settings.Scene.Brightness.Value = Math.Max(0f, settings.Scene.Brightness.Value);
        settings.Scene.Brightness.Weight = Clamp01(settings.Scene.Brightness.Weight);
        settings.Fog.Brightness.Value = Math.Max(0f, settings.Fog.Brightness.Value);
        settings.Fog.Brightness.Weight = Clamp01(settings.Fog.Brightness.Weight);
        settings.Scene.MinimumLight = ClampFloat(settings.Scene.MinimumLight, 0f, 0.8f);
        settings.Scene.LightLift.Red = Clamp01(settings.Scene.LightLift.Red);
        settings.Scene.LightLift.Green = Clamp01(settings.Scene.LightLift.Green);
        settings.Scene.LightLift.Blue = Clamp01(settings.Scene.LightLift.Blue);
        settings.LerpSpeed = ClampFloat(settings.LerpSpeed <= 0f ? 0.08f : settings.LerpSpeed, 0.001f, 10f);
    }

    private static float Clamp01(float value)
    {
        return ClampFloat(value, 0f, 1f);
    }

    private static float ClampFloat(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }

}
