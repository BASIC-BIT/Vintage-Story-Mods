using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.Utilities;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

internal sealed class SetupWizardSearchPanel
{
    private readonly Dictionary<object, string> _identities = new(ReferenceEqualityComparer.Instance);
    private readonly SetupWizardSettingsForm _form = new();
    private bool _newOnly;
    internal bool NewOnly
    {
        get => _newOnly;
        set { if (_newOnly == value) return; _newOnly = value; Selection = 0; _form.Reset("search"); }
    }
    internal IReadOnlyCollection<string> ReviewedKeys { get; set; }
    internal string Query { get; private set; } = "";
    internal int Selection { get; private set; }
    internal IReadOnlyList<ConfigSearchResult> Results { get; private set; } = Array.Empty<ConfigSearchResult>();
    private string Identity(object entry)
    {
        if (!_identities.TryGetValue(entry, out var id)) _identities[entry] = id = (_identities.Count + 1).ToString(CultureInfo.InvariantCulture);
        return id;
    }
    internal LanguageConfigEntryMessage ResolveLanguage(SetupWizardDraft draft, string id) => draft.Languages.FirstOrDefault(entry => Identity(entry) == id);
    internal CharacterSheetFieldConfigEntryMessage ResolveField(SetupWizardDraft draft, string id) => draft.SheetFields.FirstOrDefault(entry => Identity(entry) == id);
    internal List<ConfigSearchResult> Index(SetupWizardDraft draft)
    {
        var values = draft.Values.ToDictionary(value => value.Key, value => value.Value);
        return ConfigAdminSearch.Build(new ModConfig(), values, values.Keys.ToDictionary(key => key, draft.GetOriginal),
            draft.Languages, draft.SheetFields, Identity, Identity);
    }
    internal void Search(SetupWizardDraft draft, string query)
    {
        var previous = Results.Count > Selection ? Results[Selection] : null;
        var changed = Query != query;
        if (changed) { Selection = 0; _form.Reset("search"); }
        Query = query ?? "";
        var entries = Index(draft);
        if (NewOnly)
            entries = ReviewedKeys == null ? new() : entries.Where(entry => entry.Destination == ConfigSearchDestination.Setting &&
                !ReviewedKeys.Contains(entry.Key, StringComparer.OrdinalIgnoreCase)).ToList();
        Results = NewOnly && string.IsNullOrWhiteSpace(Query) ? entries : ConfigAdminSearch.Find(entries, Query);
        if (!changed && previous != null)
        {
            var index = Results.ToList().FindIndex(result => result.Destination == previous.Destination && result.EntryId == previous.EntryId && result.Key == previous.Key);
            if (index >= 0) Selection = index;
        }
        Selection = Math.Clamp(Selection, 0, Math.Max(0, Results.Count - 1));
    }
    internal void Add(GuiComposer composer, ICoreClientAPI api, double width, double bottom, Action<ConfigSearchResult> select)
    {
        composer.AddStaticText(NewOnly ? "New settings" : "Search settings", CairoFont.WhiteSmallText().WithFontSize(20), ElementBounds.Fixed(0, 48, width, 28));
        composer.AddStaticText(Results.Count == 0 ? NewOnly ? "No new settings match." : string.IsNullOrWhiteSpace(Query) ? "Search names, config keys, help or draft values." : "No matching settings." : $"{Results.Count} matches",
            CairoFont.WhiteSmallText().WithFontSize(14), ElementBounds.Fixed(0, 84, width, 26));
        _form.Begin(composer, "search", ElementBounds.Fixed(0, 120, width, bottom - 128));
        for (var i = 0; i < Results.Count; i++)
        {
            var index = i; var result = Results[i];
            var button = new SetupWizardViewportButton(api, Short(result.Label, 64), () => { Selection = index; select(result); return true; },
                ElementBounds.Fixed(0, i * 86, width - 24, 28));
            button.SetOrientation(EnumTextOrientation.Left);
            composer.AddInteractiveElement(button, "search-result-" + i);
            var page = result.Destination == ConfigSearchDestination.Setting ? SetupWizardCatalog.FindPageForSetting(result.Key) :
                SetupWizardCatalog.Pages.First(p => p.Id == (result.Destination == ConfigSearchDestination.Language ? "chat.languageeditor" : "characters.builder"));
            var context = SetupWizardCatalog.GetTopicForPage(page.Id).Title + " / " + page.Title;
            composer.AddInteractiveElement(new SetupWizardViewportRichtext(api, VtmlUtils.EscapeVtml(context + "\n" + Short(result.Value, 70) + (result.Unsaved ? " (unsaved)" : "")),
                CairoFont.WhiteSmallText().WithFontSize(13), ElementBounds.Fixed(2, i * 86 + 32, width - 28, 48)), "search-context-" + i);
        }
        _form.End(composer, api, Results.Count * 86, () => { });
    }
    internal bool HasResultFocus(GuiComposer composer) => Enumerable.Range(0, Results.Count).Any(index => composer.GetElement("search-result-" + index)?.HasFocus == true);

    internal void RememberFocus(GuiComposer composer)
    {
        for (var i = 0; i < Results.Count; i++)
            if (composer.GetElement("search-result-" + i)?.HasFocus == true) { Selection = i; break; }
    }
    internal void Move(GuiComposer composer, int offset)
    {
        RememberFocus(composer);
        if (Results.Count == 0) return;
        Selection = Math.Clamp(Selection + offset, 0, Results.Count - 1);
        FocusSelection(composer);
    }
    internal void FocusSelection(GuiComposer composer)
    {
        if (composer.GetElement("search-result-" + Selection) is { } control) composer.FocusElement(control.TabIndex);
    }
    private static string Short(string value, int length)
    {
        value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ');
        return value.Length > length ? value[..length] + "..." : value;
    }
}
