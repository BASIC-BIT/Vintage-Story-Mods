using System.Reflection;
using TheBasics.GuiPreview;
using thebasics.Gui;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.CharacterSheets.Models;
using Vintagestory.API.Client;
using Xunit;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class CharacterSheetTextTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void Long_fields_accept_more_than_visible_rows_and_scroll_to_caret(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        CharacterSheetSaveRequest? saved = null;
        using var dialog = new CharacterSheetDialog(host.Api, new CharacterSheetViewMessage
        {
            CanEdit = true,
            Fields = new[] { "Appearance", "Background" }.Select(id => new CharacterSheetFieldViewMessage
            {
                FieldId = id, Label = id, Type = CharacterSheetFieldTypes.LongString,
                CanEdit = true, EditorRows = 6, MaxLength = 2000
            }).ToList()
        }, request => saved = request);
        var areas = (Dictionary<int, GuiElementTextArea>)typeof(CharacterSheetDialog)
            .GetField("_textAreas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!;
        foreach (var area in areas.Values)
        {
            var visibleHeight = area.Bounds.ParentBounds.fixedHeight;
            var text = string.Join('\n', Enumerable.Range(1, 12).Select(i => "Line " + i));
            area.SetValue(text);
            Assert.Equal(text, area.GetText());
            area.OnFocusGained();
            area.OnKeyPress(host.Api, new KeyEvent { KeyChar = '!' });
            Assert.Equal(text + "!", area.GetText());
            var scrollable = Assert.IsAssignableFrom<ScrollableTextArea>(area);
            Assert.NotNull(scrollable.Scrollbar);
            Assert.True(scrollable.Scrollbar.CurrentYPosition > 0);
            Assert.Equal(visibleHeight, area.Bounds.ParentBounds.fixedHeight);
            Assert.False(area.IsPositionInside((int)area.Bounds.absX, (int)(area.Bounds.ParentBounds.absY - 1)));
            Assert.Equal("textselect", area.MouseOverCursor);
        }
        host.RenderGuiDialog(dialog, 0);
        Assert.Empty(host.UnsupportedCalls);
        typeof(CharacterSheetDialog).GetMethod("OnSave", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, null);
        Assert.NotNull(saved);
        Assert.All(saved.Fields, field => Assert.EndsWith("Line 12!", field.Value));
    }
}
