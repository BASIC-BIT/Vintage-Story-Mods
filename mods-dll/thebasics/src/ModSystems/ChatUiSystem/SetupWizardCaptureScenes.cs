using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Named data fixtures for the production dialog, shared by layout and native captures.</summary>
public static class SetupWizardCaptureScenes
{
    public static readonly string[] Names =
    [
        "wizard-hub", "wizard-chat-default", "wizard-chat-edited", "wizard-chat-presentation", "wizard-chat-language", "wizard-chat-ranges", "wizard-chat-tabs",
        "wizard-travel-default", "wizard-travel-edited", "wizard-notifications-default", "wizard-notifications-edited", "wizard-notifications-sleep",
        "wizard-review", "wizard-error", "wizard-restart-dedicated", "wizard-restart-integrated",
        "wizard-appearance-bubbles", "wizard-appearance-names", "wizard-map-visibility", "wizard-characters-sheets",
        "wizard-roleplay-dice", "wizard-server-history", "wizard-server-stats"
    ];

    public static List<ConfigAdminSettingValue> DefaultValues()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return ConfigAdminSettingRegistry.Settings.Where(setting => SetupWizardCatalog.IsSettingKey(setting.Key)).Select(setting => new ConfigAdminSettingValue
        { Key = setting.Key, Value = setting.GetValue(config) }).ToList();
    }

    public static void Validate(string scenario, double previewTime)
    {
        if (!Names.Contains(scenario, StringComparer.Ordinal)) throw new ArgumentException("Unknown wizard capture scene: " + scenario);
        if (!double.IsFinite(previewTime) || previewTime is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(previewTime), "Preview time must be 0..10 seconds.");
    }

    public static void ApplyDraft(SetupWizardDraft draft, string scenario)
    {
        Validate(scenario, 0);
        // Only QA-owned drafts use these deterministic values. Nothing is submitted to the server.
        foreach (var value in DefaultValues()) draft.Set(value.Key, value.Value);
        if (scenario is "wizard-chat-edited" or "wizard-review" or "wizard-error" or "wizard-restart-dedicated" or "wizard-restart-integrated")
        {
            draft.Set("ProximityChatPresentationMode", "Prose");
            draft.Set("UseGeneralChannelAsProximityChat", "1");
        }
        if (scenario == "wizard-travel-edited")
        {
            draft.Set("AllowPlayerTpa", "0");
            draft.Set("Teleportation.RegisterTopCommand", "0");
        }
        if (scenario == "wizard-notifications-edited")
        {
            draft.Set("SendServerSaveAnnouncement", "1");
            draft.Set("ServerSaveAnnouncementAsNotification", "0");
            draft.Set("SendServerSaveFinishedAnnouncement", "0");
        }
    }

    public static void Show(SetupWizardDialog dialog, string scenario, double previewTime)
    {
        Validate(scenario, previewTime);
        ApplyDraft(dialog.Draft, scenario);
        var page = scenario switch
        {
            "wizard-hub" => "hub",
            "wizard-chat-language" => "chat.language",
            "wizard-chat-ranges" => "chat.ranges",
            "wizard-chat-tabs" => "chat.tabs",
            "wizard-travel-default" or "wizard-travel-edited" => "teleport.tools",
            "wizard-notifications-default" or "wizard-notifications-edited" => "notifications.savestart",
            "wizard-notifications-sleep" => "notifications.sleep",
            "wizard-appearance-bubbles" => "appearance.bubbles",
            "wizard-appearance-names" => "appearance.names",
            "wizard-map-visibility" => "map.visibility",
            "wizard-characters-sheets" => "characters.sheets",
            "wizard-roleplay-dice" => "roleplay.dice",
            "wizard-server-history" => "server.history",
            "wizard-server-stats" => "server.stats",
            "wizard-review" => "review",
            _ => "chat.basics"
        };
        dialog.ShowPage(page);
        if (scenario == "wizard-error") dialog.SetRequestFailure("The setting changed on the server. Review your changes and try saving again.");
        if (scenario.StartsWith("wizard-restart-", StringComparison.Ordinal))
        {
            dialog.Draft.CreateSaveRequest(1, dialog.RunId);
            dialog.SetResult(new TheBasicsSetupWizardResultMessage
            {
                Kind = SetupWizardResultKind.SaveResult, RequestId = 1, RunId = dialog.RunId, Success = true,
                Message = "Settings saved. Restart the server to apply them.",
                Values = dialog.Draft.Values.ToList(), RestartRequiredKeys = ["UseGeneralChannelAsProximityChat"],
                IsDedicated = scenario == "wizard-restart-dedicated"
            });
        }
        dialog.SetPreviewTime(previewTime);
        if (scenario == "wizard-chat-presentation")
        {
            var bounds = dialog.SingleComposer.GetElement("ProximityChatPresentationMode").Bounds;
            var x = (int)(bounds.renderX + bounds.OuterWidth / 2);
            var y = (int)(bounds.renderY + bounds.OuterHeight / 2);
            dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
    }
}
