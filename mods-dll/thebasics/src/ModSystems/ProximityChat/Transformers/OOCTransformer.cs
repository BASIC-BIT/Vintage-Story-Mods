#pragma warning disable S101 // OOC is the player-facing command acronym used throughout chat code.
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class OOCTransformer : MessageTransformerBase
{
    public OOCTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_OOC);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var color = ChatVisualPreferenceResolver.GetOocColor(context.ReceivingPlayer, _config);
        context.Message = ChatHelper.Color($"(OOC) {context.GetMetadata<string>(MessageContext.FORMATTED_NAME)}: {context.Message}", color);

        return context;
    }
}
