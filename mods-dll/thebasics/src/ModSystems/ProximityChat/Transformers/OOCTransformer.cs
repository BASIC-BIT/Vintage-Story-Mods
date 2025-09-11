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
        context.Message = $"(OOC) {context.GetMetadata<string>(MessageContext.FORMATTED_NAME)}: {context.Message}";

        var oocColor = _config.ColorThemes.OOCTheme.GetEffectiveColor(context.SendingPlayer);
        context.Message = ChatHelper.Color(context.Message, oocColor);

        return context;
    }
}