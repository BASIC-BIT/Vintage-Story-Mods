using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace thebasics.ModSystems.ChatUiSystem;

public class LanguageConfigDialog : GuiDialog
{
    private const double DialogWidth = 900;
    private const double ContentWidth = DialogWidth - 48;
    private const double PanelHeight = 392;
    private const double StatusHeight = 56;
    private const double FieldHeight = 28;
    private const double FieldRowHeight = 55;
    private readonly Action<List<LanguageConfigEntryMessage>> _onSave;
    private readonly Action _onReload;
    private readonly Action _onClose;
    private readonly Dictionary<string, GuiElementTextInput> _textInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GuiElementDropDown> _dropDowns = new(StringComparer.OrdinalIgnoreCase);
    private List<LanguageConfigEntryMessage> _languages;
    private string _message;
    private bool _success;
    private int _selectedIndex;
    private bool _closing;

    public LanguageConfigDialog(
        ICoreClientAPI capi,
        List<LanguageConfigEntryMessage> languages,
        string message,
        bool success,
        Action<List<LanguageConfigEntryMessage>> onSave,
        Action onReload,
        Action onClose) : base(capi)
    {
        _languages = EnsureDraft(languages);
        _message = message;
        _success = success;
        _onSave = onSave;
        _onReload = onReload;
        _onClose = onClose;
        _selectedIndex = 0;
        ComposeDialog();
    }

    public override bool PrefersUngrabbedMouse => true;

    public override string ToggleKeyCombinationCode => null;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.26;

    public void SetView(List<LanguageConfigEntryMessage> languages, string message, bool success)
    {
        _languages = EnsureDraft(languages);
        _message = message;
        _success = success;
        _selectedIndex = ClampSelectedIndex(_selectedIndex);
        ComposeDialog();
    }

    public override bool TryClose()
    {
        var closed = base.TryClose();
        if (closed && !_closing)
        {
            _closing = true;
            _onClose?.Invoke();
        }

        return closed;
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();
        _textInputs.Clear();
        _dropDowns.Clear();
        _selectedIndex = ClampSelectedIndex(_selectedIndex);

        var contentTop = GuiStyle.TitleBarHeight + 10;
        var helpHeight = 52;
        var helpBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, helpHeight);
        var statusBounds = ElementBounds.Fixed(0, helpBounds.fixedY + helpHeight + 6, ContentWidth, StatusHeight);
        var panelY = statusBounds.fixedY + StatusHeight + 10;
        var leftPanelWidth = 240;
        var panelGap = 15;
        var rightPanelX = leftPanelWidth + panelGap;
        var rightPanelWidth = ContentWidth - rightPanelX;
        var leftPanelBounds = ElementBounds.Fixed(0, panelY, leftPanelWidth, PanelHeight);
        var rightPanelBounds = ElementBounds.Fixed(rightPanelX, panelY, rightPanelWidth, PanelHeight);
        var buttonY = panelY + PanelHeight + 12;
        var buttonHeight = 30;
        var bodyHeight = buttonY + buttonHeight + 8;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

        var composer = capi.Gui.CreateCompo("thebasics-language-config", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:language-config-title"), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(leftPanelBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddInset(rightPanelBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3);

        AddRichtext(composer, VtmlUtils.EscapeVtml(Lang.Get("thebasics:language-config-help")), helpBounds);
        AddStatus(composer, statusBounds);
        AddLanguageList(composer, leftPanelBounds);
        AddSelectedLanguageFields(composer, rightPanelBounds);

        AddButton(composer, "reload", Lang.Get("thebasics:language-config-reload"), OnReload, ElementBounds.Fixed(0, buttonY, 118, buttonHeight), Lang.Get("thebasics:language-config-reload-tooltip"));
        AddButton(composer, "save", Lang.Get("thebasics:language-config-save"), OnSave, ElementBounds.Fixed(ContentWidth - 250, buttonY, 118, buttonHeight), Lang.Get("thebasics:language-config-save-tooltip"));
        AddButton(composer, "close", Lang.Get("thebasics:language-config-close"), OnCancel, ElementBounds.Fixed(ContentWidth - 122, buttonY, 110, buttonHeight), Lang.Get("thebasics:language-config-close-tooltip"));

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
    }

