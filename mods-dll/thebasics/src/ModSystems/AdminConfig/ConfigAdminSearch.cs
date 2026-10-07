using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.AdminConfig;

internal enum ConfigSearchDestination { Setting, Language, CharacterField }

internal sealed record ConfigSearchResult(ConfigSearchDestination Destination, string EntryId, string Key,
    string Label, string Group, string Description, string Value, bool Unsaved = false, string RawValue = null)
{
    internal string SearchText => $"{EntryId} {Key} {Label} {Group} {Description} {Value} {RawValue}";
}

internal static class ConfigAdminSearch
{
    internal static List<ConfigSearchResult> Find(IEnumerable<ConfigSearchResult> entries, string query)
    {
        var words = (query ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return new();
        return entries.Where(entry => words.All(word => entry.SearchText.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(entry => entry.Label, StringComparer.OrdinalIgnoreCase).ThenBy(entry => entry.Group).ToList();
    }

    internal static List<ConfigSearchResult> Build(ModConfig config, IReadOnlyDictionary<string, string> draft,
        IReadOnlyDictionary<string, string> loaded)
    {
        var results = ConfigAdminSettingRegistry.Settings.Select(setting =>
        {
            var value = draft.TryGetValue(setting.Key, out var current) ? current : setting.GetValue(config);
            var unsaved = loaded.TryGetValue(setting.Key, out var saved) && value != saved;
            var display = setting.Kind == ConfigAdminSettingKind.Boolean ? Lang.Get(value == "1" ? "On" : "Off") : value;
            if (setting.Kind == ConfigAdminSettingKind.Select)
            {
                var index = setting.Options.ToList().FindIndex(option => option == value);
                if (index >= 0) display = setting.OptionNames[index];
            }
            return new ConfigSearchResult(ConfigSearchDestination.Setting, null, setting.Key,
                setting.Label, setting.Group, setting.Description, display, unsaved,
                setting.Kind == ConfigAdminSettingKind.Boolean ? (value == "1" ? "1 true yes" : "0 false no") : value);
        }).ToList();

        foreach (var entry in LanguageConfigAdmin.BuildEntries(config))
        {
            var fields = new[] { ("Name", "name"), ("Description", "description"), ("Prefix", "prefix"),
                ("Syllables", "syllables"), ("Color", "color"), ("Default", "default"), ("Hidden", "hidden"),
                ("GrantedToClasses", "classes"), ("GrantedToTraits", "traits"), ("GrantedToModels", "models"),
                ("GrantedToModelGroups", "model-groups") };
            foreach (var (key, translation) in fields)
                results.Add(Entry(ConfigSearchDestination.Language, entry.OriginalName, entry.Name, key,
                    "Languages", "language-config-" + translation, typeof(LanguageConfigEntryMessage).GetProperty(key).GetValue(entry)));
        }
        foreach (var entry in CharacterSheetFieldConfigAdmin.BuildEntries(config))
        {
            var fields = new[] { ("Id", "key"), ("Label", "label"), ("Description", "description"), ("Type", "type"),
                ("Optional", "optional"), ("Options", "options"), ("BindTo", "bind-to"), ("MaxLength", "max-length"),
                ("Visibility", "visibility"), ("ShowInLook", "show-in-look"), ("EditorRows", "editor-rows"),
                ("LayoutSection", "layout"), ("Width", "width") };
            foreach (var (key, translation) in fields)
                results.Add(Entry(ConfigSearchDestination.CharacterField, entry.OriginalId, entry.Label, key,
                    "Character sheet fields", "charsheet-field-config-" + translation,
                    typeof(CharacterSheetFieldConfigEntryMessage).GetProperty(key).GetValue(entry)));
        }
        return results;
    }

    private static ConfigSearchResult Entry(ConfigSearchDestination destination, string id, string name,
        string key, string group, string translation, object value) => new(destination, id, key,
        $"{name}: {Lang.Get("thebasics:" + translation)}", group, Lang.Get("thebasics:" + translation + "-tooltip"),
        value is bool enabled ? Lang.Get(enabled ? "Yes" : "No") : Convert.ToString(value, CultureInfo.InvariantCulture),
        RawValue: Convert.ToString(value, CultureInfo.InvariantCulture));
}
