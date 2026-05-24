
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TheBasicsPlayerNicknameMessage
{
    public string PlayerUID { get; set; }
    public string Nickname { get; set; }
}
