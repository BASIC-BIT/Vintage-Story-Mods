using DimensionLib.Api;
using FluentAssertions;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace DimensionLib.Tests.Api;

public class DimensionTests
{
    [Fact]
    public void Bounds_AreDerivedFromBackingChunks()
    {
        var dimension = TestDimensions.Create(chunkX: 10, chunkZ: 20, chunkSizeX: 2, chunkSizeZ: 3, spawnY: 77);

        dimension.MinBlockX.Should().Be(10 * GlobalConstants.ChunkSize);
        dimension.MinBlockZ.Should().Be(20 * GlobalConstants.ChunkSize);
        dimension.MaxBlockX.Should().Be(12 * GlobalConstants.ChunkSize - 1);
        dimension.MaxBlockZ.Should().Be(23 * GlobalConstants.ChunkSize - 1);
        dimension.SpawnX.Should().Be(11 * GlobalConstants.ChunkSize);
        dimension.SpawnY.Should().Be(77);
        dimension.SpawnZ.Should().Be(21.5 * GlobalConstants.ChunkSize);
    }

    [Fact]
    public void ContainsBlock_TreatsDimensionAsFullHeightRectangle()
    {
        var dimension = TestDimensions.Create(dimensionPlaneId: 4, chunkX: 1, chunkZ: 2, chunkSizeX: 1, chunkSizeZ: 1);

        dimension.ContainsBlock(new BlockPos(32, -50, 64, 4)).Should().BeTrue();
        dimension.ContainsBlock(new BlockPos(63, 9999, 95, 4)).Should().BeTrue();
        dimension.ContainsBlock(new BlockPos(64, 90, 64, 4)).Should().BeFalse();
        dimension.ContainsBlock(new BlockPos(32, 90, 96, 4)).Should().BeFalse();
        dimension.ContainsBlock(new BlockPos(32, 90, 64, 3)).Should().BeFalse();
    }

    [Fact]
    public void Constructor_ClonesVisualSettings()
    {
        var settings = new DimensionVisualSettings
        {
            Scene = new DimensionSceneVisualSettings { MinimumLight = 0.35f },
        };

        var dimension = TestDimensions.Create(visualSettings: settings);
        settings.Scene.MinimumLight = 0.1f;

        dimension.VisualSettings!.Scene.MinimumLight.Should().Be(0.35f);
    }
}
