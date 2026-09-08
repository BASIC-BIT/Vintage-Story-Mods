using System.Collections.Generic;
using System.Linq;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.ProximityChat.Models;
namespace thebasics.ModSystems.DiceRolling;

internal static class DiceAnalytics
{
    internal static void RecordResult(DiceRollResult result, ProximityChatMode mode, bool isPrivate)
    {
        if (isPrivate) return;
        var mechanics = result.Mechanics;
        AnalyticsService.TrackFeatureUsed("dice", "send", properties: new Dictionary<string, object>
        {
            ["chat_type"] = mode.ToString().ToLowerInvariant(),
            ["dice_complexity"] = result.SimpleSides.HasValue ? "simple" : "advanced",
            ["dice_explode"] = mechanics.Contains("explode"),
            ["dice_reroll"] = mechanics.Contains("reroll"),
            ["dice_keep_drop"] = mechanics.Contains("keep_drop"),
            ["dice_success_pool"] = mechanics.Contains("success_pool"),
            ["dice_arithmetic"] = mechanics.Contains("arithmetic"),
            ["dice_helpers"] = mechanics.Any(value => value is "floor" or "ceil" or "round")
        });
    }
}
