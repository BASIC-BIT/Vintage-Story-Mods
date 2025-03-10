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
        return context.HasMetadata(MessageContext.IS_OOC);
    }

    public override MessageContext Transform(MessageContext context)
    {
        context.Message = ChatHelper.Color("#808080", $"(OOC) {context.GetMetadata<string>(MessageContext.FORMATTED_NAME)}: {context.Message}");

        return context;
    }
} 