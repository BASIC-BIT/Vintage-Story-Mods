using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

// Escapes only raw speech content before any VTML is added downstream
public class EscapeSpeechContentTransformer : MessageTransformerBase
{
    public EscapeSpeechContentTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_SPEECH);
    }

    public override MessageContext Transform(MessageContext context)
    {
        context.Message = ChatHelper.EscapeMarkup(context.Message);
        return context;
    }
}



