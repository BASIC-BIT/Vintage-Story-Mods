using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Require nicknames if we're doing RP chat
public class NicknameRequirementTransformer : IMessageTransformer
{
    private readonly RPProximityChatSystem _chatSystem;
    
    public NicknameRequirementTransformer(RPProximityChatSystem chatSystem)
    {
        _chatSystem = chatSystem;
    }

    private bool RequiresNickname(MessageContext context)
    {
        var config = _chatSystem.GetModConfig();
        return !config.DisableNickname && 
        context.Metadata["isRoleplay"] as bool == true;
    }
    
    public MessageContext Transform(MessageContext context)
    {   
        // Check if the player has a nickname
        if (RequiresNickname(context) && !context.SendingPlayer.HasNickname())
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