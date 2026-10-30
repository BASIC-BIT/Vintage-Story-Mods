using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class GuiElementColorPickerTests
{
    [VisualTheory]
    [InlineData("navy")]
    [InlineData("rgb(12, 34, 56)")]
    public void OpeningAnUnsupportedStoredColorDoesNotRewriteIt(string stored)
    {
        using var host = Host(1.25);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), stored, changes.Add);
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.Open();
        picker.ExpandCustom();
        Render(host, composer, picker);
        Assert.Equal(stored, picker.Value);
        Assert.Empty(changes);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void RgbaPaletteAndSpectrumPreserveStoredOpacity(double scale)
    {
        using var host = Host(scale, 1280, 720);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "#FF000080", changes.Add, ColorPickerMode.RgbaWithTheme);
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.Open();
        Click(host, picker, picker.PopupComposer["palette-6"].Bounds);
        Assert.Equal("#7EAFE080", picker.Value);
        picker.SetValue("#FF000080");
        picker.ExpandCustom();
        picker.PopupComposer.GetTextInput("number-0").SetValue("120");
        Assert.Equal("#00FF0080", picker.Value);
        Assert.Equal(new[] { "#7EAFE080", "#00FF0080" }, changes);
        Render(host, composer, picker);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void RgbaOpacityAndThemeRemainEditableWithoutExpandingCustom(double scale)
    {
        using var host = Host(scale, 1280, 720);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "#33669980", changes.Add, ColorPickerMode.RgbaWithTheme);
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.Open();
        var opacity = picker.PopupComposer.GetTextInput("number-3");
        picker.PopupComposer.FocusElement(opacity.TabIndex);
        opacity.SetValue("25");
        Assert.Equal("#33669940", picker.Value);
        opacity.SetValue("101");
        Assert.Equal("#33669940", picker.Value);
        picker.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Tab });
        Assert.Equal("25", opacity.GetText());
        Click(host, picker, picker.PopupComposer["theme"].Bounds);
        Assert.Equal("", picker.Value);
        Assert.False(picker.IsCustomExpanded);
        Click(host, picker, picker.PopupComposer["palette-6"].Bounds);
        Assert.Equal("#7EAFE0", picker.Value);
        Click(host, picker, picker.PopupComposer["original"].Bounds);
        Assert.Equal("#33669980", picker.Value);
        Assert.Equal(new[] { "#33669940", "", "#7EAFE0", "#33669980" }, changes);
        Render(host, composer, picker);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void RgbaAcceptsRgbRgbaAndThemeButRejectsMalformedValues(double scale)
    {
        using var host = Host(scale, 1280, 720);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "", changes.Add, ColorPickerMode.RgbaWithTheme);
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.SetValue("#aabbccdd");
        Assert.Equal("#AABBCCDD", picker.Value);
        foreach (var invalid in new[] { "#abc", "#1234567", "#112233zz", "navy", " #11223344" })
        {
            picker.SetValue(invalid);
            Assert.Equal("#AABBCCDD", picker.Value);
        }
        picker.SetValue("#123456");
        Assert.Equal("#123456", picker.Value);
        picker.SetValue("");
        picker.Open();
        picker.ExpandCustom();
        Assert.Equal("", picker.Value);
        Assert.Empty(changes);
        Render(host, composer, picker);
        Assert.True(picker.PopupBounds.renderY + picker.PopupBounds.OuterHeight <= 720);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void OriginalRestoresRawStringWhenTheStoredColorIsNotRgb(double scale)
    {
        using var host = Host(scale);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "navy", changes.Add);
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.Open();
        Click(host, picker, picker.PopupComposer["palette-6"].Bounds);
        Assert.Equal("#7EAFE0", picker.Value);
        Click(host, picker, picker.PopupComposer["original"].Bounds);
        Assert.Equal("navy", picker.Value);
        Assert.Equal(new[] { "#7EAFE0", "navy" }, changes);
        Render(host, composer, picker);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ExpandedPickerKeepsHexEntryAvailableAndRestoresIncompleteTextOnBlur(double scale)
    {
        using var host = Host(scale);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "#F0B86E", changes.Add);
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.Open();
        picker.ExpandCustom();
        var hex = picker.PopupComposer.GetTextInput("hex");
        Assert.NotNull(hex);
        picker.PopupComposer.FocusElement(hex.TabIndex);
        hex.SetValue("#46b5ae");
        Assert.Equal("#46B5AE", picker.Value);
        Assert.Equal(new[] { "#46B5AE" }, changes);
        Assert.True(hex.HasFocus);
        hex.SetValue("#46");
        Assert.Equal("#46B5AE", picker.Value);
        picker.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Tab });
        Assert.Equal("#46B5AE", hex.GetText());
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PaletteChangesColorAndOriginalRestoresWithoutProgrammaticNotifications(double scale)
    {
        using var host = Host(scale);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "#f0b86e", changes.Add);
        composer.AddInteractiveElement(picker, "picker").Compose();
        foreach (var invalid in new[] { "not a color", "#abc", "abc", "###123456", "123456", "#12345678", " #123456", "# 12345" })
        {
            picker.SetValue(invalid);
            Assert.Equal("#F0B86E", picker.Value);
        }
        picker.SetValue("#abcdef");
        Assert.Equal("#ABCDEF", picker.Value);
        Assert.Empty(changes);
        picker.Open();
        Render(host, composer, picker);
        Click(host, picker, picker.PopupComposer!["palette-6"].Bounds);
        Assert.Equal("#7EAFE0", picker.Value);
        Assert.Equal(new[] { "#7EAFE0" }, changes);
        Click(host, picker, picker.PopupComposer["original"].Bounds);
        Assert.Equal("#f0b86e", picker.Value);
        Assert.Equal(new[] { "#7EAFE0", "#f0b86e" }, changes);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void CustomSpectrumClampsDragAndKeepsHueWhenBlack(double scale)
    {
        using var host = Host(scale);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "#0000FF", changes.Add);
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.Open();
        picker.ExpandCustom();
        Render(host, composer, picker);
        Assert.True(picker.IsCustomExpanded);
        var plane = picker.PopupComposer!["spectrum"].Bounds;
        Down(host, picker, (int)(plane.renderX + plane.InnerWidth), (int)(plane.renderY + plane.InnerHeight));
        Assert.Equal("#000000", picker.Value);
        Move(host, picker, (int)(plane.renderX + plane.InnerWidth + 100), (int)(plane.renderY - 100));
        Assert.Equal("#0000FF", picker.Value);
        picker.OnMouseUp(host.Api, new MouseEvent(0, 0, EnumMouseButton.Left, 0));
        var previous = changes.Count;
        Move(host, picker, (int)plane.renderX, (int)plane.renderY);
        Assert.Equal(previous, changes.Count);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PopupFitsScreenPreservesChatColumnAndDismissalConsumesOutsideClick(double scale)
    {
        using var host = Host(scale);
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(600, 350, 32, 28), "#123456", _ => { })
        {
            PopupMinimumX = 310
        };
        composer.AddInteractiveElement(picker, "picker").Compose();
        picker.Open();
        picker.ExpandCustom();
        Render(host, composer, picker);
        Assert.True(picker.PopupBounds!.renderX >= 310 * scale);
        Assert.True(picker.PopupBounds.renderX + picker.PopupBounds.OuterWidth <= 800);
        Assert.True(picker.PopupBounds.renderY >= 0);
        Assert.True(picker.PopupBounds.renderY + picker.PopupBounds.OuterHeight <= 600);
        var outside = new MouseEvent(10, 10, EnumMouseButton.Left, 0);
        picker.OnMouseDown(host.Api, outside);
        Assert.True(outside.Handled);
        Assert.False(picker.IsOpen);
        Assert.True(picker.HasFocus);
        picker.Open();
        var escape = new KeyEvent { KeyCode = (int)GlKeys.Escape };
        picker.OnKeyDown(host.Api, escape);
        Assert.True(escape.Handled);
        Assert.False(picker.IsOpen);
        picker.Open();
        picker.Enabled = false;
        Assert.False(picker.IsOpen);
        Assert.False(picker.HasFocus);
        Assert.Null(picker.MouseOverCursor);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void KeyboardPaletteAndNumericChannelsEditWithoutReplacingFocusedInput(double scale)
    {
        using var host = Host(scale);
        var changes = new List<string>();
        using var composer = Composer(host);
        var picker = new GuiElementColorPicker(host.Api, ElementBounds.Fixed(360, 200, 32, 28), "#FF0000", changes.Add);
        composer.AddInteractiveElement(picker, "picker").Compose();
        composer.FocusElement(picker.TabIndex);
        picker.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Space });
        Assert.True(picker.IsOpen);
        picker.PopupComposer!.FocusElement(picker.PopupComposer["palette-6"].TabIndex);
        picker.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Enter });
        Assert.Equal("#7EAFE0", picker.Value);
        picker.SetValue("#FF0000");
        picker.ExpandCustom();
        var input = picker.PopupComposer.GetTextInput("number-0");
        picker.PopupComposer.FocusElement(input.TabIndex);
        input.SetValue("120");
        Assert.Equal("#00FF00", picker.Value);
        Assert.True(input.HasFocus);
        Assert.Equal("120", input.GetText());
        input.SetValue("361");
        Assert.Equal("#00FF00", picker.Value);
        picker.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Tab });
        Assert.False(input.HasFocus);
        Assert.Equal("120", input.GetText());
        Assert.Equal(new[] { "#7EAFE0", "#00FF00" }, changes);
        Render(host, composer, picker);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost Host(double scale, int width = 800, int height = 600) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);
    private static GuiComposer Composer(PreviewHost host) => host.Api.Gui.CreateCompo("color", ElementBounds.Fixed(0, 0, 640, 480));
    private static void Render(PreviewHost host, GuiComposer composer, GuiElementColorPicker picker)
    {
        host.Canvas.Clear(30, 30, 30, 255);
        composer.Render(0);
        picker.RenderPopup(0);
    }
    private static void Click(PreviewHost host, GuiElementColorPicker picker, ElementBounds bounds)
    {
        int x = (int)(bounds.renderX + bounds.InnerWidth / 2), y = (int)(bounds.renderY + bounds.InnerHeight / 2);
        Down(host, picker, x, y);
        picker.OnMouseUp(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
    private static void Down(PreviewHost host, GuiElementColorPicker picker, int x, int y)
    {
        host.SetMouse(x, y);
        picker.OnMouseDown(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
    private static void Move(PreviewHost host, GuiElementColorPicker picker, int x, int y)
    {
        host.SetMouse(x, y);
        picker.OnMouseMove(host.Api, new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
}
