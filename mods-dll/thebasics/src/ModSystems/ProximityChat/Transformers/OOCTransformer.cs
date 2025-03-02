using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class OOCTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public OOCTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        // Check if this message has already been processed as OOC
        if (context.Metadata.ContainsKey("isProcessedOOC"))
        {
            return context;
        }
        
        var content = context.Message;
        
        // Check if message is OOC format (processed prior to transformer)
        if (context.Metadata.TryGetValue("isOOC", out var _))
        {
            // Format the OOC message
            var player = context.SendingPlayer;
            content = ChatHelper.Color("#808080", $"(OOC) {player.PlayerName}: {content}");
            context.Message = content;
            context.Metadata["isProcessedOOC"] = true;
            return context;
        }
        
        // Check if it's a regular OOC format with parentheses
        if (content.StartsWith("(") && !content.EndsWith(")"))
        {
            // Add the closing parenthesis if missing
            content += ")";
            context.Message = content;
        }
        
        // Mark as processed
        context.Metadata["isProcessedOOC"] = true;
        return context;
    }
} 