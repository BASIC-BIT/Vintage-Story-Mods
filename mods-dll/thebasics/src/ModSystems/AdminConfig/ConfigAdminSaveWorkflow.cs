using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;

namespace thebasics.ModSystems.AdminConfig;

internal static class ConfigAdminSaveWorkflow
{
    public static List<string> ApplyValues(ModConfig draft, IEnumerable<ConfigAdminSettingValue> values)
    {
        var errors = new List<string>();
        foreach (var value in values ?? Enumerable.Empty<ConfigAdminSettingValue>())
        {
            ApplyValue(draft, value, errors);
        }

        return errors;
    }

    public static void MarkReviewedKeys(ModConfig draft, IEnumerable<string> keys)
    {
        var reviewed = new HashSet<string>(draft.ReviewedConfigSettingKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys?.Where(key => ConfigAdminSettingRegistry.TryGet(key, out _)) ?? Enumerable.Empty<string>())
        {
            reviewed.Add(key);
        }

        draft.ReviewedConfigSettingKeys = reviewed.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string BuildConfigSaveMessage(IReadOnlyCollection<string> changedKeys, IReadOnlyCollection<string> restartRequired)
    {
        return restartRequired.Count == 0
            ? $"Saved The BASICs config. Live-applied settings: {changedKeys.Count}."
            : $"Saved The BASICs config. Restart required for: {string.Join(", ", restartRequired)}.";
    }

    private static void ApplyValue(ModConfig draft, ConfigAdminSettingValue value, List<string> errors)
    {
        if (!ConfigAdminSettingRegistry.TryGet(value.Key, out var setting))
        {
            errors.Add($"Unknown setting: {value.Key}");
            return;
        }

        if (!setting.TrySetValue(draft, value.Value, out var error))
        {
            errors.Add(error);
        }
    }
}
