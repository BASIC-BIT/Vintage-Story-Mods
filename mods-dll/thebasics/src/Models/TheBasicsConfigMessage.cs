using ProtoBuf;
using thebasics.Configs;

namespace thebasics.Models;

[ProtoContract]
public class TheBasicsConfigMessage
{
    [ProtoMember(1)]
    public int ProximityGroupId { get; set; }

    // Full config object instead of individual properties
    [ProtoMember(2)]
    public ModConfig Config { get; set; }

    [ProtoMember(3)]
    public int? LastSelectedGroupId { get; set; }
}
