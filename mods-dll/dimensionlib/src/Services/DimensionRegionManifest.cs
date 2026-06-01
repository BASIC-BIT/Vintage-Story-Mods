using System.Collections.Generic;
using DimensionLib.Api;

namespace DimensionLib.Services;

public sealed class DimensionRegionManifest
{
    public int SchemaVersion { get; set; } = 1;

    public List<DimensionRegionManifestEntry> Dimensions { get; set; } = new List<DimensionRegionManifestEntry>();
}

public sealed class DimensionRegionManifestEntry
{
    public string DimensionId { get; set; }

    public string OwnerModId { get; set; }

    public int DimensionPlaneId { get; set; }

    public int ChunkX { get; set; }

    public int ChunkZ { get; set; }

    public int ChunkSizeX { get; set; }

    public int ChunkSizeZ { get; set; }

    public int SpawnY { get; set; }

    public string GeneratorId { get; set; }

    public string VisualProfileId { get; set; }

    public long Seed { get; set; }

    public DimensionKind Kind { get; set; }

    public DimensionAccessPolicy AccessPolicy { get; set; }

    public DimensionMutability Mutability { get; set; }

    public bool IsTransient { get; set; }

    public float MinimumSceneLight { get; set; }

    public bool IsOrphaned { get; set; }

    public List<long> PreparedChunkKeys { get; set; } = new List<long>();

    public string CreatedUtc { get; set; }

    public string UpdatedUtc { get; set; }

    public static DimensionRegionManifestEntry FromDimension(Dimension dimension, bool isOrphaned, string nowUtc, DimensionRegionManifestEntry existing = null)
    {
        return new DimensionRegionManifestEntry
        {
            DimensionId = dimension.DimensionId,
            OwnerModId = dimension.OwnerModId,
            DimensionPlaneId = dimension.DimensionPlaneId,
            ChunkX = dimension.ChunkX,
            ChunkZ = dimension.ChunkZ,
            ChunkSizeX = dimension.ChunkSizeX,
            ChunkSizeZ = dimension.ChunkSizeZ,
            SpawnY = dimension.SpawnY,
            GeneratorId = dimension.GeneratorId,
            VisualProfileId = dimension.VisualProfileId,
            Seed = dimension.Seed,
            Kind = dimension.Kind,
            AccessPolicy = dimension.AccessPolicy,
            Mutability = dimension.Mutability,
            IsTransient = dimension.IsTransient,
            MinimumSceneLight = dimension.MinimumSceneLight,
            IsOrphaned = isOrphaned,
            CreatedUtc = string.IsNullOrWhiteSpace(existing?.CreatedUtc) ? nowUtc : existing.CreatedUtc,
            UpdatedUtc = nowUtc,
        };
    }

    public Dimension ToDimension()
    {
        return new Dimension(
            DimensionId,
            OwnerModId,
            DimensionPlaneId,
            ChunkX,
            ChunkZ,
            ChunkSizeX,
            ChunkSizeZ,
            SpawnY,
            GeneratorId,
            VisualProfileId,
            Seed,
            Kind,
            AccessPolicy,
            Mutability,
            IsTransient,
            MinimumSceneLight);
    }
}
