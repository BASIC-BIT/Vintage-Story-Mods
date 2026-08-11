using thebasics.Configs;
using thebasics.Extensions;
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
        return context.HasFlag(MessageContext.IS_SPEECH) &&
               ProximityChatPresentationModes.Normalize(_config.ProximityChatPresentationMode) != ProximityChatPresentationModes.Prose;
    }

    public override MessageContext Transform(MessageContext context)
    {
        var content = context.Message;

        // Pass the message's chat mode explicitly: a /yell from a player whose sticky mode is Normal
        // must obfuscate against the yell range that chose the recipients, not the sticky one.
        _distanceObfuscationSystem.ObfuscateMessage(context.SendingPlayer, context.ReceivingPlayer, ref content,
            tempMode: context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode()),
            occlusionPenalty: context.GetOcclusionPenalty(context.ReceivingPlayer));

        context.Message = content;
        return context;
    }
}
