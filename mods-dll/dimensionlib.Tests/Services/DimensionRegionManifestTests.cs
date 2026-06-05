using DimensionLib.Api;
using DimensionLib.Services;
using FluentAssertions;

namespace DimensionLib.Tests.Services;

public class DimensionRegionManifestTests
{
    [Fact]
    public void FromDimension_PreservesDimensionMetadataAndExistingCreationTime()
    {
        var visualSettings = new DimensionVisualSettings { Scene = new DimensionSceneVisualSettings { MinimumLight = 0.25f } };
        var dimension = TestDimensions.Create(
            dimensionId: "test:dim",
            ownerModId: "test-owner",
            dimensionPlaneId: 5,
            chunkX: 123,
            chunkZ: 456,
            chunkSizeX: 7,
            chunkSizeZ: 8,
            spawnY: 91,
            generatorId: "test:generator",
            visualSettings: visualSettings,
            seed: 42,
            accessPolicy: DimensionAccessPolicy.Public,
            mutability: DimensionMutability.ReadOnly,
            isTransient: false);
        var existing = new DimensionRegionManifestEntry { CreatedUtc = "created" };

        var entry = DimensionRegionManifestEntry.FromDimension(dimension, isOrphaned: true, nowUtc: "updated", existing);

        entry.DimensionId.Should().Be("test:dim");
        entry.OwnerModId.Should().Be("test-owner");
        entry.DimensionPlaneId.Should().Be(5);
        entry.ChunkX.Should().Be(123);
        entry.ChunkZ.Should().Be(456);
        entry.ChunkSizeX.Should().Be(7);
        entry.ChunkSizeZ.Should().Be(8);
        entry.SpawnY.Should().Be(91);
        entry.GeneratorId.Should().Be("test:generator");
        entry.VisualSettings!.Scene.MinimumLight.Should().Be(0.25f);
        entry.Seed.Should().Be(42);
        entry.AccessPolicy.Should().Be(DimensionAccessPolicy.Public);
        entry.Mutability.Should().Be(DimensionMutability.ReadOnly);
        entry.IsTransient.Should().BeFalse();
        entry.IsOrphaned.Should().BeTrue();
        entry.CreatedUtc.Should().Be("created");
        entry.UpdatedUtc.Should().Be("updated");
    }

    [Fact]
    public void ToSpec_RoundTripsManifestEntryIntoDimensionSpec()
    {
        var entry = new DimensionRegionManifestEntry
        {
            DimensionId = "test:dim",
            OwnerModId = "test-owner",
            DimensionPlaneId = 5,
            ChunkX = 123,
            ChunkZ = 456,
            ChunkSizeX = 7,
            ChunkSizeZ = 8,
            SpawnY = 91,
            GeneratorId = "test:generator",
            VisualSettings = new DimensionVisualSettings { Scene = new DimensionSceneVisualSettings { MinimumLight = 0.25f } },
            Seed = 42,
            AccessPolicy = DimensionAccessPolicy.Public,
            Mutability = DimensionMutability.ReadOnly,
            IsTransient = false,
        };

        var spec = entry.ToSpec();

        spec.DimensionId.Should().Be(entry.DimensionId);
        spec.OwnerModId.Should().Be(entry.OwnerModId);
        spec.DimensionPlaneId.Should().Be(entry.DimensionPlaneId);
        spec.ChunkX.Should().Be(entry.ChunkX);
        spec.ChunkZ.Should().Be(entry.ChunkZ);
        spec.ChunkSizeX.Should().Be(entry.ChunkSizeX);
        spec.ChunkSizeZ.Should().Be(entry.ChunkSizeZ);
        spec.SpawnY.Should().Be(entry.SpawnY);
        spec.GeneratorId.Should().Be(entry.GeneratorId);
        spec.VisualSettings!.Scene.MinimumLight.Should().Be(0.25f);
        spec.Seed.Should().Be(entry.Seed);
        spec.AccessPolicy.Should().Be(entry.AccessPolicy);
        spec.Mutability.Should().Be(entry.Mutability);
        spec.IsTransient.Should().BeFalse();
    }
}
