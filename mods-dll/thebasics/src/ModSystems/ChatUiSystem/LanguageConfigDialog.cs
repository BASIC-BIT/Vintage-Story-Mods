using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

public class LanguageConfigDialog : GuiDialog
{
    private const double DialogWidth = 760;
    private const double MaxScrollHeight = 560;
    private const double ScrollContentBottomPadding = 36;
    private const double TextInputClipTolerance = 6;
    private readonly Action<List<LanguageConfigEntryMessage>> _onSave;
    private readonly Action _onReload;
    private readonly Dictionary<string, GuiElementTextInput> _textInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, GuiElementDropDown> _dropDowns = new(StringComparer.OrdinalIgnoreCase);
    private GuiElementContainer _scrollContainer;
    private GuiElementDropDown _selectedLanguageDropDown;
    private List<LanguageConfigEntryMessage> _languages;
    private string _message;
    private bool _success;

    public LanguageConfigDialog(ICoreClientAPI capi, List<LanguageConfigEntryMessage> languages, string message, bool success, Action<List<LanguageConfigEntryMessage>> onSave, Action onReload) : base(capi)
    {
        _languages = EnsureDraft(languages);
        _message = message;
        _success = success;
        _onSave = onSave;
        _onReload = onReload;
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
        ComposeDialog();
    }

    private void ComposeDialog()
    {
        SingleComposer?.Dispose();
        _textInputs.Clear();
        _dropDowns.Clear();
        _selectedLanguageDropDown = null;
        _scrollContainer = null;

        var contentHeight = CalculateContentHeight(_languages, _message);
        var scrollHeight = Math.Min(MaxScrollHeight, Math.Max(180, contentHeight));
        var contentTop = GuiStyle.TitleBarHeight + 10;
        var buttonGap = 10;
        var buttonHeight = 30;
        var buttonRowGap = 6;
        var bodyHeight = contentTop + scrollHeight + buttonGap + buttonHeight * 2 + buttonRowGap + 8;
        var bodyBounds = ElementBounds.Fixed(0, 0, DialogWidth - 10, bodyHeight).WithFixedPadding(GuiStyle.ElementToDialogPadding);
        var scrollClipBounds = ElementBounds.Fixed(0, contentTop, DialogWidth - 36, scrollHeight);
        var scrollInsetBounds = scrollClipBounds.FlatCopy().FixedGrow(3).WithFixedOffset(-3, -3);
        var scrollContentBounds = ElementBounds.Fixed(0, 0, DialogWidth - 48, contentHeight);
        var row = ElementBounds.Fixed(0, 8, DialogWidth - 48, 24);
        var firstButtonY = scrollClipBounds.fixedY + scrollHeight + buttonGap;
        var secondButtonY = firstButtonY + buttonHeight + buttonRowGap;
        var dialogBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        _scrollContainer = new SafeGuiElementContainer(capi, scrollContentBounds)
        {
            Tabbable = true,
            unscaledCellSpacing = 0
        };

        var composer = capi.Gui.CreateCompo("thebasics-language-config", dialogBounds)
            .AddShadedDialogBG(bodyBounds)
            .AddDialogTitleBar(Lang.Get("thebasics:language-config-title"), OnTitleBarCloseClicked)
            .BeginChildElements(bodyBounds)
            .AddInset(scrollInsetBounds, 3)
            .BeginClip(scrollClipBounds)
            .AddInteractiveElement(_scrollContainer, "scrollBody");

        row = AddIntro(row);
        AddLanguageFields(row);

        composer.EndClip()
            .AddVerticalScrollbar(OnNewScrollbarValue, ElementStdBounds.VerticalScrollbar(scrollClipBounds), "scrollbar")
            .AddSmallButton(Lang.Get("thebasics:language-config-add"), OnAddLanguage, ElementBounds.Fixed(0, firstButtonY, 106, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:language-config-delete"), OnDeleteSelectedLanguage, ElementBounds.Fixed(116, firstButtonY, 120, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:language-config-up"), OnMoveSelectedLanguageUp, ElementBounds.Fixed(246, firstButtonY, 90, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:language-config-down"), OnMoveSelectedLanguageDown, ElementBounds.Fixed(346, firstButtonY, 90, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:language-config-reload"), OnReload, ElementBounds.Fixed(446, firstButtonY, 110, buttonHeight))
            .AddSmallButton(Lang.Get("thebasics:language-config-save"), OnSave, ElementBounds.Fixed(DialogWidth - 260, secondButtonY, 120, buttonHeight), EnumButtonStyle.Normal, "saveButton")
            .AddSmallButton(Lang.Get("thebasics:language-config-close"), OnCancel, ElementBounds.Fixed(DialogWidth - 130, secondButtonY, 110, buttonHeight));

        SingleComposer = composer.EndChildElements().Compose(focusFirstElement: false);
        SingleComposer.GetScrollbar("scrollbar").SetHeights((float)scrollHeight, (float)_scrollContainer.Bounds.fixedHeight);
        RecalculateScrolledBounds(_scrollContainer.Bounds);
    }

