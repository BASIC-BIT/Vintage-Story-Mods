using DimensionLib.Api;
using DimensionLib.Services;
using FluentAssertions;

namespace DimensionLib.Tests.Services;

public class DimensionMappingResolverTests
{
    [Fact]
    public void MapLocalPosition_IdentityMappingPreservesLocalCoordinates()
    {
        var source = TestDimensions.Create(dimensionId: "test:source", chunkX: 10, chunkZ: 20, chunkSizeX: 2, chunkSizeZ: 2);
        var target = TestDimensions.Create(dimensionId: "test:target", dimensionPlaneId: 4, chunkX: 100, chunkZ: 200, chunkSizeX: 2, chunkSizeZ: 2);

        var mapped = DimensionMappingResolver.MapLocalPosition(
            DimensionMappingTransform.Identity(),
            source,
            target,
            source.MinBlockX + 12.5,
            80,
            source.MinBlockZ + 9.25,
            reverse: false,
            options: null!);

        mapped.DimensionId.Should().Be("test:target");
        mapped.DimensionPlaneId.Should().Be(4);
        mapped.X.Should().Be(target.MinBlockX + 12.5);
        mapped.Y.Should().Be(80);
        mapped.Z.Should().Be(target.MinBlockZ + 9.25);
    }

    [Fact]
    public void MapLocalPosition_AppliesScalePersistentOffsetAndCallOffsetForward()
    {
        var source = TestDimensions.Create(dimensionId: "test:source", chunkX: 10, chunkZ: 20);
        var target = TestDimensions.Create(dimensionId: "test:target", chunkX: 100, chunkZ: 200);
        var transform = new DimensionMappingTransform
        {
            ScaleX = 0.125,
            ScaleY = 2,
            ScaleZ = 0.25,
            OffsetX = 3,
            OffsetY = 4,
            OffsetZ = 5,
        };
        var options = new DimensionMappingTeleportOptions { OffsetX = 1, OffsetY = -2, OffsetZ = 6 };

        var mapped = DimensionMappingResolver.MapLocalPosition(
            transform,
            source,
            target,
            source.MinBlockX + 80,
            10,
            source.MinBlockZ + 40,
            reverse: false,
            options);

        mapped.X.Should().Be(target.MinBlockX + 80 * 0.125 + 3 + 1);
        mapped.Y.Should().Be(10 * 2 + 4 - 2);
        mapped.Z.Should().Be(target.MinBlockZ + 40 * 0.25 + 5 + 6);
    }

    [Fact]
    public void MapLocalPosition_UsesInverseTransformForReverseMapping()
    {
        var source = TestDimensions.Create(dimensionId: "test:source", chunkX: 10, chunkZ: 20);
        var target = TestDimensions.Create(dimensionId: "test:target", chunkX: 100, chunkZ: 200);
        var transform = new DimensionMappingTransform
        {
            ScaleX = 0.125,
            ScaleY = 2,
            ScaleZ = 0.25,
            OffsetX = 3,
            OffsetY = 4,
            OffsetZ = 5,
        };

        var mapped = DimensionMappingResolver.MapLocalPosition(
            transform,
            target,
            source,
            target.MinBlockX + 13,
            24,
            target.MinBlockZ + 15,
            reverse: true,
            options: new DimensionMappingTeleportOptions());

        mapped.X.Should().Be(source.MinBlockX + 80);
        mapped.Y.Should().Be(10);
        mapped.Z.Should().Be(source.MinBlockZ + 40);
    }

    [Fact]
    public void MapLocalPosition_AppliesPerCallOffsetAfterReverseMapping()
    {
        var source = TestDimensions.Create(dimensionId: "test:source", chunkX: 10, chunkZ: 20);
        var target = TestDimensions.Create(dimensionId: "test:target", chunkX: 100, chunkZ: 200);
        var transform = new DimensionMappingTransform { ScaleX = 2, ScaleY = 1, ScaleZ = 2 };
        var options = new DimensionMappingTeleportOptions { OffsetX = -1, OffsetY = 3, OffsetZ = 4 };

        var mapped = DimensionMappingResolver.MapLocalPosition(
            transform,
            target,
            source,
            target.MinBlockX + 20,
            70,
            target.MinBlockZ + 40,
            reverse: true,
            options);

        mapped.X.Should().Be(source.MinBlockX + 9);
        mapped.Y.Should().Be(73);
        mapped.Z.Should().Be(source.MinBlockZ + 24);
    }
}
