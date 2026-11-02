using System;
using System.Linq;
using thebasics.Configs;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class ChatTabLayout
{
    internal static bool Arrange(GuiTab[] tabs, GuiTab[] gameOrder, ModConfig config, int? proximityGroupId)
    {
        var ordered = gameOrder.ToList();
        var proximityIndex = ordered.FindIndex(tab => tab.DataInt == proximityGroupId);
        if (!config.UseGeneralChannelAsProximityChat && proximityIndex >= 0 &&
            (config.ProximityChatTabPosition == "First" || config.ProximityChatTabPosition == "AfterGeneral"))
        {
            var proximity = ordered[proximityIndex];
            ordered.RemoveAt(proximityIndex);
            var generalIndex = ordered.FindIndex(tab => tab.DataInt == GlobalConstants.GeneralChatGroup);
            ordered.Insert(config.ProximityChatTabPosition == "First" ? 0 : generalIndex + 1, proximity);
        }

        if (tabs.SequenceEqual(ordered)) return false;
        ordered.CopyTo(tabs);
        return true;
    }

    internal static int OpeningGroup(GuiTab[] tabs, ModConfig config, int? rememberedGroupId, int? proximityGroupId)
    {
        // General mode does not save tab choices, so do not restore a choice from separate-channel mode.
        if (config.UseGeneralChannelAsProximityChat) return GlobalConstants.GeneralChatGroup;
        if (config.PreserveDefaultChatChoice && rememberedGroupId.HasValue &&
            Array.Exists(tabs, tab => tab.DataInt == rememberedGroupId.Value)) return rememberedGroupId.Value;
        if (config.ProximityChatAsDefault && proximityGroupId.HasValue &&
            Array.Exists(tabs, tab => tab.DataInt == proximityGroupId.Value)) return proximityGroupId.Value;
        return GlobalConstants.GeneralChatGroup;
    }
}
