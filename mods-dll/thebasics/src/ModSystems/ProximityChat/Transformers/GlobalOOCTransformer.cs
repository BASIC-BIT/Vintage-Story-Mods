#pragma warning disable S101 // OOC is the player-facing command acronym used throughout chat code.
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class GlobalOOCTransformer : MessageTransformerBase
{
    public GlobalOOCTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_GLOBAL_OOC);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var color = ChatVisualPreferenceResolver.GetGlobalOocColor(context.ReceivingPlayer, _config);
        context.Message = ChatHelper.Color($"(GOOC) {context.GetMetadata<string>(MessageContext.FORMATTED_NAME)}: {context.Message}", color);

        return context;
    }
}
