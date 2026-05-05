using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class LanguageTransformer : MessageTransformerBase
{
    private readonly LanguageSystem _languageSystem;

    public LanguageTransformer(LanguageSystem languageSystem, RPProximityChatSystem chatSystem) : base(chatSystem)
    {
        _languageSystem = languageSystem;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        // Emotes language is handled by the EmoteTransformer
        // Prose treats only quoted segments as speech and handles those during formatting.
        return context.HasFlag(MessageContext.IS_SPEECH) &&
               ProximityChatPresentationModes.Normalize(_config.ProximityChatPresentationMode) != ProximityChatPresentationModes.Prose &&
               _config.EnableLanguageSystem &&
               !_config.DisableRPChat;
    }

    public override MessageContext Transform(MessageContext context)
    {
        var content = context.Message;
        _languageSystem.ProcessMessage(context.ReceivingPlayer, ref content, context.GetMetadata<Language>(MessageContext.LANGUAGE));

        context.Message = content;

        return context;
    }
}
