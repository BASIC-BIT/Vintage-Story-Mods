using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using TheBasics.GuiPreview;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardLanguageEditorTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void OneScrollingFormKeepsPreviewsFixedAndRevealsKeyboardFocus(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        wizard.Draft.Languages[0].Default = false;
        wizard.Draft.Languages[1].Default = true;
        wizard.ShowLanguageEditor();
        var composer = wizard.SingleComposer;
        Assert.Null(composer.GetElement("language-editor-tabs"));
        Assert.NotNull(composer.GetTextInput("language-editor-Name"));
        var last = composer.GetTextInput("language-editor-GrantedToModelGroups");
        var clip = last.InsideClipBounds;
        var selectorY = composer.GetDropDown("language-editor-selector").Bounds.renderY;
        Assert.False(SetupWizardViewportClip.FullyInside(last.Bounds, clip));
        composer.FocusElement(last.TabIndex);
        Assert.True(SetupWizardViewportClip.FullyInside(last.Bounds, clip));
        var scroll = composer.GetScrollbar("language-editor-scroll").CurrentYPosition;
        Assert.True(scroll > 0);
        Assert.Equal(selectorY, composer.GetDropDown("language-editor-selector").Bounds.renderY);
        host.SetMouse(400, (int)(selectorY + 5));
        wizard.OnMouseWheel(new MouseWheelEventArgs { delta = -1, deltaPrecise = -1 });
        Assert.Equal(scroll, composer.GetScrollbar("language-editor-scroll").CurrentYPosition);
        last.SetValue("custom-group");
        wizard.ShowPage("chat.colors");
        wizard.ShowLanguageEditor();
        Assert.Equal(scroll, wizard.SingleComposer.GetScrollbar("language-editor-scroll").CurrentYPosition);
        Assert.Equal("custom-group", wizard.SingleComposer.GetTextInput("language-editor-GrantedToModelGroups").GetText());
        wizard.ShowLanguageEditor(1);
        Assert.Equal(0, wizard.SingleComposer.GetScrollbar("language-editor-scroll").CurrentYPosition);
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LanguageDetailsOffersAColorPickerWithoutReplacingHexEntry(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        host.RenderGuiDialog(wizard, 0);

        Assert.NotNull(wizard.SingleComposer.GetElement("language-editor-color-picker"));
        Assert.Equal(wizard.Draft.Languages[0].Color, wizard.SingleComposer.GetTextInput("language-editor-Color").GetText());
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void HexEditsKeepThePickerAndDraftInSyncWithoutDiscardingInvalidInput(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        host.RenderGuiDialog(wizard, 0);
        var composer = wizard.SingleComposer;
        var picker = Assert.IsType<GuiElementColorPicker>(composer.GetElement("language-editor-color-picker"));
        composer.FocusElement(picker.TabIndex);
        picker.Open();

        composer.GetTextInput("language-editor-Color").SetValue("#46b5ae");
        Assert.Equal("#46b5ae", wizard.Draft.Languages[0].Color);
        Assert.Equal("#46B5AE", picker.Value);
        Assert.True(picker.IsOpen);
        composer.GetTextInput("language-editor-Color").SetValue("blue");
        Assert.Equal("blue", wizard.Draft.Languages[0].Color);
        Assert.Equal("#46B5AE", picker.Value);
        Assert.True(wizard.Draft.LanguagesChanged);
        Assert.Same(composer, wizard.SingleComposer);

        Assert.True(wizard.OnEscapePressed());
        Assert.False(picker.IsOpen);
        Assert.True(picker.HasFocus);
        Assert.True(wizard.IsOpened());
        Assert.Equal("blue", composer.GetTextInput("language-editor-Color").GetText());
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    public void ChangingLanguageClosesTheOldPickerAndKeepsEachRowsColor(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        var first = Assert.IsType<GuiElementColorPicker>(wizard.SingleComposer.GetElement("language-editor-color-picker"));
        wizard.SingleComposer.GetTextInput("language-editor-Color").SetValue("#B497D6");
        first.Open();

        wizard.ShowLanguageEditor(1);
        var second = Assert.IsType<GuiElementColorPicker>(wizard.SingleComposer.GetElement("language-editor-color-picker"));
        Assert.False(first.IsOpen);
        Assert.NotSame(first, second);
        Assert.Equal("#CCA4EF", second.Value);
        Assert.Equal("#B497D6", wizard.Draft.Languages[0].Color);
        Assert.Equal("#cca4ef", wizard.Draft.Languages[1].Color);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData("wizard-language-color-palette", 1600, 1000, 1.0, false)]
    [InlineData("wizard-language-color-palette", 1280, 720, 1.25, false)]
    [InlineData("wizard-language-color-custom", 1600, 1000, 1.0, true)]
    [InlineData("wizard-language-color-custom", 1280, 720, 1.25, true)]
    [InlineData("wizard-language-color-selected", 1280, 720, 1.25, false)]
    public void ColorCaptureScenesKeepThePopupInsideTheFrameAndClearOfChat(string name, int width, int height, double scale, bool custom)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);
        using var scene = PreviewScene.Create(name, host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        host.RenderGuiDialog(wizard, 0);
        var picker = Assert.IsType<GuiElementColorPicker>(wizard.SingleComposer.GetElement("language-editor-color-picker"));
        Assert.True(picker.IsOpen);
        Assert.Equal(custom, picker.IsCustomExpanded);
        var bounds = picker.PopupBounds;
        Assert.InRange(bounds.renderX, 0, width - bounds.OuterWidth + 1);
        Assert.InRange(bounds.renderY, 0, height - bounds.OuterHeight + 1);
        Assert.True(bounds.renderX >= wizard.SingleComposer.GetTextInput("language-editor-Name").Bounds.renderX - 1);
        if (name == "wizard-language-color-selected")
        {
            Assert.Equal("#B497D6", picker.Value);
            Assert.Equal("#B497D6", wizard.Draft.Languages[0].Color);
            Assert.True(wizard.Draft.LanguagesChanged);
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ChoosingASwatchUpdatesHexDraftAndBothChatExamplesWithoutRecomposition(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-color-palette", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        var composer = wizard.SingleComposer;
        var picker = Assert.IsType<GuiElementColorPicker>(composer.GetElement("language-editor-color-picker"));
        var original = wizard.Draft.Languages[0].Color;
        Click(picker.PopupComposer.GetElement("palette-5").Bounds);

        Assert.Equal("#B497D6", wizard.Draft.Languages[0].Color);
        Assert.Equal("#B497D6", composer.GetTextInput("language-editor-Color").GetText());
        Assert.True(picker.IsOpen);
        Assert.Same(composer, wizard.SingleComposer);
        foreach (var key in new[] { "language-editor-understood", "language-editor-unfamiliar" })
            Assert.Contains(composer.GetRichtext(key).Components.OfType<RichTextComponent>(), component =>
                Math.Abs(component.Font.Color[0] - 0xb4 / 255d) < 0.01 && Math.Abs(component.Font.Color[1] - 0x97 / 255d) < 0.01);

        Click(picker.PopupComposer.GetElement("original").Bounds);
        Assert.Equal(original, wizard.Draft.Languages[0].Color);
        Assert.Equal(original, composer.GetTextInput("language-editor-Color").GetText());
        Assert.False(wizard.Draft.LanguagesChanged);
        Assert.Empty(host.UnsupportedCalls);

        void Click(ElementBounds bounds)
        {
            var x = (int)(bounds.renderX + bounds.InnerWidth / 2);
            var y = (int)(bounds.renderY + bounds.InnerHeight / 2);
            host.SetMouse(x, y);
            wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
            wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
    }

    [VisualTheory]
    [InlineData(1.25)]
    public void PreviewRefreshKeepsTheLanguageMenuOpenAndItsPointerCursor(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        host.RenderGuiDialog(wizard, 0);
        var selector = wizard.SingleComposer.GetDropDown("language-editor-selector");
        Assert.Equal("linkselect", selector.MouseOverCursor);
        var names = selector.listMenu.Names;
        var x = (int)(selector.Bounds.absX + selector.Bounds.OuterWidth / 2);
        var y = (int)(selector.Bounds.absY + selector.Bounds.OuterHeight / 2);
        host.SetMouse(x, y);
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.True(selector.listMenu.IsOpened);
        var menuEffects = host.RecordedEffects.ToArray();
        Assert.All(menuEffects, effect => Assert.StartsWith("sound:", effect));

        wizard.SingleComposer.GetTextInput("language-editor-Color").SetValue("#DBB070");
        for (var frame = 0; frame < 4; frame++) host.RenderGuiDialog(wizard, 0.25f);

        Assert.Same(names, selector.listMenu.Names);
        Assert.True(selector.listMenu.IsOpened);
        Assert.Equal(menuEffects, host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompactLanguageEditorKeepsValidationAndActionsAboveSaveFailureStatus(bool grants)
    {
        const double scale = 1.25;
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        wizard.Draft.Languages[0].Name = "";
        wizard.Draft.Languages[0].Prefix = "";
        wizard.SetRequestFailure("Server could not save settings. Your edits are still here.");
        wizard.ShowLanguageEditor(0);
        if (grants) wizard.SingleComposer.GetScrollbar("language-editor-scroll").ScrollToBottom();
        host.RenderGuiDialog(wizard, 0);

        var composer = wizard.SingleComposer;
        var errors = composer.GetRichtext("language-editor-errors");
        var expected = LanguageConfigAdmin.ValidateEntries(wizard.Draft.Languages, out _).Take(2);
        var text = string.Concat(errors.Components.OfType<RichTextComponent>().Select(component => component.DisplayText));
        Assert.All(expected, error => Assert.Contains(error, text));
        var statusTop = composer.GetElement("footer-rule").Bounds.renderY - 28 * scale;
        Assert.True(errors.Bounds.renderY + errors.TotalHeight <= statusTop);
        foreach (var key in new[] { "language-editor-remove", "language-editor-reset" })
        {
            var action = composer.GetButton(key);
            Assert.True(action.Bounds.renderY + action.Bounds.OuterHeight <= statusTop);
        }
        var lastInput = grants ? "language-editor-GrantedToModelGroups" : "language-editor-Hidden";
        var last = composer.GetElement(lastInput);
        Assert.True(last.Bounds.renderY + last.Bounds.OuterHeight <= errors.Bounds.renderY);
        Assert.Empty(host.UnsupportedCalls);
    }

    [Fact]
    public void ResetRestoresLanguageRowsAndLeavesOtherDraftChangesAlone()
    {
        var draft = Draft();
        var original = draft.Languages.Select(entry => entry with { }).ToArray();
        draft.Languages[0].Name = "Discard this edit";
        draft.Set("EnableLanguageSystem", "0");
        var changes = 0;
        var editor = new SetupWizardLanguageEditor(null!, draft, () => changes++, null!) { SelectedIndex = 1 };
        editor.AddLanguage();

        editor.ResetLanguages();

        Assert.Equal(original, draft.Languages);
        Assert.False(draft.LanguagesChanged);
        Assert.Equal("0", draft.Get("EnableLanguageSystem"));
        Assert.Contains("EnableLanguageSystem", draft.ChangedKeys);
        Assert.Equal(1, editor.SelectedIndex);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void AddingALanguageAvoidsExistingNamesAndPrefixesAndLeavesOtherRowsUntouched()
    {
        var draft = Draft();
        draft.Languages[0].Name = "New language 1";
        draft.Languages[1].Prefix = "new2";
        var before = draft.Languages.Select(entry => entry with { }).ToArray();
        var changes = 0;
        var editor = new SetupWizardLanguageEditor(null!, draft, () => changes++, null!);

        editor.AddLanguage();

        Assert.Equal(3, draft.Languages.Count);
        Assert.Equal(before, draft.Languages.Take(2));
        Assert.Equal("New language 3", draft.Languages[2].Name);
        Assert.Equal("new3", draft.Languages[2].Prefix);
        Assert.Null(draft.Languages[2].OriginalName);
        Assert.False(draft.Languages[2].Default);
        Assert.Equal(2, editor.SelectedIndex);
        Assert.Equal(1, changes);
        Assert.Empty(LanguageConfigAdmin.ValidateEntries(draft.Languages, out _));
    }

    [Fact]
    public void MovingKeepsTheSelectedRowAndEveryGrantTogether()
    {
        var draft = Draft();
        var selected = draft.Languages[1];
        var editor = new SetupWizardLanguageEditor(null!, draft, null!, null!) { SelectedIndex = 1 };

        editor.MoveSelected(-1);

        Assert.Same(selected, draft.Languages[0]);
        Assert.Equal(0, editor.SelectedIndex);
        Assert.Equal("hunter, tailor", draft.Languages[0].GrantedToClasses);
        Assert.Equal("keeneyes", draft.Languages[0].GrantedToTraits);
        Assert.Equal("fox", draft.Languages[0].GrantedToModels);
        Assert.Equal("beastfolk", draft.Languages[0].GrantedToModelGroups);
        Assert.Equal("Dock Cant", draft.Languages[0].OriginalName);
        editor.MoveSelected(-1);
        Assert.Same(selected, draft.Languages[0]);
    }

    [Fact]
    public void RemovingKeepsSelectionInRangeAndCannotRemoveTheLastLanguage()
    {
        var draft = Draft();
        var changes = 0;
        var editor = new SetupWizardLanguageEditor(null!, draft, () => changes++, null!) { SelectedIndex = 1 };

        editor.RemoveSelected();
        editor.RemoveSelected();

        Assert.Equal("Quayside", Assert.Single(draft.Languages).Name);
        Assert.Equal(0, editor.SelectedIndex);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void FirstLanguageInAnEmptyDraftStartsAsADefault()
    {
        var draft = new SetupWizardDraft(Array.Empty<ConfigAdminSettingValue>(), Array.Empty<LanguageConfigEntryMessage>());
        var editor = new SetupWizardLanguageEditor(null!, draft, null!, null!);

        editor.AddLanguage();

        Assert.True(Assert.Single(draft.Languages).Default);
        Assert.Empty(LanguageConfigAdmin.ValidateEntries(draft.Languages, out _));
    }

    [Fact]
    public void PreviewConfigurationUsesDraftSettingsAndConfiguredLanguages()
    {
        var draft = Draft();
        draft.Set("ProximityChatPresentationMode", "PlainProximity");
        draft.Set("EnableLanguageSystem", "0");
        var editor = new SetupWizardLanguageEditor(null!, draft, null!, null!);

        var config = editor.BuildConfig();

        Assert.Equal("PlainProximity", config.ProximityChatPresentationMode);
        Assert.False(config.EnableLanguageSystem);
        Assert.Equal(new[] { "Quayside", "Dock Cant" }, config.Languages.Select(language => language.Name));
        Assert.Equal("#46B5AE", config.Languages[1].Color);
        Assert.Equal(new[] { "dok", "esh" }, config.Languages[1].Syllables);
    }

    [Fact]
    public void InvalidDraftRowsNeverReintroduceShippedSampleLanguages()
    {
        var draft = Draft();
        draft.Languages[1].Color = "blue";
        var editor = new SetupWizardLanguageEditor(null!, draft, null!, null!);

        var config = editor.BuildConfig();

        Assert.Equal("Quayside", Assert.Single(config.Languages).Name);
        Assert.DoesNotContain(config.Languages, language => language.Name is "Common" or "Tradeband");
    }

    private static SetupWizardDraft Draft() => new(SetupWizardCaptureScenes.DefaultValues(),
    [
        new LanguageConfigEntryMessage
        {
            OriginalName = "Quayside", Name = "Quayside", Description = "The shared language of the harbor.", Prefix = "quay",
            Color = "#E9DDCE", Syllables = "vel, en", Default = true
        },
        new LanguageConfigEntryMessage
        {
            OriginalName = "Dock Cant", Name = "Dock Cant", Description = "Quick words for a busy dock.", Prefix = "dock",
            Color = "#46B5AE", Syllables = "dok, esh", GrantedToClasses = "hunter, tailor", GrantedToTraits = "keeneyes",
            GrantedToModels = "fox", GrantedToModelGroups = "beastfolk"
        }
    ]);
}