    private ElementBounds AddIntro(ElementBounds row)
    {
        AddRichtext(_scrollContainer, VtmlUtils.EscapeVtml(Lang.Get("thebasics:language-config-help")), row.WithFixedHeight(48));
        row = row.BelowCopy(0, 8);

        if (!string.IsNullOrWhiteSpace(_message))
        {
            var message = _success ? _message : Lang.Get("thebasics:language-config-error-prefix", _message);
            AddRichtext(_scrollContainer, VtmlUtils.EscapeVtml(message), row.WithFixedHeight(64));
            row = row.BelowCopy(0, 8);
        }

        var values = Enumerable.Range(0, _languages.Count).Select(index => index.ToString()).ToArray();
        var names = _languages.Select((language, index) => $"{index + 1}. {DisplayName(language)}").ToArray();
        var selectedBounds = row.FlatCopy().WithFixedHeight(30);
        AddLabel(_scrollContainer, Lang.Get("thebasics:language-config-selected"), selectedBounds.WithFixedHeight(20));
        selectedBounds = selectedBounds.BelowCopy(0, 2);
        _selectedLanguageDropDown = new GuiElementDropDown(capi, values, names, 0, null, selectedBounds, CairoFont.WhiteSmallText(), multiSelect: false);
        _scrollContainer.Add(_selectedLanguageDropDown);
        return selectedBounds.BelowCopy(0, 12);
    }

