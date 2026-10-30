using System;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>A repeatable chat-focus example, with no real chat or client-setting changes.</summary>
public static class SetupWizardChatFocusPreview
{
    public const double DurationSeconds = 10;
    private static readonly GuiTab[] Tabs =
        [new() { Name = "General", DataInt = 0 }, new() { Name = "Proximity", DataInt = 1 }];

    public readonly record struct State(int PhaseIndex, int SelectedTabIndex, bool IsOpen, string StageText, string Messages);

    public static State At(SetupWizardDraft draft, double timeSeconds, bool autoChatOpenSelected,
        bool autoChat = true, int? selectedTabIndex = null)
    {
        double time = double.IsFinite(timeSeconds) ? Math.Max(0, timeSeconds) % DurationSeconds : 0;
        int phase = time < 1 ? 0 : time < 2 ? 1 : time < 4 ? 2 : time < 6 ? 3 : time < 7 ? 4 : 5;
        var config = new ModConfig
        {
            UseGeneralChannelAsProximityChat = On(draft, "UseGeneralChannelAsProximityChat", false),
            ProximityChatAsDefault = On(draft, "ProximityChatAsDefault", true),
            PreserveDefaultChatChoice = On(draft, "PreserveDefaultChatChoice", true),
            PreventProximityChannelSwitching = On(draft, "PreventProximityChannelSwitching", true)
        };
        int proximity = config.UseGeneralChannelAsProximityChat ? 0 : 1;
        int opening = ChatTabLayout.OpeningGroup(Tabs, config, null, proximity);
        int selected = opening;

        // The game switches for a notification only when both client preferences allow it.
        bool noticeSwitches = autoChat && autoChatOpenSelected &&
            !(config.PreventProximityChannelSwitching && selected == proximity);
        if (phase >= 1 && noticeSwitches) selected = 0;
        if (phase >= 2) selected = 0; // The player explicitly chooses General.
        if (phase >= 5) selected = ChatTabLayout.OpeningGroup(Tabs, config, 0, proximity);
        if (selectedTabIndex.HasValue)
            selected = config.UseGeneralChannelAsProximityChat || selectedTabIndex.Value != 1 ? 0 : 1;

        string name = selected == 0 ? "General" : string.IsNullOrEmpty(draft?.Get("ProximityChatName")) ? "Proximity" : draft.Get("ProximityChatName");
        string stage = phase switch
        {
            0 => "Chat opens on " + name + ".",
            1 when noticeSwitches && opening != 0 => "A General notice switches to General.",
            1 => "A General notice arrives. " + name + " stays selected.",
            2 => "You choose General.",
            3 when config.UseGeneralChannelAsProximityChat => "Nearby speech arrives in General.",
            3 => "Nearby speech arrives in Proximity. Speech does not switch tabs.",
            4 => "You close chat.",
            _ when config.UseGeneralChannelAsProximityChat => "You reopen chat on General.",
            _ when config.PreserveDefaultChatChoice => "Reopening remembers your General tab.",
            _ => "Reopening uses the default " + name + " tab."
        };
        bool isOpen = phase != 4;
        if (selectedTabIndex.HasValue && isOpen) stage = "You choose " + name + " in the paused example.";
        string messages = string.Empty;
        if (isOpen)
        {
            if (selected == 0)
            {
                messages = "MaraPlayer: Anyone heading north?";
                if (phase >= 1) messages += "<br><font color=\"#CCe0cfbb\">Server notice: The market is open.</font>";
            }
            // Nearby speech is OthersMessage, which the game's incoming-message handler never auto-selects.
            if (phase >= 3 && selected == proximity)
                messages += (messages.Length == 0 ? "" : "<br>") + "Pip: Meet me by the gate.";
        }
        return new State(phase, selected, isOpen, stage, messages);
    }

    private static bool On(SetupWizardDraft draft, string key, bool fallback) =>
        ConfigAdminSettingDefinition.TryParseBool(draft?.Get(key), out bool value) ? value : fallback;
}
