using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Handles simple non-RP messages that don't require special formatting.
/// </summary>
public class SimpleMessageTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public SimpleMessageTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        // Only process simple messages
        if (!context.Metadata.ContainsKey("isSimpleMessage"))
        {
            return context;
        }
        
        // For simple messages, we don't need any special transformations
        // The message content is already set, and we just ensure the chat type is set correctly
        if (!context.Metadata.ContainsKey("chatType"))
        {
            // Default to OthersMessage for simple messages
            context.Metadata["chatType"] = EnumChatType.OthersMessage;
        }
        
        return context;
    }
    
    /// <summary>
    /// Creates a message context for a simple, non-RP message
    /// </summary>
    public static MessageContext CreateSimpleMessageContext(IServerPlayer sender, string message, ProximityChatMode? chatMode = null)
    {
        return new MessageContext
        {
            Message = message,
            SendingPlayer = sender,
            ReceivingPlayer = sender, // Initially set to sender for validation
            Metadata = new System.Collections.Generic.Dictionary<string, object>
            {
                ["isSimpleMessage"] = true,
                ["chatMode"] = chatMode ?? sender.GetChatMode()
            }
        };
    }
} 