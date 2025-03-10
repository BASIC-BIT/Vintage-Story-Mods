using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class BabbleWarningTransformer : MessageTransformerBase
{
    public BabbleWarningTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        var lang = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        return context.HasFlag(MessageContext.IS_PLAYER_CHAT) && lang == LanguageSystem.BabbleLang;
    }

    public override MessageContext Transform(MessageContext context)
    {   
        // Send babble warning directly to the player
        context.SendingPlayer.SendMessage(
            _chatSystem.ProximityChatId,
            $"Warning: You are speaking in babble. Other players may not understand you.",
            EnumChatType.Notification
        );

        return context;
    }
}