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
        // Unlimited speech does not need player positions.
        var distance = ModConfig.IsUnlimitedRange(_config.GetModeDistance(chatMode)) ? 0 : sendingPlayer.GetDistance(receivingPlayer) + occlusionPenalty;
        return CalculateFontSize(_config, chatMode, distance);
    }

    internal static int CalculateFontSize(ModConfig config, ProximityChatMode mode, double distance)
    {
        var range = config.GetModeDistance(mode);
        var defaultSize = config.GetModeDefaultFontSize(mode);
        var sizes = config.ProximityChatClampFontSizes is { Length: > 0 } ? config.ProximityChatClampFontSizes : ModConfig.DefaultClampFontSizes;
        var size = ModConfig.IsUnlimitedRange(range) ? defaultSize :
            ((defaultSize - sizes.Min()) * (1.0d - distance / range)) + sizes.Min();
        return sizes.MinBy(candidate => Math.Abs(candidate - size));
    }
}
