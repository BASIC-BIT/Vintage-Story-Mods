using System;
using DimensionLib.Api;
using DimensionLib.Core;

namespace DimensionLib.Lab;

internal static class BuiltInTestDimensionFactory
{
    public const string DebugDimensionId = "dimensionlib:debug-spike";
    public const string OverworldOppositeDimensionId = "dimensionlib:test-overworld-opposite";
    public const string NetherCavernDimensionId = "dimensionlib:test-nether-cavern";

    public static DimensionSpec DebugDimensionSpec()
    {
        return new DimensionSpec
        {
            DimensionId = DebugDimensionId,
            OwnerModId = DimensionLibModSystem.ModId,
            DimensionPlaneId = DimensionLibModSystem.FirstPrototypeDimension,
            Placement = DimensionPlacement.Explicit,
            ChunkX = 0,
            ChunkZ = 0,
            ChunkSizeX = 3,
            ChunkSizeZ = 3,
            SpawnY = 92,
            Kind = DimensionKind.Debug,
            AccessPolicy = DimensionAccessPolicy.AdminOnly,
            Mutability = DimensionMutability.Mutable,
            IsTransient = true,
        };
    }

    public static bool IsBuiltInTestDimension(Dimension dimension, string normalizedTestId)
    {
        return string.Equals(dimension.OwnerModId, DimensionLibModSystem.ModId, StringComparison.Ordinal) &&
            ((normalizedTestId == "overworld-opposite" && string.Equals(dimension.GeneratorId, DimensionGeneratorIds.OverworldOpposite, StringComparison.Ordinal)) ||
            (normalizedTestId == "nether-cavern" && string.Equals(dimension.GeneratorId, DimensionGeneratorIds.NetherCavern, StringComparison.Ordinal)));
    }

    public static bool TryCreateTestDimensionSpec(string testId, string dimensionId, int? sizeChunks, long? seed, out DimensionSpec spec, out string normalizedTestId)
    {
        normalizedTestId = (testId ?? string.Empty).Trim().ToLowerInvariant();
        var requestedSize = sizeChunks.HasValue ? Math.Max(3, Math.Min(16, sizeChunks.Value)) : (int?)null;
        switch (normalizedTestId)
        {
            case "overworld":
            case "opposite":
            case "overworld-opposite":
                normalizedTestId = "overworld-opposite";
                var overworldSize = requestedSize ?? 5;
                spec = new DimensionSpec
                {
                    DimensionId = string.IsNullOrWhiteSpace(dimensionId) ? OverworldOppositeDimensionId : dimensionId.Trim(),
                    OwnerModId = DimensionLibModSystem.ModId,
                    DimensionPlaneId = DimensionLibModSystem.FirstPrototypeDimension,
                    ChunkX = 8,
                    ChunkZ = 0,
                    ChunkSizeX = overworldSize,
                    ChunkSizeZ = overworldSize,
                    SpawnY = 108,
                    GeneratorId = DimensionGeneratorIds.OverworldOpposite,
                    VisualSettings = CreateOppositeDayVisualSettings(),
                    Seed = seed ?? 2026052901,
                    Kind = DimensionKind.Pocket,
                    AccessPolicy = DimensionAccessPolicy.AdminOnly,
                    Mutability = DimensionMutability.Mutable,
                    IsTransient = true,
                };
                return true;

            case "nether":
            case "cavern":
            case "nether-cavern":
                normalizedTestId = "nether-cavern";
                var netherSize = requestedSize ?? 9;
                spec = new DimensionSpec
                {
                    DimensionId = string.IsNullOrWhiteSpace(dimensionId) ? NetherCavernDimensionId : dimensionId.Trim(),
                    OwnerModId = DimensionLibModSystem.ModId,
                    DimensionPlaneId = DimensionLibModSystem.FirstPrototypeDimension,
                    ChunkX = 16,
                    ChunkZ = 0,
                    ChunkSizeX = netherSize,
                    ChunkSizeZ = netherSize,
                    SpawnY = 68,
                    GeneratorId = DimensionGeneratorIds.NetherCavern,
                    VisualSettings = CreateNetherCavernVisualSettings(),
                    Seed = seed ?? 2026052902,
                    Kind = DimensionKind.Pocket,
                    AccessPolicy = DimensionAccessPolicy.AdminOnly,
                    Mutability = DimensionMutability.Mutable,
                    IsTransient = true,
                };
                return true;

            default:
                spec = null;
                return false;
        }
    }

    private static DimensionVisualSettings CreateOppositeDayVisualSettings()
    {
        return new DimensionVisualSettings
        {
            FogRed = 0.12f,
            FogGreen = 0.14f,
            FogBlue = 0.28f,
            FogColorWeight = 0.65f,
            AmbientRed = 0.2f,
            AmbientGreen = 0.22f,
            AmbientBlue = 0.42f,
            AmbientColorWeight = 0.55f,
            FogDensity = 0.026f,
            FogDensityWeight = 0.45f,
            FlatFogDensity = 0.025f,
            FlatFogDensityWeight = 0.45f,
            CloudDensity = 0.9f,
            CloudDensityWeight = 0.5f,
            CloudBrightness = 0.35f,
            CloudBrightnessWeight = 0.35f,
        };
    }

    private static DimensionVisualSettings CreateNetherCavernVisualSettings()
    {
        return new DimensionVisualSettings
        {
            RenderSkyCover = true,
            SkyRed = 0.035f,
            SkyGreen = 0.0035f,
            SkyBlue = 0.002f,
            SkyAlpha = 1f,
            FogRed = 0.24f,
            FogGreen = 0.045f,
            FogBlue = 0.018f,
            FogColorWeight = 0.16f,
            AmbientRed = 0.74f,
            AmbientGreen = 0.34f,
            AmbientBlue = 0.2f,
            AmbientColorWeight = 0.48f,
            FogDensity = 0.0016f,
            FogDensityWeight = 0.16f,
            CloudDensityWeight = 0.7f,
            CloudBrightnessWeight = 0.7f,
            SceneBrightness = 1.0f,
            SceneBrightnessWeight = 0.45f,
            FogBrightness = 0.95f,
            FogBrightnessWeight = 0.2f,
            MinimumSceneLight = 0.08f,
            LightLiftRed = 0.85f,
            LightLiftGreen = 0.42f,
            LightLiftBlue = 0.24f,
        };
    }
}
