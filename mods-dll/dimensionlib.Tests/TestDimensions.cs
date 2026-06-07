using DimensionLib.Api;

namespace DimensionLib.Tests;

internal static class TestDimensions
{
    public static Dimension Create(
        string dimensionId = "test:dimension",
        string ownerModId = "test",
        int dimensionPlaneId = 3,
        int chunkX = 10,
        int chunkZ = 20,
        int chunkSizeX = 2,
        int chunkSizeZ = 3,
        int spawnY = 90,
        string? generatorId = null,
        DimensionVisualSettings? visualSettings = null,
        long seed = 0,
        DimensionAccessPolicy accessPolicy = DimensionAccessPolicy.OwnerOnly,
        DimensionMutability mutability = DimensionMutability.Mutable,
        bool isTransient = true)
    {
        return new Dimension(
            dimensionId,
            ownerModId,
            dimensionPlaneId,
            chunkX,
            chunkZ,
            chunkSizeX,
            chunkSizeZ,
            spawnY,
            generatorId,
            visualSettings,
            seed,
            accessPolicy,
            mutability,
            isTransient);
    }

    public static DimensionSpec Spec(
        string dimensionId = "test:dimension",
        string ownerModId = "test",
        int dimensionPlaneId = 3,
        int chunkX = 10,
        int chunkZ = 20,
        int chunkSizeX = 2,
        int chunkSizeZ = 3,
        int spawnY = 90,
        string? generatorId = null,
        DimensionVisualSettings? visualSettings = null,
        long seed = 0,
        DimensionAccessPolicy accessPolicy = DimensionAccessPolicy.OwnerOnly,
        DimensionMutability mutability = DimensionMutability.Mutable,
        bool isTransient = true)
    {
        return new DimensionSpec
        {
            DimensionId = dimensionId,
            OwnerModId = ownerModId,
            DimensionPlaneId = dimensionPlaneId,
            ChunkX = chunkX,
            ChunkZ = chunkZ,
            ChunkSizeX = chunkSizeX,
            ChunkSizeZ = chunkSizeZ,
            SpawnY = spawnY,
            GeneratorId = generatorId,
            VisualSettings = visualSettings,
            Seed = seed,
            AccessPolicy = accessPolicy,
            Mutability = mutability,
            IsTransient = isTransient,
        };
    }
}
