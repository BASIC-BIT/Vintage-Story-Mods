#pragma warning disable S107 // Dialog composition helpers pass UI geometry explicitly for readability at call sites.
using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public class CharacterSheetFieldConfigDialog : GuiDialog
{
    private const double DialogWidth = 980;
    private const double ContentWidth = DialogWidth - 48;
    private const double PanelHeight = 470;
    private const double FieldHeight = 28;
    private const double FieldRowHeight = 55;
    private readonly Action<List<CharacterSheetFieldConfigEntryMessage>> _onSave;
    private readonly Action _onReload;
    private readonly Action _onClose;
    private readonly Dictionary<string, GuiElementTextInput> _textInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GuiElementDropDown> _dropDowns = new(StringComparer.OrdinalIgnoreCase);
    private List<CharacterSheetFieldConfigEntryMessage> _fields;
    private List<CharacterSheetFieldConfigEntryMessage> _lastLoadedFields;
    private int _selectedIndex;
    private bool _closing;
    private bool _forceClose;
    private GuiDialogConfirm _unsavedCloseConfirm;
    private bool _suspendCallbacks;

    public CharacterSheetFieldConfigDialog(
        ICoreClientAPI capi,
        List<CharacterSheetFieldConfigEntryMessage> fields,
        Action<List<CharacterSheetFieldConfigEntryMessage>> onSave,
        Action onReload,
        Action onClose) : base(capi)
    {
        _fields = EnsureDraft(fields);
        _lastLoadedFields = _fields.Select(CloneEntry).ToList();
        _onSave = onSave;
        _onReload = onReload;
        _onClose = onClose;
        ComposeDialog();
    }

    public override bool PrefersUngrabbedMouse => true;

    public override string ToggleKeyCombinationCode => null;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.27;

    public void SetView(List<CharacterSheetFieldConfigEntryMessage> fields, bool updateBaseline)
    {
        _fields = EnsureDraft(fields);
        if (updateBaseline)
        {
            _lastLoadedFields = _fields.Select(CloneEntry).ToList();
        }

        _selectedIndex = ClampSelectedIndex(_selectedIndex);
        ComposeDialog();
    }

    public override bool TryClose()
    {
        if (!_forceClose)
        {
            CaptureSelectedInputToDraft();
            if (HasUnsavedChanges())
            {
                ConfirmCloseWithUnsavedChanges();
                return false;
            }
        }

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
        _suspendCallbacks = true;

        var contentTop = GuiStyle.TitleBarHeight + 10;
        var panelY = contentTop;
        var leftPanelWidth = 250;
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

        var composer = capi.Gui.CreateCompo("thebasics-charsheet-field-config", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:charsheet-field-config-title"), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(leftPanelBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3)
            .AddInset(rightPanelBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3), 3);

        AddFieldList(composer, leftPanelBounds);
        AddSelectedFieldEditor(composer, rightPanelBounds);

        AddButton(composer, "reload", Lang.Get("thebasics:charsheet-field-config-reload"), OnReload, ElementBounds.Fixed(0, buttonY, 118, buttonHeight), Lang.Get("thebasics:charsheet-field-config-reload-tooltip"));
        AddButton(composer, "save", Lang.Get("thebasics:charsheet-field-config-save"), OnSave, ElementBounds.Fixed(ContentWidth - 250, buttonY, 118, buttonHeight), Lang.Get("thebasics:charsheet-field-config-save-tooltip"));
        AddButton(composer, "close", Lang.Get("thebasics:charsheet-field-config-close"), OnCancel, ElementBounds.Fixed(ContentWidth - 122, buttonY, 110, buttonHeight), Lang.Get("thebasics:charsheet-field-config-close-tooltip"));

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
        _suspendCallbacks = false;
    }

    private void AddFieldList(GuiComposer composer, ElementBounds panelBounds)
    {
        var x = panelBounds.fixedX + 8;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 16;
        AddLabel(composer, Lang.Get("thebasics:charsheet-field-config-selected"), ElementBounds.Fixed(x, y, width, 22), bold: true, Lang.Get("thebasics:charsheet-field-config-selected-tooltip"));

        var values = Enumerable.Range(0, _fields.Count).Select(index => index.ToString()).ToArray();
        var names = _fields.Select((field, index) => TrimListText($"{index + 1}. {DisplayLabel(field)}", 28)).ToArray();
        var selectedBounds = ElementBounds.Fixed(x, y + 26, width, FieldHeight);
        var selector = new GuiElementDropDown(capi, values, names, _selectedIndex, OnSelectedFieldChanged, selectedBounds, CairoFont.WhiteSmallText(), multiSelect: false);
        composer.AddInteractiveElement(selector, "field-selector");
        AddTooltip(composer, "field-selector", selectedBounds, Lang.Get("thebasics:charsheet-field-config-selected-tooltip"));

        y += 68;
        AddButton(composer, "add", Lang.Get("thebasics:charsheet-field-config-add"), OnAddField, ElementBounds.Fixed(x, y, 104, 28), Lang.Get("thebasics:charsheet-field-config-add-tooltip"));
        AddButton(composer, "delete", Lang.Get("thebasics:charsheet-field-config-delete"), OnDeleteSelectedField, ElementBounds.Fixed(x + 114, y, 104, 28), Lang.Get("thebasics:charsheet-field-config-delete-tooltip"));
        y += 36;
        AddButton(composer, "up", Lang.Get("thebasics:charsheet-field-config-up"), OnMoveSelectedFieldUp, ElementBounds.Fixed(x, y, 104, 28), Lang.Get("thebasics:charsheet-field-config-up-tooltip"));
        AddButton(composer, "down", Lang.Get("thebasics:charsheet-field-config-down"), OnMoveSelectedFieldDown, ElementBounds.Fixed(x + 114, y, 104, 28), Lang.Get("thebasics:charsheet-field-config-down-tooltip"));

        y += 44;
        AddLabel(composer, Lang.Get("thebasics:charsheet-field-config-order"), ElementBounds.Fixed(x, y, width, 22), bold: true, Lang.Get("thebasics:charsheet-field-config-order-tooltip"));
        y += 24;

        foreach (var line in BuildOrderSummaryLines())
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

    private void AddSelectedFieldEditor(GuiComposer composer, ElementBounds panelBounds)
    {
        var field = _fields[_selectedIndex];
        var x = panelBounds.fixedX + 10;
        var y = panelBounds.fixedY + 8;
        var width = panelBounds.fixedWidth - 20;
        var columnGap = 18;
        var columnWidth = (width - columnGap) / 2;
        var rightColumnX = x + columnWidth + columnGap;
        AddLabel(composer, Lang.Get("thebasics:charsheet-field-config-editing", _selectedIndex + 1, DisplayName(field)), ElementBounds.Fixed(x, y, width, 24), bold: true, Lang.Get("thebasics:charsheet-field-config-editing-tooltip"));

        var leftY = y + 34;
        leftY = AddTextInput(composer, nameof(CharacterSheetFieldConfigEntryMessage.Label), Lang.Get("thebasics:charsheet-field-config-label"), field.Label, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), 100, Lang.Get("thebasics:charsheet-field-config-label-tooltip"), OnLabelChanged);
        leftY = AddIdEditor(composer, field, x, leftY, columnWidth);
        leftY = AddTextInput(composer, nameof(CharacterSheetFieldConfigEntryMessage.Description), Lang.Get("thebasics:charsheet-field-config-description"), field.Description, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), 280, Lang.Get("thebasics:charsheet-field-config-description-tooltip"));
        leftY = AddSelectDropDown(composer, nameof(CharacterSheetFieldConfigEntryMessage.Type), Lang.Get("thebasics:charsheet-field-config-type"), GetTypeValues(), GetTypeNames(), field.Type, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:charsheet-field-config-type-tooltip"));
        leftY = AddBoolDropDown(composer, nameof(CharacterSheetFieldConfigEntryMessage.Optional), Lang.Get("thebasics:charsheet-field-config-optional"), field.Optional, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:charsheet-field-config-optional-tooltip"));
        leftY = AddSelectDropDown(composer, nameof(CharacterSheetFieldConfigEntryMessage.Visibility), Lang.Get("thebasics:charsheet-field-config-visibility"), GetVisibilityValues(), GetVisibilityNames(), field.Visibility, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:charsheet-field-config-visibility-tooltip"));
        AddBoolDropDown(composer, nameof(CharacterSheetFieldConfigEntryMessage.ShowInLook), Lang.Get("thebasics:charsheet-field-config-show-in-look"), field.ShowInLook, ElementBounds.Fixed(x, leftY, columnWidth, FieldHeight), Lang.Get("thebasics:charsheet-field-config-show-in-look-tooltip"));

        var rightY = y + 34;
        rightY = AddSelectDropDown(composer, nameof(CharacterSheetFieldConfigEntryMessage.BindTo), Lang.Get("thebasics:charsheet-field-config-bind-to"), GetBindValues(), GetBindNames(), field.BindTo, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), Lang.Get("thebasics:charsheet-field-config-bind-to-tooltip"));
        rightY = AddTextInput(composer, nameof(CharacterSheetFieldConfigEntryMessage.MaxLength), Lang.Get("thebasics:charsheet-field-config-max-length"), field.MaxLength, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 16, Lang.Get("thebasics:charsheet-field-config-max-length-tooltip"));
        rightY = AddTextInput(composer, nameof(CharacterSheetFieldConfigEntryMessage.EditorRows), Lang.Get("thebasics:charsheet-field-config-editor-rows"), field.EditorRows, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 16, Lang.Get("thebasics:charsheet-field-config-editor-rows-tooltip"));
        rightY = AddSelectDropDown(composer, nameof(CharacterSheetFieldConfigEntryMessage.LayoutSection), Lang.Get("thebasics:charsheet-field-config-layout"), GetLayoutValues(), GetLayoutNames(), field.LayoutSection, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), Lang.Get("thebasics:charsheet-field-config-layout-tooltip"));
        rightY = AddSelectDropDown(composer, nameof(CharacterSheetFieldConfigEntryMessage.Width), Lang.Get("thebasics:charsheet-field-config-width"), GetWidthValues(), GetWidthNames(), field.Width, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), Lang.Get("thebasics:charsheet-field-config-width-tooltip"));
        AddTextInput(composer, nameof(CharacterSheetFieldConfigEntryMessage.Options), Lang.Get("thebasics:charsheet-field-config-options"), field.Options, ElementBounds.Fixed(rightColumnX, rightY, columnWidth, FieldHeight), 512, Lang.Get("thebasics:charsheet-field-config-options-tooltip"));
    }

    private double AddIdEditor(GuiComposer composer, CharacterSheetFieldConfigEntryMessage field, double x, double y, double columnWidth)
    {
        if (string.IsNullOrWhiteSpace(field.OriginalId))
        {
            return AddTextInput(composer, nameof(CharacterSheetFieldConfigEntryMessage.Id), Lang.Get("thebasics:charsheet-field-config-key"), field.Id, ElementBounds.Fixed(x, y, columnWidth, FieldHeight), 72, Lang.Get("thebasics:charsheet-field-config-key-tooltip"), OnIdChanged);
        }

        var labelBounds = ElementBounds.Fixed(x, y, columnWidth, 20);
        AddLabel(composer, Lang.Get("thebasics:charsheet-field-config-key-locked"), labelBounds, tooltip: Lang.Get("thebasics:charsheet-field-config-key-locked-tooltip"));
        var valueBounds = ElementBounds.Fixed(x, y + 22, columnWidth, FieldHeight);
        composer.AddStaticText(field.Id ?? string.Empty, CairoFont.WhiteSmallText(), valueBounds);
        AddTooltip(composer, "field-key-locked", valueBounds, Lang.Get("thebasics:charsheet-field-config-key-locked-tooltip"));
        return y + FieldRowHeight;
    }

    private double AddTextInput(GuiComposer composer, string field, string label, string value, ElementBounds inputBounds, int maxLength, string tooltip, Action<string> onTextChanged = null)
    {
        var labelBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY, inputBounds.fixedWidth, 20);
        AddLabel(composer, label, labelBounds, tooltip: tooltip);

        var actualInputBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY + 22, inputBounds.fixedWidth, inputBounds.fixedHeight);
        var input = new GuiElementTextInput(capi, actualInputBounds, onTextChanged, CairoFont.TextInput());
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

    private double AddSelectDropDown(GuiComposer composer, string field, string label, string[] values, string[] names, string selectedValue, ElementBounds inputBounds, string tooltip)
    {
        var labelBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY, inputBounds.fixedWidth, 20);
        AddLabel(composer, label, labelBounds, tooltip: tooltip);

        var selectedIndex = Array.FindIndex(values, value => string.Equals(value, selectedValue ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        var actualInputBounds = ElementBounds.Fixed(inputBounds.fixedX, inputBounds.fixedY + 22, inputBounds.fixedWidth, inputBounds.fixedHeight);
        var dropDown = new GuiElementDropDown(capi, values, names, selectedIndex, null, actualInputBounds, CairoFont.WhiteSmallText(), multiSelect: false);
        _dropDowns[field] = dropDown;
        composer.AddInteractiveElement(dropDown, "dropdown-" + field);
        AddTooltip(composer, "dropdown-" + field, labelBounds, tooltip);
        return inputBounds.fixedY + FieldRowHeight;
    }

    private void CaptureSelectedInputToDraft()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _fields.Count)
        {
            return;
        }

        var field = _fields[_selectedIndex];
        field.Label = GetText(nameof(CharacterSheetFieldConfigEntryMessage.Label));
        field.Description = GetText(nameof(CharacterSheetFieldConfigEntryMessage.Description));
        field.Type = GetSelectedValue(nameof(CharacterSheetFieldConfigEntryMessage.Type));
        field.Optional = GetBool(nameof(CharacterSheetFieldConfigEntryMessage.Optional));
        field.Options = GetText(nameof(CharacterSheetFieldConfigEntryMessage.Options));
        field.BindTo = GetSelectedValue(nameof(CharacterSheetFieldConfigEntryMessage.BindTo));
        field.MaxLength = GetText(nameof(CharacterSheetFieldConfigEntryMessage.MaxLength));
        field.Visibility = GetSelectedValue(nameof(CharacterSheetFieldConfigEntryMessage.Visibility));
        field.ShowInLook = GetBool(nameof(CharacterSheetFieldConfigEntryMessage.ShowInLook));
        field.EditorRows = GetText(nameof(CharacterSheetFieldConfigEntryMessage.EditorRows));
        field.LayoutSection = GetSelectedValue(nameof(CharacterSheetFieldConfigEntryMessage.LayoutSection));
        field.Width = GetSelectedValue(nameof(CharacterSheetFieldConfigEntryMessage.Width));

        if (!string.IsNullOrWhiteSpace(field.OriginalId))
        {
            field.Id = field.OriginalId;
            field.AutoGenerateId = false;
            return;
        }

        var enteredId = GetText(nameof(CharacterSheetFieldConfigEntryMessage.Id));
        field.Id = field.AutoGenerateId
            ? CharacterSheetFieldConfigAdmin.GenerateSuggestedId(field.Label)
            : enteredId;
    }

    private void OnLabelChanged(string value)
    {
        if (_suspendCallbacks || _selectedIndex < 0 || _selectedIndex >= _fields.Count)
        {
            return;
        }

        var field = _fields[_selectedIndex];
        field.Label = value ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(field.OriginalId) || !field.AutoGenerateId)
        {
            return;
        }

        var generatedId = CharacterSheetFieldConfigAdmin.GenerateSuggestedId(field.Label);
        field.Id = generatedId;
        if (_textInputs.TryGetValue(nameof(CharacterSheetFieldConfigEntryMessage.Id), out var idInput))
        {
            _suspendCallbacks = true;
            idInput.SetValue(generatedId);
            _suspendCallbacks = false;
        }
    }

    private void OnIdChanged(string value)
    {
        if (_suspendCallbacks || _selectedIndex < 0 || _selectedIndex >= _fields.Count)
        {
            return;
        }

        var field = _fields[_selectedIndex];
        if (!string.IsNullOrWhiteSpace(field.OriginalId))
        {
            return;
        }

        field.Id = value ?? string.Empty;
        field.AutoGenerateId = string.Equals(
            (field.Id ?? string.Empty).Trim(),
            CharacterSheetFieldConfigAdmin.GenerateSuggestedId(field.Label),
            StringComparison.Ordinal);
    }

    private bool OnAddField()
    {
        CaptureSelectedInputToDraft();
        var label = Lang.Get("thebasics:charsheet-field-config-new-label");
        _fields.Add(new CharacterSheetFieldConfigEntryMessage
        {
            Id = GetUniqueDraftId(label),
            Label = label,
            Description = string.Empty,
            Type = CharacterSheetFieldTypes.String,
            Optional = true,
            Options = string.Empty,
            BindTo = string.Empty,
            MaxLength = "0",
            Visibility = CharacterSheetFieldVisibilities.Public,
            ShowInLook = true,
            EditorRows = "0",
            LayoutSection = CharacterSheetLayoutSections.Body,
            Width = CharacterSheetFieldWidths.Full,
            AutoGenerateId = true
        });
        _selectedIndex = _fields.Count - 1;
        ComposeDialog();
        return true;
    }

    private bool OnDeleteSelectedField()
    {
        CaptureSelectedInputToDraft();
        if (_fields.Count <= 1 || _selectedIndex < 0 || _selectedIndex >= _fields.Count)
        {
            return true;
        }

        var indexToDelete = _selectedIndex;
        var fieldName = DisplayName(_fields[indexToDelete]);
        new GuiDialogConfirm(capi, Lang.Get("thebasics:charsheet-field-config-delete-confirm", fieldName), ok =>
        {
            if (ok)
            {
                DeleteFieldAt(indexToDelete);
            }
        }).TryOpen();
        return true;
    }

    private void DeleteFieldAt(int index)
    {
        if (_fields.Count <= 1 || index < 0 || index >= _fields.Count)
        {
            return;
        }

        _fields.RemoveAt(index);
        _selectedIndex = ClampSelectedIndex(index);
        ComposeDialog();
    }

    private void ConfirmCloseWithUnsavedChanges()
    {
        if (_unsavedCloseConfirm?.IsOpened() == true)
        {
            return;
        }

        _unsavedCloseConfirm = new GuiDialogConfirm(capi, Lang.Get("thebasics:charsheet-field-config-close-unsaved-confirm"), ok =>
        {
            if (ok)
            {
                _forceClose = true;
                try
                {
                    TryClose();
                }
                finally
                {
                    _forceClose = false;
                }
            }
        });
        _unsavedCloseConfirm.TryOpen();
    }

    private bool OnMoveSelectedFieldUp()
    {
        return MoveSelected(-1);
    }

    private bool OnMoveSelectedFieldDown()
    {
        return MoveSelected(1);
    }

    private bool MoveSelected(int offset)
    {
        CaptureSelectedInputToDraft();
        var nextIndex = _selectedIndex + offset;
        if (_selectedIndex < 0 || nextIndex < 0 || nextIndex >= _fields.Count)
        {
            return true;
        }

        (_fields[_selectedIndex], _fields[nextIndex]) = (_fields[nextIndex], _fields[_selectedIndex]);
        _selectedIndex = nextIndex;
        ComposeDialog();
        return true;
    }

    private bool OnSave()
    {
        CaptureSelectedInputToDraft();
        _onSave(_fields.Select(CloneEntry).ToList());
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

    private void OnSelectedFieldChanged(string value, bool selected)
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
        return _textInputs.TryGetValue(field, out var input) ? input.GetText()?.Trim() ?? string.Empty : string.Empty;
    }

    private string GetSelectedValue(string field)
    {
        return _dropDowns.TryGetValue(field, out var dropDown) ? dropDown.SelectedValue ?? string.Empty : string.Empty;
    }

    private bool GetBool(string field)
    {
        return _dropDowns.TryGetValue(field, out var dropDown) && dropDown.SelectedValue == "1";
    }

    private int ClampSelectedIndex(int index)
    {
        if (_fields.Count == 0)
        {
            return 0;
        }

        return Math.Clamp(index, 0, _fields.Count - 1);
    }

    private string GetUniqueDraftId(string label)
    {
        var baseId = CharacterSheetFieldConfigAdmin.GenerateSuggestedId(label);
        var existing = new HashSet<string>(_fields.Select(field => field.Id), StringComparer.OrdinalIgnoreCase);
        if (!existing.Contains(baseId))
        {
            return baseId;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{baseId}-{index}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseId}-{_fields.Count + 1}";
    }

    private List<(string Text, bool IsSelected)> BuildOrderSummaryLines()
    {
        const int maxLines = 12;
        var lines = new List<(string Text, bool IsSelected)>();
        for (var index = 0; index < Math.Min(maxLines, _fields.Count); index++)
        {
            var marker = index == _selectedIndex ? "> " : "  ";
            var text = $"{marker}{index + 1}. {DisplayLabel(_fields[index])}";
            var maxLength = index == _selectedIndex ? 24 : 28;
            lines.Add((TrimListText(text, maxLength), index == _selectedIndex));
        }

        if (_fields.Count > maxLines)
        {
            lines.Add((Lang.Get("thebasics:charsheet-field-config-order-more", _fields.Count - maxLines), false));
        }

        return lines;
    }

    private static string DisplayName(CharacterSheetFieldConfigEntryMessage field)
    {
        var label = DisplayLabel(field);
        var id = string.IsNullOrWhiteSpace(field?.Id) ? "?" : field.Id;
        return $"{label} [{id}]";
    }

    private static string DisplayLabel(CharacterSheetFieldConfigEntryMessage field)
    {
        return string.IsNullOrWhiteSpace(field?.Label)
            ? Lang.Get("thebasics:charsheet-field-config-unnamed")
            : field.Label.Trim();
    }

    private static string TrimListText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..Math.Max(0, maxLength - 3)].TrimEnd() + "...";
    }

    private bool HasUnsavedChanges()
    {
        if (_lastLoadedFields == null || _fields.Count != _lastLoadedFields.Count)
        {
            return true;
        }

        for (var index = 0; index < _fields.Count; index++)
        {
            if (!EntriesMatch(_fields[index], _lastLoadedFields[index]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EntriesMatch(CharacterSheetFieldConfigEntryMessage left, CharacterSheetFieldConfigEntryMessage right)
    {
        return GetComparableEntryValues(left).SequenceEqual(GetComparableEntryValues(right), StringComparer.Ordinal);
    }

    private static string[] GetComparableEntryValues(CharacterSheetFieldConfigEntryMessage entry)
    {
        entry ??= new CharacterSheetFieldConfigEntryMessage();
        return
        [
            CompareValue(entry.OriginalId),
            CompareValue(entry.Id),
            CompareValue(entry.Label),
            CompareValue(entry.Description),
            CompareValue(entry.Type),
            entry.Optional.ToString(),
            CompareValue(entry.Options),
            CompareValue(entry.BindTo),
            CompareValue(entry.MaxLength),
            CompareValue(entry.Visibility),
            entry.ShowInLook.ToString(),
            CompareValue(entry.EditorRows),
            CompareValue(entry.LayoutSection),
            CompareValue(entry.Width)
        ];
    }

    private static string CompareValue(string value)
    {
        return value ?? string.Empty;
    }

    private static List<CharacterSheetFieldConfigEntryMessage> EnsureDraft(List<CharacterSheetFieldConfigEntryMessage> fields)
    {
        var draft = (fields ?? new List<CharacterSheetFieldConfigEntryMessage>()).Select(CloneEntry).ToList();
        if (draft.Count == 0)
        {
            var label = Lang.Get("thebasics:charsheet-field-config-new-label");
            draft.Add(new CharacterSheetFieldConfigEntryMessage
            {
                Id = CharacterSheetFieldConfigAdmin.GenerateSuggestedId(label),
                Label = label,
                Type = CharacterSheetFieldTypes.String,
                Optional = true,
                Visibility = CharacterSheetFieldVisibilities.Public,
                ShowInLook = true,
                MaxLength = "0",
                EditorRows = "0",
                LayoutSection = CharacterSheetLayoutSections.Body,
                Width = CharacterSheetFieldWidths.Full,
                AutoGenerateId = true
            });
        }

        return draft;
    }

    private static CharacterSheetFieldConfigEntryMessage CloneEntry(CharacterSheetFieldConfigEntryMessage source)
    {
        source ??= new CharacterSheetFieldConfigEntryMessage();
        return new CharacterSheetFieldConfigEntryMessage
        {
            OriginalId = source.OriginalId,
            Id = source.Id,
            Label = source.Label,
            Description = source.Description,
            Type = source.Type,
            Optional = source.Optional,
            Options = source.Options,
            BindTo = source.BindTo,
            MaxLength = source.MaxLength,
            Visibility = source.Visibility,
            ShowInLook = source.ShowInLook,
            EditorRows = source.EditorRows,
            LayoutSection = source.LayoutSection,
            Width = source.Width,
            AutoGenerateId = string.IsNullOrWhiteSpace(source.OriginalId)
                && string.Equals(
                    (source.Id ?? string.Empty).Trim(),
                    CharacterSheetFieldConfigAdmin.GenerateSuggestedId(source.Label),
                    StringComparison.Ordinal)
        };
    }

    private static string[] GetTypeValues() =>
    [
        CharacterSheetFieldTypes.String,
        CharacterSheetFieldTypes.LongString,
        CharacterSheetFieldTypes.Number,
        CharacterSheetFieldTypes.Option
    ];

    private static string[] GetTypeNames() =>
    [
        Lang.Get("thebasics:charsheet-field-config-type-string"),
        Lang.Get("thebasics:charsheet-field-config-type-longstring"),
        Lang.Get("thebasics:charsheet-field-config-type-number"),
        Lang.Get("thebasics:charsheet-field-config-type-option")
    ];

    private static string[] GetVisibilityValues() =>
    [
        CharacterSheetFieldVisibilities.Public,
        CharacterSheetFieldVisibilities.Nearby,
        CharacterSheetFieldVisibilities.Self,
        CharacterSheetFieldVisibilities.Admin
    ];

    private static string[] GetVisibilityNames() =>
    [
        Lang.Get("thebasics:charsheet-field-config-visibility-public"),
        Lang.Get("thebasics:charsheet-field-config-visibility-nearby"),
        Lang.Get("thebasics:charsheet-field-config-visibility-self"),
        Lang.Get("thebasics:charsheet-field-config-visibility-admin")
    ];

    private static string[] GetBindValues() =>
    [
        string.Empty,
        "thebasics.fullName",
        "thebasics.nickname"
    ];

    private static string[] GetBindNames() =>
    [
        Lang.Get("thebasics:charsheet-field-config-bind-none"),
        "thebasics.fullName",
        "thebasics.nickname"
    ];

    private static string[] GetLayoutValues() =>
    [
        CharacterSheetLayoutSections.Body,
        CharacterSheetLayoutSections.HeaderSide
    ];

    private static string[] GetLayoutNames() =>
    [
        Lang.Get("thebasics:charsheet-field-config-layout-body"),
        Lang.Get("thebasics:charsheet-field-config-layout-header-side")
    ];

    private static string[] GetWidthValues() =>
    [
        CharacterSheetFieldWidths.Full,
        CharacterSheetFieldWidths.Half
    ];

    private static string[] GetWidthNames() =>
    [
        Lang.Get("thebasics:charsheet-field-config-width-full"),
        Lang.Get("thebasics:charsheet-field-config-width-half")
    ];

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
