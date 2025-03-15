using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat;

public interface IMessageTransformer
{
    bool ShouldTransform(MessageContext context);
    MessageContext Transform(MessageContext context);
}