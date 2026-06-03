using DimensionLib.Api;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

public sealed class BlockAccessorChunkColumnWriter : IChunkColumnWriter
{
    private readonly ICoreServerAPI _api;
    private readonly Dimension _dimension;
    private bool _warnedOutOfBounds;

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

        if (!IsInsideMaterializedColumn(localPos))
        {
            WarnOutOfBounds(localPos);
            return;
        }

        var worldPos = new BlockPos(_dimension.MinBlockX + localPos.X, localPos.Y, _dimension.MinBlockZ + localPos.Z, _dimension.DimensionPlaneId);
        _api.World.BlockAccessor.SetBlock(blockId, worldPos);
    }

    private bool IsInsideMaterializedColumn(BlockPos localPos)
    {
        var chunkSize = GlobalConstants.ChunkSize;
        var minLocalX = LocalChunkX * chunkSize;
        var minLocalZ = LocalChunkZ * chunkSize;
        var maxLocalX = minLocalX + chunkSize - 1;
        var maxLocalZ = minLocalZ + chunkSize - 1;

        return localPos.X >= minLocalX && localPos.X <= maxLocalX &&
            localPos.Z >= minLocalZ && localPos.Z <= maxLocalZ &&
            localPos.Y >= 0 && localPos.Y < _api.WorldManager.MapSizeY;
    }

    private void WarnOutOfBounds(BlockPos localPos)
    {
        if (_warnedOutOfBounds)
        {
            return;
        }

        _warnedOutOfBounds = true;
        _api.Logger.Warning(
            "[DimensionLib] Block source for '{0}' attempted to write local block ({1},{2},{3}) outside local chunk ({4},{5}); ignoring out-of-bounds writes.",
            _dimension.DimensionId,
            localPos.X,
            localPos.Y,
            localPos.Z,
            LocalChunkX,
            LocalChunkZ);
    }
}
