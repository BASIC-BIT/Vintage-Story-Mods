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
        var envColor = _config.ColorThemes.EnvironmentalTheme.GetEffectiveColor(context.SendingPlayer);
        
        context.Message = ChatHelper.Italic(context.Message);
        context.Message = ChatHelper.Color(context.Message, envColor);
        
        return context;
    }
} 