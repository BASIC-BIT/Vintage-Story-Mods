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
        // TODO: Does this same logic need to be applied in the EmoteTransformer?
        return context.HasFlag(MessageContext.IS_SPEECH);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var content = context.Message;

        _distanceObfuscationSystem.ObfuscateMessage(context.SendingPlayer, context.ReceivingPlayer, ref content);
        
        context.Message = content;
        return context;
    }
} 