using System.Collections.Generic;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class PlayerChatTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public PlayerChatTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        // Only process messages that have the isPlayerChat flag
        if (!context.Metadata.ContainsKey("isPlayerChat"))
        {
            return context;
        }
        
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
            context.Metadata["isGlobalOOC"] = true;
            context.State = MessageContextState.STOP; // Stop further processing
            return context;
        }
        
        // Handle Emote
        if (isEmote)
        {
            context.Message = content.Substring(1); // Remove the * character
            context.Metadata["isEmote"] = true;
            return context;
        }
        
        // Handle OOC
        if (isOOC)
        {
            context.Metadata["isOOC"] = true;
            return context;
        }
        
        // Handle Environment Message
        if (isEnvironmentMessage)
        {
            context.Message = content.Substring(1); // Remove the ! character
            context.Metadata["isEnvironmental"] = true;
            return context;
        }
        
        return context;
    }
} 