using ProtoBuf;
using DimensionLib.Api;

namespace DimensionLib.Network;

[ProtoContract]
public sealed class DimensionTransferMessage
{
    [ProtoMember(1)]
    public int DimensionPlaneId { get; set; }

    [ProtoMember(2)]
    public double X { get; set; }

    [ProtoMember(3)]
    public double Y { get; set; }

    [ProtoMember(4)]
    public double Z { get; set; }

    [ProtoMember(5)]
    public float Yaw { get; set; }

    [ProtoMember(6)]
    public float Pitch { get; set; }

    [ProtoMember(7)]
    public float Roll { get; set; }

    [ProtoMember(8)]
    public int ChunkX { get; set; }

    [ProtoMember(9)]
    public int ChunkZ { get; set; }

    [ProtoMember(10)]
    public int ChunkSizeX { get; set; }

    [ProtoMember(11)]
    public int ChunkSizeZ { get; set; }

    [ProtoMember(12)]
    public string DimensionId { get; set; }

    [ProtoMember(13)]
    public DimensionVisualSettings VisualSettings { get; set; }
}
