using System.Collections.Generic;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class PlayerChatTransformer : MessageTransformerBase
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public PlayerChatTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_PLAYER_CHAT);
    }

    public override MessageContext Transform(MessageContext context)
    {   
        var content = context.Message;
        var config = _chatSystem.GetModConfig();
        
        // Check message type based on first character or pattern
        var isEmote = content.StartsWith("*");
        var isGlobalOoc = config.EnableGlobalOOC && content.StartsWith("((");
        var isOOC = !isGlobalOoc && content.StartsWith("(");
        var isEnvironmentMessage = content.StartsWith("!");
        
        // Handle Global OOC - this will be processed normally by the server
        if (isGlobalOoc)
        {
            context.SetFlag(MessageContext.IS_GLOBAL_OOC);
            context.State = MessageContextState.STOP; // Stop further processing
            return context;
        }
        
        // Handle Emote
        if (isEmote)
        {
            context.Message = content.Substring(1); // Remove the * character
            context.SetFlag(MessageContext.IS_EMOTE);
            return context;
        }
        
        // Handle OOC
        if (isOOC)
        {
            context.SetFlag(MessageContext.IS_OOC);
            return context;
        }
        
        // Handle Environment Message
        if (isEnvironmentMessage)
        {
            context.Message = content.Substring(1); // Remove the ! character
            context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
            return context;
        }
        
        return context;
    }
    
    // Static utility method to check if a message is a global OOC message
    public static bool IsGlobalOOC(string message, ModConfig config)
    {
        return config.EnableGlobalOOC && message.StartsWith("((");
    }
} 