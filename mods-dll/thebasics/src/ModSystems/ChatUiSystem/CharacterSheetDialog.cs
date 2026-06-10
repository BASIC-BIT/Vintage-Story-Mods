using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Models;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public class CharacterSheetDialog : GuiDialog
{
    private const double DialogWidth = 520;
    private const double MaxScrollHeight = 520;
    private const int DefaultLongTextRows = 6;
    private const double ScrollContentBottomPadding = 30;
    private const double TextInputClipTolerance = 6;
    private const double LongTextBaseHeight = 28;
    private const double LongTextRowHeight = 19;
    private const double HeadshotSize = 96;
    private const double HeadshotButtonHeight = 24;
    private const double HeadshotButtonGap = 4;
    private const double TopSectionBottomPadding = 14;
    // Two-column grid for header-side fields. Each cell holds a label above its input.
    private const int HeaderGridCols = 2;
    private const double HeaderGridColGap = 12;
    private const double HeaderGridRowGap = 8;
    private const double HeaderGridLabelHeight = 22;
    private const double HeaderGridInputHeight = 30;
    private const double HeaderGridCellHeight = HeaderGridLabelHeight + HeaderGridInputHeight;
    private readonly Action<CharacterSheetSaveRequest> _onSave;
    private readonly HeadshotDialogCallbacks _headshotCallbacks;
    private readonly Dictionary<int, GuiElementTextInput> _textInputs = new();
    private readonly Dictionary<int, GuiElementTextArea> _textAreas = new();
    private readonly Dictionary<int, string> _textAreaInitialValues = new();
    private readonly Dictionary<int, GuiElementDropDown> _dropDowns = new();
    private GuiElementContainer _scrollContainer;
    private HeadshotElement _headshotElement;
    private HeadshotUrlPromptDialog _urlPrompt;
    private GuiDialogConfirm _unsavedCloseConfirm;
    private CharacterSheetViewMessage _view;
    private bool _forceClose;
    private Action _afterDiscardConfirmed;
    private readonly Action _onClosed;
    private bool _closing;

    public CharacterSheetDialog(ICoreClientAPI capi, CharacterSheetViewMessage view, Action<CharacterSheetSaveRequest> onSave, HeadshotDialogCallbacks headshotCallbacks = null, Action onClosed = null) : base(capi)
    {
        _view = view;
        _onSave = onSave;
        _headshotCallbacks = headshotCallbacks;
        _onClosed = onClosed;
        ComposeDialog();
    }

    public string CurrentTargetPlayerUid => _view?.TargetPlayerUid;

    public bool CanEditHeadshot => _view?.CanEditHeadshot == true && _view?.Success == true;

    /// <summary>
    /// Hot-swaps the headshot element's texture without recomposing the dialog. Pass null for
    /// the empty-state placeholder.
    /// </summary>
    public void ApplyHeadshotTexture(LoadedTexture texture)
    {
        if (_headshotElement == null)
        {
            return;
        }

        if (texture == null || texture.TextureId == 0)
        {
            _headshotElement.ClearTexture();
            _headshotElement.SetStatusText(_view?.CanEditHeadshot == true
                ? Lang.Get("thebasics:headshot-empty-editable")
                : Lang.Get("thebasics:headshot-empty-readonly"));
            return;
        }

        _headshotElement.SetTexture(texture);
    }

    public void SetHeadshotStatus(string status)
    {
        _headshotElement?.SetStatusText(status);
    }

    public override string ToggleKeyCombinationCode => "thebasicscharsheet";

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.25;

    public bool TryCloseFromParent(Action afterDiscardConfirmed)
    {
        if (!_forceClose && HasUnsavedChanges())
        {
            _afterDiscardConfirmed = afterDiscardConfirmed;
            ConfirmCloseWithUnsavedChanges();
            return false;
        }

        return TryClose();
    }

    public override bool TryClose()
    {
        if (!_forceClose && HasUnsavedChanges())
        {
            _afterDiscardConfirmed = null;
            ConfirmCloseWithUnsavedChanges();
            return false;
        }

        return base.TryClose();
    }

    public void SetView(CharacterSheetViewMessage view)
    {
        _view = view;
        ComposeDialog();
    }

    private void ComposeDialog()
    {
        ResetCompositionState();

        var showHeadshotSection = ShouldShowHeadshotSection();
        var title = GetDialogTitle();
        var allFields = GetViewFields();

        // Hoist name fields next to the headshot when the headshot section is visible. When it's
        // not (no callbacks / errored view), name fields render in the scroll list with everything else.
        var hoistedIndices = GetHoistedIndices(showHeadshotSection, allFields);
        var hoistedSet = new HashSet<int>(hoistedIndices);
        var scrollFields = CollectScrollFields(allFields, hoistedSet);

        var contentHeight = CalculateContentHeight(scrollFields.Select(f => f.Field).ToList(), _view);
        var layout = CreateDialogLayout(showHeadshotSection, hoistedIndices.Count, contentHeight);
        _scrollContainer = CreateScrollContainer(layout.ScrollContentBounds, scrollFields);

        var composer = capi.Gui.CreateCompo("thebasics-character-sheet", layout.DialogBounds)
            .AddShadedDialogBG(layout.BodyBounds)
            .AddDialogTitleBar(title, OnTitleBarCloseClicked)
            .BeginChildElements(layout.BodyBounds);

        // Single inset wraps the header + scroll content. Draw it before adding interactive
        // children so the children render on top of the frame.
        composer.AddInset(layout.ScrollInsetBounds, 3);

        if (showHeadshotSection)
        {
            ComposeHeadshotSection(composer, layout.HeadshotTop, hoistedIndices);
        }

        composer
            .BeginClip(layout.ScrollClipBounds)
            .AddInteractiveElement(_scrollContainer, "scrollBody");

        AddScrollContent(layout.Row, scrollFields);

        composer.EndClip()
            .AddVerticalScrollbar(OnNewScrollbarValue, ElementStdBounds.VerticalScrollbar(layout.ScrollClipBounds), "scrollbar")
            .AddSmallButton(Lang.Get("Cancel"), OnCancel, ElementBounds.Fixed(0, layout.ButtonY, 120, layout.ButtonHeight))
            .AddSmallButton(Lang.Get("thebasics:guide-button"), OnGuide, ElementBounds.Fixed(130, layout.ButtonY, 120, layout.ButtonHeight));

        AddSaveButtonIfEditable(composer, layout.ButtonY, layout.ButtonHeight);

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
        SingleComposer.GetScrollbar("scrollbar").SetHeights((float)layout.ScrollHeight, (float)_scrollContainer.Bounds.fixedHeight);
        SetDeferredTextAreaValues();
        RecalculateScrolledBounds(_scrollContainer.Bounds);

        _headshotCallbacks?.RequestHeadshotForView?.Invoke(this, _view);
    }

    private bool ShouldShowHeadshotSection()
    {
        return _headshotCallbacks != null && _view?.Success == true;
    }

    private string GetDialogTitle()
    {
        return string.IsNullOrWhiteSpace(_view?.Title) ? Lang.Get("thebasics:charsheet-gui-title") : _view.Title;
    }

    private IList<CharacterSheetFieldViewMessage> GetViewFields()
    {
        return _view?.Fields ?? Array.Empty<CharacterSheetFieldViewMessage>();
    }

    private static List<int> GetHoistedIndices(bool showHeadshotSection, IList<CharacterSheetFieldViewMessage> allFields)
    {
        return showHeadshotSection ? CollectHoistedFieldIndices(allFields) : new List<int>();
    }

    private DialogLayout CreateDialogLayout(bool showHeadshotSection, int hoistedFieldCount, double contentHeight)
    {
        const double buttonGap = 10;
        const double buttonHeight = 30;
        const double bottomPadding = 6;

        var scrollHeight = Math.Min(MaxScrollHeight, Math.Max(100, contentHeight));
        var contentTop = GuiStyle.TitleBarHeight + 10;
        var headshotSectionHeight = showHeadshotSection ? ComputeHeadshotSectionHeight(hoistedFieldCount) : 0;
        var scrollTop = contentTop + headshotSectionHeight;
        var scrollClipBounds = ElementBounds.Fixed(0, scrollTop, DialogWidth - 36, scrollHeight);
        var unifiedInsetTop = showHeadshotSection ? contentTop : scrollTop;
        var unifiedInsetHeight = (scrollTop + scrollHeight) - unifiedInsetTop;

        return new DialogLayout
        {
            ScrollHeight = scrollHeight,
            HeadshotTop = contentTop,
            ButtonY = scrollClipBounds.fixedY + scrollHeight + buttonGap,
            ButtonHeight = buttonHeight,
            BodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, scrollTop + scrollHeight + buttonGap + buttonHeight + bottomPadding).WithFixedPadding(GuiStyle.ElementToDialogPadding),
            ScrollClipBounds = scrollClipBounds,
            ScrollInsetBounds = ElementBounds.Fixed(0, unifiedInsetTop, DialogWidth - 36, unifiedInsetHeight).FixedGrow(3).WithFixedOffset(-3, -3),
            ScrollContentBounds = ElementBounds.Fixed(0, 0, DialogWidth - 48, contentHeight),
            Row = ElementBounds.Fixed(0, 8, DialogWidth - 48, 24),
            DialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle)
        };
    }

    private SafeGuiElementContainer CreateScrollContainer(ElementBounds scrollContentBounds, IList<(int Index, CharacterSheetFieldViewMessage Field)> scrollFields)
    {
        return new SafeGuiElementContainer(capi, scrollContentBounds)
        {
            Tabbable = scrollFields.Any(f => f.Field.CanEdit),
            unscaledCellSpacing = 0
        };
    }

    private void ResetCompositionState()
    {
        SingleComposer?.Dispose();
        _textInputs.Clear();
        _textAreas.Clear();
        _textAreaInitialValues.Clear();
        _dropDowns.Clear();
        _scrollContainer = null;
        _headshotElement?.Dispose();
        _headshotElement = null;
    }

    private static List<(int Index, CharacterSheetFieldViewMessage Field)> CollectScrollFields(IList<CharacterSheetFieldViewMessage> allFields, HashSet<int> hoistedSet)
    {
        var scrollFields = new List<(int Index, CharacterSheetFieldViewMessage Field)>();
        for (var i = 0; i < allFields.Count; i++)
        {
            if (!hoistedSet.Contains(i))
            {
                scrollFields.Add((i, allFields[i]));
            }
        }

        return scrollFields;
    }

    private void AddScrollContent(ElementBounds row, IList<(int Index, CharacterSheetFieldViewMessage Field)> scrollFields)
    {
        if (_view?.Success == false)
        {
            AddRichtext(_scrollContainer, VtmlUtils.EscapeVtml(_view.Message), row.WithFixedHeight(56));
            return;
        }

        var nextRow = AddFields(_scrollContainer, row, scrollFields);
        if (!string.IsNullOrWhiteSpace(_view?.Message))
        {
            AddRichtext(_scrollContainer, VtmlUtils.EscapeVtml(_view.Message), nextRow.WithFixedHeight(36));
        }
    }

    private void AddSaveButtonIfEditable(GuiComposer composer, double buttonY, double buttonHeight)
    {
        if (_view?.CanEdit == true && _view.Success)
        {
            composer.AddSmallButton(Lang.Get("Save"), OnSave, ElementBounds.Fixed(DialogWidth - 142, buttonY, 120, buttonHeight), EnumButtonStyle.Normal, "saveButton");
        }
    }

    /// <summary>
    /// Returns indices of fields whose <c>LayoutSection</c> places them next to the headshot, in
    /// the original config order. Empty when no fields are flagged for HeaderSide.
    /// </summary>
    private static List<int> CollectHoistedFieldIndices(IList<CharacterSheetFieldViewMessage> fields)
    {
        var result = new List<int>();
        for (var i = 0; i < fields.Count; i++)
        {
            if (CharacterSheetLayoutSections.Normalize(fields[i].LayoutSection) == CharacterSheetLayoutSections.HeaderSide)
            {
                result.Add(i);
            }
        }
        return result;
    }

    /// <summary>
    /// The section sizes to fit whichever column is taller: the headshot+buttons stack on the left,
    /// or the 2-column field grid on the right.
    /// </summary>
    private double ComputeHeadshotSectionHeight(int hoistedFieldCount)
    {
        var leftColHeight = HeadshotSize;
        var canEdit = _view?.CanEditHeadshot == true;
        var allowUrl = canEdit && _headshotCallbacks?.UrlUploadAllowed == true;
        if (canEdit)
        {
            // Buttons block: optional Set URL + always-on Clear, stacked under the PFP.
            var buttonsBlock = HeadshotButtonGap + HeadshotButtonHeight + (allowUrl ? HeadshotButtonGap + HeadshotButtonHeight : 0);
            leftColHeight += buttonsBlock;
        }

        var rows = (hoistedFieldCount + HeaderGridCols - 1) / HeaderGridCols;
        var rightColHeight = rows == 0 ? 0 : rows * HeaderGridCellHeight + Math.Max(0, rows - 1) * HeaderGridRowGap;
        return Math.Max(leftColHeight, rightColHeight) + TopSectionBottomPadding;
    }

    private void ComposeHeadshotSection(GuiComposer composer, double top, List<int> hoistedIndices)
    {
        var canEdit = _view?.CanEditHeadshot == true;
        var allowUrl = canEdit && _headshotCallbacks?.UrlUploadAllowed == true;

        AddHeadshotControls(composer, top, canEdit, allowUrl);
        AddHoistedFieldGrid(composer, top, hoistedIndices);
    }

    private void AddHoistedFieldGrid(GuiComposer composer, double top, List<int> hoistedIndices)
    {
        var rightX = HeadshotSize + 16;
        var rightWidth = DialogWidth - 48 - rightX;
        if (rightWidth <= 0 || hoistedIndices.Count == 0)
        {
            return;
        }

        var allFields = _view?.Fields ?? Array.Empty<CharacterSheetFieldViewMessage>();
        var labelFont = CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold);
        var cellWidth = (rightWidth - HeaderGridColGap * (HeaderGridCols - 1)) / HeaderGridCols;
        for (var slot = 0; slot < hoistedIndices.Count; slot++)
        {
            var fieldIndex = hoistedIndices[slot];
            if (fieldIndex < 0 || fieldIndex >= allFields.Count)
            {
                continue;
            }

            var field = allFields[fieldIndex];
            var col = slot % HeaderGridCols;
            var row = slot / HeaderGridCols;
            var cellX = rightX + col * (cellWidth + HeaderGridColGap);
            var cellY = top + row * (HeaderGridCellHeight + HeaderGridRowGap);

            AddHoistedField(composer, labelFont, field, fieldIndex, cellX, cellY, cellWidth);
        }
    }

    private void AddHoistedField(GuiComposer composer, CairoFont labelFont, CharacterSheetFieldViewMessage field, int fieldIndex, double cellX, double cellY, double cellWidth)
    {
        var label = field.Optional ? field.Label : field.Label + " *";
        composer.AddStaticText(label, labelFont, ElementBounds.Fixed(cellX, cellY, cellWidth, HeaderGridLabelHeight));

        var inputBounds = ElementBounds.Fixed(cellX, cellY + HeaderGridLabelHeight, cellWidth, HeaderGridInputHeight);
        if (field.CanEdit)
        {
            AddEditableField(el => composer.AddInteractiveElement(el, $"hoistedField-{fieldIndex}"), inputBounds, field, fieldIndex);
            return;
        }

        var value = string.IsNullOrWhiteSpace(field.Value) ? Lang.Get("thebasics:charsheet-unset") : field.Value;
        composer.AddRichtext(VtmlUtils.EscapeVtml(value), CairoFont.WhiteSmallText(), inputBounds);
    }

    private void AddHeadshotControls(GuiComposer composer, double top, bool canEdit, bool allowUrl)
    {
        var headshotBounds = ElementBounds.Fixed(0, top, HeadshotSize, HeadshotSize);
        _headshotElement = new HeadshotElement(capi, headshotBounds);
        composer.AddInteractiveElement(_headshotElement, "headshot");

        if (!canEdit)
        {
            return;
        }

        var buttonsTop = top + HeadshotSize + HeadshotButtonGap;
        AddHeadshotUrlButton(composer, buttonsTop, allowUrl);

        var clearTop = buttonsTop + (allowUrl ? HeadshotButtonHeight + HeadshotButtonGap : 0);
        composer.AddSmallButton(
            Lang.Get("thebasics:headshot-button-clear"),
            OnHeadshotClearClicked,
            ElementBounds.Fixed(0, clearTop, HeadshotSize, HeadshotButtonHeight),
            EnumButtonStyle.Small,
            "headshotClearButton");
    }

    private void AddHeadshotUrlButton(GuiComposer composer, double buttonsTop, bool allowUrl)
    {
        if (!allowUrl)
        {
            return;
        }

        composer.AddSmallButton(
            Lang.Get("thebasics:headshot-button-set-url"),
            OnHeadshotSetUrlClicked,
            ElementBounds.Fixed(0, buttonsTop, HeadshotSize, HeadshotButtonHeight),
            EnumButtonStyle.Small,
            "headshotSetUrlButton");
    }

    private bool OnHeadshotSetUrlClicked()
    {
        _urlPrompt?.TryClose();
        _urlPrompt = new HeadshotUrlPromptDialog(capi, OnUrlPromptSubmitted);
        _urlPrompt.TryOpen();
        return true;
    }

    private void OnUrlPromptSubmitted(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        SetHeadshotStatus(Lang.Get("thebasics:headshot-status-uploading"));
        _headshotCallbacks?.UploadFromUrl?.Invoke(_view?.TargetPlayerUid, url);
    }

    private bool OnHeadshotClearClicked()
    {
        _headshotCallbacks?.ClearHeadshot?.Invoke(_view?.TargetPlayerUid);
        SetHeadshotStatus(Lang.Get("thebasics:headshot-status-clearing"));
        return true;
    }

    public override void OnGuiClosed()
    {
        base.OnGuiClosed();
        _urlPrompt?.TryClose();
        _urlPrompt = null;
        var unsavedCloseConfirm = _unsavedCloseConfirm;
        _unsavedCloseConfirm = null;
        unsavedCloseConfirm?.TryClose();
        if (!_closing)
        {
            _closing = true;
            _onClosed?.Invoke();
        }
    }

    private ElementBounds AddFields(GuiElementContainer container, ElementBounds row, IList<(int Index, CharacterSheetFieldViewMessage Field)> fields)
    {
        var rowWidth = row.fixedWidth;
        const double HalfGap = 12;
        var halfWidth = Math.Max(60, (rowWidth - HalfGap) / 2);

        var i = 0;
        while (i < fields.Count)
        {
            var current = fields[i];
            var currentIsHalf = IsHalfWidth(current.Field);
            var pairable = currentIsHalf
                && i + 1 < fields.Count
                && IsHalfWidth(fields[i + 1].Field);

            if (pairable)
            {
                var next = fields[i + 1];
                AddFieldCell(container, row.fixedY, current.Index, current.Field, x: 0, width: halfWidth);
                AddFieldCell(container, row.fixedY, next.Index, next.Field, x: halfWidth + HalfGap, width: halfWidth);
                var rowHeight = Math.Max(GetCellHeight(current.Field), GetCellHeight(next.Field));
                row = ElementBounds.Fixed(0, row.fixedY + rowHeight + 8, rowWidth, row.fixedHeight);
                i += 2;
            }
            else
            {
                var width = currentIsHalf ? halfWidth : rowWidth;
                AddFieldCell(container, row.fixedY, current.Index, current.Field, x: 0, width: width);
                row = ElementBounds.Fixed(0, row.fixedY + GetCellHeight(current.Field) + 8, rowWidth, row.fixedHeight);
                i += 1;
            }
        }

        if (fields.Count == 0)
        {
            AddRichtext(container, VtmlUtils.EscapeVtml(_view?.Message ?? Lang.Get("thebasics:charsheet-empty")), row.FlatCopy().WithFixedHeight(28));
            row = row.BelowCopy(0, 28);
        }

        return row;
    }

    /// <summary>
    /// Renders a single field (label + input/value) inside <paramref name="container"/> at the given
    /// <paramref name="x"/> offset and <paramref name="width"/>, with the label at <paramref name="topY"/>.
    /// The caller decides how to advance the row position so paired half-width fields can share a row.
    /// </summary>
    private void AddFieldCell(
        GuiElementContainer container,
        double topY,
        int fieldIndex,
        CharacterSheetFieldViewMessage field,
        double x,
        double width)
    {
        var label = field.Optional ? field.Label : field.Label + " *";
        var labelBounds = ElementBounds.Fixed(x, topY, width, 24);
        container.Add(new GuiElementStaticText(capi, label, EnumTextOrientation.Left, labelBounds, CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold)));

        var inputY = topY + 22;
        if (field.CanEdit)
        {
            var inputBounds = ElementBounds.Fixed(x, inputY, width, GetEditControlHeight(field));
            AddEditableField(el => container.Add(el), inputBounds, field, fieldIndex);
        }
        else
        {
            var value = string.IsNullOrWhiteSpace(field.Value) ? Lang.Get("thebasics:charsheet-unset") : field.Value;
            var valueBounds = ElementBounds.Fixed(x, inputY, width, GetReadOnlyHeight(value));
            AddRichtext(container, VtmlUtils.EscapeVtml(value), valueBounds);
        }
    }

    private static bool IsHalfWidth(CharacterSheetFieldViewMessage field)
    {
        return CharacterSheetFieldWidths.Normalize(field.Width) == CharacterSheetFieldWidths.Half;
    }

    private static double GetCellHeight(CharacterSheetFieldViewMessage field)
    {
        // Label height + input/value height. Used to align paired half-width cells vertically.
        var inputH = field.CanEdit ? GetEditControlHeight(field) : GetReadOnlyHeight(field.Value);
        return 22 + inputH;
    }

    /// <summary>
    /// Builds the editable input element for a field and registers it under its <paramref name="index"/>
    /// in the appropriate dictionary so OnSave can find it. The caller passes in the "add to parent"
    /// step — this lets the same builder serve both the scrollable container and the headshot row.
    /// </summary>
    private void AddEditableField(Action<GuiElement> add, ElementBounds inputBounds, CharacterSheetFieldViewMessage field, int index)
    {
        if (field.Type == CharacterSheetFieldTypes.Option && field.Options?.Count > 0)
        {
            AddOptionField(add, inputBounds, field, index);
            return;
        }

        if (field.Type == CharacterSheetFieldTypes.LongString)
        {
            AddLongStringField(add, inputBounds, field, index);
            return;
        }

        AddTextField(add, inputBounds, field, index);
    }

    private void AddOptionField(Action<GuiElement> add, ElementBounds inputBounds, CharacterSheetFieldViewMessage field, int index)
    {
        var values = field.Optional ? new[] { string.Empty }.Concat(field.Options).ToArray() : field.Options.ToArray();
        var displayValues = field.Optional ? new[] { Lang.Get("thebasics:charsheet-unset") }.Concat(field.Options).ToArray() : values;
        var selectedIndex = string.IsNullOrWhiteSpace(field.Value) && field.Optional
            ? 0
            : Math.Max(0, Array.FindIndex(values, option => option.Equals(field.Value, StringComparison.OrdinalIgnoreCase)));
        var dropDown = new GuiElementDropDown(capi, values, displayValues, selectedIndex, null, inputBounds, CairoFont.WhiteSmallText(), multiSelect: false);
        _dropDowns[index] = dropDown;
        add(dropDown);
    }

    private void AddLongStringField(Action<GuiElement> add, ElementBounds inputBounds, CharacterSheetFieldViewMessage field, int index)
    {
        var textArea = new ScrollClippedTextArea(capi, inputBounds, null, CairoFont.TextInput())
        {
            Autoheight = false
        };
        textArea.SetMaxLines(GetEditorRows(field.EditorRows));
        if (field.MaxLength > 0)
        {
            textArea.SetMaxLength(field.MaxLength);
        }

        _textAreas[index] = textArea;
        _textAreaInitialValues[index] = field.Value ?? string.Empty;
        add(textArea);
    }

    private void AddTextField(Action<GuiElement> add, ElementBounds inputBounds, CharacterSheetFieldViewMessage field, int index)
    {
        var textInput = new ScrollClippedTextInput(capi, inputBounds, null, CairoFont.TextInput());
        textInput.SetValue(field.Value ?? string.Empty);
        if (field.MaxLength > 0)
        {
            textInput.SetMaxLength(field.MaxLength);
        }

        _textInputs[index] = textInput;
        add(textInput);
    }

    private void SetDeferredTextAreaValues()
    {
        foreach (var (index, value) in _textAreaInitialValues)
        {
            if (_textAreas.TryGetValue(index, out var textArea))
            {
                textArea.SetValue(value);
            }
        }
    }

    private void AddRichtext(GuiElementContainer container, string vtmlCode, ElementBounds bounds)
    {
        container.Add(new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, vtmlCode, CairoFont.WhiteSmallText()), bounds));
    }

    private bool OnSave()
    {
        var request = new CharacterSheetSaveRequest
        {
            TargetPlayerUid = _view.TargetPlayerUid,
            IsAdminAction = _view.IsAdminView
        };

        for (var index = 0; index < _view.Fields.Count; index++)
        {
            var field = _view.Fields[index];
            if (!field.CanEdit)
            {
                continue;
            }

            request.Fields.Add(new CharacterSheetFieldValueMessage
            {
                FieldId = field.FieldId,
                Value = GetFieldInputValue(index, field)
            });
        }

        _onSave(request);
        return true;
    }

    private bool OnGuide()
    {
        return HandbookGuide.Open(capi, HandbookGuide.CharacterSheetPage);
    }

    private bool HasUnsavedChanges()
    {
        if (_view == null || !_view.CanEdit || !_view.Success || _view.Fields == null)
        {
            return false;
        }

        for (var index = 0; index < _view.Fields.Count; index++)
        {
            var field = _view.Fields[index];
            if (!field.CanEdit)
            {
                continue;
            }

            if (!string.Equals(GetFieldInputValue(index, field), field.Value ?? string.Empty, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ConfirmCloseWithUnsavedChanges()
    {
        if (_unsavedCloseConfirm?.IsOpened() == true)
        {
            return;
        }

        _unsavedCloseConfirm = new GuiDialogConfirm(capi, Lang.Get("thebasics:charsheet-close-unsaved-confirm"), ok =>
        {
            _unsavedCloseConfirm = null;
            if (!ok)
            {
                _afterDiscardConfirmed = null;
                return;
            }

            var afterClose = _afterDiscardConfirmed;
            _afterDiscardConfirmed = null;
            _forceClose = true;
            try
            {
                if (TryClose())
                {
                    afterClose?.Invoke();
                }
            }
            finally
            {
                _forceClose = false;
            }
        });
        _unsavedCloseConfirm.TryOpen();
    }

    private string GetFieldInputValue(int index, CharacterSheetFieldViewMessage field)
    {
        if (field.Type == CharacterSheetFieldTypes.Option && field.Options?.Count > 0)
        {
            return _dropDowns.TryGetValue(index, out var dropDown) ? dropDown.SelectedValue : string.Empty;
        }

        if (field.Type == CharacterSheetFieldTypes.LongString)
        {
            return _textAreas.TryGetValue(index, out var textArea) ? textArea.GetText() : string.Empty;
        }

        return _textInputs.TryGetValue(index, out var textInput) ? textInput.GetText() : string.Empty;
    }

    private void OnNewScrollbarValue(float value)
    {
        if (_scrollContainer == null)
        {
            return;
        }

        _scrollContainer.Bounds.fixedY = -Math.Max(0, value);
        RecalculateScrolledBounds(_scrollContainer.Bounds);
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

    private static void RecalculateScrolledBounds(ElementBounds bounds)
    {
        bounds.MarkDirtyRecursive();
        bounds.CalcWorldBounds();
    }

    private static double CalculateContentHeight(IList<CharacterSheetFieldViewMessage> fields, CharacterSheetViewMessage view)
    {
        var height = 12.0;
        if (view?.Success == false)
        {
            return 56;
        }

        // Mirror AddFields' pair-when-adjacent rule so the scroll content sizes match the rendered layout.
        var i = 0;
        while (i < fields.Count)
        {
            var current = fields[i];
            var pairable = IsHalfWidth(current)
                && i + 1 < fields.Count
                && IsHalfWidth(fields[i + 1]);

            if (pairable)
            {
                height += GetPairedFieldHeight(current, fields[i + 1]);
                i += 2;
            }
            else
            {
                height += GetSingleFieldHeight(current);
                i += 1;
            }
        }

        if (fields.Count == 0 || !string.IsNullOrWhiteSpace(view?.Message))
        {
            height += 36;
        }

        return Math.Max(80, height + ScrollContentBottomPadding);
    }

    private static double GetPairedFieldHeight(CharacterSheetFieldViewMessage first, CharacterSheetFieldViewMessage second)
    {
        return Math.Max(GetCellHeight(first), GetCellHeight(second)) + 8;
    }

    private static double GetSingleFieldHeight(CharacterSheetFieldViewMessage field)
    {
        return field.CanEdit ? GetEditableFieldHeight(field) : 44 + GetReadOnlyHeight(field.Value);
    }

    private static double GetEditableFieldHeight(CharacterSheetFieldViewMessage field)
    {
        return 30 + GetEditControlHeight(field) + 8;
    }

    private static double GetEditControlHeight(CharacterSheetFieldViewMessage field)
    {
        return field.Type == CharacterSheetFieldTypes.LongString ? GetLongTextInputHeight(field.EditorRows) : 30;
    }

    private static double GetLongTextInputHeight(int editorRows)
    {
        var rows = GetEditorRows(editorRows);
        return LongTextBaseHeight + rows * LongTextRowHeight;
    }

    private static int GetEditorRows(int editorRows)
    {
        return editorRows > 0 ? Math.Clamp(editorRows, 2, 16) : DefaultLongTextRows;
    }

    private static double GetReadOnlyHeight(string value)
    {
        var lineCount = Math.Max(1, (value ?? string.Empty).Split('\n').Length);
        var wrappedLines = Math.Max(0, (value ?? string.Empty).Length / 72);
        return Math.Min(120, 22 + (lineCount + wrappedLines - 1) * 18);
    }

    private sealed class DialogLayout
    {
        public double ScrollHeight { get; init; }
        public double HeadshotTop { get; init; }
        public double ButtonY { get; init; }
        public double ButtonHeight { get; init; }
        public ElementBounds BodyBounds { get; init; }
        public ElementBounds ScrollClipBounds { get; init; }
        public ElementBounds ScrollInsetBounds { get; init; }
        public ElementBounds ScrollContentBounds { get; init; }
        public ElementBounds Row { get; init; }
        public ElementBounds DialogBounds { get; init; }
    }

    private sealed class SafeGuiElementContainer : GuiElementContainer
    {
        public SafeGuiElementContainer(ICoreClientAPI capi, ElementBounds bounds) : base(capi, bounds)
        {
        }

        public override void OnFocusGained()
        {
            if (FirstTabbableElement == null)
            {
                return;
            }

            base.OnFocusGained();
        }

    }

    private sealed class ScrollClippedTextInput : GuiElementTextInput
    {
        public ScrollClippedTextInput(ICoreClientAPI capi, ElementBounds bounds, Action<string> onTextChanged, CairoFont font)
            : base(capi, bounds, onTextChanged, font)
        {
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            if (!IsFullyInsideClip())
            {
                return;
            }

            base.RenderInteractiveElements(deltaTime);
        }

        private bool IsFullyInsideClip()
        {
            if (InsideClipBounds == null)
            {
                return true;
            }

            var clipTop = InsideClipBounds.renderY;
            var clipBottom = InsideClipBounds.renderY + InsideClipBounds.OuterHeight;
            var elementTop = Bounds.renderY;
            var elementBottom = Bounds.renderY + Bounds.OuterHeight;

            var tolerance = scaled(TextInputClipTolerance);
            return elementTop >= clipTop - tolerance && elementBottom <= clipBottom + tolerance;
        }
    }

    private sealed class ScrollClippedTextArea : GuiElementTextArea
    {
        public ScrollClippedTextArea(ICoreClientAPI capi, ElementBounds bounds, Action<string> onTextChanged, CairoFont font)
            : base(capi, bounds, onTextChanged, font)
        {
        }

        public override void RenderInteractiveElements(float deltaTime)
        {
            var pushedParentClip = false;
            if (InsideClipBounds != null)
            {
                api.Render.PushScissor(InsideClipBounds, stacking: true);
                pushedParentClip = true;
            }

            api.Render.PushScissor(Bounds, stacking: true);
            base.RenderInteractiveElements(deltaTime);
            api.Render.PopScissor();

            if (pushedParentClip)
            {
                api.Render.PopScissor();
            }
        }
    }
}
