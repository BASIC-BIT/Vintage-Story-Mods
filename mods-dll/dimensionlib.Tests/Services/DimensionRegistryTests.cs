using DimensionLib.Services;
using FluentAssertions;
using Vintagestory.API.MathTools;

namespace DimensionLib.Tests.Services;

public class DimensionRegistryTests
{
    [Fact]
    public void Register_AddsDimensionAndIndexesByPosition()
    {
        var registry = new DimensionRegistry(firstAllowedDimensionPlaneId: 3);
        var spec = TestDimensions.Spec(dimensionId: "test:negative", chunkX: -2, chunkZ: -1, chunkSizeX: 2, chunkSizeZ: 1);

        var result = registry.Register(spec);

        result.Success.Should().BeTrue();
        registry.Get("test:negative").Success.Should().BeTrue();
        registry.GetAt(new BlockPos(-64, 5, -32, 3)).Success.Should().BeTrue();
        registry.GetAt(new BlockPos(-1, 5, -1, 3)).Success.Should().BeTrue();
        registry.GetAt(new BlockPos(-65, 5, -32, 3)).Success.Should().BeFalse();
    }

    [Fact]
    public void Register_ReturnsIdempotentSuccessForSameClaim()
    {
        var registry = new DimensionRegistry(firstAllowedDimensionPlaneId: 3);
        registry.Register(TestDimensions.Spec(dimensionId: "test:dim")).Success.Should().BeTrue();

        var result = registry.Register(TestDimensions.Spec(dimensionId: "test:dim"));

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("already registered");
    }

    [Fact]
    public void Register_RejectsSameIdWithDifferentClaim()
    {
        var registry = new DimensionRegistry(firstAllowedDimensionPlaneId: 3);
        registry.Register(TestDimensions.Spec(dimensionId: "test:dim")).Success.Should().BeTrue();

        var result = registry.Register(TestDimensions.Spec(dimensionId: "test:dim", chunkX: 200));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("dimension-id-conflict");
    }

    [Fact]
    public void Register_RejectsOverlappingRegionsOnSamePlane()
    {
        var registry = new DimensionRegistry(firstAllowedDimensionPlaneId: 3);
        registry.Register(TestDimensions.Spec(dimensionId: "test:first", chunkX: 10, chunkZ: 10, chunkSizeX: 2, chunkSizeZ: 2)).Success.Should().BeTrue();

        var result = registry.Register(TestDimensions.Spec(dimensionId: "test:second", chunkX: 11, chunkZ: 11, chunkSizeX: 2, chunkSizeZ: 2));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("dimension-overlap");
    }

    [Fact]
    public void Register_AllowsSameBackingRegionOnDifferentPlanes()
    {
        var registry = new DimensionRegistry(firstAllowedDimensionPlaneId: 3);
        registry.Register(TestDimensions.Spec(dimensionId: "test:first", dimensionPlaneId: 3)).Success.Should().BeTrue();

        var result = registry.Register(TestDimensions.Spec(dimensionId: "test:second", dimensionPlaneId: 4));

        result.Success.Should().BeTrue();
    }

    [Fact]
    public void MarkOrphanedAndRemove_UpdateLifecycleState()
    {
        var registry = new DimensionRegistry(firstAllowedDimensionPlaneId: 3);
        registry.Register(TestDimensions.Spec(dimensionId: "test:dim")).Success.Should().BeTrue();

        registry.MarkOrphaned("test:dim");
        registry.IsOrphaned("test:dim").Should().BeTrue();
        registry.Remove("test:dim");

        registry.Get("test:dim").Success.Should().BeFalse();
        registry.IsOrphaned("test:dim").Should().BeFalse();
    }
}
