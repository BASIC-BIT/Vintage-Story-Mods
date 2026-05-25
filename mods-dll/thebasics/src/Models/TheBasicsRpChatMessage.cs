
using ProtoBuf;

namespace thebasics.Models;

[ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
public class TheBasicsRpChatMessage
{
    public string PlayerUID { get; set; }
    public string Message { get; set; }
}
