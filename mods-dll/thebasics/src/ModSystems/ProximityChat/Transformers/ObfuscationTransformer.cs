using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class ObfuscationTransformer : IMessageTransformer
{
    private readonly DistanceObfuscationSystem _distanceObfuscationSystem;
    
    public ObfuscationTransformer(DistanceObfuscationSystem distanceObfuscationSystem)
    {
        _distanceObfuscationSystem = distanceObfuscationSystem;
    }
    
    public MessageContext Transform(MessageContext context)
    {
        var content = context.Message;
        _distanceObfuscationSystem.ObfuscateMessage(context.SendingPlayer, context.ReceivingPlayer, ref content);
        
        if (context.Metadata.TryGetValue("language", out var langObj) && langObj is Language lang)
        {
            if (context.Metadata.ContainsKey("isEmote"))
            {
                // For emotes, we need to handle font size differently
                if (_distanceObfuscationSystem.IsDistanceFontSizeEnabled())
                {
                    var fontSize = _distanceObfuscationSystem.GetFontSize(context.SendingPlayer, context.ReceivingPlayer);
                    content = $"<font color=\"{lang.Color}\" size=\"{fontSize}\">{content}</font>";
                }
                else
                {
                    content = $"<font color=\"{lang.Color}\">{content}</font>";
                }
            }
        }
        
        context.Message = content;
        return context;
    }
} 