using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class ConfigAdminToggleTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void AdvancedTogglesKeepJsonBooleanEncodingAndDraftAcrossRecomposition(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var value = "1";
        using var dialog = new ScrollableConfigAdminDialog(new JsonDialogSettings
        {
            Code = "toggle-test",
            Alignment = EnumDialogArea.CenterMiddle,
            SizeMultiplier = 0.9,
            Padding = 16,
            OnGet = _ => value,
            OnSet = (_, updated) => value = updated,
            Rows = [
                new(new DialogElement { Code = "group-chat", Type = EnumDialogElementType.Text, Text = "Chat", Width = 720, Height = 30 }),
                new(new DialogElement { Code = "EnableGlobalOOC", Type = EnumDialogElementType.Switch, Label = "Allow global OOC", Width = 720, Height = 32 }),
                new(new DialogElement { Code = "save", Type = EnumDialogElementType.Button, Text = "Save", Width = 150, Height = 34 })
            ]
        }, host.Api, false);
        var wrapper = dialog.SingleComposer["switch-2"];
        var field = wrapper.GetType().GetField("_widget", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True(Assert.IsType<GuiElementSetupToggle>(field.GetValue(wrapper)).Value);
        Assert.True(dialog.JumpTo("EnableGlobalOOC"));
        dialog.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Space });
        Assert.Equal("0", value);
        dialog.Recompose();
        wrapper = dialog.SingleComposer["switch-2"];
        Assert.False(Assert.IsType<GuiElementSetupToggle>(field.GetValue(wrapper)).Value);
        host.RenderGuiDialog(dialog, 0);
        var x = (int)(wrapper.Bounds.absX + wrapper.Bounds.OuterWidth / 2);
        var y = (int)(wrapper.Bounds.absY + wrapper.Bounds.OuterHeight / 2);
        dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        Assert.Equal("1", value);
        Assert.Empty(host.UnsupportedCalls);
    }
}
