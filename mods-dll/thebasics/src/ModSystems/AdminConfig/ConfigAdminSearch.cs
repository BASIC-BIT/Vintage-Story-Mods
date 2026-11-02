using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.AdminConfig;

internal enum ConfigSearchDestination { Setting, Language, CharacterField }

internal sealed record ConfigSearchResult(ConfigSearchDestination Destination, string EntryId, string Key,
    string Label, string Group, string Description, string Value, bool Unsaved = false, string RawValue = null, string Aliases = null)
{
    internal string SearchText => $"{Key} {Label} {Group} {Description} {Value} {RawValue} {Aliases}";
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
        => Build(config, draft, loaded, LanguageConfigAdmin.BuildEntries(config), CharacterSheetFieldConfigAdmin.BuildEntries(config),
            entry => entry.OriginalName, entry => entry.OriginalId);

    internal static List<ConfigSearchResult> Build(ModConfig config, IReadOnlyDictionary<string, string> draft,
        IReadOnlyDictionary<string, string> loaded, IEnumerable<LanguageConfigEntryMessage> languages,
        IEnumerable<CharacterSheetFieldConfigEntryMessage> fields, Func<LanguageConfigEntryMessage, string> languageId,
        Func<CharacterSheetFieldConfigEntryMessage, string> fieldId)
    {
        var results = ConfigAdminSettingRegistry.Settings.Where(setting => loaded.ContainsKey(setting.Key)).Select(setting =>
        {
            var value = draft.TryGetValue(setting.Key, out var current) ? current : loaded[setting.Key];
            var unsaved = loaded.TryGetValue(setting.Key, out var saved) && value != saved;
            var display = setting.Kind == ConfigAdminSettingKind.Boolean ? Lang.Get(value == "1" ? "On" : "Off") : value;
            if (setting.Kind == ConfigAdminSettingKind.Select)
            {
                var index = setting.Options.ToList().FindIndex(option => option == value);
                if (index >= 0) display = setting.OptionNames[index];
            }
            return new ConfigSearchResult(ConfigSearchDestination.Setting, null, setting.Key,
                SetupWizardDialog.SettingLabel(setting), setting.Group, setting.Description, display, unsaved,
                setting.Kind == ConfigAdminSettingKind.Boolean ? (value == "1" ? "1 true yes" : "0 false no") : value, setting.Label);
        }).ToList();

        foreach (var entry in languages)
        {
            var properties = new[] { ("Name", "name"), ("Description", "description"), ("Prefix", "prefix"),
                ("Syllables", "syllables"), ("Color", "color"), ("Default", "default"), ("Hidden", "hidden"),
                ("GrantedToClasses", "classes"), ("GrantedToTraits", "traits"), ("GrantedToModels", "models"),
                ("GrantedToModelGroups", "model-groups") };
            foreach (var (key, translation) in properties)
                results.Add(Entry(ConfigSearchDestination.Language, languageId(entry), entry.Name, key,
                    "Languages", "language-config-" + translation, typeof(LanguageConfigEntryMessage).GetProperty(key).GetValue(entry)));
        }
        foreach (var entry in fields)
        {
            var properties = new[] { ("Id", "key"), ("Label", "label"), ("Description", "description"), ("Type", "type"),
                ("Optional", "optional"), ("Options", "options"), ("BindTo", "bind-to"), ("MaxLength", "max-length"),
                ("Visibility", "visibility"), ("ShowInLook", "show-in-look"), ("EditorRows", "editor-rows"),
                ("LayoutSection", "layout"), ("Width", "width") };
            foreach (var (key, translation) in properties)
                results.Add(Entry(ConfigSearchDestination.CharacterField, fieldId(entry), entry.Label, key,
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
