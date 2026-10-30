using TheBasics.GuiPreview;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.ModSystems.CharacterSheets.Models;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardSheetPreviewTests
{
    [VisualTheory]
    [InlineData(1d)]
    public void Unfinished_half_field_keeps_draft_order_and_pairing(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1200, scale);
        var unfinished = CharacterSheetFieldConfigAdmin.CreateBuilderEntry(); unfinished.Width = "half";
        var fields = new[] { unfinished, Field("first", "First", "public") with { Width = "half" }, Field("last", "Last", "public") };
        using var preview = new SetupWizardSheetPreview(host.Api, ElementBounds.Fixed(10, 10, SetupWizardSheetPreview.RequiredWidth, 600), fields, null, (_, _) => new Actor());
        Assert.DoesNotContain(preview.View.Fields, f => f.FieldId == unfinished.Id);
        Assert.Equal(new[] { unfinished.Id, "first", "last" }, preview.Dialog.TemplateSelections.Keys);
        var blank = preview.Dialog.TemplateSelections[unfinished.Id].Bounds;
        var first = preview.Dialog.TemplateSelections["first"].Bounds;
        Assert.Equal(blank.fixedY, first.fixedY); Assert.True(blank.fixedX < first.fixedX);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void Owning_composer_can_tab_into_through_and_out_of_preview_in_both_directions(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1200, scale);
        var fields = new List<CharacterSheetFieldConfigEntryMessage>
        {
            Field("header", "Name", "public") with { LayoutSection = "header-side" },
            Field("first", "First", "public"), Field("last", "Last", "public")
        };
        string? selected = null;
        using var preview = new SetupWizardSheetPreview(host.Api, ElementBounds.Fixed(0, 40, SetupWizardSheetPreview.RequiredWidth, 600), fields, id => selected = id, (_, _) => new Actor());
        using var owner = host.Api.Gui.CreateCompo("sheet-keyboard-owner", ElementBounds.Fixed(10, 10, 600, 720))
            .AddSmallButton("Before", () => true, ElementBounds.Fixed(0, 0, 100, 30), key: "before")
            .AddInteractiveElement(preview, "sheet")
            .AddSmallButton("After", () => true, ElementBounds.Fixed(0, 650, 100, 30), key: "after")
            .Compose(focusFirstElement: false);
        owner.FocusElement(owner.GetElement("before").TabIndex);
        Tab(false);
        Activate("header");
        Tab(false);
        Activate("first");
        Tab(false);
        Activate("last");
        Tab(false);
        Assert.True(owner.GetElement("after").HasFocus);
        Tab(true);
        Activate("last");
        Tab(true);
        Activate("first");
        Tab(true);
        Activate("header");
        Tab(true);
        Assert.True(owner.GetElement("before").HasFocus);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);

        void Tab(bool reverse)
        {
            var key = new KeyEvent { KeyCode = (int)GlKeys.Tab, ShiftPressed = reverse };
            owner.OnKeyDown(key, true);
            Assert.True(key.Handled);
        }
        void Activate(string id)
        {
            Assert.True(preview.HasFocus);
            Assert.True(preview.Dialog.TemplateSelections[id].HasFocus);
            var enter = new KeyEvent { KeyCode = (int)GlKeys.Enter };
            owner.OnKeyDown(enter, true);
            Assert.True(enter.Handled);
            Assert.Equal(id, selected);
        }
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void Contexts_preserve_hidden_selection_and_invalid_drafts_keep_last_valid_snapshot(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1200, scale);
        var fields = new List<CharacterSheetFieldConfigEntryMessage>
        {
            Field("public", "Public", "public"), Field("private", "Private", "self"), Field("nearby", "Nearby", "nearby"), Field("staff", "Staff", "admin")
        };
        using var preview = new SetupWizardSheetPreview(host.Api, ElementBounds.Fixed(10, 10, SetupWizardSheetPreview.RequiredWidth, 600), fields, null, host.CreateGuide);
        preview.Refresh(fields, "private", SheetPreviewContext.Player, true);
        Assert.Equal(new[] { "public", "private", "nearby" }, preview.View.Fields.Select(f => f.FieldId));
        preview.Refresh(fields, "private", SheetPreviewContext.OtherPlayer, true);
        Assert.Equal("private", preview.SelectedId);
        Assert.True(preview.SelectionHidden);
        Assert.Single(preview.View.Fields);
        preview.Refresh(fields, "private", SheetPreviewContext.Staff, false);
        Assert.Equal(4, preview.View.Fields.Count);
        Assert.IsType<SetupWizardActorLayer>(preview.Composer.GetElement("template-portrait"));
        Assert.True(preview.IsReady);
        preview.RenderInteractiveElements(0);
        Assert.NotEmpty(host.AssetFiles);
        Assert.All(preview.View.Fields, f => Assert.Empty(f.Value));
        Assert.All(preview.Dialog.TemplateAnswerValues, value => Assert.Empty(value));
        fields[0].Type = CharacterSheetFieldTypes.Option;
        fields[0].Options = "";
        preview.Refresh(fields, "private", SheetPreviewContext.Staff, true);
        Assert.True(preview.ShowingLastValid);
        Assert.Equal(CharacterSheetFieldTypes.String, preview.View.Fields[0].Type);
        var unfinished = CharacterSheetFieldConfigAdmin.CreateBuilderEntry();
        fields[0].Type = CharacterSheetFieldTypes.String;
        fields.Add(unfinished);
        preview.Refresh(fields, unfinished.Id, SheetPreviewContext.Player, true);
        Assert.DoesNotContain(preview.View.Fields, f => f.FieldId == unfinished.Id);
        Assert.Contains(unfinished.Id, preview.Dialog.TemplateSelections.Keys);
        preview.Refresh(fields.Take(4).Select(f => f with { Visibility = "admin" }).ToList(), "private", SheetPreviewContext.OtherPlayer, true);
        Assert.Empty(preview.View.Fields);
        Assert.True(preview.SelectionHidden);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void Inspection_uses_actual_chat_and_local_type_samples_obey_limits(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1200, scale);
        var fields = new List<CharacterSheetFieldConfigEntryMessage> { Field("choice", "Choice", "public") with { Type = "option", Options = "First,Second" }, Field("number", "Number", "nearby") with { Type = "number", MaxLength = "1" } };
        using var preview = new SetupWizardSheetPreview(host.Api, ElementBounds.Fixed(10, 10, SetupWizardSheetPreview.RequiredWidth, 600), fields, null, (_, _) => new Actor());
        preview.Refresh(fields, "choice", SheetPreviewContext.NearbyInspection, true);
        Assert.NotNull(preview.Composer.GetElement("preview-chat-input"));
        Assert.Null(preview.Composer.GetElement("scrollBody"));
        Assert.Equal("First", preview.View.Fields[0].Value);
        Assert.Equal("2", preview.View.Fields[1].Value);
        Assert.Contains("Choice: First", preview.InspectionText);
        Assert.Empty(host.UnsupportedCalls);
        Assert.Empty(host.RecordedEffects);
    }

    private static CharacterSheetFieldConfigEntryMessage Field(string id, string label, string visibility) => new()
    { Id = id, Label = label, Type = "string", Optional = true, Visibility = visibility, ShowInLook = true, LayoutSection = "body", Width = "full" };

    private sealed class Actor : ISetupGuidePreview
    {
        public bool IsReady => true;
        public void PlayAnimation(string code) { }
        public void SetAnimation(string code, double seconds) { }
        public void BeginDrag(double x) { }
        public void DragTo(double x) { }
        public void EndDrag(bool allowMomentum = true) { }
        public void Render(float dt, ElementBounds bounds, float opacity = 1, float yawOffset = 0) { }
        public void Dispose() { }
    }
}
