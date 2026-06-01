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

        settings.SkyRed = Clamp01(settings.SkyRed);
        settings.SkyGreen = Clamp01(settings.SkyGreen);
        settings.SkyBlue = Clamp01(settings.SkyBlue);
        settings.SkyAlpha = Clamp01(settings.SkyAlpha);
        settings.FogRed = Clamp01(settings.FogRed);
        settings.FogGreen = Clamp01(settings.FogGreen);
        settings.FogBlue = Clamp01(settings.FogBlue);
        settings.FogColorWeight = Clamp01(settings.FogColorWeight);
        settings.AmbientRed = Clamp01(settings.AmbientRed);
        settings.AmbientGreen = Clamp01(settings.AmbientGreen);
        settings.AmbientBlue = Clamp01(settings.AmbientBlue);
        settings.AmbientColorWeight = Clamp01(settings.AmbientColorWeight);
        settings.FogDensity = Math.Max(0f, settings.FogDensity);
        settings.FogDensityWeight = Clamp01(settings.FogDensityWeight);
        settings.FlatFogDensity = Math.Max(0f, settings.FlatFogDensity);
        settings.FlatFogDensityWeight = Clamp01(settings.FlatFogDensityWeight);
        settings.CloudDensity = Clamp01(settings.CloudDensity);
        settings.CloudDensityWeight = Clamp01(settings.CloudDensityWeight);
        settings.CloudBrightness = Clamp01(settings.CloudBrightness);
        settings.CloudBrightnessWeight = Clamp01(settings.CloudBrightnessWeight);
        settings.SceneBrightness = Math.Max(0f, settings.SceneBrightness);
        settings.SceneBrightnessWeight = Clamp01(settings.SceneBrightnessWeight);
        settings.FogBrightness = Math.Max(0f, settings.FogBrightness);
        settings.FogBrightnessWeight = Clamp01(settings.FogBrightnessWeight);
        settings.MinimumSceneLight = ClampFloat(settings.MinimumSceneLight, 0f, 0.8f);
        settings.LightLiftRed = Clamp01(settings.LightLiftRed);
        settings.LightLiftGreen = Clamp01(settings.LightLiftGreen);
        settings.LightLiftBlue = Clamp01(settings.LightLiftBlue);
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
