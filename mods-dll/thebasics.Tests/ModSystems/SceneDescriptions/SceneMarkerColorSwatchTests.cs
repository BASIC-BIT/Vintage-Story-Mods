using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.SceneDescriptions;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection("Standalone GUI")]
public class SceneMarkerColorSwatchTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void FiveNativeSwatchesUseRuntimeColorsAndSelectStoredPreset(double scale)
    {
        using var host = Host(scale);
        using var dialog = Dialog(host);
        host.RenderGuiDialog(dialog, 0);
        var pixels = host.Canvas.GetRgbaPixels();
        foreach (var color in Enum.GetValues<SceneMarkerColor>())
        {
            var swatch = Swatch(dialog, color);
            Assert.Equal(color == SceneMarkerColor.Blue, swatch.On);
            Assert.Equal("linkselect", swatch.MouseOverCursor);
            Assert.True(swatch.ShowToolTip);
            var bounds = swatch.Bounds;
            var offset = ((int)(bounds.renderY + bounds.InnerHeight / 2) * host.Canvas.Width + (int)(bounds.renderX + bounds.InnerWidth / 2)) * 4;
            var rgb = SceneMarkerVisuals.Palette(color);
            Assert.InRange(pixels[offset], (byte)(rgb.R * 255 - 1), (byte)Math.Min(255, rgb.R * 255 + 1));
            Assert.InRange(pixels[offset + 1], (byte)(rgb.G * 255 - 1), (byte)Math.Min(255, rgb.G * 255 + 1));
            Assert.InRange(pixels[offset + 2], (byte)(rgb.B * 255 - 1), (byte)Math.Min(255, rgb.B * 255 + 1));
        }
        Assert.Null(dialog.SingleComposer.GetElement("color-5"));
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PointerSelectionRemainsSingleAndSaveKeepsExistingData(double scale)
    {
        using var host = Host(scale);
        SceneDescriptionData? saved = null;
        using var dialog = Dialog(host, save: data => saved = data);
        var green = Swatch(dialog, SceneMarkerColor.Green);
        var click = new MouseEvent(0, 0, EnumMouseButton.Left, 0);
        green.OnMouseDownOnElement(host.Api, click);
        Assert.True(click.Handled);
        AssertSelection(dialog, SceneMarkerColor.Green);
        green.OnMouseDownOnElement(host.Api, new MouseEvent(0, 0, EnumMouseButton.Left, 0));
        AssertSelection(dialog, SceneMarkerColor.Green);
        Assert.True((bool)typeof(SceneDescriptionDialog).GetMethod("OnSave", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, null)!);
        Assert.NotNull(saved);
        Assert.Equal(SceneMarkerColor.Green, saved.Color);
        Assert.Equal("The old watchtower", saved.Title);
        Assert.Equal("Wind whistles through the broken stonework.", saved.Body);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void KeyboardSelectsPresetsAndTabContinuesThroughTheDialog(double scale)
    {
        using var host = Host(scale);
        using var dialog = Dialog(host);
        var composer = dialog.SingleComposer;
        composer.FocusElement(Swatch(dialog, SceneMarkerColor.Blue).TabIndex);
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Right }, true);
        AssertSelection(dialog, SceneMarkerColor.Green);
        Assert.True(Swatch(dialog, SceneMarkerColor.Green).HasFocus);
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Home }, true);
        AssertSelection(dialog, SceneMarkerColor.Gold);
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Tab }, true);
        Assert.True(Swatch(dialog, SceneMarkerColor.Parchment).HasFocus);
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Space }, true);
        AssertSelection(dialog, SceneMarkerColor.Parchment);
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter }, true);
        AssertSelection(dialog, SceneMarkerColor.Parchment);
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.End }, true);
        AssertSelection(dialog, SceneMarkerColor.Red);
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Left }, true);
        AssertSelection(dialog, SceneMarkerColor.Green);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LockedMarkerSwatchesCannotChangeStoredColor(double scale)
    {
        using var host = Host(scale);
        using var dialog = Dialog(host, locked: true);
        foreach (var color in Enum.GetValues<SceneMarkerColor>())
        {
            var swatch = Swatch(dialog, color);
            Assert.False(swatch.Enabled);
            Assert.False(swatch.Focusable);
            Assert.Null(swatch.MouseOverCursor);
            swatch.OnMouseDownOnElement(host.Api, new MouseEvent(0, 0, EnumMouseButton.Left, 0));
            swatch.OnFocusGained();
            swatch.OnKeyDown(host.Api, new KeyEvent { KeyCode = (int)GlKeys.Space });
        }
        AssertSelection(dialog, SceneMarkerColor.Blue);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);

    private static SceneDescriptionDialog Dialog(PreviewHost host, bool locked = false, Action<SceneDescriptionData>? save = null) => new(host.Api,
        new SceneDescriptionData { Title = "The old watchtower", Body = "Wind whistles through the broken stonework.", Color = SceneMarkerColor.Blue, LockItemCode = locked ? "game:padlock" : "" },
        true, (data, _) => save?.Invoke(data), null!, null!, null!, null!);

    private static GuiElementColorListPicker Swatch(SceneDescriptionDialog dialog, SceneMarkerColor color) =>
        Assert.IsAssignableFrom<GuiElementColorListPicker>(dialog.SingleComposer.GetElement("color-" + (int)color));

    private static void AssertSelection(SceneDescriptionDialog dialog, SceneMarkerColor expected)
    {
        foreach (var color in Enum.GetValues<SceneMarkerColor>()) Assert.Equal(color == expected, Swatch(dialog, color).On);
        var draft = (SceneDescriptionData)typeof(SceneDescriptionDialog).GetMethod("BuildDraft", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, null)!;
        Assert.Equal(expected, draft.Color);
    }
}
