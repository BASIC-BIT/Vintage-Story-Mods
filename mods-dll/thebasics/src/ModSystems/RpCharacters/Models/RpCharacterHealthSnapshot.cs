using ProtoBuf;

namespace thebasics.ModSystems.RpCharacters.Models;

[ProtoContract]
public class RpCharacterHealthSnapshot
{
    [ProtoMember(1)]
    public bool Available { get; set; }

    [ProtoMember(2)]
    public float Health { get; set; }

    [ProtoMember(3)]
    public float PreviousHealth { get; set; }

    [ProtoMember(4)]
    public float HealthChangeRate { get; set; }

    [ProtoMember(5)]
    public float BaseMaxHealth { get; set; }

    [ProtoMember(6)]
    public float MaxHealth { get; set; }

    [ProtoMember(7)]
    public bool HasFutureHealth { get; set; }

    [ProtoMember(8)]
    public float FutureHealth { get; set; }
}
