using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class LanguageConfigColorPickerTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PickedColorSurvivesSelectionAndSavesWithoutMutatingSource(double scale)
    {
        using var host = Host(scale);
        var languages = Languages();
        List<LanguageConfigEntryMessage>? saved = null;
        using var dialog = Dialog(host, languages, value => saved = value);
        var picker = Picker(dialog);
        picker.Open();
        Click(host, dialog, picker.PopupComposer["palette-6"].Bounds);
        Assert.Equal("#7EAFE0", dialog.SingleComposer.GetTextInput("input-color").GetText());
        Assert.True(dialog.JumpTo("Trade", "Color"));
        Assert.False(picker.IsOpen);
        Assert.False(picker.Focusable);
        Assert.True(dialog.JumpTo("Common", "Color"));
        Assert.Equal("#7EAFE0", dialog.SingleComposer.GetTextInput("input-color").GetText());
        Activate(dialog.SingleComposer, "save");
        Assert.Equal("#7EAFE0", Assert.IsType<List<LanguageConfigEntryMessage>>(saved)[0].Color);
        Assert.Equal("#f0b86e", languages[0].Color);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void HexTypingUpdatesChipAndPreviewWithoutDiscardingIncompleteInput(double scale)
    {
        using var host = Host(scale);
        List<LanguageConfigEntryMessage>? saved = null;
        using var dialog = Dialog(host, Languages(), value => saved = value);
        var picker = Picker(dialog);
        var input = dialog.SingleComposer.GetTextInput("input-color");
        input.SetValue("#46b5ae");
        Assert.Equal("#46B5AE", picker.Value);
        var preview = Assert.IsType<GuiElementDynamicText>(dialog.SingleComposer["color-preview"]);
        Assert.Equal(70 / 255d, preview.Font.Color[0], 5);
        Assert.Equal(181 / 255d, preview.Font.Color[1], 5);
        input.SetValue("#46");
        Assert.Equal("#46B5AE", picker.Value);
        Activate(dialog.SingleComposer, "save");
        Assert.Equal("#46", Assert.IsType<List<LanguageConfigEntryMessage>>(saved)[0].Color);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void OriginalRestorePreservesStoredColorSpelling(double scale)
    {
        using var host = Host(scale);
        List<LanguageConfigEntryMessage>? saved = null;
        using var dialog = Dialog(host, Languages(), value => saved = value);
        var picker = Picker(dialog);
        picker.Open();
        Click(host, dialog, picker.PopupComposer["palette-6"].Bounds);
        Click(host, dialog, picker.PopupComposer["original"].Bounds);
        Assert.Equal("#f0b86e", dialog.SingleComposer.GetTextInput("input-color").GetText());
        Assert.True(dialog.JumpTo("Trade", "Color"));
        Assert.True(dialog.JumpTo("Common", "Color"));
        Assert.Equal("#f0b86e", dialog.SingleComposer.GetTextInput("input-color").GetText());
        Activate(dialog.SingleComposer, "save");
        Assert.Equal("#f0b86e", Assert.IsType<List<LanguageConfigEntryMessage>>(saved)[0].Color);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void EscapeAndOutsideClickDismissOnlyThePopupAndCloseReleasesIt(double scale)
    {
        using var host = Host(scale);
        using var dialog = Dialog(host, Languages(), _ => { });
        dialog.TryOpen();
        var picker = Picker(dialog);
        picker.Open();
        picker.ExpandCustom();
        host.RenderGuiDialog(dialog, 0);
        Assert.True(dialog.OnEscapePressed());
        Assert.False(picker.IsOpen);
        Assert.True(dialog.IsOpened());
        picker.Open();
        var description = dialog.SingleComposer.GetTextInput("input-Description");
        var outside = new MouseEvent((int)description.Bounds.renderX + 2, (int)description.Bounds.renderY + 2, EnumMouseButton.Left, 0);
        Assert.False(picker.PopupBounds.PointInside(outside.X, outside.Y));
        dialog.OnMouseDown(outside);
        Assert.True(outside.Handled);
        Assert.False(picker.IsOpen);
        Assert.False(description.HasFocus);
        picker.Open();
        dialog.TryClose();
        Assert.False(picker.IsOpen);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static List<LanguageConfigEntryMessage> Languages() =>
    [
        new() { OriginalName = "Common", Name = "Common", Prefix = "c", Color = "#f0b86e", Default = true },
        new() { OriginalName = "Trade", Name = "Trade", Prefix = "t", Color = "#ffffff" }
    ];
    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
    private static LanguageConfigDialog Dialog(PreviewHost host, List<LanguageConfigEntryMessage> languages, Action<List<LanguageConfigEntryMessage>> save) =>
        new(host.Api, languages, "", true, save, () => { }, () => { });
    private static GuiElementColorPicker Picker(LanguageConfigDialog dialog) =>
        Assert.IsType<GuiElementColorPicker>(dialog.SingleComposer.GetElement("color-picker"));
    private static void Activate(GuiComposer composer, string key)
    {
        Assert.True(composer.FocusElement(composer[key].TabIndex));
        composer.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter }, true);
    }
    private static void Click(PreviewHost host, LanguageConfigDialog dialog, ElementBounds bounds)
    {
        int x = (int)(bounds.renderX + bounds.InnerWidth / 2), y = (int)(bounds.renderY + bounds.InnerHeight / 2);
        host.SetMouse(x, y);
        dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
}
