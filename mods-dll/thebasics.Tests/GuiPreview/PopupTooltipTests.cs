using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class PopupTooltipTests
{
    [VisualTheory]
    [InlineData("language-dropdown")]
    [InlineData("sheet-preferences-dropdown")]
    public void OpenDropdownSuppressesBackgroundTooltip(string scenario)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, 1.25);
        using var scene = PreviewScene.Create(scenario, host);
        var help = new GuiElementHoverText(host.Api, "Background setting", CairoFont.WhiteSmallText(), 300,
            ElementBounds.Fixed(10, 10, 30, 30).WithEmptyParent());
        scene.Dialog.SingleComposer.AddInteractiveElement(help, "qa-background-help").ReCompose();
        help.Bounds.CalcWorldBounds();
        host.SetMouse((int)(help.Bounds.renderX + 8), (int)(help.Bounds.renderY + 8));
        Assert.True(help.IsPositionInside(host.Api.Input.MouseX, host.Api.Input.MouseY));
        host.RenderGuiDialog(scene.Dialog, 0);
        Assert.False(help.IsNowShown);
    }

    [VisualTheory]
    [InlineData("wizard-chat-colors-custom", "color-picker-OOCColor", 1.0)]
    [InlineData("wizard-chat-colors-custom", "color-picker-OOCColor", 1.25)]
    [InlineData("language-color-custom", "color-picker", 1.25)]
    [InlineData("scene-marker-custom-picker", "custom-color-picker", 1.25)]
    [InlineData("admin-chat-colors-custom", "color-picker-EmoteColor", 1.25)]
    public void PaletteDoesNotShowTheSettingTooltipBehindIt(string scenario, string pickerKey, double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create(scenario, host);
        var dialog = scene.Dialog;
        var element = dialog.SingleComposer.GetElement(pickerKey);
        var picker = Assert.IsType<GuiElementColorPicker>(element is ScrollableConfigAdminDialog.ScrollingWidget wrapper ? wrapper.Widget : element);
        Assert.True(picker.IsOpen);
        var swatch = picker.PopupComposer["palette-1"].Bounds;
        var help = new GuiElementHoverText(host.Api, "Background setting", CairoFont.WhiteSmallText(), 300, ElementBounds.Fixed(0, 0, 30, 30));
        dialog.SingleComposer.AddInteractiveElement(help, "qa-background-help").ReCompose();
        // Put the underlying setting's hit area beneath the swatch, independent of compact layout.
        help.Bounds = ElementBounds.Fixed(swatch.renderX / scale, swatch.renderY / scale, 30, 30).WithEmptyParent();
        help.Bounds.CalcWorldBounds();
        host.SetMouse((int)(swatch.renderX + 8), (int)(swatch.renderY + 8));
        Assert.True(help.IsPositionInside(host.Api.Input.MouseX, host.Api.Input.MouseY));
        host.RenderGuiDialog(dialog, 0);
        Assert.False(help.IsNowShown);
        picker.Close();
        host.RenderGuiDialog(dialog, 0);
        Assert.True(help.IsNowShown);
        Assert.Empty(host.UnsupportedCalls);
    }
}
