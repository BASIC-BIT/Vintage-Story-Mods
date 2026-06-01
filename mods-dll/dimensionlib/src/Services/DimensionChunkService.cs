using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

internal sealed class DimensionChunkService
{
    private readonly ICoreServerAPI _api;

    public DimensionChunkService(ICoreServerAPI api)
    {
        _api = api;
    }

    public void CreateChunkColumns(Dimension dimension)
    {
        for (var cx = dimension.ChunkX; cx < dimension.ChunkX + dimension.ChunkSizeX; cx++)
        {
            for (var cz = dimension.ChunkZ; cz < dimension.ChunkZ + dimension.ChunkSizeZ; cz++)
            {
                _api.WorldManager.CreateChunkColumnForDimension(cx, cz, dimension.DimensionPlaneId);
            }
        }
    }

    public void CreateChunkColumn(Dimension dimension, int localChunkX, int localChunkZ)
    {
        _api.WorldManager.CreateChunkColumnForDimension(dimension.ChunkX + localChunkX, dimension.ChunkZ + localChunkZ, dimension.DimensionPlaneId);
    }

    public void LoadLocalChunkColumns(Dimension dimension, IEnumerable<Vec2i> localChunks)
    {
        foreach (var chunk in localChunks)
        {
            LoadLocalChunkColumn(dimension, chunk.X, chunk.Y);
        }
    }

    public void LoadLocalChunkColumn(Dimension dimension, int localChunkX, int localChunkZ)
    {
        _api.WorldManager.LoadChunkColumnForDimension(dimension.ChunkX + localChunkX, dimension.ChunkZ + localChunkZ, dimension.DimensionPlaneId);
    }

    public bool IsLocalChunkColumnLoaded(Dimension dimension, int localChunkX, int localChunkZ)
    {
        var chunkX = dimension.ChunkX + localChunkX;
        var chunkZ = dimension.ChunkZ + localChunkZ;
        var dimensionChunkOffset = dimension.DimensionPlaneId * GlobalConstants.DimensionSizeInChunks;
        var verticalChunks = (_api.WorldManager.MapSizeY + GlobalConstants.ChunkSize - 1) / GlobalConstants.ChunkSize;
        for (var chunkY = 0; chunkY < verticalChunks; chunkY++)
        {
            if (_api.WorldManager.GetChunk(chunkX, dimensionChunkOffset + chunkY, chunkZ) == null)
            {
                return false;
            }
        }

        return true;
    }

    public bool TryMaterializeLocalChunkColumnFromSource(Dimension dimension, int localChunkX, int localChunkZ, IServerChunk[] sourceChunks)
    {
        if (sourceChunks == null || sourceChunks.Length == 0)
        {
            return false;
        }

        CreateChunkColumn(dimension, localChunkX, localChunkZ);

        var chunkX = dimension.ChunkX + localChunkX;
        var chunkZ = dimension.ChunkZ + localChunkZ;
        var dimensionChunkOffset = dimension.DimensionPlaneId * GlobalConstants.DimensionSizeInChunks;
        var verticalChunks = System.Math.Min(sourceChunks.Length, (_api.WorldManager.MapSizeY + GlobalConstants.ChunkSize - 1) / GlobalConstants.ChunkSize);
        var copiedAny = false;
        for (var chunkY = 0; chunkY < verticalChunks; chunkY++)
        {
            var sourceChunk = sourceChunks[chunkY];
            var destinationChunk = _api.WorldManager.GetChunk(chunkX, dimensionChunkOffset + chunkY, chunkZ);
            if (sourceChunk == null || destinationChunk == null)
            {
                continue;
            }

            copiedAny |= TryCopyChunkData(sourceChunk, destinationChunk);
        }

        return copiedAny;
    }

    public void LoadAllChunkColumns(Dimension dimension)
    {
        for (var localChunkX = 0; localChunkX < dimension.ChunkSizeX; localChunkX++)
        {
            for (var localChunkZ = 0; localChunkZ < dimension.ChunkSizeZ; localChunkZ++)
            {
                _api.WorldManager.LoadChunkColumnForDimension(dimension.ChunkX + localChunkX, dimension.ChunkZ + localChunkZ, dimension.DimensionPlaneId);
            }
        }
    }

    public void ForceSendLocalChunkColumns(Dimension dimension, IServerPlayer player, IEnumerable<Vec2i> localChunks)
    {
        foreach (var chunk in localChunks)
        {
            ForceSendLocalChunkColumn(dimension, player, chunk.X, chunk.Y);
        }
    }

    public void ForceSendAllChunkColumns(Dimension dimension, IServerPlayer player)
    {
        for (var localChunkX = 0; localChunkX < dimension.ChunkSizeX; localChunkX++)
        {
            for (var localChunkZ = 0; localChunkZ < dimension.ChunkSizeZ; localChunkZ++)
            {
                ForceSendLocalChunkColumn(dimension, player, localChunkX, localChunkZ);
            }
        }
    }

