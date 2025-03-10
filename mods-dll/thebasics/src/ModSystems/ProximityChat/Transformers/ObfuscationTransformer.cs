using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ObfuscationTransformer : MessageTransformerBase
{
    private readonly DistanceObfuscationSystem _distanceObfuscationSystem;
    
    public ObfuscationTransformer(DistanceObfuscationSystem distanceObfuscationSystem, RPProximityChatSystem chatSystem) : base(chatSystem)
    {
        _distanceObfuscationSystem = distanceObfuscationSystem;
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_ROLEPLAY) && !context.HasFlag(MessageContext.IS_EMOTE) && !context.HasFlag(MessageContext.IS_ENVIRONMENTAL) && !context.HasFlag(MessageContext.IS_OOC);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var content = context.Message;
        _distanceObfuscationSystem.ObfuscateMessage(context.SendingPlayer, context.ReceivingPlayer, ref content);
        
        if (context.TryGetMetadata<Language>(MessageContext.LANGUAGE, out var lang))
        {
            if (context.HasMetadata(MessageContext.IS_EMOTE))
            {
                // For emotes, we need to handle font size differently
                if (_distanceObfuscationSystem.IsDistanceFontSizeEnabled())
                {
                    var fontSize = _distanceObfuscationSystem.GetFontSize(context.SendingPlayer, context.ReceivingPlayer);
                    content = $"<font color=\"{lang.Color}\" size=\"{fontSize}\">{content}</font>";
                }
                else
                {
                    content = ChatHelper.LangColor(content, lang);
                }
            }
        }
        
        context.Message = content;
        return context;
    }
} 