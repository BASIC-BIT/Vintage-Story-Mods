using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace DimensionLib.Api;

public sealed class Dimension
{
    public Dimension(
        string dimensionId,
        string ownerModId,
        int dimensionPlaneId,
        int chunkX,
        int chunkZ,
        int chunkSizeX,
        int chunkSizeZ,
        int spawnY,
        string generatorId = null,
        DimensionVisualSettings visualSettings = null,
        long seed = 0,
        DimensionAccessPolicy accessPolicy = DimensionAccessPolicy.OwnerOnly,
        DimensionMutability mutability = DimensionMutability.Mutable,
        bool isTransient = true)
    {
        DimensionId = dimensionId;
        OwnerModId = ownerModId;
        DimensionPlaneId = dimensionPlaneId;
        ChunkX = chunkX;
        ChunkZ = chunkZ;
        ChunkSizeX = chunkSizeX;
        ChunkSizeZ = chunkSizeZ;
        SpawnY = spawnY;
        GeneratorId = generatorId;
        VisualSettings = visualSettings?.Clone();
        Seed = seed;
        AccessPolicy = accessPolicy;
        Mutability = mutability;
        IsTransient = isTransient;
    }

    public string DimensionId { get; }

    public string OwnerModId { get; }

    public int DimensionPlaneId { get; }

    public int ChunkX { get; }

    public int ChunkZ { get; }

    public int ChunkSizeX { get; }

    public int ChunkSizeZ { get; }

    public int SpawnY { get; }

    public string GeneratorId { get; }

    public DimensionVisualSettings VisualSettings { get; }

    public long Seed { get; }

    public DimensionAccessPolicy AccessPolicy { get; }

    public DimensionMutability Mutability { get; }

    public bool IsTransient { get; }

    public float MinimumSceneLight => VisualSettings?.Scene.MinimumLight ?? 0f;

    public int MinBlockX => ChunkX * GlobalConstants.ChunkSize;

    public int MinBlockZ => ChunkZ * GlobalConstants.ChunkSize;

    public int MaxBlockX => (ChunkX + ChunkSizeX) * GlobalConstants.ChunkSize - 1;

    public int MaxBlockZ => (ChunkZ + ChunkSizeZ) * GlobalConstants.ChunkSize - 1;

    public double SpawnX => MinBlockX + ChunkSizeX * GlobalConstants.ChunkSize / 2.0;

    public double SpawnZ => MinBlockZ + ChunkSizeZ * GlobalConstants.ChunkSize / 2.0;

    public BlockPos MinBlockPos => new BlockPos(MinBlockX, 0, MinBlockZ, DimensionPlaneId);

    public BlockPos MaxBlockPos(int mapSizeY) => new BlockPos(MaxBlockX, mapSizeY - 1, MaxBlockZ, DimensionPlaneId);

    public bool ContainsBlock(BlockPos pos)
    {
        return pos != null &&
            pos.dimension == DimensionPlaneId &&
            pos.X >= MinBlockX && pos.X <= MaxBlockX &&
            pos.Z >= MinBlockZ && pos.Z <= MaxBlockZ;
    }

    public DimensionLocalPosition ToLocalPosition(double x, double y, double z)
    {
        return new DimensionLocalPosition
        {
            DimensionId = DimensionId,
            DimensionPlaneId = DimensionPlaneId,
            X = x - MinBlockX,
            Y = y,
            Z = z - MinBlockZ,
        };
    }

    public DimensionLocalPosition ToLocalPosition(DimensionLocation location)
    {
        return location == null ? null : ToLocalPosition(location.X, location.Y, location.Z);
    }
}
