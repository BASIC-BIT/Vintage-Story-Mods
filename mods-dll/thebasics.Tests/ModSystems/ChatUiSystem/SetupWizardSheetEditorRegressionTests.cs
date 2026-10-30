using TheBasics.GuiPreview;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardSheetEditorRegressionTests
{
    [Theory]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("option")]
    public void Inactive_height_stays_in_draft_but_does_not_block_type_change_save(string type)
    {
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        var original = CharacterSheetFieldConfigAdmin.BuildEntries(config);
        original[0].BindTo = ""; original[0].Type = "longstring"; original[0].EditorRows = "6";
        Assert.True(CharacterSheetFieldConfigAdmin.TryApplyEntries(config, original, out _));
        var draft = new SetupWizardDraft([], fields: CharacterSheetFieldConfigAdmin.BuildEntries(config));
        var field = draft.SheetFields[0]; field.Type = type; field.Options = "One,Two";
        field.Type = "longstring"; Assert.Equal("6", field.EditorRows); field.Type = type;
        var request = draft.CreateSaveRequest(1, "run");
        Assert.Equal("6", field.EditorRows);
        Assert.Equal("0", request.SheetFields[0].EditorRows);
        Assert.True(new SetupWizardState(config).TryValidatePatch(config, request, out var saved, out _, out _, out var errors), string.Join(" ", errors));
        Assert.Equal(0, saved.CharacterSheetFields[0].EditorRows);
        Assert.Equal("6", field.EditorRows);
    }
    [VisualTheory]
    [InlineData(850d)]
    [InlineData(849d)]
    public void Invalid_structural_choice_keeps_last_valid_template_after_context_and_compact_roundtrip(double width)
    {
        using var f = new Fixture(width, 1);
        var prior = f.Editor.Preview!.View.Fields.Select(v => (v.FieldId, v.Label, v.Value)).ToArray();
        f.SelectFirst(); f.Click("sheet-editor-option-add"); f.Flush();
        if (width < 850) { f.Click("sheet-editor-back"); f.Flush(); }
        Assert.True(f.Editor.Preview!.ShowingLastValid);
        Assert.Equal(prior, f.Editor.Preview.View.Fields.Select(v => (v.FieldId, v.Label, v.Value)));
        var context = f.Owner.GetDropDown("sheet-editor-context"); f.Owner.FocusElement(context.TabIndex);
        f.Owner.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.End }, true); f.Flush();
        Assert.True(f.Editor.Preview!.ShowingLastValid);
        Assert.Equal(prior, f.Editor.Preview.View.Fields.Select(v => (v.FieldId, v.Label, v.Value)));
    }
    [VisualTheory]
    [InlineData(1d)]
    public void Compact_structural_choice_errors_are_immediate_and_beside_the_affected_row(double scale)
    {
        using var f = new Fixture(849, scale);
        f.SelectFirst(); f.Click("sheet-editor-option-add"); f.Flush();
        Assert.Contains("Choices must not be empty", Text(f.Owner.GetRichtext("sheet-editor-errors")));
        Assert.Contains("Choice 3", Text(f.Owner.GetRichtext("sheet-editor-option-error-2")));
        f.Owner.GetTextInput("sheet-editor-Option-2").SetValue("One");
        Assert.Contains("duplicates", Text(f.Owner.GetRichtext("sheet-editor-option-error-2")));
        f.Owner.GetTextInput("sheet-editor-Option-2").SetValue("Three");
        Assert.Empty(Text(f.Owner.GetRichtext("sheet-editor-option-error-2")));
    }
    [VisualTheory]
    [InlineData(850d, 1d)]
    [InlineData(849d, 1d)]
    [InlineData(850d, 1.25d)]
    [InlineData(849d, 1.25d)]
    public void Actual_drawer_selection_reveals_offscreen_field_after_return_to_sheet(double width, double scale)
    {
        using var f = new Fixture(width, scale);
        f.Click("sheet-editor-all"); f.Flush();
        f.Click("sheet-editor-field-17"); f.Flush();
        Assert.Equal("f17", f.Editor.SelectedId);
        if (width < 850) { f.Click("sheet-editor-back"); f.Flush(); }
        var cell = f.Editor.Preview!.Dialog.TemplateSelections["f17"];
        var clip = cell.InsideClipBounds!;
        Assert.True(cell.Bounds.renderY >= clip.renderY - 1);
        Assert.True(cell.Bounds.renderY + cell.Bounds.OuterHeight <= clip.renderY + clip.OuterHeight + 1);
        Assert.True(f.Editor.Preview.Composer.GetScrollbar("scrollbar").CurrentYPosition > 0);
    }
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void Sheet_and_properties_align_and_choice_errors_reflow_without_replacing_inputs(double scale)
    {
        using var f = new Fixture(850, scale);
        f.SelectFirst();
        var input = f.Owner.GetTextInput("sheet-editor-Label");
        var properties = input.InsideClipBounds!;
        Assert.Equal(f.Owner.GetHorizontalTabs("sheet-editor-tabs").Bounds.renderY + 38 * scale, properties.renderY, 1);
        Assert.True(f.Editor.Preview!.Bounds.renderY + 32 * scale >= properties.renderY);
        Assert.Null(f.Owner.GetElement("sheet-editor-staff"));
        var first = f.Owner.GetTextInput("sheet-editor-Option-0");
        var second = f.Owner.GetTextInput("sheet-editor-Option-1");
        var validY = second.Bounds.fixedY;
        Assert.InRange(validY - first.Bounds.fixedY, 50, 65);
        first.SetValue("");
        Assert.Same(first, f.Owner.GetTextInput("sheet-editor-Option-0"));
        Assert.Equal(validY + 28, second.Bounds.fixedY);
        first.SetValue("One");
        Assert.Equal(validY, second.Bounds.fixedY);
        Assert.Same(first, f.Owner.GetTextInput("sheet-editor-Option-0"));
    }
    [VisualTheory]
    [InlineData(850d)]
    [InlineData(849d)]
    public void Disabled_sheet_and_invalid_or_hidden_selection_share_one_status_without_replacing_inputs(double width)
    {
        using var f = new Fixture(width, 1);
        f.SelectFirst();
        var input = f.Owner.GetTextInput("sheet-editor-Label");
        f.Editor.Selected.Visibility = "admin";
        f.Editor.Refresh();
        Assert.Contains("off", Text(f.Owner.GetRichtext("sheet-editor-status")));
        Assert.Contains("hidden", Text(f.Owner.GetRichtext("sheet-editor-status")));
        Assert.Null(f.Owner.GetElement("sheet-editor-preview-status"));
        Assert.Same(input, f.Owner.GetTextInput("sheet-editor-Label"));
        f.Click("sheet-editor-option-add"); f.Flush();
        input = f.Owner.GetTextInput("sheet-editor-Option-2");
        input.SetValue("One");
        var status = Text(f.Owner.GetRichtext("sheet-editor-status"));
        Assert.Contains("off", status);
        Assert.Contains("last valid sheet", status);
        Assert.Contains("hidden", status);
        Assert.Null(f.Owner.GetElement("sheet-editor-preview-status"));
        Assert.Same(input, f.Owner.GetTextInput("sheet-editor-Option-2"));
    }
    [VisualTheory]
    [InlineData(850d)]
    [InlineData(849d)]
    public void Native_tabs_group_field_visibility_and_layout_without_losing_draft_or_identity(double width)
    {
        using var f = new Fixture(width, 1);
        f.SelectFirst();
        var label = f.Owner.GetTextInput("sheet-editor-Label");
        f.Owner.FocusElement(label.TabIndex);
        label.SetValue("Renamed calling");
        Assert.Same(label, f.Owner.GetTextInput("sheet-editor-Label"));
        Assert.True(label.HasFocus);
        Assert.Contains("Renamed calling", Text(f.Owner.GetRichtext("sheet-editor-title")));
        Assert.NotNull(f.Owner.GetTextInput("sheet-editor-Option-0"));
        Assert.Null(f.Owner.GetElement("sheet-editor-Visibility"));
        Assert.Null(f.Owner.GetElement("sheet-editor-Placement"));
        Assert.Null(f.Owner.GetElement("sheet-editor-MaxLength"));
        var required = f.Owner.GetElement("sheet-editor-Required");
        var choice = f.Owner.GetTextInput("sheet-editor-Option-0");
        Assert.True(choice.Bounds.fixedY > required.Bounds.fixedY);
        var remove = f.Owner.GetButton("sheet-editor-remove");
        Assert.True(remove.Bounds.fixedWidth <= 160);
        Assert.Null(remove.InsideClipBounds);
        if (width < 850)
        {
            Assert.Null(f.Owner.GetElement("sheet-editor-context"));
            Assert.Null(f.Owner.GetElement("sheet-editor-filled"));
            Assert.Equal(width - 44, label.Bounds.fixedWidth);
        }
        f.Tab(1);
        Assert.NotNull(f.Owner.GetDropDown("sheet-editor-Visibility"));
        Assert.NotNull(f.Owner.GetElement("sheet-editor-ShowInLook"));
        Assert.Null(f.Owner.GetElement("sheet-editor-Type"));
        Assert.Null(f.Owner.GetElement("sheet-editor-Placement"));
        f.Tab(2);
        Assert.NotNull(f.Owner.GetDropDown("sheet-editor-Placement"));
        Assert.NotNull(f.Owner.GetDropDown("sheet-editor-Width"));
        Assert.NotNull(f.Owner.GetButton("sheet-editor-up"));
        Assert.Null(f.Owner.GetElement("sheet-editor-Visibility"));
        f.Tab(0);
        Assert.Equal("Renamed calling", f.Owner.GetTextInput("sheet-editor-Label").GetText());
        Assert.Equal("One", f.Owner.GetTextInput("sheet-editor-Option-0").GetText());
        f.Click("sheet-editor-advanced"); f.Flush();
        Assert.NotNull(f.Owner.GetTextInput("sheet-editor-MaxLength"));
        Assert.NotNull(f.Owner.GetTextInput("sheet-editor-Description"));
        Assert.NotNull(f.Owner.GetTextInput("sheet-editor-Id"));
        Assert.Equal("f0", f.Editor.SelectedId);
    }
    [VisualTheory]
    [InlineData(850d)]
    public void Discard_is_secondary_in_browse_and_blocked_by_pending_and_unconfirmed_saves(double width)
    {
        using var f = new Fixture(width, 1);
        Assert.Null(f.Owner.GetElement("sheet-editor-reset"));
        f.Click("sheet-editor-all"); f.Flush();
        Assert.False(f.Owner.GetButton("sheet-editor-reset").Enabled);
        f.Editor.SelectField("f0");
        f.Editor.Selected.Label = "Changed";
        f.Editor.Refresh();
        Assert.True(f.Owner.GetButton("sheet-editor-reset").Enabled);
        f.Draft.CreateSaveRequest(1, "test");
        f.Editor.Refresh();
        Assert.False(f.Owner.GetButton("sheet-editor-reset").Enabled);
        f.Draft.MarkSaveTimedOut();
        f.Editor.Refresh();
        Assert.False(f.Owner.GetButton("sheet-editor-reset").Enabled);
    }
    [VisualTheory]
    [InlineData(850d)]
    [InlineData(849d)]
    public void Authoring_subviews_share_a_return_header_and_keep_the_selected_field_draft(double width)
    {
        using var f = new Fixture(width, 1);
        f.SelectFirst();
        f.Owner.GetTextInput("sheet-editor-Label").SetValue("Keep this draft");
        if (width < 850) { f.Click("sheet-editor-back"); f.Flush(); }
        var selected = f.Editor.SelectedId;
        f.Click("sheet-editor-add"); f.Flush();
        var back = f.Owner.GetButton("sheet-editor-back").Bounds;
        Assert.Equal(0, back.fixedX); Assert.Equal(48, back.fixedY);
        Assert.Equal(132, f.Owner.GetButton("sheet-editor-add-0").Bounds.fixedY);
        Assert.True(f.Owner.GetButton("sheet-editor-add-2").Bounds.fixedY > f.Owner.GetButton("sheet-editor-add-0").Bounds.fixedY);
        Assert.Null(f.Owner.GetElement("sheet-editor-add"));
        Assert.Null(f.Owner.GetElement("sheet-editor-all"));
        Assert.Null(f.Owner.GetElement("sheet-editor-context"));
        f.Click("sheet-editor-back"); f.Flush();
        Assert.NotNull(f.Editor.Preview);
        Assert.Equal(selected, f.Editor.SelectedId);
        Assert.Equal("Keep this draft", f.Editor.Selected.Label);
        f.Click("sheet-editor-all"); f.Flush();
        var browseBack = f.Owner.GetButton("sheet-editor-back").Bounds;
        Assert.Equal(back.fixedX, browseBack.fixedX); Assert.Equal(back.fixedY, browseBack.fixedY);
        var first = f.Owner.GetButton("sheet-editor-field-0");
        Assert.Equal(132, first.InsideClipBounds.fixedY);
        Assert.StartsWith("Selected: 1.", first.Text);
        Assert.Null(f.Owner.GetElement("sheet-editor-add"));
        Assert.Null(f.Owner.GetElement("sheet-editor-all"));
        Assert.NotNull(f.Owner.GetButton("sheet-editor-reset"));
        f.Click("sheet-editor-back"); f.Flush();
        Assert.NotNull(f.Editor.Preview);
        Assert.Equal(selected, f.Editor.SelectedId);
        Assert.Equal("Keep this draft", f.Editor.Selected.Label);
    }
    [VisualTheory]
    [InlineData(850d)]
    public void Inspector_starts_unselected_and_live_name_matches_selected_preview(double width)
    {
        using var f = new Fixture(width, 1);
        Assert.Null(f.Editor.SelectedId);
        Assert.Null(f.Editor.Preview!.SelectedId);
        Assert.All(f.Editor.Preview.Dialog.TemplateSelections.Values, cell => Assert.False(cell.Selected));
        Assert.Equal("Choose a field", Text(f.Owner.GetRichtext("sheet-editor-title")));
        Assert.Null(f.Owner.GetElement("sheet-editor-tabs"));
        Assert.Null(f.Owner.GetElement("sheet-editor-Label"));
        Assert.Null(f.Owner.GetElement("sheet-editor-remove"));
        f.SelectFirst();
        Assert.Equal("f0", f.Editor.Preview!.SelectedId);
        Assert.True(f.Editor.Preview.Dialog.TemplateSelections["f0"].Selected);
        Assert.All(f.Editor.Preview.Dialog.TemplateSelections.Values.Where(cell => cell.FieldId != "f0"), cell => Assert.False(cell.Selected));
        var input = f.Owner.GetTextInput("sheet-editor-Label");
        f.Owner.FocusElement(input.TabIndex);
        input.SetValue("<b>Live name & identity</b>");
        Assert.Equal("<b>Live name & identity</b>", Text(f.Owner.GetRichtext("sheet-editor-title")));
        input.SetValue("Live name & identity");
        Assert.Equal("Live name & identity", Text(f.Owner.GetRichtext("sheet-editor-title")));
        Assert.Equal("Live name & identity", f.Editor.Preview.View.Fields.First().Label);
        Assert.True(input.HasFocus);
        Assert.Same(input, f.Owner.GetTextInput("sheet-editor-Label"));
    }
    [Fact]
    public void Removal_clears_selection_and_reset_only_keeps_existing_identity()
    {
        var draft = new SetupWizardDraft([], fields: new[] { SetupWizardSheetEditorTests.Field("a", "body"), SetupWizardSheetEditorTests.Field("b", "body") });
        using var editor = new SetupWizardSheetEditor(null!, draft, null, action => action());
        editor.SelectField("a"); editor.Selected.Label = "Rename"; editor.ResetFields();
        Assert.Equal("a", editor.SelectedId);
        editor.AddField("string"); editor.ResetFields();
        Assert.Null(editor.SelectedId);
        editor.SelectField("a"); Assert.True(editor.RemoveSelected(true));
        Assert.Null(editor.SelectedId);
    }
    private static string Text(GuiElementRichtext? element) => string.Concat(element!.Components.OfType<RichTextComponent>().Select(c => c.DisplayText));
    private sealed class Fixture : IDisposable
    {
        internal PreviewHost Host { get; }
        internal SetupWizardSheetEditor Editor { get; }
        internal SetupWizardDraft Draft { get; }
        internal GuiComposer Owner { get; private set; } = null!;
        private Action? pending;
        private readonly double width;
        internal Fixture(double width, double scale)
        {
            this.width = width;
            Host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1800, 1200, scale);
            var fields = Enumerable.Range(0, 18).Select(i => SetupWizardSheetEditorTests.Field("f" + i, "body")).ToList();
            fields[0].Type = "option"; fields[0].Options = "One,Two";
            Draft = new SetupWizardDraft([], fields: fields);
            Editor = new SetupWizardSheetEditor(Host.Api, Draft, null, action => pending = action, Host.CreateGuide);
            Compose();
        }
        private void Compose()
        {
            Editor.Detach(); Owner?.Dispose();
            Owner = Host.Api.Gui.CreateCompo("editor-regression", ElementBounds.Fixed(10, 10, width, 1000));
            Editor.SetAvailableHeight(600); Editor.Add(Owner, width, false); Owner.Compose(focusFirstElement: false);
        }
        internal void Tab(int index)
        {
            Owner.FocusElement(Owner.GetHorizontalTabs("sheet-editor-tabs").TabIndex);
            while (Editor.InspectorTab != index)
            {
                var key = new KeyEvent { KeyCode = (int)GlKeys.Right };
                Owner.OnKeyDown(key, true);
                Assert.True(key.Handled); Flush();
                Assert.True(Owner.GetHorizontalTabs("sheet-editor-tabs").HasFocus);
            }
        }
        internal void Flush() { Assert.NotNull(pending); var action = pending!; pending = null; action(); Compose(); }
        internal void Click(string key)
        {
            var element = Owner.GetElement(key); Assert.NotNull(element);
            Owner.FocusElement(element.TabIndex);
            Click(element.Bounds);
        }
        private void Click(ElementBounds bounds)
        {
            var x = (int)(bounds.renderX + bounds.InnerWidth / 2); var y = (int)(bounds.renderY + bounds.InnerHeight / 2);
            Host.SetMouse(x, y); Owner.OnMouseDown(new MouseEvent(x, y, EnumMouseButton.Left, 0)); Owner.OnMouseUp(new MouseEvent(x, y, EnumMouseButton.Left, 0));
        }
        internal void SelectFirst() { Click(Editor.Preview!.Dialog.TemplateSelections["f0"].Bounds); Flush(); }
        public void Dispose() { Editor.Dispose(); Owner.Dispose(); Assert.Empty(Host.UnsupportedCalls); Host.Dispose(); }
    }
}
