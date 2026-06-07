using DimensionLib.Services;
using FluentAssertions;

namespace DimensionLib.Tests.Services;

public class PreparedDimensionTrackerTests
{
    [Fact]
    public void MarkAllChunksPrepared_TracksEveryLocalChunk()
    {
        var dimension = TestDimensions.Create(dimensionId: "test:dim", chunkSizeX: 2, chunkSizeZ: 3);
        var tracker = new PreparedDimensionTracker();

        tracker.MarkAllChunksPrepared(dimension);
        tracker.MarkDimensionPrepared(dimension.DimensionId);

        tracker.IsDimensionPrepared(" test:dim ").Should().BeTrue();
        tracker.GetPreparedChunkCount(dimension).Should().Be(6);
        tracker.TryGetPartialPreparedLocalChunks(dimension, out _).Should().BeFalse();
        tracker.GetPreparedLocalChunks(dimension).Should().HaveCount(6);
    }

    [Fact]
    public void PartialPreparedChunks_AreReportedUntilDimensionIsFullyPrepared()
    {
        var dimension = TestDimensions.Create(dimensionId: "test:dim", chunkSizeX: 2, chunkSizeZ: 2);
        var tracker = new PreparedDimensionTracker();

        tracker.MarkChunkPrepared(dimension.DimensionId, 1, 0);
        tracker.MarkDimensionPrepared(dimension.DimensionId);

        tracker.IsChunkPrepared(dimension.DimensionId, 1, 0).Should().BeTrue();
        tracker.IsChunkPrepared(dimension.DimensionId, 0, 0).Should().BeFalse();
        tracker.TryGetPartialPreparedLocalChunks(dimension, out var chunks).Should().BeTrue();
        chunks.Should().ContainSingle(chunk => chunk.X == 1 && chunk.Y == 0);
    }

    [Fact]
    public void LoadPreparedChunks_FiltersOutKeysOutsideDimensionBounds()
    {
        var dimension = TestDimensions.Create(dimensionId: "test:dim", chunkSizeX: 2, chunkSizeZ: 2);
        var tracker = new PreparedDimensionTracker();
        var validKey = ChunkKey(1, 1);
        var invalidXKey = ChunkKey(2, 0);
        var invalidZKey = ChunkKey(0, 2);

        tracker.LoadPreparedChunks(dimension, [validKey, invalidXKey, invalidZKey]);

        tracker.IsDimensionPrepared(dimension.DimensionId).Should().BeTrue();
        tracker.GetPreparedChunkKeys(dimension).Should().Equal(validKey);
    }

    [Fact]
    public void RemoveDimension_ClearsPreparedStateAndChunks()
    {
        var dimension = TestDimensions.Create(dimensionId: "test:dim");
        var tracker = new PreparedDimensionTracker();
        tracker.MarkDimensionPrepared(dimension.DimensionId);
        tracker.MarkChunkPrepared(dimension.DimensionId, 0, 0);

        tracker.RemoveDimension(dimension.DimensionId);

        tracker.IsDimensionPrepared(dimension.DimensionId).Should().BeFalse();
        tracker.GetPreparedChunkKeys(dimension).Should().BeEmpty();
    }

    private static long ChunkKey(int localChunkX, int localChunkZ)
    {
        return ((long)localChunkX << 32) | (uint)localChunkZ;
    }
}
