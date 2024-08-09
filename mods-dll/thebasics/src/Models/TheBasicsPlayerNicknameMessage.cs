
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TheBasicsPlayerNicknameMessage
{
    public string PlayerUID;
    public string Nickname;
}