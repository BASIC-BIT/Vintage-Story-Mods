using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

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
        var config = _chatSystem.GetModConfig();
        
        // Skip if nicknames are disabled in the config
        if (config.DisableNicknames)
        {
            return context;
        }
        
        // Only check for nickname requirement during player chat
        if (!context.Metadata.ContainsKey("isPlayerChat"))
        {
            return context;
        }
        
        // Only process for the sending player
        if (context.SendingPlayer != context.ReceivingPlayer)
        {
            return context;
        }
        
        // Check if the player has a nickname
        if (!context.SendingPlayer.HasNickname())
        {
            // Send nickname requirement warning directly to the player
            context.SendingPlayer.SendMessage(
                _chatSystem.GetProximityChatGroupId(),
                "You need a nickname to use proximity chat! You can set it with `/nick MyName`",
                EnumChatType.CommandError
            );
            
            // Stop processing this message
            context.State = MessageContextState.STOP;
        }
        
        return context;
    }
} 