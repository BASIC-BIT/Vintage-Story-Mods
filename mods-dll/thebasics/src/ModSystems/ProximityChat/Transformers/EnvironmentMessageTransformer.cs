using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class EnvironmentMessageTransformer : MessageTransformerBase
{
    public EnvironmentMessageTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {

    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_ENVIRONMENTAL);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var formattedMessage = ChatHelper.Italic(context.Message);
        context.SetMetadata(MessageContext.BUBBLE_TEXT_BASE, formattedMessage);

        context.Message = ChatHelper.ApplyFreeformAttribution(formattedMessage, context.SendingPlayer, _config);

        return context;
    }
}
