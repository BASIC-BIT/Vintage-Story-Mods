using System.Threading;
using DimensionLib.Api;
using Vintagestory.API.Server;

namespace DimensionLib.Services;

internal sealed class ChunkColumnMaterializer
{
    private readonly ICoreServerAPI _api;

    public ChunkColumnMaterializer(ICoreServerAPI api)
    {
        _api = api;
    }

    public void Materialize(Dimension dimension, IBlockVolumeSource source, CancellationToken token)
    {
        for (var localChunkX = 0; localChunkX < dimension.ChunkSizeX; localChunkX++)
        {
            for (var localChunkZ = 0; localChunkZ < dimension.ChunkSizeZ; localChunkZ++)
            {
                token.ThrowIfCancellationRequested();
                MaterializeChunk(dimension, source, localChunkX, localChunkZ, token);
            }
        }
    }

    public void MaterializeChunk(Dimension dimension, IBlockVolumeSource source, int localChunkX, int localChunkZ, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var writer = new BlockAccessorChunkColumnWriter(_api, dimension, localChunkX, localChunkZ);
        source.FillColumn(writer, localChunkX, localChunkZ, token);
    }
}
