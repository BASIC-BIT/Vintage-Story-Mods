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
        // Apply italic formatting
        context.Message = ChatHelper.Italic(context.Message);
        
        return context;
    }
} 