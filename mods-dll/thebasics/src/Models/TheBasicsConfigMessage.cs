
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TheBasicsConfigMessage
{
    public int ProximityGroupId;
    public bool PreventProximityChannelSwitching;
}