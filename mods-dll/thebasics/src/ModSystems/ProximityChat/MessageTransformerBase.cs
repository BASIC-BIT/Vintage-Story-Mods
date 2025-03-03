using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat;

public abstract class MessageTransformerBase : IMessageTransformer
{
    protected readonly RPProximityChatSystem _chatSystem;

    public MessageTransformerBase(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }

    public abstract bool ShouldTransform(MessageContext context);

    public abstract MessageContext Transform(MessageContext context);
} 