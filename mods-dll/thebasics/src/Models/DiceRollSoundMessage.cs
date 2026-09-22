using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public sealed class DiceRollSoundMessage
{
    [ProtoMember(1)] public int Clip { get; set; }
    [ProtoMember(2)] public double X { get; set; }
    [ProtoMember(3)] public double InternalY { get; set; }
    [ProtoMember(4)] public double Z { get; set; }
    [ProtoMember(5)] public int Dimension { get; set; }
}
