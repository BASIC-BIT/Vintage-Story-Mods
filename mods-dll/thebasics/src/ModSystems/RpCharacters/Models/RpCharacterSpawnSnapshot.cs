using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterSpawnSnapshot
{
    [ProtoMember(1)]
    public bool HasSpawn { get; set; }

    [ProtoMember(2)]
    public int X { get; set; }

    [ProtoMember(3)]
    public int? Y { get; set; }

    [ProtoMember(4)]
    public int Z { get; set; }

    [ProtoMember(5)]
    public float? Yaw { get; set; }

    [ProtoMember(6)]
    public float? Pitch { get; set; }

    [ProtoMember(7)]
    public float? Roll { get; set; }

    [ProtoMember(8)]
    public int RemainingUses { get; set; } = -1;
}
