using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Configs;
namespace thebasics.ModSystems.ProximityChat;

public abstract class MessageTransformerBase : IMessageTransformer
{
    protected readonly RPProximityChatSystem _chatSystem;
    protected readonly ModConfig _config;

    public MessageTransformerBase(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
        _config = chatSystem.Config;
    }

    public abstract bool ShouldTransform(MessageContext context);

    public abstract MessageContext Transform(MessageContext context);
} 