    public void ForceSendLocalChunkColumn(Dimension dimension, IServerPlayer player, int localChunkX, int localChunkZ)
    {
        _api.WorldManager.ForceSendChunkColumn(player, dimension.ChunkX + localChunkX, dimension.ChunkZ + localChunkZ, dimension.DimensionPlaneId);
    }

    public void Relight(Dimension dimension)
    {
        _api.WorldManager.FullRelight(dimension.MinBlockPos, dimension.MaxBlockPos(_api.WorldManager.MapSizeY), sendToClients: false);
    }

    public void RelightWindow(Dimension dimension, IEnumerable<Vec2i> preparedChunks)
    {
        var chunks = preparedChunks.ToArray();
        if (chunks.Length == 0)
        {
            return;
        }

        var minLocalChunkX = chunks.Min(chunk => chunk.X);
        var maxLocalChunkX = chunks.Max(chunk => chunk.X);
        var minLocalChunkZ = chunks.Min(chunk => chunk.Y);
        var maxLocalChunkZ = chunks.Max(chunk => chunk.Y);
        var chunkSize = GlobalConstants.ChunkSize;
        var min = new BlockPos((dimension.ChunkX + minLocalChunkX) * chunkSize, 0, (dimension.ChunkZ + minLocalChunkZ) * chunkSize, dimension.DimensionPlaneId);
        var max = new BlockPos((dimension.ChunkX + maxLocalChunkX + 1) * chunkSize - 1, _api.WorldManager.MapSizeY - 1, (dimension.ChunkZ + maxLocalChunkZ + 1) * chunkSize - 1, dimension.DimensionPlaneId);
        EnsureRelightChunkState(dimension, minLocalChunkX, maxLocalChunkX, minLocalChunkZ, maxLocalChunkZ);
        _api.WorldManager.FullRelight(min, max, sendToClients: false);
    }

    public void ClearBlocks(Dimension dimension)
    {
        CreateChunkColumns(dimension);
        var accessor = _api.World.BlockAccessor;
        var pos = new BlockPos(dimension.DimensionPlaneId);
        for (var x = dimension.MinBlockX; x <= dimension.MaxBlockX; x++)
        {
            for (var z = dimension.MinBlockZ; z <= dimension.MaxBlockZ; z++)
            {
                for (var y = 0; y < _api.WorldManager.MapSizeY; y++)
                {
                    pos.Set(x, y, z);
                    accessor.SetBlock(0, pos);
                }
            }
        }

        Relight(dimension);
    }

    private static bool TryCopyChunkData(IServerChunk sourceChunk, IServerChunk destinationChunk)
    {
        sourceChunk.Unpack_ReadOnly();
        destinationChunk.Unpack();
        if (sourceChunk.Data == null || destinationChunk.Data == null)
        {
            return false;
        }

        destinationChunk.LightPositions ??= new HashSet<int>();
        destinationChunk.Data.ClearBlocksAndPrepare();

        var length = System.Math.Min(sourceChunk.Data.Length, destinationChunk.Data.Length);
        for (var index = 0; index < length; index++)
        {
            var solidBlockId = sourceChunk.Data.GetBlockId(index, BlockLayersAccess.Solid);
            if (solidBlockId != 0)
            {
                destinationChunk.Data.SetBlockUnsafe(index, solidBlockId);
            }

            var fluidBlockId = sourceChunk.Data.GetFluid(index);
            if (fluidBlockId != 0)
            {
                destinationChunk.Data.SetFluid(index, fluidBlockId);
            }
        }

        destinationChunk.Empty = sourceChunk.Empty;
        destinationChunk.MarkModified();
        return true;
    }

    private void EnsureRelightChunkState(Dimension dimension, int minLocalChunkX, int maxLocalChunkX, int minLocalChunkZ, int maxLocalChunkZ)
    {
        var minChunkX = System.Math.Max(0, minLocalChunkX - 1);
        var maxChunkX = System.Math.Min(dimension.ChunkSizeX - 1, maxLocalChunkX + 1);
        var minChunkZ = System.Math.Max(0, minLocalChunkZ - 1);
        var maxChunkZ = System.Math.Min(dimension.ChunkSizeZ - 1, maxLocalChunkZ + 1);
        var dimensionChunkOffset = dimension.DimensionPlaneId * GlobalConstants.DimensionSizeInChunks;
        var verticalChunks = (_api.WorldManager.MapSizeY + GlobalConstants.ChunkSize - 1) / GlobalConstants.ChunkSize;

        for (var localChunkX = minChunkX; localChunkX <= maxChunkX; localChunkX++)
        {
            for (var localChunkZ = minChunkZ; localChunkZ <= maxChunkZ; localChunkZ++)
            {
                var chunkX = dimension.ChunkX + localChunkX;
                var chunkZ = dimension.ChunkZ + localChunkZ;
                for (var chunkY = 0; chunkY < verticalChunks; chunkY++)
                {
                    var chunk = _api.WorldManager.GetChunk(chunkX, dimensionChunkOffset + chunkY, chunkZ);
                    if (chunk == null)
                    {
                        continue;
                    }

                    chunk.LightPositions ??= new HashSet<int>();
                }
            }
        }
    }
}
