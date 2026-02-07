using ProtoBuf;

namespace thebasics.Models;

[ProtoContract]
public class ChatTypingStateMessage
{
    // Server should fill this from the sending player's entity id.
    [ProtoMember(1)]
    public long EntityId { get; set; }

    [ProtoMember(2)]
    public bool IsTyping { get; set; }
}
