using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class EnvironmentMessageTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public EnvironmentMessageTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        if (!context.Metadata.ContainsKey("isEnvironmental"))
        {
            return context;
        }
        
        // Environment messages are already tagged with "isEnvironmental" by GetPlayerChat
        // We just need to ensure they're properly formatted
        
        var content = context.Message;
        
        // Apply italic formatting to environmental messages for visual distinction
        context.Message = $"<i>{content}</i>";
        
        return context;
    }
} 