    private ElementBounds AddLanguageFields(ElementBounds row)
    {
        for (var index = 0; index < _languages.Count; index++)
        {
            var language = _languages[index];
            var headerBounds = row.FlatCopy().WithFixedHeight(28);
            AddLabel(_scrollContainer, $"{index + 1}. {DisplayName(language)}", headerBounds, bold: true);
            row = headerBounds.BelowCopy(0, 2);

            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.Name), Lang.Get("thebasics:language-config-name"), language.Name, 64);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.Prefix), Lang.Get("thebasics:language-config-prefix"), language.Prefix, 32);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.Description), Lang.Get("thebasics:language-config-description"), language.Description, 256);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.Color), Lang.Get("thebasics:language-config-color"), language.Color, 16);
            row = AddBoolDropDown(row, index, nameof(LanguageConfigEntryMessage.Default), Lang.Get("thebasics:language-config-default"), language.Default);
            row = AddBoolDropDown(row, index, nameof(LanguageConfigEntryMessage.Hidden), Lang.Get("thebasics:language-config-hidden"), language.Hidden);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.Syllables), Lang.Get("thebasics:language-config-syllables"), language.Syllables, 512);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.GrantedToClasses), Lang.Get("thebasics:language-config-classes"), language.GrantedToClasses, 512);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.GrantedToTraits), Lang.Get("thebasics:language-config-traits"), language.GrantedToTraits, 512);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.GrantedToModels), Lang.Get("thebasics:language-config-models"), language.GrantedToModels, 512);
            row = AddTextInput(row, index, nameof(LanguageConfigEntryMessage.GrantedToModelGroups), Lang.Get("thebasics:language-config-model-groups"), language.GrantedToModelGroups, 512);
            row = row.BelowCopy(0, 10);
        }

        return row;
    }

    private ElementBounds AddTextInput(ElementBounds row, int index, string field, string label, string value, int maxLength)
    {
        AddLabel(_scrollContainer, label, row.WithFixedHeight(20));
        var inputBounds = row.BelowCopy(0, 2).WithFixedHeight(28);
        var input = new ScrollClippedTextInput(capi, inputBounds, null, CairoFont.TextInput());
        input.SetValue(value ?? string.Empty);
        if (maxLength > 0)
        {
            input.SetMaxLength(maxLength);
        }

        _textInputs[Key(index, field)] = input;
        _scrollContainer.Add(input);
        return inputBounds.BelowCopy(0, 7);
    }

    private ElementBounds AddBoolDropDown(ElementBounds row, int index, string field, string label, bool value)
    {
        AddLabel(_scrollContainer, label, row.WithFixedHeight(20));
        var inputBounds = row.BelowCopy(0, 2).WithFixedHeight(28);
        var dropDown = new GuiElementDropDown(capi, ["0", "1"], [Lang.Get("No"), Lang.Get("Yes")], value ? 1 : 0, null, inputBounds, CairoFont.WhiteSmallText(), multiSelect: false);
        _dropDowns[Key(index, field)] = dropDown;
        _scrollContainer.Add(dropDown);
        return inputBounds.BelowCopy(0, 7);
    }

    private void CaptureInputsToDraft()
    {
        for (var index = 0; index < _languages.Count; index++)
        {
            var language = _languages[index];
            language.Name = GetText(index, nameof(LanguageConfigEntryMessage.Name));
            language.Prefix = GetText(index, nameof(LanguageConfigEntryMessage.Prefix));
            language.Description = GetText(index, nameof(LanguageConfigEntryMessage.Description));
            language.Color = GetText(index, nameof(LanguageConfigEntryMessage.Color));
            language.Default = GetBool(index, nameof(LanguageConfigEntryMessage.Default));
            language.Hidden = GetBool(index, nameof(LanguageConfigEntryMessage.Hidden));
            language.Syllables = GetText(index, nameof(LanguageConfigEntryMessage.Syllables));
            language.GrantedToClasses = GetText(index, nameof(LanguageConfigEntryMessage.GrantedToClasses));
            language.GrantedToTraits = GetText(index, nameof(LanguageConfigEntryMessage.GrantedToTraits));
            language.GrantedToModels = GetText(index, nameof(LanguageConfigEntryMessage.GrantedToModels));
            language.GrantedToModelGroups = GetText(index, nameof(LanguageConfigEntryMessage.GrantedToModelGroups));
        }
    }

    private bool OnAddLanguage()
    {
        CaptureInputsToDraft();
        _languages.Add(new LanguageConfigEntryMessage
        {
            Name = "NewLanguage",
            Description = "Describe this language",
            Prefix = "new",
            Color = "#E9DDCE",
            Syllables = "na, la, ra"
        });
        _message = Lang.Get("thebasics:language-config-added-draft");
        _success = true;
        ComposeDialog();
        return true;
    }

    private bool OnDeleteSelectedLanguage()
    {
        CaptureInputsToDraft();
        var index = GetSelectedIndex();
        if (_languages.Count <= 1 || index < 0 || index >= _languages.Count)
        {
            _message = Lang.Get("thebasics:language-config-delete-last");
            _success = false;
            ComposeDialog();
            return true;
        }

        _languages.RemoveAt(index);
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
        CaptureInputsToDraft();
        var index = GetSelectedIndex();
        var nextIndex = index + offset;
        if (index < 0 || nextIndex < 0 || nextIndex >= _languages.Count)
        {
            return true;
        }

        (_languages[index], _languages[nextIndex]) = (_languages[nextIndex], _languages[index]);
        ComposeDialog();
        return true;
    }

    private bool OnSave()
    {
        CaptureInputsToDraft();
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

    private int GetSelectedIndex()
    {
        return int.TryParse(_selectedLanguageDropDown?.SelectedValue, out var index) ? index : 0;
    }

    private string GetText(int index, string field)
    {
        return _textInputs.TryGetValue(Key(index, field), out var input) ? input.GetText() ?? string.Empty : string.Empty;
    }

    private bool GetBool(int index, string field)
    {
        return _dropDowns.TryGetValue(Key(index, field), out var dropDown) && dropDown.SelectedValue == "1";
    }

    private static string Key(int index, string field)
    {
        return index + ":" + field;
    }

    private static string DisplayName(LanguageConfigEntryMessage language)
    {
        return string.IsNullOrWhiteSpace(language?.Name) ? Lang.Get("thebasics:language-config-unnamed") : language.Name;
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

    private void AddLabel(GuiElementContainer container, string text, ElementBounds bounds, bool bold = false)
    {
        var font = CairoFont.WhiteSmallText();
        if (bold)
        {
            font = font.WithWeight(Cairo.FontWeight.Bold);
        }

        container.Add(new GuiElementStaticText(capi, text, EnumTextOrientation.Left, bounds, font));
    }

    private void AddRichtext(GuiElementContainer container, string vtmlCode, ElementBounds bounds)
    {
        container.Add(new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, vtmlCode, CairoFont.WhiteSmallText()), bounds));
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

    private static double CalculateContentHeight(IList<LanguageConfigEntryMessage> languages, string message)
    {
        var height = 130.0;
        if (!string.IsNullOrWhiteSpace(message))
        {
            height += 72;
        }

        height += languages.Count * 630;
        return Math.Max(180, height + ScrollContentBottomPadding);
    }

    private static void RecalculateScrolledBounds(ElementBounds bounds)
    {
        bounds.MarkDirtyRecursive();
        bounds.CalcWorldBounds();
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
}
