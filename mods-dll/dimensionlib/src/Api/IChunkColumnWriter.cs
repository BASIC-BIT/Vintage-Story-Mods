using Vintagestory.API.MathTools;

namespace DimensionLib.Api;

/// <summary>
/// Writes local block coordinates into the region currently being materialized.
/// </summary>
public interface IChunkColumnWriter
{
    int DimensionPlaneId { get; }

    int ChunkX { get; }

    int ChunkZ { get; }

    void SetBlock(int blockId, BlockPos localPos);
}
