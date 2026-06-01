using System.Collections.Generic;
using System.Linq;
using DimensionLib.Api;
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
            _api.WorldManager.LoadChunkColumnForDimension(dimension.ChunkX + chunk.X, dimension.ChunkZ + chunk.Y, dimension.DimensionPlaneId);
        }
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
}
