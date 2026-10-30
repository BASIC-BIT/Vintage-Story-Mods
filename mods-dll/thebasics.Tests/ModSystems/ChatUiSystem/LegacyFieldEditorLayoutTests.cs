using Vintagestory.API.Client;
using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class LegacyFieldEditorLayoutTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void ApplicabilityUpdatesLiveWithoutErasingInactiveValues(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var scene = PreviewScene.Create("sheet-fields", host);
        var dialog = Assert.IsType<CharacterSheetFieldConfigDialog>(scene.Dialog);
        var composer = dialog.SingleComposer;
        void Select(string field, string value)
        {
            var dropdown = composer.GetDropDown("dropdown-" + field);
            dropdown.SetSelectedValue(value);
            var changed = (SelectionChangedDelegate)typeof(GuiElementDropDown).GetField("onSelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dropdown)!;
            changed(value, true);
        }
        foreach (var type in new[] { "option", "longstring", "number", "string", "option" })
        {
            Select("Type", type);
            Assert.Equal(type == "longstring", composer.GetTextInput("input-EditorRows").Enabled);
            Assert.Equal(type == "option", composer.GetTextInput("input-Options").Enabled);
            Assert.Equal(type == "longstring" ? 1d : .38, composer.GetDynamicText("label-EditorRows").Font.Color[3]);
            Assert.Equal(type == "option" ? 1d : .38, composer.GetDynamicText("label-Options").Font.Color[3]);
            Assert.Equal(type != "longstring", composer.GetHoverText("help-EditorRows").Text.StartsWith("Choose Long text"));
            Assert.Equal(type != "option", composer.GetHoverText("help-Options").Text.StartsWith("Choose Options list"));
            var hoverBounds = composer.GetHoverText("help-EditorRows").Bounds;
            var inputBounds = composer.GetTextInput("input-EditorRows").Bounds;
            Assert.True(hoverBounds.absY < inputBounds.absY);
            Assert.True(hoverBounds.absY + hoverBounds.OuterHeight >= inputBounds.absY + inputBounds.OuterHeight);
            Assert.Equal(type == "string", composer.GetDropDown("dropdown-BindTo").Enabled);
            Assert.True(composer.GetTextInput("input-MaxLength").Enabled);
            Assert.Equal("6", composer.GetTextInput("input-EditorRows").GetText());
            Assert.Equal("Scribe, Glassblower, Herbalist", composer.GetTextInput("input-Options").GetText());
        }
        Select("Type", "string");
        Select("BindTo", "thebasics.nickname");
        Select("Type", "number");
        Assert.True(composer.GetDropDown("dropdown-BindTo").Enabled);
        Select("BindTo", "");
        Assert.False(composer.GetDropDown("dropdown-BindTo").Enabled);
        Select("LayoutSection", "header-side");
        Assert.False(composer.GetDropDown("dropdown-Width").Enabled);
        Assert.Equal(.38, composer.GetDynamicText("label-Width").Font.Color[3]);
        Assert.StartsWith("Fields beside the portrait", composer.GetHoverText("help-Width").Text);
        Select("LayoutSection", "body");
        Assert.True(composer.GetDropDown("dropdown-Width").Enabled);
        Assert.Equal(1d, composer.GetDynamicText("label-Width").Font.Color[3]);
        Select("Visibility", "self");
        Assert.False(Assert.IsType<GuiElementSetupToggle>(composer["toggle-ShowInLook"]).Enabled);
        Select("Visibility", "public");
        Assert.True(Assert.IsType<GuiElementSetupToggle>(composer["toggle-ShowInLook"]).Enabled);
        Assert.True(dialog.JumpTo("field2", "Label"));
        Assert.True(dialog.JumpTo("field1", "Label"));
        Assert.Equal("6", dialog.SingleComposer.GetTextInput("input-EditorRows").GetText());
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData("default", 0)]
    [InlineData("on", 1)]
    [InlineData("off", 2)]
    public void PreferencesKeepServerDefaultDistinctFromExplicitChoices(string choice, int expected)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, 1.25);
        var saved = -1;
        using var dialog = new CharacterSheetSettingsDialog(host.Api, true, 0, value => saved = value);
        Assert.Equal("linkselect", dialog.SingleComposer.GetDropDown("autoopen").MouseOverCursor);
        dialog.SingleComposer.GetDropDown("autoopen").SetSelectedValue(choice);
        typeof(CharacterSheetSettingsDialog).GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, null);
        Assert.Equal(expected, saved);
    }

    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void LegacyFieldEditorFitsCompactViewportWithConsistentActionsAndInputCursors(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var dialog = new CharacterSheetFieldConfigDialog(host.Api,
            Enumerable.Range(1, 15).Select(index => new CharacterSheetFieldConfigEntryMessage
            {
                OriginalId = "field" + index,
                Id = "field" + index,
                Label = "Field " + index,
                Description = "A long description shown from its beginning",
                Type = "string"
            }).ToList(),
            _ => { }, () => { }, () => { });
        dialog.TryOpen();
        host.RenderGuiDialog(dialog, 0);
        var composer = dialog.SingleComposer;
        Assert.True(composer.Bounds.absY >= 0);
        Assert.True(composer.Bounds.absY + composer.Bounds.OuterHeight <= 720);
        Assert.True(composer["close"].Bounds.absX < composer["save"].Bounds.absX);
        Assert.True(composer["delete"].Bounds.absY > composer["down"].Bounds.absY);
        Assert.Equal(0, composer.GetTextInput("input-Description").CaretPosWithoutLineBreaks);
        foreach (var key in new[] { "field-selector", "dropdown-Type", "dropdown-Visibility", "dropdown-BindTo", "dropdown-LayoutSection", "dropdown-Width" })
            Assert.Equal("linkselect", composer[key].MouseOverCursor);
        var summary = Assert.IsType<List<(string Text, bool IsSelected)>>(typeof(CharacterSheetFieldConfigDialog)
            .GetMethod("BuildOrderSummaryLines", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, null));
        Assert.Equal(10, summary.Count);
        Assert.True(dialog.JumpTo("field15", "Description"));
        Assert.True(dialog.SingleComposer["input-Description"].HasFocus);
        Assert.Empty(host.UnsupportedCalls);
    }
}

