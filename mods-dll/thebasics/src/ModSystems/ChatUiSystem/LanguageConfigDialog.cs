using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
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
    private const double StatusHeight = 40;
    private const double FieldHeight = 28;
    private const double FieldRowHeight = 55;
    private readonly Action<List<LanguageConfigEntryMessage>> _onSave;
    private readonly Action _onReload;
    private readonly Action _onClose;
    private readonly Dictionary<string, GuiElementTextInput> _textInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GuiElementSetupToggle> _toggles = new(StringComparer.OrdinalIgnoreCase);
    private GuiElementColorPicker _colorPicker;
    private List<LanguageConfigEntryMessage> _languages;
    private string _message;
    private bool _success;
    private int _selectedIndex;
    private bool _showGrants;
    private bool _closing;
    private readonly DialogDraftState _draftState;
    private Dictionary<LanguageConfigEntryMessage, string> _submittedNames;
    private bool _requiresAuthoritativeRefresh;
    internal static Dictionary<LanguageConfigEntryMessage, string> CaptureSubmittedNames(List<LanguageConfigEntryMessage> languages)
    {
        var submitted = new Dictionary<LanguageConfigEntryMessage, string>(ReferenceEqualityComparer.Instance);
        foreach (var entry in languages) submitted.Add(entry, entry.Name ?? string.Empty);
        return submitted;
    }

    internal void OnRequestFailed()
    {
        _draftState.CancelRequest();
        if (!_requiresAuthoritativeRefresh) _submittedNames = null;
    }

    internal void OnRequestTimedOut()
    {
        _draftState.CancelRequest();
        if (_requiresAuthoritativeRefresh || _submittedNames == null) return;
        _requiresAuthoritativeRefresh = true;
        SendReload();
    }
    internal static void RebaseSavedNames(List<LanguageConfigEntryMessage> draft, Dictionary<LanguageConfigEntryMessage, string> submitted, List<LanguageConfigEntryMessage> saved)
    {
        if (submitted == null) return;
        foreach (var entry in draft)
        {
            if (!submitted.TryGetValue(entry, out var submittedName)) continue;
            var acknowledged = saved.Find(language => string.Equals(language.Name, submittedName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (acknowledged != null) entry.OriginalName = acknowledged.OriginalName;
        }
    }

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
        _draftState = new DialogDraftState(JsonConvert.SerializeObject(_languages));
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

    internal bool JumpTo(string name, string field)
    {
        CaptureSelectedInputToDraft();
        var index = _languages.FindIndex(entry => string.Equals(entry.OriginalName, name, StringComparison.OrdinalIgnoreCase));
        if (index < 0) return false;
        _selectedIndex = index;
        _showGrants = field.StartsWith("GrantedTo", StringComparison.Ordinal);
        ComposeDialog();
        GuiElement control = _textInputs.GetValueOrDefault(field);
        control ??= _toggles.GetValueOrDefault(field);
        if (control == null) return false;
        SingleComposer.FocusElement(control.TabIndex);
        return true;
    }

    public void SetView(List<LanguageConfigEntryMessage> languages, string message, bool success)
    {
        CaptureSelectedInputToDraft();
        var incoming = EnsureDraft(languages);
        if (!_draftState.ApplyResponse(
                JsonConvert.SerializeObject(_languages),
                JsonConvert.SerializeObject(incoming),
                success,
                preserveCurrent: _requiresAuthoritativeRefresh))
        {
            _languages = incoming;
        }
        else if (success)
        {
            RebaseSavedNames(_languages, _submittedNames, incoming);
        }
        if (success)
        {
            _submittedNames = null;
            _requiresAuthoritativeRefresh = false;
        }
        _message = message;
        _success = success;
        _selectedIndex = ClampSelectedIndex(_selectedIndex);
        ComposeDialog();
    }

    public override bool TryClose()
    {
        _colorPicker?.Close();
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
        _colorPicker = null;
        _textInputs.Clear();
        _toggles.Clear();
        _selectedIndex = ClampSelectedIndex(_selectedIndex);

        var contentTop = GuiStyle.TitleBarHeight + 10;
        var compact = capi.Render.FrameHeight / (double)RuntimeEnv.GUIScale < 650;
        var helpHeight = compact ? 0 : 32;
        var helpBounds = ElementBounds.Fixed(0, contentTop, ContentWidth, helpHeight);
        var statusBounds = ElementBounds.Fixed(0, helpBounds.fixedY + helpHeight + (compact ? 0 : 6), ContentWidth, StatusHeight);
        var panelY = statusBounds.fixedY + StatusHeight + (compact ? 6 : 10);
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

        if (!compact) AddRichtext(composer, VtmlUtils.EscapeVtml(Lang.Get("thebasics:language-config-help")), helpBounds);
        AddStatus(composer, statusBounds);
        AddLanguageList(composer, leftPanelBounds);
        AddSelectedLanguageFields(composer, rightPanelBounds);

        AddButton(composer, "reload", Lang.Get("thebasics:language-config-reload"), OnReload, ElementBounds.Fixed(0, buttonY, 118, buttonHeight), Lang.Get("thebasics:language-config-reload-tooltip"));
        AddButton(composer, "close", Lang.Get("thebasics:language-config-close"), OnCancel, ElementBounds.Fixed(ContentWidth - 250, buttonY, 110, buttonHeight), Lang.Get("thebasics:language-config-close-tooltip"));
        AddButton(composer, "save", Lang.Get("thebasics:language-config-save"), OnSave, ElementBounds.Fixed(ContentWidth - 130, buttonY, 118, buttonHeight), Lang.Get("thebasics:language-config-save-tooltip"));

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
        var selectedLanguageDropDown = new LanguageSelector(capi, values, names, _selectedIndex, OnSelectedLanguageChanged, selectedBounds);
        composer.AddInteractiveElement(selectedLanguageDropDown, "language-selector");
        AddTooltip(composer, "language-selector", selectedBounds, Lang.Get("thebasics:language-config-selected-tooltip"));

        y += 68;
        AddButton(composer, "add", Lang.Get("thebasics:language-config-add"), OnAddLanguage, ElementBounds.Fixed(x, y, 104, 28), Lang.Get("thebasics:language-config-add-tooltip"));
        AddButton(composer, "delete", Lang.Get("thebasics:language-config-delete"), OnDeleteSelectedLanguage, ElementBounds.Fixed(x, panelBounds.fixedY + panelBounds.fixedHeight - 40, width, 28), Lang.Get("thebasics:language-config-delete-tooltip"));
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
        AddLabel(composer, Lang.Get("thebasics:language-config-section"), ElementBounds.Fixed(x, y, width, 20));
        composer.AddDynamicText(DisplayName(language), CairoFont.WhiteSmallText().WithFontSize(18).WithColor(new[] { .95, .72, .3, 1d }),
            ElementBounds.Fixed(x, y + 22, width, 27), "language-title");
        composer.AddHorizontalTabs(new[] { new GuiTab { Name = Lang.Get("thebasics:language-config-details"), DataInt = 0 }, new GuiTab { Name = Lang.Get("thebasics:language-config-grants"), DataInt = 1 } },
            ElementBounds.Fixed(x, y + 56, width, 28), index =>
            {
                if (_showGrants == (index == 1)) return;
                CaptureSelectedInputToDraft();
                _showGrants = index == 1;
                ComposeDialog();
            }, CairoFont.WhiteSmallText(), CairoFont.WhiteSmallText(), "language-tabs");
        composer.GetHorizontalTabs("language-tabs").SetValue(_showGrants ? 1 : 0, false);
        if (_showGrants)
        {
            AddLabel(composer, Lang.Get("thebasics:language-config-grants-help"), ElementBounds.Fixed(x, y + 96, width, 22));
            var grantsY = y + 130;
            AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToClasses), Lang.Get("thebasics:language-config-classes"), language.GrantedToClasses, ElementBounds.Fixed(x, grantsY, width, FieldHeight), 512, Lang.Get("thebasics:language-config-classes-tooltip"));
            AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToTraits), Lang.Get("thebasics:language-config-traits"), language.GrantedToTraits, ElementBounds.Fixed(x, grantsY + FieldRowHeight, width, FieldHeight), 512, Lang.Get("thebasics:language-config-traits-tooltip"));
            AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToModels), Lang.Get("thebasics:language-config-models"), language.GrantedToModels, ElementBounds.Fixed(x, grantsY + FieldRowHeight * 2, width, FieldHeight), 512, Lang.Get("thebasics:language-config-models-tooltip"));
            AddTextInput(composer, nameof(LanguageConfigEntryMessage.GrantedToModelGroups), Lang.Get("thebasics:language-config-model-groups"), language.GrantedToModelGroups, ElementBounds.Fixed(x, grantsY + FieldRowHeight * 3, width, FieldHeight), 512, Lang.Get("thebasics:language-config-model-groups-tooltip"));
            return;
        }

        var leftY = y + 96;
        leftY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Name), Lang.Get("thebasics:language-config-name"), language.Name, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), 64, Lang.Get("thebasics:language-config-name-tooltip"));
        leftY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Prefix), Lang.Get("thebasics:language-config-prefix"), language.Prefix, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), 32, Lang.Get("thebasics:language-config-prefix-tooltip"));
        leftY = AddColorInput(composer, language, x, leftY, columnWidth);
        leftY = AddBoolToggle(composer, nameof(LanguageConfigEntryMessage.Default), Lang.Get("thebasics:language-config-default"), language.Default, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:language-config-default-tooltip"));
        AddBoolToggle(composer, nameof(LanguageConfigEntryMessage.Hidden), Lang.Get("thebasics:language-config-hidden"), language.Hidden, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:language-config-hidden-tooltip"));

        var rightY = y + 96;
        rightY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Description), Lang.Get("thebasics:language-config-description"), language.Description, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 256, Lang.Get("thebasics:language-config-description-tooltip"));
        rightY = AddTextInput(composer, nameof(LanguageConfigEntryMessage.Syllables), Lang.Get("thebasics:language-config-syllables"), language.Syllables, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 512, Lang.Get("thebasics:language-config-syllables-tooltip"));
    }

    private double AddColorInput(GuiComposer composer, LanguageConfigEntryMessage language, double x, double y, double columnWidth)
    {
        var labelBounds = ElementBounds.Fixed(x, y, columnWidth, 20);
        AddLabel(composer, Lang.Get("thebasics:language-config-color"), labelBounds, tooltip: Lang.Get("thebasics:language-config-color-tooltip"));

        var originalColor = language.Color;
        var inputWidth = columnWidth - 133;
        var inputBounds = ElementBounds.Fixed(x + 39, y + 22, inputWidth, FieldHeight);
        var input = new GuiElementTextInput(capi, inputBounds, value =>
        {
            _colorPicker?.SetValue(value);
            if (composer.GetElement("color-preview") is GuiElementDynamicText preview)
            {
                preview.Font = GetColorPreviewFont(value).WithOrientation(EnumTextOrientation.Center);
                preview.RecomposeText();
            }
        }, CairoFont.TextInput());
        input.SetValue(language.Color ?? string.Empty);
        input.SetMaxLength(16);
        _textInputs[nameof(LanguageConfigEntryMessage.Color)] = input;
        composer.AddInteractiveElement(input, "input-color");

        _colorPicker = new GuiElementColorPicker(capi, ElementBounds.Fixed(x, y + 22, 32, FieldHeight), language.Color, color =>
        {
            // Keep the stored spelling when a user restores the original color.
            if (string.Equals(color, originalColor, StringComparison.OrdinalIgnoreCase)) color = originalColor;
            input.SetValue(color ?? string.Empty);
        });
        composer.AddInteractiveElement(_colorPicker, "color-picker");
        var previewBounds = ElementBounds.Fixed(x + columnWidth - 86, y + 24, 82, 22);
        composer.AddDynamicText(Lang.Get("thebasics:language-config-color-preview"), GetColorPreviewFont(language.Color).WithOrientation(EnumTextOrientation.Center), previewBounds, "color-preview");
        AddTooltip(composer, "input-color", labelBounds, Lang.Get("thebasics:language-config-color-tooltip"));
        return y + FieldRowHeight;
    }

    public override void OnRenderGUI(float deltaTime)
    {
        GuiPopupHoverGuard.RenderBackground(SingleComposer, _colorPicker?.IsOpen == true, () => base.OnRenderGUI(deltaTime));
        if (_colorPicker?.IsOpen != true) return;
        _colorPicker.RenderPopup(deltaTime);
        if (_colorPicker.IsPositionInside(capi.Input.MouseX, capi.Input.MouseY))
            MouseOverCursor = _colorPicker.MouseOverCursor;
    }

    public override bool OnEscapePressed()
    {
        if (_colorPicker?.IsOpen != true) return base.OnEscapePressed();
        _colorPicker.Close();
        SingleComposer.FocusElement(_colorPicker.TabIndex);
        return true;
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (_colorPicker?.IsOpen == true) _colorPicker.OnMouseDown(capi, args);
        if (!args.Handled) base.OnMouseDown(args);
    }

    public override void OnMouseMove(MouseEvent args)
    {
        if (_colorPicker?.IsOpen == true) _colorPicker.OnMouseMove(capi, args);
        if (!args.Handled) base.OnMouseMove(args);
    }

    public override void OnMouseUp(MouseEvent args)
    {
        if (_colorPicker?.IsOpen == true) _colorPicker.OnMouseUp(capi, args);
        if (!args.Handled) base.OnMouseUp(args);
    }

    public override void OnMouseWheel(MouseWheelEventArgs args)
    {
        if (_colorPicker?.IsOpen == true) _colorPicker.OnMouseWheel(capi, args);
        if (!args.IsHandled) base.OnMouseWheel(args);
    }

    public override void OnKeyDown(KeyEvent args)
    {
        if (_colorPicker?.IsOpen == true) _colorPicker.OnKeyDown(capi, args);
        if (!args.Handled) base.OnKeyDown(args);
    }

    public override void OnKeyPress(KeyEvent args)
    {
        if (_colorPicker?.IsOpen == true) _colorPicker.OnKeyPress(capi, args);
        if (!args.Handled) base.OnKeyPress(args);
    }

    private sealed class LanguageSelector : GuiElementDropDown
    {
        internal LanguageSelector(ICoreClientAPI api, string[] values, string[] names, int index, SelectionChangedDelegate changed, ElementBounds bounds)
            : base(api, values, names, index, changed, bounds, CairoFont.WhiteSmallText(), false)
        { MouseOverCursor = "linkselect"; }
    }

    private double AddTextInput(GuiComposer composer, string field, string label, string value, ElementBounds inputBounds, int maxLength, string tooltip)
    {
        var labelBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY, inputBounds.fixedWidth, 20);
        AddLabel(composer, label, labelBounds, tooltip: tooltip);

        var actualInputBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY + 22, inputBounds.fixedWidth, inputBounds.fixedHeight);
        var input = new GuiElementTextInput(capi, actualInputBounds, field == nameof(LanguageConfigEntryMessage.Name)
            ? value => composer.GetDynamicText("language-title")?.SetNewText(string.IsNullOrWhiteSpace(value) ? Lang.Get("thebasics:language-config-unnamed") : value) : null, CairoFont.TextInput());
        input.SetValue(value ?? string.Empty, setCaretPosToEnd: false);
        if (maxLength > 0)
        {
            input.SetMaxLength(maxLength);
        }

        _textInputs[field] = input;
        composer.AddInteractiveElement(input, "input-" + field);
        AddTooltip(composer, "input-" + field, labelBounds, tooltip);
        return inputBounds.fixedY + FieldRowHeight;
    }

    private double AddBoolToggle(GuiComposer composer, string field, string label, bool value, ElementBounds inputBounds, string tooltip)
    {
        var labelBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY, inputBounds.fixedWidth, 20);
        AddLabel(composer, label, labelBounds, tooltip: tooltip);

        var actualInputBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY + 22, 112, inputBounds.fixedHeight);
        var toggle = new GuiElementSetupToggle(capi, value, _ => { }, actualInputBounds);
        _toggles[field] = toggle;
        composer.AddInteractiveElement(toggle, "toggle-" + field);
        AddTooltip(composer, "toggle-" + field, labelBounds, tooltip);
        return inputBounds.fixedY + FieldRowHeight;
    }

    private void CaptureSelectedInputToDraft()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _languages.Count)
        {
            return;
        }

        var language = _languages[_selectedIndex];
        if (_showGrants)
        {
            language.GrantedToClasses = GetText(nameof(LanguageConfigEntryMessage.GrantedToClasses));
            language.GrantedToTraits = GetText(nameof(LanguageConfigEntryMessage.GrantedToTraits));
            language.GrantedToModels = GetText(nameof(LanguageConfigEntryMessage.GrantedToModels));
            language.GrantedToModelGroups = GetText(nameof(LanguageConfigEntryMessage.GrantedToModelGroups));
            return;
        }
        language.Name = GetText(nameof(LanguageConfigEntryMessage.Name));
        language.Prefix = GetText(nameof(LanguageConfigEntryMessage.Prefix));
        language.Description = GetText(nameof(LanguageConfigEntryMessage.Description));
        language.Color = GetText(nameof(LanguageConfigEntryMessage.Color));
        language.Default = GetBool(nameof(LanguageConfigEntryMessage.Default));
        language.Hidden = GetBool(nameof(LanguageConfigEntryMessage.Hidden));
        language.Syllables = GetText(nameof(LanguageConfigEntryMessage.Syllables));
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
        _showGrants = false;
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
        if (_requiresAuthoritativeRefresh)
        {
            SendReload();
            return true;
        }
        if (!_draftState.TryBeginRequest(JsonConvert.SerializeObject(_languages))) return true;
        _submittedNames = CaptureSubmittedNames(_languages);
        _onSave(_languages.Select(CloneEntry).ToList());
        return true;
    }

    private bool OnReload()
    {
        SendReload();
        return true;
    }

    private void SendReload()
    {
        CaptureSelectedInputToDraft();
        if (!_draftState.TryBeginRequest(JsonConvert.SerializeObject(_languages))) return;
        if (!_requiresAuthoritativeRefresh) _submittedNames = null;
        _onReload();
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
        return _toggles.TryGetValue(field, out var toggle) && toggle.Value;
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
        const int maxLines = 7; // Leave room for the overflow count and Delete below the order summary.
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
