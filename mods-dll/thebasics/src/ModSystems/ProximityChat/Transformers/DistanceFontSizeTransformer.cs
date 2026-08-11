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
        var maxRange = _config.GetModeDistance(chatMode);
        var defaultSize = _config.GetModeDefaultFontSize(chatMode);

        // Unlimited range has no far edge to scale against, so every listener reads it at full size.
        // Checked before reading positions so this never depends on both players having entities.
        if (ModConfig.IsUnlimitedRange(maxRange))
        {
            return GetClampedFontSize(defaultSize);
        }

        var distance = sendingPlayer.GetDistance(receivingPlayer) + occlusionPenalty;

        var minFontSize = ClampFontSizes.Min();

        var unclampedSize = ((defaultSize - minFontSize) * (1.0d - (distance / maxRange))) + minFontSize;

        var clampedSize = GetClampedFontSize(unclampedSize);

        return clampedSize;
    }

    private static readonly int[] FallbackClampFontSizes = [30, 16, 12, 9];

    private int[] ClampFontSizes =>
        _config.ProximityChatClampFontSizes is { Length: > 0 }
            ? _config.ProximityChatClampFontSizes
            : FallbackClampFontSizes;

    private int GetClampedFontSize(double unclamped)
    {
        // Closest allowed size to the computed one. A hand-edited empty array survives the ??=
        // default, so this reads through the guarded accessor rather than the config directly.
        return ClampFontSizes.MinBy(size => Math.Abs(size - unclamped));
    }
}
