using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class LanguageEditorContextTests
{
    [VisualTheory]
    [InlineData("language-default")]
    [InlineData("language-error")]
    [InlineData("language-color-custom")]
    public void CompactLegacyDialogKeepsTitleAndFooterOnScreen(string scenario)
    {
        using var host = Host(1.25);
        using var scene = PreviewScene.Create(scenario, host);
        var composer = scene.Dialog.SingleComposer;
        host.RenderGuiDialog(scene.Dialog, 0);
        Assert.True(composer.Bounds.absY >= 0);
        Assert.True(composer.Bounds.absY + composer.Bounds.OuterHeight <= 720);
        foreach (var key in new[] { "save", "close", "reload" })
        {
            var bounds = composer[key].Bounds;
            Assert.InRange(bounds.absY, 0, 720 - bounds.OuterHeight);
        }
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LongLanguageListsLeaveRoomForDeleteAndKeepLaterLanguagesReachable(double scale)
    {
        using var host = Host(scale);
        using var dialog = new LanguageConfigDialog(host.Api,
            Enumerable.Range(1, 12).Select(index => new LanguageConfigEntryMessage
                { OriginalName = "Language " + index, Name = "Language " + index, Prefix = "l" + index, Color = "#FFFFFF" }).ToList(),
            "", true, _ => { }, () => { }, () => { });
        var method = typeof(LanguageConfigDialog).GetMethod("BuildOrderSummaryLines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var lines = Assert.IsType<List<(string Text, bool IsSelected)>>(method.Invoke(dialog, null));
        Assert.Equal(8, lines.Count);
        Assert.Contains("5", lines.Last().Text);
        Assert.True(dialog.JumpTo("Language 12", "Name"));
        Assert.Equal("Language 12", dialog.SingleComposer.GetTextInput("input-Name").GetText());
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LegacyTabsPreserveBothGroupsAndSearchOpensTheMatchingGroup(double scale)
    {
        using var host = Host(scale);
        List<LanguageConfigEntryMessage>? saved = null;
        using var dialog = new LanguageConfigDialog(host.Api,
            [new() { OriginalName = "Common", Name = "Common", Prefix = "c", Color = "#FFFFFF", Default = true, GrantedToClasses = "hunter" }],
            "", true, value => saved = value, () => { }, () => { });
        var composer = dialog.SingleComposer;
        Assert.Equal(0, composer.GetTextInput("input-Name").CaretPosWithoutLineBreaks);
        Assert.False(composer.GetTextInput("input-Name").HasFocus);
        Assert.True(composer["close"].Bounds.fixedX < composer["save"].Bounds.fixedX);
        composer.GetTextInput("input-Name").SetValue("Harbor talk");
        Assert.Equal("Harbor talk", composer.GetDynamicText("language-title").GetText());
        Assert.Equal("linkselect", composer.GetDropDown("language-selector").MouseOverCursor);
        composer.GetHorizontalTabs("language-tabs").SetValue(1);
        Assert.Equal("hunter", dialog.SingleComposer.GetTextInput("input-GrantedToClasses").GetText());
        dialog.SingleComposer.GetTextInput("input-GrantedToClasses").SetValue("tailor");
        dialog.SingleComposer.GetHorizontalTabs("language-tabs").SetValue(0);
        Assert.Equal("Harbor talk", dialog.SingleComposer.GetTextInput("input-Name").GetText());
        Assert.True(dialog.SingleComposer.GetElement("toggle-Default") is GuiElementSetupToggle { Value: true });
        Assert.True(dialog.JumpTo("Common", "GrantedToClasses"));
        Assert.True(dialog.SingleComposer["input-GrantedToClasses"].HasFocus);
        Assert.Equal("tailor", dialog.SingleComposer.GetTextInput("input-GrantedToClasses").GetText());
        Assert.True(dialog.SingleComposer.FocusElement(dialog.SingleComposer["save"].TabIndex));
        dialog.SingleComposer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter }, true);
        var language = Assert.Single(Assert.IsType<List<LanguageConfigEntryMessage>>(saved));
        Assert.Equal("Harbor talk", language.Name);
        Assert.Equal("tailor", language.GrantedToClasses);
        Assert.Equal("#FFFFFF", language.Color);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void WizardHeadingFollowsNameWithoutReplacingFocusedInput(double scale)
    {
        using var host = Host(scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        var composer = wizard.SingleComposer;
        var input = composer.GetTextInput("language-editor-Name");
        Assert.True(composer.FocusElement(input.TabIndex));
        input.SetValue("Harbor <talk>");
        Assert.Same(composer, wizard.SingleComposer);
        Assert.True(input.HasFocus);
        var title = string.Concat(composer.GetRichtext("language-editor-title").Components.OfType<RichTextComponent>().Select(component => component.DisplayText));
        Assert.Equal("Harbor <talk>", title);
        host.RenderGuiDialog(wizard, 0);
        Assert.True(composer.GetElement("language-editor-reset").Bounds.renderY > composer.GetElement("language-editor-add").Bounds.renderY);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
}
