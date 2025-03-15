using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat;

public interface IMessageTransformer
{
    MessageContext Transform(MessageContext context);
} 