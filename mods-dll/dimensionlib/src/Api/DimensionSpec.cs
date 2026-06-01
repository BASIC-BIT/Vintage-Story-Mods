namespace DimensionLib.Api;

/// <summary>
/// Declarative request used by a consuming mod to claim a finite DimensionLib dimension.
/// </summary>
public sealed class DimensionSpec
{
    public string DimensionId { get; set; }

    public string OwnerModId { get; set; }

    public int DimensionPlaneId { get; set; } = 3;

    public DimensionPlacement Placement { get; set; } = DimensionPlacement.AutomaticSparse;

    public int ChunkX { get; set; }

    public int ChunkZ { get; set; }

    public int ChunkSizeX { get; set; } = 1;

    public int ChunkSizeZ { get; set; } = 1;

    public int SpawnY { get; set; } = 90;

    public string GeneratorId { get; set; }

    public string VisualProfileId { get; set; }

    public long Seed { get; set; }

    public DimensionKind Kind { get; set; } = DimensionKind.Pocket;

    public DimensionAccessPolicy AccessPolicy { get; set; } = DimensionAccessPolicy.OwnerOnly;

    public DimensionMutability Mutability { get; set; } = DimensionMutability.Mutable;

    public bool IsTransient { get; set; } = true;

    /// <summary>
    /// Per-dimension post-process lightness floor for sealed or non-solar spaces. 0 keeps normal VS lighting.
    /// </summary>
    public float MinimumSceneLight { get; set; }

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
