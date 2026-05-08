using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterPositionSnapshot
{
    [ProtoMember(1)]
    public bool Available { get; set; }

    [ProtoMember(2)]
    public double X { get; set; }

    [ProtoMember(3)]
    public double Y { get; set; }

    [ProtoMember(4)]
    public double Z { get; set; }

    [ProtoMember(5)]
    public int Dimension { get; set; }

    [ProtoMember(6)]
    public float Yaw { get; set; }

    [ProtoMember(7)]
    public float Pitch { get; set; }

    [ProtoMember(8)]
    public float Roll { get; set; }

    [ProtoMember(9)]
    public float HeadYaw { get; set; }

    [ProtoMember(10)]
    public float HeadPitch { get; set; }

    [ProtoMember(11)]
    public double MotionX { get; set; }

    [ProtoMember(12)]
    public double MotionY { get; set; }

    [ProtoMember(13)]
    public double MotionZ { get; set; }
}
