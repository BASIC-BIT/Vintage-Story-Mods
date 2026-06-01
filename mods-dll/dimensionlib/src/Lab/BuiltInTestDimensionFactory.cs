using System;
using DimensionLib.Api;
using DimensionLib.Core;
using DimensionLib.Lighting;

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
            VisualProfileId = DimensionVisualProfileIds.Debug,
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
            case "opposite-day":
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
                    VisualProfileId = DimensionVisualProfileIds.OppositeDay,
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
                    VisualProfileId = DimensionVisualProfileIds.NetherCavern,
                    MinimumSceneLight = DimensionLightPolicy.NetherCavern.MinimumSceneLight,
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
}
