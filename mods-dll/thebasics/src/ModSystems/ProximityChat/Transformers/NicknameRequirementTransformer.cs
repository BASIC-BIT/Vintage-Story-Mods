using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class NicknameRequirementTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public NicknameRequirementTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        // Only check for the sender's context
        if (context.SendingPlayer != context.ReceivingPlayer)
        {
            return context;
        }
        
        // Skip check if it's not a message type that requires a nickname
        bool requiresNickname = context.Metadata.ContainsKey("isEmote") || 
                                (context.Metadata.ContainsKey("isPlayerChat") && !context.Metadata.ContainsKey("isGlobalOOC"));
        
        if (!requiresNickname)
        {
            return context;
        }
        
        // Check if player has a nickname
        if (!context.SendingPlayer.HasNickname())
        {
            // Add warning flag to metadata
            context.Metadata["showNicknameWarning"] = true;
            // Stop processing this message
            context.State = MessageContextState.STOP;
        }
        
        return context;
    }
    
    // Static method to send warning if needed
    public static void SendNicknameWarningIfNeeded(MessageContext context)
    {
        if (context.Metadata.TryGetValue("showNicknameWarning", out var showWarning) && (bool)showWarning)
        {
            context.SendingPlayer.SendMessage(
                GlobalConstants.CurrentChatGroup,
                "You need a nickname to use this feature! You can set it with `/nick MyName`",
                EnumChatType.CommandError
            );
        }
    }
} 