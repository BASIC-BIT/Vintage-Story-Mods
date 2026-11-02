using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Cairo;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class SetupWizardLanguageEditor
{
    private readonly ICoreClientAPI _api;
    private readonly SetupWizardDraft _draft;
    private readonly Action _changed;
    private readonly Action<Action> _queueRecompose;
    private GuiComposer _composer;
    private GuiElementDropDown _selector;
    private GuiElementColorPicker _colorPicker;
    private string[] _selectorNames;
    private bool _updating;
    private float _scroll;
    private int _composedIndex = -1;

    public SetupWizardLanguageEditor(ICoreClientAPI api, SetupWizardDraft draft, Action changed, Action<Action> queueRecompose)
    {
        _api = api;
        _draft = draft ?? throw new ArgumentNullException(nameof(draft));
        _changed = changed;
        _queueRecompose = queueRecompose;
    }

    public int SelectedIndex { get; set; }
    private bool CanResetLanguages => _draft.LanguagesChanged && !_draft.IsSavePending && !_draft.HasUnconfirmedSave;
    private LanguageConfigEntryMessage Selected => _draft.Languages.Count == 0 ? null : _draft.Languages[Math.Clamp(SelectedIndex, 0, _draft.Languages.Count - 1)];

    public void Add(GuiComposer composer, double width, bool compact)
    {
        _composer = composer;
        _colorPicker = null;
        SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, _draft.Languages.Count - 1));
        if (_composedIndex != SelectedIndex) _scroll = 0;
        _composedIndex = SelectedIndex;
        var bottomY = compact ? 420 : 442;
        Text(composer, "Languages", 0, 48, width, 27, 20, true);
        Button(composer, "Discard edits", "reset", 161, bottomY, 149, () => Queue(ResetLanguages), CanResetLanguages);
        composer.AddHoverText("Discard language edits and restore the latest server language list. Other settings keep their edits.",
            Font(13), 360, ElementBounds.Fixed(161, bottomY, 149, 26), "language-editor-help-reset");
        var values = Enumerable.Range(0, _draft.Languages.Count).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray();
        _selectorNames = Names();
        _selector = new LanguageSelector(_api, values, _selectorNames, SelectedIndex,
            (value, selected) =>
            {
                if (selected && int.TryParse(value, out var index) && index != SelectedIndex)
                    Queue(() => SelectedIndex = index);
            }, ElementBounds.Fixed(0, 84, 310, 28));
        composer.AddInteractiveElement(_selector, "language-editor-selector");
        Button(composer, "Add language", "add", 0, 120, 310, () => Queue(AddLanguage));
        Button(composer, "Remove language", "remove", 0, bottomY, 149, () => Queue(RemoveSelected), _draft.Languages.Count > 1);
        Button(composer, "Move up", "up", 0, 154, 149, () => Queue(() => MoveSelected(-1)), SelectedIndex > 0);
        Button(composer, "Move down", "down", 161, 154, 149, () => Queue(() => MoveSelected(1)), SelectedIndex + 1 < _draft.Languages.Count);

        Text(composer, "Speaks this language", 0, 187, 310, 20, 14, true);
        AddChat(composer, 210, "understood");
        Text(composer, "Doesn't speak this language", 0, 304, 310, 20, 14, true);
        AddChat(composer, 327, "unfamiliar");

        const double panelX = 334;
        composer.AddInset(ElementBounds.Fixed(panelX, 48, width - panelX, compact ? 398 : 420), 3);
        const double x = panelX + 12;
        var formWidth = width - x - 32;
        Text(composer, "Language", x, 58, formWidth, 18, 12);
        composer.AddRichtext(string.Empty, Font(18).WithColor(new[] { .95, .72, .3, 1d }),
            ElementBounds.Fixed(x, 78, formWidth, 27), "language-editor-title");
        var clip = ElementBounds.Fixed(x, 112, formWidth, compact ? 284 : 318);
        var content = ElementBounds.Fixed(0, -_scroll, formWidth, 607);
        composer.BeginClip(clip).BeginChildElements(content);
        if (Selected != null)
        {
            AddDetails(composer, 0, formWidth);
            Text(composer, "Starting speakers", 0, 328, formWidth, 26, 17, true);
            AddGrants(composer, 0, formWidth);
        }
        composer.EndChildElements().EndClip();
        var scrollbar = new SetupWizardSheetEditor.BuilderScrollbar(_api, value =>
        {
            _scroll = value;
            _colorPicker?.Close();
            content.fixedY = -value;
            content.MarkDirtyRecursive();
            content.CalcWorldBounds();
        }, ElementBounds.Fixed(x + formWidth + 6, 112, 12, clip.fixedHeight), clip);
        composer.AddInteractiveElement(scrollbar, "language-editor-scroll");
        var initialScroll = _scroll;
        composer.OnComposed += () =>
        {
            scrollbar.SetHeights((float)clip.fixedHeight, (float)content.fixedHeight);
            scrollbar.MoveTo(initialScroll);
        };
        composer.OnFocusChanged += focused =>
        {
            var control = composer.CurrentTabIndexElement;
            if (!focused || control?.Bounds.ParentBounds != content) return;
            var top = Math.Max(0, control.Bounds.fixedY - 22);
            var bottom = control.Bounds.fixedY + control.Bounds.fixedHeight;
            if (top < _scroll) scrollbar.MoveTo((float)top);
            else if (bottom > _scroll + clip.fixedHeight) scrollbar.MoveTo((float)(bottom - clip.fixedHeight));
        };
        composer.AddRichtext(Errors(), Font(12).WithColor(new[] { 1d, 0.72, 0.56, 1d }),
            ElementBounds.Fixed(x, compact ? 402 : 438, formWidth, compact ? 44 : 26), "language-editor-errors");
        composer.OnComposed += RefreshPreview;
    }

    private void AddDetails(GuiComposer composer, double x, double width)
    {
        Input(composer, "Name", "Language name", Selected.Name, x, 0, width, 64, value => Selected.Name = value);
        const double rowHeight = 58;
        var half = (width - 14) / 2;
        Input(composer, "Prefix", "Chat code", Selected.Prefix, x, rowHeight, half, 32, value => Selected.Prefix = value,
            "The code players type after a colon to speak this language, for example :c.");
        var colorX = x + half + 14;
        var originalColor = Selected.Color;
        _colorPicker = new GuiElementColorPicker(_api, ElementBounds.Fixed(colorX, 22 + rowHeight, 32, 28), Selected.Color, color =>
        {
            // Preserve the original spelling so restoring its RGB value also clears the draft change.
            if (string.Equals(color, originalColor, StringComparison.OrdinalIgnoreCase)) color = originalColor;
            Selected.Color = color;
            _updating = true;
            try { composer.GetTextInput("language-editor-Color").SetValue(color); }
            finally { _updating = false; }
            NotifyChanged();
        });
        composer.AddInteractiveElement(_colorPicker, "language-editor-color-picker");
        Input(composer, "Color", "Text color", Selected.Color, colorX, rowHeight, half, 16, value =>
        {
            Selected.Color = value;
            _colorPicker.SetValue(value);
        }, "A hex color such as #E9DDCE.", leading: 39);
        Input(composer, "Description", "Description", Selected.Description, x, rowHeight * 2, width, 256, value => Selected.Description = value);
        Input(composer, "Syllables", "Unfamiliar sounds", Selected.Syllables, x, rowHeight * 3, width, 512, value => Selected.Syllables = value,
            "Separate syllables with commas. Players who don't know this language see words made from these syllables.");
        Toggle(composer, "Hidden", "Hide until known", Selected.Hidden, x, rowHeight * 4, width, value => Selected.Hidden = value,
            "Hide this language from players who don't know it. Known or granted languages remain usable.");
    }

    private void AddGrants(GuiComposer composer, double x, double width)
    {
        Toggle(composer, "Default", "Everyone starts with it", Selected.Default, x, 360, width, value =>
        {
            Selected.Default = value;
            Queue(() => { });
        }, "All characters know this language. Turn this off to choose starting speakers below.");
        Text(composer, Selected.Default ? "Everyone knows this language. Turn this off to use the rules below." :
            "Characters matching any rule below start with this language.", x, 417, width, 30, 13);
        var half = (width - 18) / 2;
        Grant("GrantedToClasses", "Character classes", Selected.GrantedToClasses, x, 455, value => Selected.GrantedToClasses = value);
        Grant("GrantedToTraits", "Character traits", Selected.GrantedToTraits, x + half + 18, 455, value => Selected.GrantedToTraits = value);
        Grant("GrantedToModels", "Character models", Selected.GrantedToModels, x, 515, value => Selected.GrantedToModels = value);
        Grant("GrantedToModelGroups", "Model groups", Selected.GrantedToModelGroups, x + half + 18, 515, value => Selected.GrantedToModelGroups = value);
        Text(composer, "Use exact IDs, separated by commas. Leave unused rules blank.", x, 575, width, 30, 12);

        void Grant(string key, string label, string value, double left, double top, Action<string> update) =>
            Input(composer, key, label, value, left, top, half, 512, update,
                Selected.Default ? "Turn off Everyone starts with it to edit starting-speaker rules. Your saved IDs are kept." :
                "Enter exact IDs, separated by commas. A match in any of these lists grants the language.",
                enabled: !Selected.Default);
    }

    private void Input(GuiComposer composer, string key, string label, string value, double x, double y, double width,
        int maxLength, Action<string> update, string help = null, double leading = 0, bool enabled = true)
    {
        var labelBounds = ElementBounds.Fixed(x, y, width, 20);
        composer.AddInteractiveElement(new SetupWizardViewportRichtext(_api, VtmlUtils.EscapeVtml(label),
            Font(13).WithColor(enabled ? new[] { 1d, 1d, 1d, 1d } : new[] { .55, .53, .49, 1d }), labelBounds), "language-editor-label-" + key);
        var bounds = ElementBounds.Fixed(x + leading, y + 22, width - leading, 28);
        var input = new SetupWizardViewportTextInput(_api, bounds, text =>
        {
            if (_updating || !enabled) return;
            update(text);
            NotifyChanged();
        }, CairoFont.TextInput().WithFontSize(15));
        input.SetMaxLength(Math.Max(maxLength, value?.Length ?? 0));
        _updating = true;
        try { input.SetValue(value ?? string.Empty); }
        finally { _updating = false; }
        input.Enabled = enabled;
        composer.AddInteractiveElement(input, "language-editor-" + key);
        if (help != null) composer.AddHoverText(help, Font(13), 360,
            ElementBounds.Fixed(x, y, width, 50), "language-editor-help-" + key);
    }

    private void Toggle(GuiComposer composer, string key, string label, bool value, double x, double y, double width,
        Action<bool> update, string help)
    {
        Text(composer, label, x, y, width, 20, 12);
        var bounds = ElementBounds.Fixed(x, y + 23, Math.Min(112, width), 28);
        composer.AddInteractiveElement(new GuiElementSetupToggle(_api, value, on => { update(on); NotifyChanged(); }, bounds), "language-editor-" + key);
        composer.AddHoverText(help, Font(13), 360, bounds.FlatCopy(), "language-editor-help-" + key);
    }

    private void AddChat(GuiComposer composer, double y, string key)
    {
        const double historyHeight = 32;
        composer.BeginChildElements(ElementBounds.Fixed(0, y, 310, historyHeight + 57))
            .AddGameOverlay(ElementBounds.Fixed(0, 23, 310, historyHeight + 34), new[] { 0.25, 0.208, 0.161, 0.75 });
        var general = ConfigAdminSettingDefinition.TryParseBool(_draft.Get("UseGeneralChannelAsProximityChat"), out var useGeneral) && useGeneral;
        composer.AddHorizontalTabs(new[] { new GuiTab { Name = general ? "General" : "Proximity", DataInt = 0 } }, ElementBounds.Fixed(0, 0, 310, 23),
            _ => { }, Font(14), Font(14), "language-editor-chat-tabs-" + key);
        composer.BeginClip(ElementBounds.Fixed(6, 26, 288, historyHeight))
            .AddRichtext(string.Empty, Font(14), ElementBounds.Fixed(0, 0, 288, historyHeight), "language-editor-" + key).EndClip();
        composer.AddInteractiveElement(new GuiElementChatInput(_api, ElementBounds.Fixed(0, historyHeight + 29, 310, 25), _ => { }) { Enabled = false },
            "language-editor-chat-input-" + key);
        composer.EndChildElements();
    }

    private void NotifyChanged()
    {
        _changed?.Invoke();
        RefreshPreview();
    }

    public void RefreshPreview()
    {
        if (_composer == null) return;
        _composer.GetRichtext("language-editor-title")?.SetNewText(VtmlUtils.EscapeVtml(
            string.IsNullOrWhiteSpace(Selected?.Name) ? "Unnamed language" : Selected.Name), Font(18).WithColor(new[] { .95, .72, .3, 1d }));
        if (_colorPicker != null)
            _colorPicker.PopupMinimumX = _composer.GetTextInput("language-editor-Name").Bounds.renderX / RuntimeEnv.GUIScale;
        var sample = SetupWizardLanguagePreview.Build(Selected, config: BuildConfig());
        _composer.GetRichtext("language-editor-understood")?.SetNewText(sample.Understood, Font(14));
        _composer.GetRichtext("language-editor-unfamiliar")?.SetNewText(sample.Unfamiliar, Font(14));
        _composer.GetRichtext("language-editor-errors")?.SetNewText(Errors(), Font(12).WithColor(new[] { 1d, 0.72, 0.56, 1d }));
        _composer.GetButton("language-editor-reset").Enabled = CanResetLanguages;
        var names = Names();
        if (_selector != null && !_selectorNames.SequenceEqual(names))
        {
            _selectorNames = names;
            _selector.SetList(Enumerable.Range(0, _draft.Languages.Count).Select(index => index.ToString(CultureInfo.InvariantCulture)).ToArray(), names);
            _selector.SetSelectedIndex(SelectedIndex);
        }
    }

    internal ModConfig BuildConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        foreach (var value in _draft.Values)
            if (ConfigAdminSettingRegistry.TryGet(value.Key, out var setting)) setting.TrySetValue(config, value.Value, out _);
        LanguageConfigAdmin.ValidateEntries(_draft.Languages, out var languages);
        config.Languages = languages;
        return config;
    }

    private string Errors()
    {
        var errors = LanguageConfigAdmin.ValidateEntries(_draft.Languages, out _);
        return VtmlUtils.EscapeVtml(string.Join(" ", errors.Take(2)));
    }

    private string[] Names() => _draft.Languages.Select((entry, index) =>
        (index + 1) + ". " + (string.IsNullOrWhiteSpace(entry?.Name) ? "Unnamed language" : entry.Name)).ToArray();

    private void Queue(Action action) => _queueRecompose?.Invoke(action);

    internal void ResetLanguages()
    {
        if (!CanResetLanguages) return;
        _draft.ResetLanguages();
        SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, _draft.Languages.Count - 1));
        _changed?.Invoke();
    }

    internal void AddLanguage()
    {
        var names = new HashSet<string>(_draft.Languages.Select(entry => entry?.Name), StringComparer.OrdinalIgnoreCase);
        var prefixes = new HashSet<string>(_draft.Languages.Select(entry => entry?.Prefix), StringComparer.OrdinalIgnoreCase);
        var suffix = 1;
        while (names.Contains("New language " + suffix) || prefixes.Contains("new" + suffix)) suffix++;
        _draft.Languages.Add(new LanguageConfigEntryMessage
        {
            Name = "New language " + suffix,
            Description = "A new language.",
            Prefix = "new" + suffix,
            Color = "#E9DDCE",
            Syllables = "na, la, ra",
            Default = _draft.Languages.Count == 0
        });
        SelectedIndex = _draft.Languages.Count - 1;
        _changed?.Invoke();
    }

    internal void RemoveSelected()
    {
        if (_draft.Languages.Count <= 1 || SelectedIndex < 0 || SelectedIndex >= _draft.Languages.Count) return;
        _draft.Languages.RemoveAt(SelectedIndex);
        SelectedIndex = Math.Min(SelectedIndex, _draft.Languages.Count - 1);
        _changed?.Invoke();
    }

    internal void MoveSelected(int offset)
    {
        var target = SelectedIndex + offset;
        if (SelectedIndex < 0 || SelectedIndex >= _draft.Languages.Count || target < 0 || target >= _draft.Languages.Count) return;
        (_draft.Languages[SelectedIndex], _draft.Languages[target]) = (_draft.Languages[target], _draft.Languages[SelectedIndex]);
        SelectedIndex = target;
        _changed?.Invoke();
    }

    private void Button(GuiComposer composer, string label, string key, double x, double y, double width, Action action, bool enabled = true)
    {
        composer.AddSmallButton(label, () => { action(); return true; }, ElementBounds.Fixed(x, y, width, 26), key: "language-editor-" + key);
        composer.GetButton("language-editor-" + key).Enabled = enabled;
    }

    private static void Text(GuiComposer composer, string text, double x, double y, double width, double height, double size, bool bold = false) =>
        composer.AddInteractiveElement(new SetupWizardViewportRichtext(composer.Api, VtmlUtils.EscapeVtml(text),
            Font(size).WithWeight(bold ? FontWeight.Bold : FontWeight.Normal), ElementBounds.Fixed(x, y, width, height)));

    private static CairoFont Font(double size) => CairoFont.WhiteSmallText().WithFontSize((float)size);

    private sealed class LanguageSelector : GuiElementDropDown
    {
        public LanguageSelector(ICoreClientAPI api, string[] values, string[] names, int selectedIndex,
            SelectionChangedDelegate changed, ElementBounds bounds)
            : base(api, values, names, selectedIndex, changed, bounds, SetupWizardLanguageEditor.Font(15), false)
        {
            MouseOverCursor = "linkselect";
        }
    }
}
