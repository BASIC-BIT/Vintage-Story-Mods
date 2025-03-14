using ProtoBuf;
using thebasics.Configs;

namespace thebasics.Models;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TheBasicsConfigMessage
{
    public int ProximityGroupId;
    public ModConfig Config;
}