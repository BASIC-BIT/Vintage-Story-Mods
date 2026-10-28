using System;
using System.Linq;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat;
using thebasics.Utilities;
namespace thebasics.ModSystems.ChatUiSystem;

internal static class SetupWizardVoicePreview
{
    internal static bool Supports(string page) => page is "chat.voices" or "chat.tts";

    internal static string Text(SetupWizardDraft draft, string page, bool? rpttsDetected)
    {
        SetupWizardCatalog.TryGetPage(page, out var definition);
        var config = SetupWizardTextAppearancePreview.Config(draft);
        var errors = ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values.Where(value => definition.SettingKeys.Contains(value.Key)));
        if (errors.Count > 0)
        {
            var message = errors[0];
            foreach (var key in definition.SettingKeys)
                if (ConfigAdminSettingRegistry.TryGet(key, out var setting)) message = message.Replace(key, setting.Label.Replace("Normal", "Talk"), StringComparison.Ordinal);
            return "Invalid draft: " + VtmlUtils.EscapeVtml(message);
        }
        return page == "chat.voices"
            ? (config.EnableChatter ? "<strong>Seraph voices on</strong>" : "<strong>Seraph voices off</strong>") +
                "<br><br>Volume 0 mutes a mode. Pitch 1 keeps the normal voice." +
                "<br><br>Self volume multiplies only the speaker's own voice. Other listeners keep the mode's volume."
            : (rpttsDetected == null ? "<strong>RPTTS availability unknown in this preview.</strong>" : rpttsDetected.Value
                ? "<strong>RPTTS detected on this client.</strong><br>Audio readiness unverified."
                : "<strong>RPTTS not active on this client.</strong>") +
                "<br><br>Gain controls audio level." +
                "<br><br>Distance falloff is passed to RPTTS; its fade curve depends on RPTTS.";
    }
    internal static string Label(string key)
    {
        var parts = key.Split('.');
        var mode = parts.Length > 1 ? parts[1] == "Normal" ? "Talk" : parts[1] : "";
        return parts[0] switch
        {
            "ChatterSelfVolumeMultiplier" => "Speaker's own volume multiplier (0 to 4)",
            "ChatterModeVolume" => mode + " volume (0 to 4)",
            "ChatterModePitch" => mode + " pitch (0.1 to 4)",
            "RPTTS_ModeGain" => mode + " gain (0 to 4)",
            "RPTTS_ModeFalloff" => mode + " distance falloff (0.1 to 10)",
            _ => null
        };
    }
}
