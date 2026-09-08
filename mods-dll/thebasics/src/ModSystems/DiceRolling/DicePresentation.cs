using System.Globalization;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.DiceRolling;

internal static class DicePresentation
{
    internal const int MaxOutputLength = 1900;

    internal static string Marker(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Whisper => "(W) ",
        ProximityChatMode.Yell => "(Y) ",
        _ => ""
    };

    internal static string Summary(DiceRollResult result, ProximityChatMode mode) =>
        Marker(mode) + result.Value.ToString("G29", CultureInfo.InvariantCulture)
        + (result.IsSuccessPool ? " successes" : "");

    // Plain body for history and extension consumers, which own their attribution and escaping.
    internal static string Body(DiceRollResult result)
    {
        var reason = string.IsNullOrWhiteSpace(result.Reason) ? "" : " (" + result.Reason + ")";
        return result.Expression + " = " + Summary(result, ProximityChatMode.Normal) + reason
            + " [" + result.Breakdown + "]";
    }
    // formattedName comes from the existing RP name resolver; all dice input is escaped here.
    internal static string Chat(DiceRollResult result, string formattedName, ProximityChatMode mode, bool isPrivate)
    {
        var heading = isPrivate ? "[Private Roll] " : Marker(mode) + formattedName + " rolled ";
        var rendered = heading + ChatHelper.EscapeMarkup(Body(result));
        // Th3Essentials batches at 1950 characters. Check the entire escaped/decoded envelope,
        // including attribution, reason and mention neutralization, before any recipient sees it.
        if (Th3EssentialsDiscordRelay.FormatRelayMessage(rendered, suppressMentions: true).Length > MaxOutputLength)
            throw new DiceRollException("limit", "Roll output is too long. Use fewer dice or a shorter reason.");
        return rendered;
    }

    internal static string Bubble(DiceRollResult result, IServerPlayer player, ProximityChatMode mode, ModConfig config, bool isPrivate)
    {
        if (isPrivate || player.Entity == null || !SpectatorChatPolicy.ShouldEmitEntityAttachedCues(player)) return null;
        var policy = OverheadChatBubbleModes.Normalize(config.OverheadChatBubbleMode, config.DisableRpOverheadBubbles);
        if (policy == OverheadChatBubbleModes.Off) return null;
        var summary = Summary(result, mode);
        if (policy == OverheadChatBubbleModes.Vanilla)
            return $"from:{player.Entity.EntityId},msg:{ChatHelper.EscapeMarkup(Marker(mode) + (result.SimpleSides is int sides ? $"d{sides} " : "Roll ") + Summary(result, ProximityChatMode.Normal))}";
        // A numeric suffix carries the plain dN badge; no expression grammar in the renderer.
        var kind = "dice" + result.SimpleSides?.ToString(CultureInfo.InvariantCulture);
        return $"from:{player.Entity.EntityId},msg\u001fkind={kind}\u001fmode={mode.ToString().ToLowerInvariant()}:{ChatHelper.EscapeMarkup(summary)}";
    }
}
