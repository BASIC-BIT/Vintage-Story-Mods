using System.Text.Json;
using TheBasics.GuiPreview;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class SheetBuilderFixtureTests
{
    public static IEnumerable<object[]> Captures =>
        from scenario in SetupWizardCaptureScenes.Names.Where(n => n.StartsWith("wizard-sheet-builder"))
        from viewport in new[] { (1600, 1000), (1280, 720) }
        from scale in new[] { 1d, 1.25d }
        select new object[] { scenario, viewport.Item1, viewport.Item2, scale };

    [Fact]
    public void Snapshot_is_independent_valid_and_preserves_all_layout_cases()
    {
        var fields = SetupWizardCaptureScenes.SheetFieldsForScenario("wizard-sheet-builder");
        Assert.Empty(CharacterSheetFieldConfigAdmin.ValidateEntries(fields, out _));
        Assert.Empty(SetupWizardState.ValidateSheetChoices(fields));
        Assert.Contains(fields, f => f.LayoutSection == "header-side");
        Assert.Equal(2, fields.Count(f => f.Width == "half"));
        Assert.Single(fields.Where(f => f.Visibility == "admin"));
        fields[0].Label = "Changed";
        Assert.Equal("Known as", SetupWizardCaptureScenes.SheetFieldsForScenario("wizard-sheet-builder")[0].Label);
        Assert.Null(SetupWizardCaptureScenes.SheetFieldsForScenario("wizard-chat-default"));
    }

    [VisualTheory]
    [MemberData(nameof(Captures))]
    public void Production_capture_scenes_drive_native_controls_without_effects(string scenario, int width, int height, double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, width, height, scale);
        using var scene = PreviewScene.Create(scenario, host);
        var wizard = Assert.IsType<SetupWizardDialog>(scene.Dialog);
        Assert.True(wizard.Draft.CanEditSheetFields);
        Assert.Equal(scenario.EndsWith("-review") ? "review" : "characters.builder", wizard.CurrentStepId);
        Assert.False(wizard.Draft.IsSavePending);
        var fixture = JsonSerializer.SerializeToElement(scene.Fixture);
        Assert.Equal(11, fixture.GetProperty("sheetFields").GetArrayLength());
        if (scenario.EndsWith("-invalid")) Assert.NotEmpty(SetupWizardState.ValidateSheetChoices(wizard.Draft.SheetFields));
        else Assert.Empty(SetupWizardState.ValidateSheetChoices(wizard.Draft.SheetFields));
        if (scenario.EndsWith("-inspector")) Assert.True(wizard.SingleComposer.GetTextInput("sheet-editor-Label").HasFocus);
        if (scenario.EndsWith("-choice")) Assert.Equal("Cartographer", wizard.SingleComposer.GetTextInput("sheet-editor-Option-0").GetText());
        if (scenario.EndsWith("-choice") || scenario.EndsWith("-invalid"))
        {
            var input = wizard.SingleComposer.GetTextInput(scenario.EndsWith("-invalid") ? "sheet-editor-Option-3" : "sheet-editor-Option-2");
            Assert.True(scenario.EndsWith("-invalid") ? wizard.SingleComposer.GetScrollbar("sheet-editor-inspector-scroll").HasFocus : input.HasFocus);
            Assert.True(input.Bounds.renderY >= input.InsideClipBounds.renderY - 1);
            Assert.True(input.Bounds.renderY + input.Bounds.OuterHeight <= input.InsideClipBounds.renderY + input.InsideClipBounds.OuterHeight + 1);
            if (scenario.EndsWith("-invalid"))
            {
                var error = wizard.SingleComposer.GetRichtext("sheet-editor-option-error-3");
                Assert.True(error.Bounds.renderY + error.Bounds.OuterHeight <= input.InsideClipBounds.renderY + input.InsideClipBounds.OuterHeight + 1);
            }
        }
        if (scenario.EndsWith("-review")) Assert.True(wizard.Draft.SheetFieldsChanged);
        if (scenario == "wizard-sheet-builder-selected")
        {
            var preview = Assert.IsType<SetupWizardSheetPreview>(wizard.SingleComposer.GetElement("sheet-editor-preview"));
            Assert.Equal("farewell", preview.SelectedId);
            Assert.True(preview.Composer.GetScrollbar("scrollbar").CurrentYPosition > 0);
            var cell = preview.Dialog.TemplateSelections["farewell"];
            Assert.True(cell.Bounds.renderY + cell.Bounds.OuterHeight <= cell.InsideClipBounds.renderY + cell.InsideClipBounds.OuterHeight + 1);
        }
        if (scenario == "wizard-sheet-builder-unselected")
        {
            var preview = Assert.IsType<SetupWizardSheetPreview>(wizard.SingleComposer.GetElement("sheet-editor-preview"));
            Assert.Null(preview.SelectedId);
            Assert.Null(wizard.SingleComposer.GetElement("sheet-editor-Label"));
            Assert.Null(wizard.SingleComposer.GetElement("sheet-editor-tabs"));
            Assert.Null(wizard.SingleComposer.GetElement("sheet-editor-remove"));
        }
        if (scenario.EndsWith("-hidden"))
        {
            Assert.Contains("Staff continuity note", string.Concat(wizard.SingleComposer.GetRichtext("sheet-editor-title").Components.OfType<RichTextComponent>().Select(c => c.DisplayText)));
            Assert.NotNull(wizard.SingleComposer.GetDropDown("sheet-editor-Visibility"));
            wizard.SingleComposer.GetHorizontalTabs("sheet-editor-tabs").SetValue(0);
            wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Unknown });
            Assert.Equal("Staff continuity note", wizard.SingleComposer.GetTextInput("sheet-editor-Label").GetText());
            wizard.SingleComposer.GetHorizontalTabs("sheet-editor-tabs").SetValue(1);
            wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Unknown });
        }
        host.RenderGuiDialog(wizard, 0);
        Assert.Empty(host.UnsupportedCalls);
        Assert.All(host.RecordedEffects, effect => Assert.Contains(effect, new[] { "sound:menubutton_down", "sound:menubutton_wood", "sound:toggleswitch" }));
    }
}
