using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class BabbleWarningTransformer : MessageTransformerBase
{
    public BabbleWarningTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        var lang = context.GetMetadata<Language>(MessageContext.LANGUAGE);
        // TODO: If it's an emote, we only want to send this if they're using quoted speech
        return (context.HasFlag(MessageContext.IS_EMOTE) ||
            context.HasFlag(MessageContext.IS_SPEECH)) &&
                lang == LanguageSystem.BabbleLang;
    }

    public override MessageContext Transform(MessageContext context)
    {   
        // Send babble warning directly to the player
        context.SendingPlayer.SendMessage(
            _chatSystem.ProximityChatId,
            Lang.Get("thebasics:chat-babble-warning"),
            EnumChatType.Notification
        );

        return context;
    }
}