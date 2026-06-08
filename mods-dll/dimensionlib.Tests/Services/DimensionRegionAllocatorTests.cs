using DimensionLib.Services;
using FluentAssertions;

namespace DimensionLib.Tests.Services;

public class DimensionRegionAllocatorTests
{
    [Fact]
    public void TryAssignSparseRegion_AssignsFarApartBackingCoordinates()
    {
        var spec = TestDimensions.Spec(dimensionId: "test:new", chunkX: 0, chunkZ: 0, chunkSizeX: 2, chunkSizeZ: 2);

        var result = DimensionRegionAllocator.TryAssignSparseRegion(spec, existingDimensions: []);

        result.Should().BeTrue();
        spec.ChunkX.Should().Be(1024);
        spec.ChunkZ.Should().Be(1024);
    }

    [Fact]
    public void TryAssignSparseRegion_SkipsOccupiedSparseSlot()
    {
        var existing = TestDimensions.Create(dimensionId: "test:existing", chunkX: 1024, chunkZ: 1024, chunkSizeX: 2, chunkSizeZ: 2);
        var spec = TestDimensions.Spec(dimensionId: "test:new", chunkX: 0, chunkZ: 0, chunkSizeX: 2, chunkSizeZ: 2);

        var result = DimensionRegionAllocator.TryAssignSparseRegion(spec, [existing]);

        result.Should().BeTrue();
        spec.ChunkX.Should().Be(2048);
        spec.ChunkZ.Should().Be(1024);
    }

    [Fact]
    public void TryAssignSparseRegion_StaysWithinWorldChunkBounds()
    {
        var occupiedSlots = new[]
        {
            TestDimensions.Create(dimensionId: "test:slot1", chunkX: 1024, chunkZ: 1024, chunkSizeX: 2, chunkSizeZ: 2),
            TestDimensions.Create(dimensionId: "test:slot2", chunkX: 2048, chunkZ: 1024, chunkSizeX: 2, chunkSizeZ: 2),
            TestDimensions.Create(dimensionId: "test:slot3", chunkX: 1024, chunkZ: 2048, chunkSizeX: 2, chunkSizeZ: 2),
        };
        var spec = TestDimensions.Spec(dimensionId: "test:new", chunkX: 0, chunkZ: 0, chunkSizeX: 2, chunkSizeZ: 2);

        var result = DimensionRegionAllocator.TryAssignSparseRegion(spec, occupiedSlots, maxChunkX: 2050, maxChunkZ: 2050);

        result.Should().BeTrue();
        spec.ChunkX.Should().Be(2048);
        spec.ChunkZ.Should().Be(2048);
    }

    [Fact]
    public void TryAssignSparseRegion_FailsWhenNoInBoundsSlotRemains()
    {
        var occupiedSlots = new[]
        {
            TestDimensions.Create(dimensionId: "test:slot1", chunkX: 1024, chunkZ: 1024, chunkSizeX: 2, chunkSizeZ: 2),
            TestDimensions.Create(dimensionId: "test:slot2", chunkX: 2048, chunkZ: 1024, chunkSizeX: 2, chunkSizeZ: 2),
            TestDimensions.Create(dimensionId: "test:slot3", chunkX: 1024, chunkZ: 2048, chunkSizeX: 2, chunkSizeZ: 2),
            TestDimensions.Create(dimensionId: "test:slot4", chunkX: 2048, chunkZ: 2048, chunkSizeX: 2, chunkSizeZ: 2),
        };
        var spec = TestDimensions.Spec(dimensionId: "test:new", chunkX: 0, chunkZ: 0, chunkSizeX: 2, chunkSizeZ: 2);

        var result = DimensionRegionAllocator.TryAssignSparseRegion(spec, occupiedSlots, maxChunkX: 2050, maxChunkZ: 2050);

        result.Should().BeFalse();
    }

    [Fact]
    public void TryAssignAvailableRegion_UsesDenseScanForSmallFixtures()
    {
        var existing = TestDimensions.Create(dimensionId: "test:existing", chunkX: 0, chunkZ: 0, chunkSizeX: 1, chunkSizeZ: 1);
        var spec = TestDimensions.Spec(dimensionId: "test:new", chunkX: 0, chunkZ: 0, chunkSizeX: 1, chunkSizeZ: 1);

        var result = DimensionRegionAllocator.TryAssignAvailableRegion(spec, [existing], maxChunkCoordinate: 4);

        result.Should().BeTrue();
        spec.ChunkX.Should().Be(2);
        spec.ChunkZ.Should().Be(0);
    }
}
