using ProtoBuf;

namespace thebasics.Models;

public enum ChatTypingIndicatorState : byte
{
    None = 0,
    ChatOpenEmpty = 1,
    ChatOpenComposing = 2,
    Typing = 3,
}
 
[ProtoContract]
public class ChatTypingStateMessage
{
    // Server should fill this from the sending player's entity id.
    [ProtoMember(1)]
    public long EntityId { get; set; }

    [ProtoMember(2)]
    public bool IsTyping { get; set; }

    // Preferred field for newer clients/servers.
    // Backwards compatible with older versions that only understand IsTyping.
    [ProtoMember(3)]
    public ChatTypingIndicatorState State { get; set; }
}
