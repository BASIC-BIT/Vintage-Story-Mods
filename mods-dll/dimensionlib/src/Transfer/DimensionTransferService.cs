using DimensionLib.Api;
using DimensionLib.Network;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace DimensionLib.Transfer;

internal sealed class DimensionTransferService
{
    private readonly IServerNetworkChannel _serverChannel;

    public DimensionTransferService(IServerNetworkChannel serverChannel)
    {
        _serverChannel = serverChannel;
    }

    public void MovePlayer(IServerPlayer player, int dimensionPlaneId, double x, double y, double z, float yaw, float pitch, float roll)
    {
        var entity = player.Entity;
        entity.Pos.SetPos(x, y, z);
        entity.Pos.Roll = roll;
        entity.Pos.Yaw = yaw;
        entity.Pos.Pitch = pitch;
        entity.Pos.Motion.Set(0, 0, 0);
        entity.PositionBeforeFalling.Set(x, y, z);
        entity.ChangeDimension(dimensionPlaneId);
    }

    public void SyncClientTransfer(IServerPlayer player, int dimensionPlaneId, double x, double y, double z, float yaw, float pitch, float roll, Dimension visibleDimension)
    {
        _serverChannel?.SendPacket(new DimensionTransferMessage
        {
            DimensionPlaneId = dimensionPlaneId,
            X = x,
            Y = y,
            Z = z,
            Yaw = yaw,
            Pitch = pitch,
            Roll = roll,
            DimensionId = visibleDimension?.DimensionId,
            VisualSettings = visibleDimension?.VisualSettings?.Clone(),
            ChunkX = visibleDimension?.ChunkX ?? 0,
            ChunkZ = visibleDimension?.ChunkZ ?? 0,
            ChunkSizeX = visibleDimension?.ChunkSizeX ?? 0,
            ChunkSizeZ = visibleDimension?.ChunkSizeZ ?? 0,
        }, player);
    }

    public void ReturnPlayer(IServerPlayer player, EntityPos returnPos)
    {
        MovePlayer(player, returnPos.Dimension, returnPos.X, returnPos.Y, returnPos.Z, returnPos.Yaw, returnPos.Pitch, returnPos.Roll);
        SyncClientTransfer(player, returnPos.Dimension, returnPos.X, returnPos.Y, returnPos.Z, returnPos.Yaw, returnPos.Pitch, returnPos.Roll, visibleDimension: null);
    }
}
