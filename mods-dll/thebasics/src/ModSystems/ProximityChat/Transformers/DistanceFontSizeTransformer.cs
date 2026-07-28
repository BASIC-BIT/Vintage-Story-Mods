using System;
using System.Linq;
using thebasics.Configs;
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
        var fontSize = GetFontSize(context.SendingPlayer, context.ReceivingPlayer, chatMode,
            context.GetOcclusionPenalty(context.ReceivingPlayer));

        context.Message = $"<font size=\"{fontSize}\">{context.Message}</font>";

        return context;
    }


    public int GetFontSize(IServerPlayer sendingPlayer, IServerPlayer receivingPlayer,
        ProximityChatMode chatMode, int occlusionPenalty = 0)
    {
        // Doesn't check if the system is disabled, that's up to the consumer

        // Matches the obfuscation gradient: occluding geometry reads as extra distance.
        var maxRange = _config.ProximityChatModeDistances[chatMode];
        var defaultSize = _config.ProximityChatDefaultFontSize[chatMode];

        // Unlimited range has no far edge to scale against, so every listener reads it at full size.
        // Checked before reading positions so this never depends on both players having entities.
        if (ModConfig.IsUnlimitedRange(maxRange))
        {
            return GetClampedFontSize(defaultSize);
        }

        var distance = sendingPlayer.GetDistance(receivingPlayer) + occlusionPenalty;

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
