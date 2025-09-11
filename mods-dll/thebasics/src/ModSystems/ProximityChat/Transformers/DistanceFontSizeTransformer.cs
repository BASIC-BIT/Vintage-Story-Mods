using System;
using System.Linq;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class DistanceFontSizeTransformer : MessageTransformerBase
{

    public DistanceFontSizeTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return _config.EnableDistanceFontSizeSystem &&
            (context.HasFlag(MessageContext.IS_EMOTE) || context.HasFlag(MessageContext.IS_SPEECH));
    }

    public override MessageContext Transform(MessageContext context)
    {
        var chatMode = context.GetMetadata(MessageContext.CHAT_MODE, context.SendingPlayer.GetChatMode());
        var fontSize = GetFontSize(context.SendingPlayer, context.ReceivingPlayer, chatMode);

        context.Message = $"<font size=\"{fontSize}\">{context.Message}</font>";

        return context;
    }
    

    public int GetFontSize(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer,
        ProximityChatMode chatMode)
    {
        // Doesn't check if the system is disabled, that's up to the consumer

        var distance = sendingPlayer.GetDistance(receivingPlayer);
        var maxRange = _config.ProximityChatModeDistances[chatMode];
        var defaultSize = _config.ProximityChatDefaultFontSize[chatMode];

        var minFontSize = _config.ProximityChatClampFontSizes.Min();
        
        var unclampedSize = ((defaultSize - minFontSize) * (1.0d - (distance / maxRange))) + minFontSize;

        var clampedSize = GetClampedFontSize(unclampedSize);

        return clampedSize;
    }

    private int GetClampedFontSize(double unclamped)
    {
        // Get the closest value in the Config.ProximityChatClampFontSizes array to the unclamped value
        return _config.ProximityChatClampFontSizes.MinBy(size => Math.Abs(size - unclamped));
    }
}