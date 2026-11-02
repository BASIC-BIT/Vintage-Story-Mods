using System.Collections;
using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class ConfigAdminColorPickerTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ColorChipsKeepRawInputsAndOriginalValuesThroughRecomposition(double scale)
    {
        using var host = Host(scale);
        var values = Values();
        var changes = new List<(string, string)>();
        using var dialog = Dialog(host, values, changes);
        changes.Clear();
        Assert.Equal(new[] { "EmoteColor", "GlobalOOCColor", "NametagBackgroundColor", "NametagBorderColor", "OOCColor" }, Pickers(dialog).Keys.Cast<string>().Order());
        var picker = Picker(dialog, "EmoteColor");
        Assert.True(dialog.JumpTo("EmoteColor"));
        Open(picker);
        Click(host, dialog, Popup(picker)["palette-6"].Bounds);
        Assert.Equal("#7EAFE0", values["EmoteColor"]);
        Assert.Equal("#7EAFE0", Input(dialog, "EmoteColor").GetText());
        Click(host, dialog, Popup(picker)["original"].Bounds);
        Assert.Equal("navy", values["EmoteColor"]);
        Assert.Equal("navy", Input(dialog, "EmoteColor").GetText());
        Input(dialog, "EmoteColor").SetValue("custom.vtml");
        Assert.Equal("custom.vtml", values["EmoteColor"]);
        dialog.JumpTo("GlobalOOCColor");
        var global = Picker(dialog, "GlobalOOCColor");
        Open(global);
        Click(host, dialog, Popup(global)["palette-6"].Bounds);
        Click(host, dialog, Popup(global)["original"].Bounds);
        Assert.Equal("#abcdef", values["GlobalOOCColor"]);
        dialog.JumpTo("NametagBackgroundColor");
        var background = Picker(dialog, "NametagBackgroundColor");
        Open(background);
        Click(host, dialog, Popup(background)["palette-6"].Bounds);
        Assert.Equal("#7EAFE0", values["NametagBackgroundColor"]);
        Click(host, dialog, Popup(background)["theme"].Bounds);
        Assert.Equal("", values["NametagBackgroundColor"]);
        dialog.Recompose();
        Assert.Equal("custom.vtml", Input(dialog, "EmoteColor").GetText());
        Assert.Equal("", values["NametagBackgroundColor"]);
        Assert.Equal("#12345678", values["NametagBorderColor"]);
        Assert.Equal("Saving soon.", Input(dialog, "TEXT_ServerSaveAnnouncement").GetText());
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PopupReceivesInputOutsideScrollClipAndScrollDismissesWithoutEdits(double scale)
    {
        using var host = Host(scale);
        var values = Values();
        var changes = new List<(string, string)>();
        using var dialog = Dialog(host, values, changes);
        changes.Clear();
        dialog.JumpTo("NametagBorderColor");
        var picker = Picker(dialog, "NametagBorderColor");
        Open(picker);
        picker.GetType().GetMethod("ExpandCustom")!.Invoke(picker, null);
        var spectrum = Popup(picker)["spectrum"].Bounds;
        Click(host, dialog, spectrum);
        Assert.NotEqual("#12345678", values["NametagBorderColor"]);
        Assert.EndsWith("78", values["NametagBorderColor"]);
        changes.Clear();
        dialog.SingleComposer.GetScrollbar("settings-scrollbar").ScrollToBottom();
        Assert.False(IsOpen(picker));
        Assert.Empty(changes);
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void OffscreenColorControlsCannotOpenAndEscapeClosesPopupBeforeDialog(double scale)
    {
        using var host = Host(scale);
        var changes = new List<(string, string)>();
        using var dialog = Dialog(host, Values(), changes);
        changes.Clear();
        dialog.JumpTo("EmoteColor");
        var picker = Picker(dialog, "EmoteColor");
        Click(host, dialog, picker.Bounds);
        Assert.True(IsOpen(picker));
        Assert.True(dialog.OnEscapePressed());
        Assert.False(IsOpen(picker));
        dialog.SingleComposer.GetScrollbar("settings-scrollbar").ScrollToBottom();
        Click(host, dialog, picker.Bounds);
        Assert.False(IsOpen(picker));
        Assert.Empty(changes);
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);

    private static Dictionary<string, string> Values() => new()
    {
        ["EmoteColor"] = "navy",
        ["OOCColor"] = "#112233",
        ["GlobalOOCColor"] = "#abcdef",
        ["NametagBackgroundColor"] = "",
        ["NametagBorderColor"] = "#12345678",
        ["TEXT_ServerSaveAnnouncement"] = "Saving soon."
    };

    private static ScrollableConfigAdminDialog Dialog(PreviewHost host, Dictionary<string, string> values, List<(string, string)> changes)
    {
        var rows = new List<DialogRow>
        {
            new(new DialogElement { Code = "group-colors", Type = EnumDialogElementType.Text, Text = "Colors", Width = 720, Height = 30 })
        };
        rows.AddRange(values.Keys.Select(key => new DialogRow(new DialogElement { Code = key, Type = EnumDialogElementType.Input, Label = key, Width = 720, Height = 28 })));
        rows.AddRange(Enumerable.Range(0, 10).Select(i => new DialogRow(new DialogElement { Code = "filler-" + i, Type = EnumDialogElementType.Input, Label = "Other setting", Width = 720, Height = 28 })));
        rows.Add(new DialogRow(new DialogElement { Code = "save", Type = EnumDialogElementType.Button, Text = "Save", Width = 150, Height = 34 }));
        return new ScrollableConfigAdminDialog(new JsonDialogSettings
        {
            Code = "color-picker-test",
            Alignment = EnumDialogArea.CenterMiddle,
            SizeMultiplier = 0.9,
            Padding = 16,
            OnGet = key => values.GetValueOrDefault(key, string.Empty),
            OnSet = (key, value) => { values[key] = value; changes.Add((key, value)); },
            Rows = rows.ToArray()
        }, host.Api, false);
    }

    private static IDictionary Pickers(ScrollableConfigAdminDialog dialog)
    {
        var field = typeof(ScrollableConfigAdminDialog).GetField("_colorPickers", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<IDictionary>(field.GetValue(dialog));
    }
    private static GuiElement Picker(ScrollableConfigAdminDialog dialog, string key) => Assert.IsAssignableFrom<GuiElement>(Pickers(dialog)[key]);
    private static GuiComposer Popup(GuiElement picker) => (GuiComposer)picker.GetType().GetProperty("PopupComposer")!.GetValue(picker)!;
    private static bool IsOpen(GuiElement picker) => (bool)picker.GetType().GetProperty("IsOpen")!.GetValue(picker)!;
    private static void Open(GuiElement picker) => picker.GetType().GetMethod("Open")!.Invoke(picker, null);
    private static GuiElementTextInput Input(ScrollableConfigAdminDialog dialog, string key)
    {
        var controls = (Dictionary<string, (string Control, double Y)>)typeof(ScrollableConfigAdminDialog).GetField("_controls", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
        var wrapper = dialog.SingleComposer.GetElement(controls[key].Control);
        return Assert.IsType<GuiElementTextInput>(wrapper.GetType().GetField("_widget", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wrapper));
    }
    private static void Click(PreviewHost host, GuiDialog dialog, ElementBounds bounds)
    {
        int x = (int)(bounds.renderX + bounds.InnerWidth / 2), y = (int)(bounds.renderY + bounds.InnerHeight / 2);
        host.SetMouse(x, y);
        dialog.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        dialog.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
    }
}
