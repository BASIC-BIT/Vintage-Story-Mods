using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ChatTypeTransformer : MessageTransformerBase
{
    public ChatTypeTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return true;
    }
    
    public override MessageContext Transform(MessageContext context)
    {
        
        // Environmental messages use Notification chat type
        if (context.HasFlag(MessageContext.IS_ENVIRONMENTAL))
        {
            context.SetMetadata(MessageContext.CHAT_TYPE, EnumChatType.Notification);
        }
        
        // Emotes always use OthersMessage
        if (context.HasFlag(MessageContext.IS_EMOTE))
        {
            context.SetMetadata(MessageContext.CHAT_TYPE, EnumChatType.OthersMessage);
        }

        // Set default chat type
        if (!context.HasMetadata(MessageContext.CHAT_TYPE))
        {
            context.SetMetadata(MessageContext.CHAT_TYPE, EnumChatType.OthersMessage);
        }
        
        return context;
    }
} 