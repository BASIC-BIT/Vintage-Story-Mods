using DimensionLib.Api;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

public sealed class BlockAccessorChunkColumnWriter : IChunkColumnWriter
{
    private readonly ICoreServerAPI _api;
    private readonly Dimension _dimension;

    public BlockAccessorChunkColumnWriter(ICoreServerAPI api, Dimension dimension, int localChunkX, int localChunkZ)
    {
        _api = api;
        _dimension = dimension;
        LocalChunkX = localChunkX;
        LocalChunkZ = localChunkZ;
    }

    public int DimensionPlaneId => _dimension.DimensionPlaneId;

    public int ChunkX => _dimension.ChunkX + LocalChunkX;

    public int ChunkZ => _dimension.ChunkZ + LocalChunkZ;

    public int LocalChunkX { get; }

    public int LocalChunkZ { get; }

    public void SetBlock(int blockId, BlockPos localPos)
    {
        if (localPos == null)
        {
            return;
        }

        var worldPos = new BlockPos(_dimension.MinBlockX + localPos.X, localPos.Y, _dimension.MinBlockZ + localPos.Z, _dimension.DimensionPlaneId);
        _api.World.BlockAccessor.SetBlock(blockId, worldPos);
    }
}
