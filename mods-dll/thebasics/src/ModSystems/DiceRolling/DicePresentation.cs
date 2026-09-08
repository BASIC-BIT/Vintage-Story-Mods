using System.Globalization;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.DiceRolling;

internal static class DicePresentation
{
    internal static string Marker(ProximityChatMode mode) => mode switch
    {
        ProximityChatMode.Whisper => "(W) ",
        ProximityChatMode.Yell => "(Y) ",
        _ => ""
    };

    internal static string Summary(DiceRollResult result, ProximityChatMode mode) =>
        Marker(mode) + result.Value.ToString("G29", CultureInfo.InvariantCulture)
        + (result.IsSuccessPool ? " successes" : "");

    // formattedName comes from the existing RP name resolver; all dice input is escaped here.
    internal static string Chat(DiceRollResult result, string formattedName, ProximityChatMode mode, bool isPrivate)
    {
        var heading = isPrivate ? "[Private Roll] " : Marker(mode) + formattedName + " rolled ";
        var reason = string.IsNullOrWhiteSpace(result.Reason) ? "" : " (" + ChatHelper.EscapeMarkup(result.Reason) + ")";
        return heading + ChatHelper.EscapeMarkup(result.Expression) + " = "
            + Summary(result, ProximityChatMode.Normal) + reason
            + " [" + ChatHelper.EscapeMarkup(result.Breakdown) + "]";
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
