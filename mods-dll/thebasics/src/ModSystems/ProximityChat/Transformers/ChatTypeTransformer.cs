using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ChatTypeTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public ChatTypeTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        // Set default chat type
        if (!context.Metadata.ContainsKey("chatType"))
        {
            context.Metadata["chatType"] = EnumChatType.OthersMessage;
        }
        
        // Environmental messages use Notification chat type
        if (context.Metadata.ContainsKey("isEnvironmental"))
        {
            context.Metadata["chatType"] = EnumChatType.Notification;
        }
        
        // Emotes always use OthersMessage
        if (context.Metadata.ContainsKey("isEmote"))
        {
            context.Metadata["chatType"] = EnumChatType.OthersMessage;
        }
        
        return context;
    }
} 