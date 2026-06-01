using System.Threading;

namespace DimensionLib.Api;

/// <summary>
/// Supplies block data for a bounded DimensionLib dimension without coupling DimensionLib to the caller's storage format.
/// </summary>
public interface IBlockVolumeSource
{
    string SourceId { get; }

    BlockVolumeBounds Bounds { get; }

    void FillColumn(IChunkColumnWriter writer, int localChunkX, int localChunkZ, CancellationToken token);
}
