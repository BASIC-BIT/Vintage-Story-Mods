
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TheBasicsNetworkMessage
{
    public int ProximityGroupId;
    public bool PreventProximityChannelSwitching;
}