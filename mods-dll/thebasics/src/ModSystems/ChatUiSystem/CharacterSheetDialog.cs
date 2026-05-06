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
    private readonly Action<CharacterSheetSaveRequest> _onSave;
    private readonly Dictionary<int, GuiElementTextInput> _textInputs = new();
    private readonly Dictionary<int, GuiElementTextArea> _textAreas = new();
    private readonly Dictionary<int, string> _textAreaInitialValues = new();
    private readonly Dictionary<int, GuiElementDropDown> _dropDowns = new();
    private GuiElementContainer _scrollContainer;
    private CharacterSheetViewMessage _view;

    public CharacterSheetDialog(ICoreClientAPI capi, CharacterSheetViewMessage view, Action<CharacterSheetSaveRequest> onSave) : base(capi)
    {
        _view = view;
        _onSave = onSave;
        ComposeDialog();
    }

    public override string ToggleKeyCombinationCode => "thebasicscharsheet";

    public override bool PrefersUngrabbedMouse => true;

    public override bool DisableMouseGrab => true;

    public override double DrawOrder => 0.25;

    public void SetView(CharacterSheetViewMessage view)
    {
        _view = view;
        ComposeDialog();
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();
        _textInputs.Clear();
        _textAreas.Clear();
        _textAreaInitialValues.Clear();
        _dropDowns.Clear();
        _scrollContainer = null;

        var title = string.IsNullOrWhiteSpace(_view?.Title) ? Lang.Get("thebasics:charsheet-gui-title") : _view.Title;
        var fields = _view?.Fields ?? Array.Empty<CharacterSheetFieldViewMessage>();
        var contentHeight = CalculateContentHeight(fields, _view);
        var scrollHeight = Math.Min(MaxScrollHeight, Math.Max(100, contentHeight));
        var contentTop = GuiStyle.TitleBarHeight + 10;
        var buttonGap = 10;
        var buttonHeight = 30;
        var bottomPadding = 6;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, contentTop + scrollHeight + buttonGap + buttonHeight + bottomPadding).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var scrollClipBounds = ElementBounds.Fixed(0, contentTop, DialogWidth - 36, scrollHeight);
        var scrollInsetBounds = scrollClipBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3);
        var scrollContentBounds = ElementBounds.Fixed(0, 0, DialogWidth - 48, contentHeight);
        var row = ElementBounds.Fixed(0, 8, DialogWidth - 48, 24);
        var buttonY = scrollClipBounds.fixedY + scrollHeight + buttonGap;
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        _scrollContainer = new SafeGuiElementContainer(capi, scrollContentBounds)
        {
            Tabbable = fields.Any(field => field.CanEdit),
            unscaledCellSpacing = 0
        };

        var composer = capi.Gui.CreateCompo("thebasics-character-sheet", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(title, OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(scrollInsetBounds, 3)
            .BeginClip(scrollClipBounds)
            .AddInteractiveElement(_scrollContainer, "scrollBody");

        if (_view?.Success == false)
        {
            AddRichtext(_scrollContainer, VtmlUtils.EscapeVtml(_view.Message), row.WithFixedHeight(56));
        }
        else
        {
            row = AddFields(_scrollContainer, row, fields);
            if (!string.IsNullOrWhiteSpace(_view?.Message))
            {
                AddRichtext(_scrollContainer, VtmlUtils.EscapeVtml(_view.Message), row.WithFixedHeight(36));
            }
        }

        composer.EndClip()
            .AddVerticalScrollbar(OnNewScrollbarValue, ElementStdBounds.VerticalScrollbar(scrollClipBounds), "scrollbar")
            .AddSmallButton(Lang.Get("Cancel"), OnCancel, ElementBounds.Fixed(0, buttonY, 120, buttonHeight));

        if (_view?.CanEdit == true && _view.Success)
        {
            composer.AddSmallButton(Lang.Get("Save"), OnSave, ElementBounds.Fixed(DialogWidth - 142, buttonY, 120, buttonHeight), EnumButtonStyle.Normal, "saveButton");
        }

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
        SingleComposer.GetScrollbar("scrollbar").SetHeights((float)scrollHeight, (float)_scrollContainer.Bounds.fixedHeight);
        SetDeferredTextAreaValues();
        RecalculateScrolledBounds(_scrollContainer.Bounds);
    }

    private ElementBounds AddFields(GuiElementContainer container, ElementBounds row, IEnumerable<CharacterSheetFieldViewMessage> fields)
    {
        var index = 0;
        foreach (var field in fields)
        {
            var label = field.Optional ? field.Label : field.Label + " *";
            var labelBounds = row.FlatCopy().WithFixedHeight(24);
            container.Add(new GuiElementStaticText(capi, label, EnumTextOrientation.Left, labelBounds, CairoFont.WhiteSmallText().WithWeight(Cairo.FontWeight.Bold)));
            row = labelBounds.BelowCopy(0, -2);

            if (field.CanEdit)
            {
                row = AddEditableField(container, row, field, index);
            }
            else
            {
                var value = string.IsNullOrWhiteSpace(field.Value) ? Lang.Get("thebasics:charsheet-unset") : field.Value;
                var valueBounds = row.FlatCopy().WithFixedHeight(GetReadOnlyHeight(value));
                AddRichtext(container, VtmlUtils.EscapeVtml(value), valueBounds);
                row = row.BelowCopy(0, 10);
            }

            index++;
        }

        if (index == 0)
        {
            AddRichtext(container, VtmlUtils.EscapeVtml(_view?.Message ?? Lang.Get("thebasics:charsheet-empty")), row.FlatCopy().WithFixedHeight(28));
            row = row.BelowCopy(0, 28);
        }

        return row;
    }

    private ElementBounds AddEditableField(GuiElementContainer container, ElementBounds row, CharacterSheetFieldViewMessage field, int index)
    {
        var inputBounds = row.FlatCopy().WithFixedHeight(GetEditControlHeight(field));
        if (field.Type == CharacterSheetFieldTypes.Option && field.Options?.Count > 0)
        {
            var options = field.Options.ToArray();
            var selectedIndex = Math.Max(0, Array.FindIndex(options, option => option.Equals(field.Value, StringComparison.OrdinalIgnoreCase)));
            var dropDown = new GuiElementDropDown(capi, options, options, selectedIndex, null, inputBounds, CairoFont.WhiteSmallText(), multiSelect: false);
            _dropDowns[index] = dropDown;
            container.Add(dropDown);
        }
        else if (field.Type == CharacterSheetFieldTypes.LongString)
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
            container.Add(textArea);
        }
        else
        {
            var textInput = new ScrollClippedTextInput(capi, inputBounds, null, CairoFont.TextInput());
            textInput.SetValue(field.Value ?? string.Empty);
            if (field.MaxLength > 0)
            {
                textInput.SetMaxLength(field.MaxLength);
            }

            _textInputs[index] = textInput;
            container.Add(textInput);
        }

        return inputBounds.BelowCopy(0, 8);
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

    private string GetFieldInputValue(int index, CharacterSheetFieldViewMessage field)
    {
        if (field.Type == CharacterSheetFieldTypes.Option && field.Options?.Count > 0)
        {
            return _dropDowns[index].SelectedValue;
        }

        if (field.Type == CharacterSheetFieldTypes.LongString)
        {
            return _textAreas[index].GetText();
        }

        return _textInputs[index].GetText();
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

        foreach (var field in fields)
        {
            height += field.CanEdit ? GetEditableFieldHeight(field) : 44 + GetReadOnlyHeight(field.Value);
        }

        if (fields.Count == 0 || !string.IsNullOrWhiteSpace(view?.Message))
        {
            height += 36;
        }

        return Math.Max(80, height + ScrollContentBottomPadding);
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
