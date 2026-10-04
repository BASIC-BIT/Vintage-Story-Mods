using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.Analytics;

namespace thebasics.ModSystems.AdminConfig;

public sealed class SetupWizardState
{
    private const int MaxValueLength = 4096;
    private const int MaxPayloadLength = 65536;
    private readonly Dictionary<string, string> _startupValues;

    public SetupWizardState(ModConfig startup)
    {
        var snapshot = BaseBasicModSystem.CloneConfig(startup);
        _startupValues = ConfigAdminSettingRegistry.Settings
            .Where(setting => setting.ReloadBehavior == ConfigAdminReloadBehavior.RestartRequired)
            .ToDictionary(setting => setting.Key, setting => setting.GetValue(snapshot), StringComparer.OrdinalIgnoreCase);
    }

    public bool TryValidatePatch(ModConfig current, TheBasicsSetupWizardRequestMessage request, out ModConfig draft,
        out List<string> changedKeys, out List<string> conflictKeys, out List<string> errors)
    {
        draft = null;
        changedKeys = new();
        conflictKeys = new();
        errors = ValidateRequest(request).ToList();
        if (request?.Kind != SetupWizardRequestKind.Save) errors.Add("Expected a setup save request.");
        if (errors.Count > 0) return false;

        var originals = request.OriginalValues.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
        var candidate = BaseBasicModSystem.CloneConfig(current);
        foreach (var value in request.Values)
        {
            ConfigAdminSettingRegistry.TryGet(value.Key, out var setting);
            if (setting.GetValue(current) != originals[value.Key]) conflictKeys.Add(setting.Key);
        }
        if (conflictKeys.Count > 0)
        {
            errors.Add($"These settings changed since setup opened: {string.Join(", ", conflictKeys)}. Review your draft and save again.");
            return false;
        }

        errors.AddRange(ConfigAdminSaveWorkflow.ApplyValues(candidate, request.Values));
        if (errors.Count == 0) errors.AddRange(ConfigAdminSettingRegistry.ValidateConfig(candidate));
        if (errors.Count > 0) return false;

        foreach (var value in request.Values)
        {
            ConfigAdminSettingRegistry.TryGet(value.Key, out var setting);
            if (setting.GetValue(current) != setting.GetValue(candidate)) changedKeys.Add(setting.Key);
        }
        draft = candidate;
        return true;
    }

    public IReadOnlyList<string> GetPendingRestartKeys(ModConfig current) =>
        ConfigAdminSettingRegistry.Settings.Where(setting => _startupValues.TryGetValue(setting.Key, out var startup) && setting.GetValue(current) != startup)
            .Select(setting => setting.Key).ToArray();

    public static IReadOnlyList<string> ValidateRequest(TheBasicsSetupWizardRequestMessage request)
    {
        var errors = new List<string>();
        if (request == null) return new[] { "Missing setup request." };
        if (!Enum.IsDefined(request.Kind)) errors.Add("Unknown setup request kind.");
        if (request.RequestId < 0 || request.Kind == SetupWizardRequestKind.Save && request.RequestId == 0) errors.Add("Invalid setup request ID.");
        if ((request.RunId?.Length ?? 0) > 64 || (request.RunId?.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_') ?? false)) errors.Add("Invalid setup run ID.");
        if ((request.StepId?.Length ?? 0) > 64 || (request.ChoiceId?.Length ?? 0) > 64 || (request.JourneyAction?.Length ?? 0) > 64) errors.Add("Setup journey fields are too long.");

        var values = ValidateValues(request.Values, "values", errors);
        var originals = ValidateValues(request.OriginalValues, "original values", errors);
        if (request.Kind == SetupWizardRequestKind.Save)
        {
            if (!values.SetEquals(originals)) errors.Add("Every changed setting needs exactly one original value.");
        }
        else if (values.Count > 0 || originals.Count > 0) errors.Add("Only save requests may contain setting values.");
        if (request.Kind == SetupWizardRequestKind.Track &&
            !AnalyticsService.IsValidSetupWizardJourney(request.RunId, 1, request.StepId, request.JourneyAction, request.ChoiceId)) errors.Add("Unknown setup journey step, action, or choice.");
        return errors;
    }

    private static HashSet<string> ValidateValues(IReadOnlyList<ConfigAdminSettingValue> values, string name, List<string> errors)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values == null)
        {
            errors.Add($"Missing setup {name}.");
            return keys;
        }
        if (values.Count > SetupWizardCatalog.Pages.Sum(page => page.SettingKeys.Count))
        {
            errors.Add($"Too many setup {name}.");
            return keys;
        }
        var length = 0;
        foreach (var value in values)
        {
            if (value?.Key == null || value.Key.Length > 128 || !SetupWizardCatalog.IsSettingKey(value.Key))
            {
                errors.Add($"Unknown setup setting in {name}.");
                continue;
            }
            if (!keys.Add(value.Key)) errors.Add($"Duplicate setup setting in {name}: {value.Key}.");
            if (value.Value == null || value.Value.Length > MaxValueLength) errors.Add($"Setup value is missing or too long: {value.Key}.");
            length += value.Key.Length + (value.Value?.Length ?? 0);
        }
        if (length > MaxPayloadLength) errors.Add($"Setup {name} payload is too long.");
        return keys;
    }
}

public sealed class SetupWizardInvitationState
{
    private readonly HashSet<string> _ready = new(StringComparer.Ordinal);
    private readonly HashSet<string> _playing = new(StringComparer.Ordinal);
    private readonly HashSet<string> _offered = new(StringComparer.Ordinal);

    public void MarkClientReady(string uid) => _ready.Add(uid);
    public void MarkPlaying(string uid) => _playing.Add(uid);
    public bool TryOffer(string uid, bool alreadySeen, bool authorized) =>
        authorized && !alreadySeen && _ready.Contains(uid) && _playing.Contains(uid) && _offered.Add(uid);

    public void Clear(string uid)
    {
        _ready.Remove(uid);
        _playing.Remove(uid);
        _offered.Remove(uid);
    }
}
