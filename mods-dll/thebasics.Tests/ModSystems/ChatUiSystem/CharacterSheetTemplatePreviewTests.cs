using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using System.Reflection;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class CharacterSheetTemplatePreviewTests
{
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void Template_selects_actual_cells_without_answer_edits_or_save(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1200, scale);
        var view = new CharacterSheetViewMessage { CanEdit = true, Fields = Enumerable.Range(0, 14).Select(i => new CharacterSheetFieldViewMessage
        {
            FieldId = "field-" + i, Label = "Field " + i, CanEdit = true, Value = i == 5 ? "2" : "Sample", Type = i == 3 ? "option" : i == 4 ? "longstring" : i == 5 ? "number" : "string",
            Options = i == 3 ? new List<string> { "Sample", "Other" } : new List<string>(), EditorRows = 2,
            Width = i is 1 or 2 ? CharacterSheetFieldWidths.Half : CharacterSheetFieldWidths.Full,
            LayoutSection = i == 0 ? CharacterSheetLayoutSections.HeaderSide : CharacterSheetLayoutSections.Body
        }).ToList() };
        int saves = 0;
        string? selected = null;
        using var dialog = new CharacterSheetDialog(host.Api, view, _ => saves++);
        dialog.ConfigureTemplatePreview(ElementBounds.Fixed(20, 20, 520, 600), id => selected = id, (_, _) => { });
        Assert.Null(dialog.TemplatePreviewComposer.GetElement("saveButton"));
        var offscreen = dialog.TemplateSelections["field-13"];
        Assert.False(offscreen.IsPositionInside((int)offscreen.Bounds.absX + 5, (int)offscreen.Bounds.absY + 5));
        foreach (var id in new[] { "field-0", "field-1", "field-2", "field-3", "field-4", "field-5", "field-13" })
        {
            dialog.SelectTemplateField(id, true);
            var cell = dialog.TemplateSelections[id];
            var mouse = new MouseEvent((int)(cell.Bounds.absX + cell.Bounds.OuterWidth / 2), (int)(cell.Bounds.absY + cell.Bounds.OuterHeight / 2), EnumMouseButton.Left, 0);
            dialog.TemplatePreviewComposer.OnMouseDown(mouse);
            Assert.True(mouse.Handled);
            Assert.Equal(id, selected);
            Assert.True(cell.HasFocus);
            dialog.TemplatePreviewComposer.OnKeyPress(new KeyEvent { KeyChar = '!' });
            var key = new KeyEvent { KeyCode = (int)GlKeys.Enter };
            dialog.TemplatePreviewComposer.OnKeyDown(key, true);
            Assert.True(key.Handled);
            Assert.Equal(id, selected);
        }
        dialog.FocusTemplateField("field-0");
        for (var index = 1; index < 14; index++)
        {
            var tab = new KeyEvent { KeyCode = (int)GlKeys.Tab };
            dialog.TemplatePreviewComposer.OnKeyDown(tab, true);
            Assert.True(dialog.TemplateSelections["field-" + index].HasFocus);
            var enter = new KeyEvent { KeyCode = (int)GlKeys.Enter };
            dialog.TemplatePreviewComposer.OnKeyDown(enter, true);
            Assert.Equal("field-" + index, selected);
        }
        var previousScroll = dialog.TemplatePreviewComposer.GetScrollbar("scrollbar").CurrentYPosition;
        dialog.SetView(view);
        Assert.True(dialog.TemplateSelections["field-13"].HasFocus);
        Assert.Equal(previousScroll, dialog.TemplatePreviewComposer.GetScrollbar("scrollbar").CurrentYPosition);
        Assert.Equal(0, saves);
        Assert.Equal(view.Fields.Select(item => item.Value), dialog.TemplateAnswerValues);
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void Template_keeps_production_control_geometry_content_and_normal_draft_lifecycle(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1200, scale);
        var view = new CharacterSheetViewMessage { TargetPlayerUid = "fictional", CanEdit = true, Fields = Enumerable.Range(0, 4).Select(i => new CharacterSheetFieldViewMessage
        {
            FieldId = "id" + i, Label = "Label " + i, Value = "Answer " + i, Type = "string", CanEdit = true,
            LayoutSection = i == 0 ? "header-side" : "body", Width = i is 1 or 2 ? "half" : "full"
        }).ToList() };
        CharacterSheetSaveRequest? saved = null;
        using var normal = new CharacterSheetDialog(host.Api, view, request => saved = request, new HeadshotDialogCallbacks());
        using var template = new CharacterSheetDialog(host.Api, view, _ => throw new Exception("Preview saved answers"));
        template.ConfigureTemplatePreview(ElementBounds.Fixed(0, 0, 520, 800), _ => { }, (_, _) => { });
        Assert.Equal(normal.SingleComposer.Bounds.OuterWidth, template.SingleComposer.Bounds.OuterWidth);
        var scrollbar = template.SingleComposer.GetScrollbar("scrollbar");
        Assert.True(scrollbar.Bounds.absX + scrollbar.Bounds.OuterWidth <= template.SingleComposer.Bounds.absX + template.SingleComposer.Bounds.OuterWidth);
        var controls = typeof(CharacterSheetDialog).GetField("_textInputs", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var actual = (Dictionary<int, GuiElementTextInput>)controls.GetValue(normal)!;
        var preview = (Dictionary<int, GuiElementTextInput>)controls.GetValue(template)!;
        foreach (var index in actual.Keys)
        {
            Assert.Equal(actual[index].GetText(), preview[index].GetText());
            Assert.Equal(actual[index].Bounds.fixedX, preview[index].Bounds.fixedX);
            Assert.Equal(actual[index].Bounds.fixedY - (index == 0 ? GuiStyle.TitleBarHeight + 10 : 0), preview[index].Bounds.fixedY);
            Assert.Equal(actual[index].Bounds.fixedWidth, preview[index].Bounds.fixedWidth);
            Assert.Equal(actual[index].Bounds.fixedHeight, preview[index].Bounds.fixedHeight);
        }
        Assert.True(normal.TryOpen());
        actual[1].SetValue("Unsaved");
        normal.SetView(view);
        actual = (Dictionary<int, GuiElementTextInput>)controls.GetValue(normal)!;
        Assert.Equal("Unsaved", actual[1].GetText());
        var save = typeof(CharacterSheetDialog).GetMethod("OnSave", BindingFlags.Instance | BindingFlags.NonPublic)!;
        save.Invoke(template, null);
        save.Invoke(normal, null);
        Assert.Equal("Unsaved", saved!.Fields.Single(item => item.FieldId == "id1").Value);
        Assert.True(normal.TryCloseWithoutPrompt());
        Assert.True(normal.TryOpen());
        Assert.Equal("Unsaved", actual[1].GetText());
        Assert.Empty(host.UnsupportedCalls);
    }
}