    private void AddStatus(GuiComposer composer, ElementBounds bounds)
    {
        var message = Lang.Get("thebasics:language-config-status-ready");
        if (!string.IsNullOrWhiteSpace(_message))
        {
            message = _success ? _message : Lang.Get("thebasics:language-config-error-prefix", _message);
        }

        AddRichtext(composer, VtmlUtils.EscapeVtml(message), bounds);
    }

    private void AddLanguageList(GuiComposer composer, ElementBounds panelBounds)
    {
        var x = panelBounds.fixedX + 8;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 16;
        var labelBounds = ElementBounds.Fixed(x, y, width, 22);
        AddLabel(composer, Lang.Get("thebasics:language-config-selected"), labelBounds, bold: true, Lang.Get("thebasics:language-config-selected-tooltip"));

        var values = Enumerable.Range(0, _languages.Count).Select(index => index.ToString()).ToArray();
        var names = _languages.Select((language, index) => $"{index + 1}. {DisplayName(language)}").ToArray();
        var selectedBounds = ElementBounds.Fixed(x, y + 26, width, FieldHeight);
        var selectedLanguageDropDown = new GuiElementDropDown(capi, values, names, _selectedIndex, OnSelectedLanguageChanged, selectedBounds, CairoFont.WhiteSmallText(), multiSelect: false);
        composer.AddInteractiveElement(selectedLanguageDropDown, "language-selector");
        AddTooltip(composer, "language-selector", selectedBounds, Lang.Get("thebasics:language-config-selected-tooltip"));

        y += 68;
        AddButton(composer, "add", Lang.Get("thebasics:language-config-add"), OnAddLanguage, ElementBounds.Fixed(x, y, 104, 28), Lang.Get("thebasics:language-config-add-tooltip"));
        AddButton(composer, "delete", Lang.Get("thebasics:language-config-delete"), OnDeleteSelectedLanguage, ElementBounds.Fixed(x + 114, y, 104, 28), Lang.Get("thebasics:language-config-delete-tooltip"));
        y += 36;
        AddButton(composer, "up", Lang.Get("thebasics:language-config-up"), OnMoveSelectedLanguageUp, ElementBounds.Fixed(x, y, 104, 28), Lang.Get("thebasics:language-config-up-tooltip"));
        AddButton(composer, "down", Lang.Get("thebasics:language-config-down"), OnMoveSelectedLanguageDown, ElementBounds.Fixed(x + 114, y, 104, 28), Lang.Get("thebasics:language-config-down-tooltip"));

        y += 44;
        AddLabel(composer, Lang.Get("thebasics:language-config-order"), ElementBounds.Fixed(x, y, width, 22), bold: true, Lang.Get("thebasics:language-config-order-tooltip"));
        y += 24;

        var orderLines = BuildOrderSummaryLines();
        foreach (var line in orderLines)
        {
            var font = CairoFont.WhiteSmallText();
            if (line.IsSelected)
            {
                font = font.WithWeight(Cairo.FontWeight.Bold);
            }

            composer.AddStaticText(line.Text, font, ElementBounds.Fixed(x, y, width, 19));
            y += 19;
        }
    }

