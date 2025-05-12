using System.Collections.Generic;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class PlayerChatTransformer : MessageTransformerBase
{
    public PlayerChatTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_PLAYER_CHAT);
    }

    public override MessageContext Transform(MessageContext context)
    {   
        var content = context.Message;
        
        // Check message type based on first character or pattern
        var isGlobalOoc = _config.EnableGlobalOOC && content.StartsWith("((");
        var isOOC = !isGlobalOoc && content.StartsWith("(");
        var isEnvironmentMessage = content.StartsWith("!");
        var isEmote = content.StartsWith("*") || (context.SendingPlayer.GetEmoteMode() && !isOOC && !isGlobalOoc && !isEnvironmentMessage);
        
        // Handle Global OOC - this will be processed normally by the server
        if (isGlobalOoc)
        {
            context.Message = content[2..]; // Remove the leading (( characters
            if (context.Message.EndsWith(")")) {
                context.Message = context.Message[..^1]; // Remove the trailing ) character if it exists
            }
            if (context.Message.EndsWith(")")) {
                context.Message = context.Message[..^1]; // Remove the trailing ) character if it exists
            }

            context.SetFlag(MessageContext.IS_GLOBAL_OOC);
        } else if (isEmote)
        {
            if (content.StartsWith("*")) {
                context.Message = content[1..]; // Remove the leading * character if it exists
            }
            context.SetFlag(MessageContext.IS_EMOTE);
        } else if (isOOC)
        {
            context.Message = content[1..]; // Remove the leading ( character
            if (context.Message.EndsWith(")")) {
                context.Message = context.Message[..^1]; // Remove the trailing ) character if it exists
            }
            context.SetFlag(MessageContext.IS_OOC);
        } else if (isEnvironmentMessage)
        {
            context.Message = content[1..]; // Remove the ! character
            context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
        } else {
            context.SetFlag(MessageContext.IS_SPEECH);
        }

        // Trim whitespace
        context.Message = context.Message.Trim();
        
        return context;
    }
}