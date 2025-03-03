using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class BabbleWarningTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;

    public BabbleWarningTransformer(RPProximityChatSystem chatSystem, LanguageSystem languageSystem) : base(chatSystem)
    {
        _languageSystem = languageSystem;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        var lang = context.Metadata["language"] as Language;
        return context.Metadata.ContainsKey("isPlayerChat") && lang != LanguageSystem.BabbleLang;
    }

    public override MessageContext Transform(MessageContext context)
    {
        // Get the message text
        var message = context.Message;
        
        // Send babble warning directly to the player
        context.SendingPlayer.SendMessage(
            _chatSystem.GetProximityChatGroupId(),
            $"Warning: You are speaking in babble. Other players may not understand you.",
            EnumChatType.Notification
        );

        return context;
    }
}