    private void AddSelectedLanguageFields(GuiComposer composer, ElementBounds panelBounds)
    {
        var language = _languages[_selectedIndex];
        var x = panelBounds.fixedX + 10;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 20;
        var columnGap = 18;
        var columnWidth = (width - columnGap) / 2;
        var rightColumnX = x + columnWidth + columnGap;
        var headerBounds = ElementBounds.Fixed(x, y, width, 24);
        AddLabel(composer, Lang.Get("thebasics:language-config-editing", _selectedIndex + 1, DisplayName(language)), headerBounds, bold: true, Lang.Get("thebasics:language-config-editing-tooltip"));

        var leftY = y + 34;
        leftY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Name), Lang.Get("thebasics:language-config-name"), language.Name, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), 64, Lang.Get("thebasics:language-config-name-tooltip"));
        leftY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Prefix), Lang.Get("thebasics:language-config-prefix"), language.Prefix, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), 32, Lang.Get("thebasics:language-config-prefix-tooltip"));
        leftY = AddColorInput(composer, language, x, leftY, columnWidth);
        leftY = AddBoolDropDown(composer, nameof(LanguageConfigEntryMessage.Default), Lang.Get("thebasics:language-config-default"), language.Default, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:language-config-default-tooltip"));
        AddBoolDropDown(composer, nameof(LanguageConfigEntryMessage.Hidden), Lang.Get("thebasics:language-config-hidden"), language.Hidden, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:language-config-hidden-tooltip"));

        var rightY = y + 34;
        rightY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Description), Lang.Get("thebasics:language-config-description"), language.Description, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 256, Lang.Get("thebasics:language-config-description-tooltip"));
        rightY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Syllables), Lang.Get("thebasics:language-config-syllables"), language.Syllables, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 512, Lang.Get("thebasics:language-config-syllables-tooltip"));
        rightY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToClasses), Lang.Get("thebasics:language-config-classes"), language.GrantedToClasses, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 512, Lang.Get("thebasics:language-config-classes-tooltip"));
        rightY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToTraits), Lang.Get("thebasics:language-config-traits"), language.GrantedToTraits, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 512, Lang.Get("thebasics:language-config-traits-tooltip"));
        rightY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToModels), Lang.Get("thebasics:language-config-models"), language.GrantedToModels, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 512, Lang.Get("thebasics:language-config-models-tooltip"));
        AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToModelGroups), Lang.Get("thebasics:language-config-model-groups"), language.GrantedToModelGroups, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 512, Lang.Get("thebasics:language-config-model-groups-tooltip"));
    }

    private double AddColorInput(GuiComposer composer, LanguageConfigEntryMessage language, double x, double y, double columnWidth)
    {
        var labelBounds = ElementBounds.Fixed(x, y, columnWidth, 20);
        AddLabel(composer, Lang.Get("thebasics:language-config-color"), labelBounds, tooltip: Lang.Get("thebasics:language-config-color-tooltip"));

        var inputWidth = columnWidth - 94;
        var inputBounds = ElementBounds.Fixed(x, y + 22, inputWidth, FieldHeight);
        var input = new GuiElementTextInput(capi, inputBounds, null, CairoFont.TextInput());
        input.SetValue(language.Color ?? string.Empty);
        input.SetMaxLength(16);
        _textInputs[nameof(LanguageConfigEntryMessage.Color)] = input;
        composer.AddInteractiveElement(input, "input-color");

        var previewBounds = ElementBounds.Fixed(x + inputWidth + 8, y + 24, 82, 22);
        composer.AddStaticText(Lang.Get("thebasics:language-config-color-preview"), GetColorPreviewFont(language.Color), EnumTextOrientation.Center, previewBounds);
        AddTooltip(composer, "input-color", labelBounds, Lang.Get("thebasics:language-config-color-tooltip"));
        return y + FieldRowHeight;
    }

    private double AddTextInput(GuiComposer composer, string field, string label, string value, ElementBounds inputBounds, int maxLength, string tooltip)
    {
        var labelBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY, inputBounds.fixedWidth, 20);
        AddLabel(composer, label, labelBounds, tooltip: tooltip);

        var actualInputBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY + 22, inputBounds.fixedWidth, inputBounds.fixedHeight);
        var input = new GuiElementTextInput(capi, actualInputBounds, null, CairoFont.TextInput());
        input.SetValue(value ?? string.Empty);
        if (maxLength > 0)
        {
            input.SetMaxLength(maxLength);
        }

        _textInputs[field] = input;
        composer.AddInteractiveElement(input, "input-" + field);
        AddTooltip(composer, "input-" + field, labelBounds, tooltip);
        return inputBounds.fixedY + FieldRowHeight;
    }

    private double AddBoolDropDown(GuiComposer composer, string field, string label, bool value, ElementBounds inputBounds, string tooltip)
    {
        var labelBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY, inputBounds.fixedWidth, 20);
        AddLabel(composer, label, labelBounds, tooltip: tooltip);

        var actualInputBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY + 22, inputBounds.fixedWidth, inputBounds.fixedHeight);
        var dropDown = new GuiElementDropDown(capi, ["0", "1"], [Lang.Get("No"), Lang.Get("Yes")], value ? 1 : 0, null, actualInputBounds, CairoFont.WhiteSmallText(), multiSelect: false);
        _dropDowns[field] = dropDown;
        composer.AddInteractiveElement(dropDown, "dropdown-" + field);
        AddTooltip(composer, "dropdown-" + field, labelBounds, tooltip);
        return inputBounds.fixedY + FieldRowHeight;
    }

    private void CaptureSelectedInputToDraft()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _languages.Count)
        {
            return;
        }

        var language = _languages[_selectedIndex];
        language.Name = GetText(nameof(LanguageConfigEntryMessage.Name));
        language.Prefix = GetText(nameof(LanguageConfigEntryMessage.Prefix));
        language.Description = GetText(nameof(LanguageConfigEntryMessage.Description));
        language.Color = GetText(nameof(LanguageConfigEntryMessage.Color));
        language.Default = GetBool(nameof(LanguageConfigEntryMessage.Default));
        language.Hidden = GetBool(nameof(LanguageConfigEntryMessage.Hidden));
        language.Syllables = GetText(nameof(LanguageConfigEntryMessage.Syllables));
        language.GrantedToClasses = GetText(nameof(LanguageConfigEntryMessage.GrantedToClasses));
        language.GrantedToTraits = GetText(nameof(LanguageConfigEntryMessage.GrantedToTraits));
        language.GrantedToModels = GetText(nameof(LanguageConfigEntryMessage.GrantedToModels));
        language.GrantedToModelGroups = GetText(nameof(LanguageConfigEntryMessage.GrantedToModelGroups));
    }

    private bool OnAddLanguage()
    {
        CaptureSelectedInputToDraft();
        var nextName = GetUniqueDraftName();
        _languages.Add(new LanguageConfigEntryMessage
        {
            Name = nextName,
            Description = "Describe this language",
            Prefix = "new" + _languages.Count,
            Color = "#E9DDCE",
            Syllables = "na, la, ra"
        });
        _selectedIndex = _languages.Count - 1;
        _message = Lang.Get("thebasics:language-config-added-draft");
        _success = true;
        ComposeDialog();
        return true;
    }

    private bool OnDeleteSelectedLanguage()
    {
        CaptureSelectedInputToDraft();
        if (_languages.Count <= 1 || _selectedIndex < 0 || _selectedIndex >= _languages.Count)
        {
            _message = Lang.Get("thebasics:language-config-delete-last");
            _success = false;
            ComposeDialog();
            return true;
        }

        _languages.RemoveAt(_selectedIndex);
        _selectedIndex = ClampSelectedIndex(_selectedIndex);
        _message = Lang.Get("thebasics:language-config-deleted-draft");
        _success = true;
        ComposeDialog();
        return true;
    }

    private bool OnMoveSelectedLanguageUp()
    {
        return MoveSelected(-1);
    }

    private bool OnMoveSelectedLanguageDown()
    {
        return MoveSelected(1);
    }

    private bool MoveSelected(int offset)
    {
        CaptureSelectedInputToDraft();
        var nextIndex = _selectedIndex + offset;
        if (_selectedIndex < 0 || nextIndex < 0 || nextIndex >= _languages.Count)
        {
            return true;
        }

        (_languages[_selectedIndex], _languages[nextIndex]) = (_languages[nextIndex], _languages[_selectedIndex]);
        _selectedIndex = nextIndex;
        _message = Lang.Get("thebasics:language-config-moved-draft");
        _success = true;
        ComposeDialog();
        return true;
    }

    private bool OnSave()
    {
        CaptureSelectedInputToDraft();
        _onSave(_languages.Select(CloneEntry).ToList());
        return true;
    }

    private bool OnReload()
    {
        _onReload();
        return true;
    }

    private bool OnCancel()
    {
        TryClose();
        return true;
    }

    private void OnTitleBarCloseClicked()
    {
        TryClose();
    }

    private void OnSelectedLanguageChanged(string value, bool selected)
    {
        if (!selected || !int.TryParse(value, out var index) || index == _selectedIndex)
        {
            return;
        }

        CaptureSelectedInputToDraft();
        _selectedIndex = ClampSelectedIndex(index);
        ComposeDialog();
    }

    private string GetText(string field)
    {
        return _textInputs.TryGetValue(field, out var input) ? input.GetText() ?? string.Empty : string.Empty;
    }

    private bool GetBool(string field)
    {
        return _dropDowns.TryGetValue(field, out var dropDown) && dropDown.SelectedValue == "1";
    }

    private int ClampSelectedIndex(int index)
    {
        if (_languages.Count == 0)
        {
            return 0;
        }

        return Math.Clamp(index, 0, _languages.Count - 1);
    }

    private string GetUniqueDraftName()
    {
        var existing = new HashSet<string>(_languages.Select(language => language.Name), StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < 1000; index++)
        {
            var candidate = index == 1 ? "NewLanguage" : "NewLanguage" + index;
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }

        return "NewLanguage" + _languages.Count;
    }

    private List<(string Text, bool IsSelected)> BuildOrderSummaryLines()
    {
        const int maxLines = 12;
        var lines = new List<(string Text, bool IsSelected)>();
        for (var index = 0; index < Math.Min(maxLines, _languages.Count); index++)
        {
            var marker = index == _selectedIndex ? "> " : "  ";
            lines.Add(($"{marker}{index + 1}. {DisplayName(_languages[index])}", index == _selectedIndex));
        }

        if (_languages.Count > maxLines)
        {
            lines.Add((Lang.Get("thebasics:language-config-order-more", _languages.Count - maxLines), false));
        }

        return lines;
    }

    private static string DisplayName(LanguageConfigEntryMessage language)
    {
        return string.IsNullOrWhiteSpace(language?.Name) ? Lang.Get("thebasics:language-config-unnamed") : language.Name;
    }

    private static CairoFont GetColorPreviewFont(string color)
    {
        var font = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold);
        try
        {
            if (!string.IsNullOrWhiteSpace(color))
            {
                return font.WithColor(ColorUtil.Hex2Doubles(color));
            }
        }
        catch
        {
            // Server-side validation reports the exact invalid color on save.
        }

        return font;
    }

    private static List<LanguageConfigEntryMessage> EnsureDraft(List<LanguageConfigEntryMessage> languages)
    {
        var draft = (languages ?? new List<LanguageConfigEntryMessage>()).Select(CloneEntry).ToList();
        if (draft.Count == 0)
        {
            draft.Add(new LanguageConfigEntryMessage
            {
                Name = "Common",
                Description = "The universal language",
                Prefix = "c",
                Color = "#E9DDCE",
                Syllables = "al, er, at",
                Default = true
            });
        }

        return draft;
    }

    private static LanguageConfigEntryMessage CloneEntry(LanguageConfigEntryMessage source)
    {
        source ??= new LanguageConfigEntryMessage();
        return new LanguageConfigEntryMessage
        {
            OriginalName = source.OriginalName,
            Name = source.Name,
            Description = source.Description,
            Prefix = source.Prefix,
            Syllables = source.Syllables,
            Color = source.Color,
            Default = source.Default,
            Hidden = source.Hidden,
            GrantedToClasses = source.GrantedToClasses,
            GrantedToModels = source.GrantedToModels,
            GrantedToModelGroups = source.GrantedToModelGroups,
            GrantedToTraits = source.GrantedToTraits
        };
    }

    private static void AddButton(GuiComposer composer, string code, string text, ActionConsumable onClick, ElementBounds bounds, string tooltip)
    {
        composer.AddSmallButton(text, onClick, bounds, EnumButtonStyle.Normal, code);
        AddTooltip(composer, code, bounds, tooltip);
    }

    private static void AddLabel(GuiComposer composer, string text, ElementBounds bounds, bool bold = false, string tooltip = null)
    {
        var font = CairoFont.WhiteSmallText();
        if (bold)
        {
            font = font.WithWeight(Cairo.FontWeight.Bold);
        }

        composer.AddStaticText(text, font, bounds);
        if (!string.IsNullOrWhiteSpace(tooltip))
        {
            AddTooltip(composer, "tooltip-label-" + Math.Abs((text + bounds.fixedX + bounds.fixedY).GetHashCode()), bounds, tooltip);
        }
    }

    private void AddRichtext(GuiComposer composer, string vtmlCode, ElementBounds bounds)
    {
        composer.AddRichtext(VtmlUtil.Richtextify(capi, vtmlCode, CairoFont.WhiteSmallText()), bounds);
    }

    private static void AddTooltip(GuiComposer composer, string key, ElementBounds bounds, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        composer.AddHoverText(text, CairoFont.WhiteSmallText(), 360, bounds.FlatCopy(), "tooltip-" + key);
        composer.GetHoverText("tooltip-" + key).SetAutoWidth(on: true);
    }
}
