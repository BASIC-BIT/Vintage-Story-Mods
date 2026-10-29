using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.SceneDescriptions;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection("Standalone GUI")]
public class SceneMarkerCustomColorUiTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void CustomPickerChangesOnlyTheDraftUntilDefaultsOrSave(double scale)
    {
        using var host = Host(scale);
        var source = Data();
        SceneDescriptionData? saved = null, defaults = null;
        using var dialog = Dialog(host, source, value => saved = value, value => defaults = value);
        Open(host, dialog);
        var picker = Picker(dialog);
        Assert.True(picker.IsOpen);
        Assert.True(picker.IsCustomExpanded);
        Click(host, dialog, picker.PopupComposer["palette-6"].Bounds);
        Assert.Equal("#7EAFE0", Draft(dialog).CustomColorHex);
        Assert.Null(saved);
        Assert.Null(defaults);
        Assert.Empty(source.CustomColorHex);
        Assert.All(Enumerable.Range(0, 5), index => Assert.False(dialog.SingleComposer.GetColorListPicker("color-" + index).On));
        picker.PopupComposer.GetTextInput("hex").SetValue("#a153cf");
        Assert.Equal("#A153CF", Draft(dialog).CustomColorHex);
        Assert.True(dialog.OnEscapePressed());
        Click(host, dialog, dialog.SingleComposer["setdefaults"].Bounds);
        Assert.Equal("#A153CF", defaults?.CustomColorHex);
        Assert.Null(saved);
        Click(host, dialog, dialog.SingleComposer["save"].Bounds);
        Assert.Equal("#A153CF", saved?.CustomColorHex);
        Assert.Equal(SceneMarkerColor.Blue, saved?.Color);
        Assert.Empty(source.CustomColorHex);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0, false)]
    [InlineData(1.25, false)]
    [InlineData(1.0, true)]
    [InlineData(1.25, true)]
    public void OriginalRestoresPresetOrCustomModeEvenWhenRgbIsUnchanged(double scale, bool custom)
    {
        using var host = Host(scale);
        var source = Data();
        if (custom) source.CustomColorHex = SceneMarkerVisuals.ColorHex(source);
        using var dialog = Dialog(host, source);
        var snapshot = Snapshot(dialog);
        if (custom)
        {
            Click(host, dialog, dialog.SingleComposer["color-2"].Bounds);
            Assert.Empty(Draft(dialog).CustomColorHex);
        }
        Open(host, dialog);
        if (!custom) Click(host, dialog, Picker(dialog).PopupComposer["palette-6"].Bounds);
        Click(host, dialog, Picker(dialog).PopupComposer["original"].Bounds);
        Assert.Equal(source.CustomColorHex, Draft(dialog).CustomColorHex);
        Assert.Equal(source.Color, Draft(dialog).Color);
        Assert.Equal(snapshot, Snapshot(dialog));
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void EscapeOutsideClickPresetAndRecompositionKeepTheCorrectDraft(double scale)
    {
        using var host = Host(scale);
        using var dialog = Dialog(host, Data());
        var customButton = dialog.SingleComposer["color-custom"];
        dialog.SingleComposer.FocusElement(customButton.TabIndex);
        dialog.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter });
        var picker = Picker(dialog);
        Assert.True(picker.IsOpen);
        picker.PopupComposer.GetTextInput("hex").SetValue("#A153CF");
        dialog.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        Assert.False(picker.IsOpen);
        Assert.True(dialog.IsOpened());
        Assert.Equal("#A153CF", Draft(dialog).CustomColorHex);
        Open(host, dialog);
        Click(host, dialog, ElementBounds.Fixed(0, 0, 1, 1).WithEmptyParent());
        Assert.False(picker.IsOpen);
        Assert.Equal("#A153CF", Draft(dialog).CustomColorHex);
        var draft = Draft(dialog);
        draft.Display = SceneDescriptionDisplay.AlwaysNearby;
        Invoke(dialog, "Compose", draft);
        Assert.False(picker.Focusable);
        Assert.Equal("#A153CF", Draft(dialog).CustomColorHex);
        Open(host, dialog);
        Click(host, dialog, Picker(dialog).PopupComposer["original"].Bounds);
        Assert.Empty(Draft(dialog).CustomColorHex);
        dialog.OnEscapePressed();
        Click(host, dialog, dialog.SingleComposer["color-3"].Bounds);
        Assert.Equal(SceneMarkerColor.Green, Draft(dialog).Color);
        Assert.Empty(Draft(dialog).CustomColorHex);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LockedCustomMarkerCannotOpenOrChangeColor(double scale)
    {
        using var host = Host(scale);
        var source = Data();
        source.CustomColorHex = "#A153CF";
        source.LockItemCode = "game:padlock";
        using var dialog = Dialog(host, source);
        Assert.False(dialog.SingleComposer.GetButton("color-custom").Enabled);
        Assert.False(Picker(dialog).Enabled);
        Click(host, dialog, dialog.SingleComposer["color-custom"].Bounds);
        Click(host, dialog, dialog.SingleComposer["custom-color-picker"].Bounds);
        Assert.False(Picker(dialog).IsOpen);
        Assert.Equal("#A153CF", Draft(dialog).CustomColorHex);
        Assert.Empty(host.UnsupportedCalls);
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);

    private static SceneDescriptionData Data() => new() { Title = "The old watchtower", Body = "Wind whistles through the broken stonework.", Color = SceneMarkerColor.Blue };

    private static SceneDescriptionDialog Dialog(PreviewHost host, SceneDescriptionData source,
        Action<SceneDescriptionData>? save = null, Action<SceneDescriptionData>? defaults = null)
    {
        var dialog = new SceneDescriptionDialog(host.Api, source, true, (data, _) => save?.Invoke(data),
            null!, null!, null!, data => defaults?.Invoke(data));
        dialog.TryOpen();
        return dialog;
    }

    private static void Open(PreviewHost host, SceneDescriptionDialog dialog)
    {
        Click(host, dialog, dialog.SingleComposer["color-custom"].Bounds);
        Assert.True(Picker(dialog).IsOpen);
    }

    private static GuiElementColorPicker Picker(SceneDescriptionDialog dialog) => Assert.IsType<GuiElementColorPicker>(dialog.SingleComposer["custom-color-picker"]);
    private static SceneDescriptionData Draft(SceneDescriptionDialog dialog) => (SceneDescriptionData)Invoke(dialog, "BuildDraft")!;
    private static string Snapshot(SceneDescriptionDialog dialog) => (string)Invoke(dialog, "Snapshot")!;
    private static object? Invoke(SceneDescriptionDialog dialog, string method, params object[] args) => typeof(SceneDescriptionDialog)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, args);

    private static void Click(PreviewHost host, SceneDescriptionDialog dialog, ElementBounds bounds)
    {
        var x = (int)(bounds.renderX + bounds.InnerWidth / 2);
        var y = (int)(bounds.renderY + bounds.InnerHeight / 2);
        host.SetMouse(x, y);
        dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
}
