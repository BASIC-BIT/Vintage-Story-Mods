using TheBasics.GuiPreview;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardSheetEditorTests
{
    [VisualTheory]
    [InlineData(0, 2)]
    [InlineData(4, 4)]
    public void Preview_uses_current_draft_nickname_limit_and_field_override(int fieldLimit, int expected)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1800, 1200, 1);
        var draft = new SetupWizardDraft(new[] { new ConfigAdminSettingValue { Key = "MaxNicknameLength", Value = "100" } }, fields: new[] { Field("nickname", "body") with { BindTo = "thebasics.nickname", MaxLength = fieldLimit.ToString() } });
        using var editor = new SetupWizardSheetEditor(host.Api, draft, null, action => action(), host.CreateGuide);
        using var owner = host.Api.Gui.CreateCompo("nickname-limit", ElementBounds.Fixed(0, 0, 1100, 900));
        editor.SetAvailableHeight(650); editor.Add(owner, 1000, false); owner.Compose(focusFirstElement: false);
        draft.Set("MaxNicknameLength", "2"); editor.Refresh();
        var field = Assert.Single(editor.Preview!.View.Fields);
        Assert.Equal(expected, field.MaxLength);
        Assert.Equal(fieldLimit == 0 ? "Pi" : "Pip", field.Value);
        Assert.Empty(host.UnsupportedCalls);
    }

    [Fact]
    public void Movement_is_section_relative_and_placement_appends()
    {
        var fields = new[] { Field("a", "body"), Field("h", "header-side"), Field("b", "body"), Field("c", "body") };
        var draft = new SetupWizardDraft(Array.Empty<ConfigAdminSettingValue>(), fields: fields);
        using var editor = new SetupWizardSheetEditor(null!, draft, null, action => action());
        editor.SelectField("b"); editor.MoveSelected(-1);
        Assert.Equal(new[] { "b", "a", "h", "c" }, draft.SheetFields.Select(f => f.Id));
        editor.SetPlacement("header-side");
        Assert.Equal(new[] { "a", "h", "b", "c" }, draft.SheetFields.Select(f => f.Id));
        Assert.Equal("b", editor.SelectedId);
    }
    [Fact]
    public void New_identity_is_stable_and_removal_requires_confirmation()
    {
        var draft = new SetupWizardDraft(Array.Empty<ConfigAdminSettingValue>(), fields: new[] { Field("a", "body") });
        using var editor = new SetupWizardSheetEditor(null!, draft, null, action => action());
        editor.AddField("longstring"); var id = editor.SelectedId;
        Assert.StartsWith("field-", id); Assert.False(editor.RemoveSelected(false));
        Assert.Equal(2, draft.SheetFields.Count); Assert.True(editor.RemoveSelected(true));
        Assert.Single(draft.SheetFields); Assert.False(editor.RemoveSelected(true));
    }
    [VisualTheory]
    [InlineData(1d, 849d)]
    [InlineData(1d, 850d)]
    [InlineData(1.25d, 849d)]
    [InlineData(1.25d, 850d)]
    public void Native_selection_typing_drawer_and_returning_inspector(double scale, double width)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1800, 1200, scale);
        var fields = Enumerable.Range(0, 18).Select(i => Field("f" + i, "body")).ToList();
        fields.Add(Field("staff", "body") with { Visibility = "admin" });
        var draft = new SetupWizardDraft([], fields: fields);
        Action? pending = null; var tracks = new List<string>();
        using var editor = new SetupWizardSheetEditor(host.Api, draft, null, action => pending = action, host.CreateGuide, tracks.Add);
        GuiComposer? owner = null;
        Compose();
        try
        {
            var preview = editor.Preview!;
            Assert.Equal(550 * scale, preview.Bounds.OuterWidth, 1);
            var cell = preview.Dialog.TemplateSelections["f0"];
            Click(cell.Bounds);
            Assert.NotNull(pending); pending!(); pending = null; Compose();
            Assert.Equal("f0", editor.SelectedId);
            Assert.True(editor.InspectorVisible);
            var label = owner!.GetTextInput("sheet-editor-Label");
            owner.FocusElement(label.TabIndex);
            label.SetValue("Changed label");
            Assert.Equal("Changed label", draft.SheetFields[0].Label);
            Assert.Same(label, owner.GetTextInput("sheet-editor-Label"));
            Assert.True(label.HasFocus);
            Assert.Null(pending);
            owner.FocusElement(owner.GetElement("sheet-editor-advanced").TabIndex);
            Click(owner.GetElement("sheet-editor-advanced").Bounds); pending!(); pending = null; Compose();
            label = owner.GetTextInput("sheet-editor-Label");
            var scroll = owner.GetScrollbar("sheet-editor-inspector-scroll");
            owner.FocusElement(scroll.TabIndex);
            var end = new KeyEvent { KeyCode = (int)GlKeys.End };
            owner.OnKeyDown(end, true);
            Assert.NotNull(owner.GetTextInput("sheet-editor-Id"));
            Assert.True(end.Handled, $"Scrollbar focusable={scroll.Focusable}, focused={scroll.HasFocus}, viewport={scroll.Bounds.fixedHeight}, idY={owner.GetTextInput("sheet-editor-Id").Bounds.fixedY}"); Assert.True(scroll.CurrentYPosition > 0);
            owner.FocusElement(label.TabIndex);
            Assert.Equal(0, scroll.CurrentYPosition);
            if (width < 850)
            {
                Click(owner.GetElement("sheet-editor-back").Bounds); pending!(); pending = null; Compose();
                Assert.False(editor.InspectorVisible); Assert.Equal("f0", editor.SelectedId);
            }
            Click(owner.GetElement("sheet-editor-all").Bounds); pending!(); pending = null; Compose();
            Assert.NotNull(owner.GetButton("sheet-editor-field-18"));
            // Drawer choices use the same selection path for staff-only fields.
            editor.SelectField("staff"); Compose(); Assert.True(editor.SelectionHidden);
            if (width < 850) { Click(owner.GetElement("sheet-editor-back").Bounds); pending!(); pending = null; Compose(); }
            Assert.True(editor.SelectionHidden);
            Assert.DoesNotContain(editor.Preview!.View.Fields, f => f.FieldId == "staff");
            Assert.All(tracks, value => Assert.Contains(value, new[] { "sheet_select" }));
            editor.Dispose();
            var oldLabel = draft.SheetFields[0].Label;
            label.SetValue("stale callback");
            Assert.Equal(oldLabel, draft.SheetFields[0].Label);
            Assert.Empty(host.UnsupportedCalls);
        }
        finally { owner?.Dispose(); }
        void Compose()
        {
            editor.Detach(); owner?.Dispose();
            owner = host.Api.Gui.CreateCompo("sheet-editor-test", ElementBounds.Fixed(10, 10, width, 1000));
            editor.SetAvailableHeight(440); editor.Add(owner, width, false); owner.Compose(focusFirstElement: false);
        }
        void Click(ElementBounds bounds)
        {
            var x = (int)(bounds.renderX + bounds.InnerWidth / 2); var y = (int)(bounds.renderY + bounds.InnerHeight / 2);
            host.SetMouse(x, y); owner!.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0)); owner.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
    }
    internal static CharacterSheetFieldConfigEntryMessage Field(string id, string section) => new() { Id = id, OriginalId = id, Label = id, Type = "string", Description = "", Options = "", BindTo = "", LayoutSection = section, Width = "full", Visibility = "public", MaxLength = "0", EditorRows = "0" };
}

