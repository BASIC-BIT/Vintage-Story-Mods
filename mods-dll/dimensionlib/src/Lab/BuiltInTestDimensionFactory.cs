using System;
using DimensionLib.Api;
using DimensionLib.Core;

namespace DimensionLib.Lab;

internal static class BuiltInTestDimensionFactory
{
    public const string DebugDimensionId = "dimensionlib:debug-spike";
    public const string OverworldOppositeDimensionId = "dimensionlib:test-overworld-opposite";
    public const string VanillaOverworldWindowDimensionId = "dimensionlib:test-vanilla-overworld-window";

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
            AccessPolicy = DimensionAccessPolicy.AdminOnly,
            Mutability = DimensionMutability.Mutable,
            IsTransient = true,
        };
    }

    public static bool IsBuiltInTestDimension(Dimension dimension, string normalizedTestId)
    {
        return string.Equals(dimension.OwnerModId, DimensionLibModSystem.ModId, StringComparison.Ordinal) &&
            ((normalizedTestId == "overworld-opposite" && string.Equals(dimension.GeneratorId, DimensionGeneratorIds.OverworldOpposite, StringComparison.Ordinal)) ||
            (normalizedTestId == "vanilla-overworld" && string.Equals(dimension.GeneratorId, DimensionGeneratorIds.StandardOverworldWindow, StringComparison.Ordinal)));
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
                    AccessPolicy = DimensionAccessPolicy.AdminOnly,
                    Mutability = DimensionMutability.Mutable,
                    IsTransient = true,
                };
                return true;

            case "vanilla":
            case "vanilla-overworld":
            case "standard-overworld":
            case "overworld-window":
                normalizedTestId = "vanilla-overworld";
                var vanillaSize = sizeChunks.HasValue ? Math.Max(3, Math.Min(4096, sizeChunks.Value)) : 256;
                spec = new DimensionSpec
                {
                    DimensionId = string.IsNullOrWhiteSpace(dimensionId) ? VanillaOverworldWindowDimensionId : dimensionId.Trim(),
                    OwnerModId = DimensionLibModSystem.ModId,
                    DimensionPlaneId = DimensionLibModSystem.FirstPrototypeDimension + 1,
                    Placement = DimensionPlacement.Explicit,
                    ChunkX = 0,
                    ChunkZ = 0,
                    ChunkSizeX = vanillaSize,
                    ChunkSizeZ = vanillaSize,
                    SpawnY = 160,
                    GeneratorId = DimensionGeneratorIds.StandardOverworldWindow,
                    Seed = seed ?? 0,
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
}
