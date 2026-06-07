using System.Threading;
using DimensionLib.Api;
using DimensionLib.Generation;
using FluentAssertions;
using Vintagestory.API.MathTools;

namespace DimensionLib.Tests.Generation;

public class BlockVolumeSourceValidatorTests
{
    [Fact]
    public void ValidateBounds_AcceptsSourceThatCoversDimensionXZBounds()
    {
        var dimension = TestDimensions.Create(chunkSizeX: 2, chunkSizeZ: 3);
        var source = new TestSource(new BlockVolumeBounds(64, 1, 96));

        var result = BlockVolumeSourceValidator.ValidateBounds(dimension, source);

        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData(63, 96)]
    [InlineData(64, 95)]
    public void ValidateBounds_RejectsSourceTooSmallForDimensionXZBounds(int sizeX, int sizeZ)
    {
        var dimension = TestDimensions.Create(chunkSizeX: 2, chunkSizeZ: 3);
        var source = new TestSource(new BlockVolumeBounds(sizeX, 256, sizeZ));

        var result = BlockVolumeSourceValidator.ValidateBounds(dimension, source);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("source-bounds-too-small");
    }

    [Fact]
    public void ValidateBounds_RejectsMissingBounds()
    {
        var dimension = TestDimensions.Create();
        var source = new TestSource(bounds: null);

        var result = BlockVolumeSourceValidator.ValidateBounds(dimension, source);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("missing-source-bounds");
    }

    private sealed class TestSource : IBlockVolumeSource
    {
        public TestSource(BlockVolumeBounds? bounds)
        {
            Bounds = bounds!;
        }

        public string SourceId => "test-source";

        public BlockVolumeBounds Bounds { get; }

        public void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token)
        {
            writer.SetBlock(0, new BlockPos(localChunkX, 0, localChunkZ));
        }
    }
}
