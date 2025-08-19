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
        
        // Check message type based on configured delimiters
        var delimiters = _config.ChatDelimiters;
        var isGlobalOoc = _config.EnableGlobalOOC && !string.IsNullOrEmpty(delimiters.GlobalOOC.Start) && content.StartsWith(delimiters.GlobalOOC.Start);
        var isOOC = !isGlobalOoc && !string.IsNullOrEmpty(delimiters.OOC.Start) && content.StartsWith(delimiters.OOC.Start);
        var isEnvironmentMessage = !string.IsNullOrEmpty(delimiters.Environmental.Start) && content.StartsWith(delimiters.Environmental.Start);
        var isEmote = (!string.IsNullOrEmpty(delimiters.Emote.Start) && content.StartsWith(delimiters.Emote.Start)) || (context.SendingPlayer.GetEmoteMode() && !isOOC && !isGlobalOoc && !isEnvironmentMessage);
        
        // Handle Global OOC - this will be processed normally by the server
        if (isGlobalOoc)
        {
            context.Message = content[delimiters.GlobalOOC.Start.Length..]; // Remove the leading delimiter
            if (!string.IsNullOrEmpty(delimiters.GlobalOOC.End))
            {
                // Remove all trailing end delimiters
                while (context.Message.EndsWith(delimiters.GlobalOOC.End))
                {
                    context.Message = context.Message[..^delimiters.GlobalOOC.End.Length];
                }
            }

            context.SetFlag(MessageContext.IS_GLOBAL_OOC);
        } else if (isEmote)
        {
            if (!string.IsNullOrEmpty(delimiters.Emote.Start) && content.StartsWith(delimiters.Emote.Start)) {
                context.Message = content[delimiters.Emote.Start.Length..]; // Remove the leading delimiter
            }
            context.SetFlag(MessageContext.IS_EMOTE);
        } else if (isOOC)
        {
            context.Message = content[delimiters.OOC.Start.Length..]; // Remove the leading delimiter
            if (!string.IsNullOrEmpty(delimiters.OOC.End) && context.Message.EndsWith(delimiters.OOC.End)) {
                context.Message = context.Message[..^delimiters.OOC.End.Length]; // Remove the trailing delimiter
            }
            context.SetFlag(MessageContext.IS_OOC);
        } else if (isEnvironmentMessage)
        {
            context.Message = content[delimiters.Environmental.Start.Length..]; // Remove the delimiter
            context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
        } else {
            context.SetFlag(MessageContext.IS_SPEECH);
        }

        // Trim whitespace
        context.Message = context.Message.Trim();
        
        return context;
    }
}