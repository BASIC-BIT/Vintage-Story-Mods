using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Require nicknames if we're doing RP chat
public class NicknameRequirementTransformer : MessageTransformerBase
{   
    public NicknameRequirementTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return !_config.DisableNicknames && 
            context.HasFlag(MessageContext.IS_ROLEPLAY) &&
            !context.SendingPlayer.HasNickname();
    }

    public override MessageContext Transform(MessageContext context)
    {   
        // Send nickname requirement warning directly to the player
        context.SendingPlayer.SendMessage(
            _chatSystem.ProximityChatId,
            "You need a nickname to use proximity chat! You can set it with `/nick MyName`",
            EnumChatType.CommandError
        );
        
        // Stop processing this message
        context.State = MessageContextState.STOP;

        return context;
    }
}
