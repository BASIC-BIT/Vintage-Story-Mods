using TheBasics.GuiPreview;
using thebasics.Tests.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardColorFieldsTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void NametagColorsOfferPickersAlongsideTheirOriginalTextFields(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        wizard.ShowPage("appearance.nametagcolors");
        host.RenderGuiDialog(wizard, 0);
        foreach (var key in new[] { "NametagBackgroundColor", "NametagBorderColor" })
        {
            Assert.NotNull(wizard.SingleComposer.GetElement("color-picker-" + key));
            Assert.Equal(wizard.Draft.Get(key), wizard.SingleComposer.GetTextInput(key).GetText());
        }
        var background = Assert.IsType<GuiElementColorPicker>(wizard.SingleComposer.GetElement("color-picker-NametagBackgroundColor"));
        wizard.SingleComposer.GetTextInput("NametagBackgroundColor").SetValue("#40352980");
        Assert.Equal("#40352980", background.Value);
        background.Open();
        background.PopupComposer.GetTextInput("number-3").SetValue("25");
        Assert.Equal("#40352940", wizard.Draft.Get("NametagBackgroundColor"));
        Click(wizard, background.PopupComposer["theme"].Bounds);
        Assert.Empty(wizard.Draft.Get("NametagBackgroundColor"));
        Assert.Empty(wizard.SingleComposer.GetTextInput("NametagBackgroundColor").GetText());
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ChatColorsPreserveTypedValuesAndUpdateDraftFromPalette(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("wizard-language-configured", host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        wizard.ShowPage("chat.colors");
        foreach (var key in new[] { "EmoteColor", "OOCColor", "GlobalOOCColor" })
            Assert.IsType<GuiElementColorPicker>(wizard.SingleComposer.GetElement("color-picker-" + key));
        var input = wizard.SingleComposer.GetTextInput("OOCColor");
        input.SetValue("navy");
        Assert.Equal("navy", wizard.Draft.Get("OOCColor"));
        host.RenderGuiDialog(wizard, 0);
        input.SetValue("\"unfinished");
        host.RenderGuiDialog(wizard, 0);
        Assert.Equal("\"unfinished", wizard.Draft.Get("OOCColor"));
        var picker = Assert.IsType<GuiElementColorPicker>(wizard.SingleComposer.GetElement("color-picker-OOCColor"));
        picker.Open();
        var bounds = picker.PopupComposer["palette-6"].Bounds;
        Click(wizard, bounds);
        Assert.Equal("#7EAFE0", wizard.Draft.Get("OOCColor"));
        Assert.Equal("#7EAFE0", input.GetText());
        picker.ExpandCustom();
        host.RenderGuiDialog(wizard, 0);
        Assert.True(picker.PopupBounds.absY + picker.PopupBounds.OuterHeight <= 720);
        Assert.True(wizard.OnEscapePressed());
        Assert.False(picker.IsOpen);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static void Click(SetupWizardDialog wizard, ElementBounds bounds)
    {
        var x = (int)(bounds.absX + bounds.OuterWidth / 2);
        var y = (int)(bounds.absY + bounds.OuterHeight / 2);
        wizard.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
}
