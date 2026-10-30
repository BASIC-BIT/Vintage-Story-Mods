using System.Text.Json;
using TheBasics.GuiPreview;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using Vintagestory.API.Client;
namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardSearchRenderTests
{
    [VisualTheory]
    [InlineData(1.25d)]
    public void EveryPageCanEnterSearchWithoutRefreshingHiddenControls(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), LanguageConfigAdmin.BuildEntries(config), CharacterSheetFieldConfigAdmin.BuildEntries(config));
        using var wizard = new SetupWizardDialog(host.Api, draft, "page-search", true, () => { }, (_, _, _) => { }, () => { }, layoutOnly: true, captureOnly: true, actorFactory: host.CreateGuide);
        foreach (var page in SetupWizardCatalog.Pages.Select(page => page.Id).Concat(new[] { "hub", "review", "finish" }))
        {
            wizard.ShowPage(page);
            wizard.ShowSearch("DebugMode");
            Assert.NotNull(wizard.SingleComposer.GetElement("search-result-0"));
            Assert.True(wizard.SingleComposer.GetButton("review").Enabled, page);
        }
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void SearchFromMapAndReturnFromMapResultUseSearchContent(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, new(SetupWizardCaptureScenes.DefaultValues()), "map-search", true, () => saves++, (_, _, _) => { }, () => { }, layoutOnly: true);
        wizard.TryOpen(); wizard.ShowPage("map.visibility");
        wizard.ShowSearch("ManageMapPlayerVisibility");
        Assert.NotNull(wizard.SingleComposer.GetElement("search-result-0"));
        Assert.Null(wizard.SingleComposer.GetElement("ManageMapPlayerVisibility"));
        Assert.True(wizard.FocusSearchResult(Assert.Single(wizard.SearchPanel.Results)));
        Assert.Equal("map.visibility", wizard.CurrentStepId);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        Assert.NotNull(wizard.SingleComposer.GetElement("search-result-0"));
        Assert.Null(wizard.SingleComposer.GetElement("ManageMapPlayerVisibility"));
        wizard.ShowPage("review"); wizard.ShowSearch("DebugMode");
        var button = wizard.SingleComposer.GetButton("review");
        var x = (int)(button.Bounds.renderX + button.Bounds.InnerWidth / 2);
        var y = (int)(button.Bounds.renderY + button.Bounds.InnerHeight / 2);
        wizard.OnMouseDown(new MouseEvent(x, y, Vintagestory.API.Common.EnumMouseButton.Left, 0));
        wizard.OnMouseUp(new MouseEvent(x, y, Vintagestory.API.Common.EnumMouseButton.Left, 0));
        Assert.Equal(0, saves);
        Assert.Null(wizard.SingleComposer.GetElement("search-result-0"));
        Assert.Equal("review", wizard.CurrentStepId);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1.25d)]
    public void EveryScalarSearchResultReachesAControl(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        using var wizard = new SetupWizardDialog(host.Api, new(SetupWizardCaptureScenes.DefaultValues()), "all-targets", true, () => { }, (_, _, _) => { }, () => { }, layoutOnly: true, captureOnly: true);
        foreach (var setting in ConfigAdminSettingRegistry.Settings)
            Assert.True(wizard.FocusSetting(setting.Key), setting.Key);
        Assert.Empty(host.RecordedEffects);
    }

    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25d)]
    public void SearchNavigatesCurrentDraftAndRestoresResults(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!, Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1280, 720, scale);
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        var draft = new SetupWizardDraft(SetupWizardCaptureScenes.DefaultValues(), LanguageConfigAdmin.BuildEntries(config), CharacterSheetFieldConfigAdmin.BuildEntries(config));
        using var wizard = new SetupWizardDialog(host.Api, draft, "search", true, () => { }, (_, _, _) => { }, () => { }, layoutOnly: true, captureOnly: true, actorFactory: host.CreateGuide);
        wizard.TryOpen();
        wizard.ShowSearch(""); Capture("empty");
        foreach (var character in "chat") wizard.OnKeyPress(new KeyEvent { KeyChar = character, KeyCode = character });
        Assert.Equal("chat", wizard.SearchPanel.Query);
        Assert.True(wizard.SingleComposer.GetTextInput("setup-search").HasFocus);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Down });
        Assert.True(wizard.SingleComposer["search-result-" + wizard.SearchPanel.Selection].HasFocus);
        var keyboardTarget = wizard.SearchPanel.Results[wizard.SearchPanel.Selection];
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Enter });
        Assert.Equal(SetupWizardCatalog.FindPageForSetting(keyboardTarget.Key).Id, wizard.CurrentStepId);
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        Assert.NotNull(wizard.SingleComposer.GetElement("search-result-0"));
        wizard.SingleComposer.FocusElement(wizard.SingleComposer.GetTextInput("setup-search").TabIndex);
        wizard.SingleComposer.GetTextInput("setup-search").CaretPosWithoutLineBreaks = 1;
        wizard.OnKeyPress(new KeyEvent { KeyChar = 'x', KeyCode = (int)GlKeys.X });
        Assert.Equal("cxhat", wizard.SearchPanel.Query);
        Assert.Equal(2, wizard.SingleComposer.GetTextInput("setup-search").CaretPosWithoutLineBreaks);
        wizard.ShowSearch("not-a-config-setting"); Assert.Empty(wizard.SearchPanel.Results); Capture("none");
        draft.Set("ProximityChatName", "Nearby draft");
        wizard.ShowPage("chat.languageeditor");
        var picker = (GuiElementColorPicker)wizard.SingleComposer["language-editor-color-picker"];
        picker.Open(); Assert.True(picker.IsOpen);
        wizard.ShowSearch("chat"); Assert.False(picker.IsOpen);
        Capture("results");
        wizard.SearchPanel.Move(wizard.SingleComposer, 9);
        var selection = wizard.SearchPanel.Selection;
        var scroll = wizard.SingleComposer.GetScrollbar("settings-scroll").CurrentYPosition;
        Assert.True(scroll > 0);
        Capture("results-scrolled");
        var first = wizard.SingleComposer["search-result-0"];
        Assert.False(first.IsPositionInside((int)first.Bounds.absX + 4, (int)first.Bounds.absY + 4));
        var beforePixels = host.Canvas.GetRgbaPixels();
        wizard.SingleComposer.GetScrollbar("settings-scroll").ScrollToBottom();
        host.RenderGuiDialog(wizard, 0);
        var afterPixels = host.Canvas.GetRgbaPixels();
        var clip = first.InsideClipBounds;
        for (var y = (int)(clip.renderY + clip.InnerHeight + 4); y < 720; y++)
            Assert.Equal(beforePixels.AsSpan(y * 1280 * 4, 1280 * 4).ToArray(), afterPixels.AsSpan(y * 1280 * 4, 1280 * 4).ToArray());
        ((SetupWizardSheetEditor.BuilderScrollbar)wizard.SingleComposer.GetScrollbar("settings-scroll")).MoveTo(scroll);
        Assert.True(wizard.FocusSearchResult(wizard.SearchPanel.Results[selection]));
        wizard.ShowSearch();
        Assert.Equal("chat", wizard.SearchPanel.Query);
        Assert.Equal(selection, wizard.SearchPanel.Selection);
        Assert.Equal(scroll, wizard.SingleComposer.GetScrollbar("settings-scroll").CurrentYPosition);
        Assert.Equal("Nearby draft", draft.Get("ProximityChatName"));
        Assert.True(wizard.FocusSetting("PlayerStatDistanceTravelledTimer"));
        var field = wizard.SingleComposer.GetTextInput("PlayerStatDistanceTravelledTimer");
        Assert.True(field.HasFocus);
        Assert.True(SetupWizardViewportClip.FullyInside(field.Bounds, field.InsideClipBounds));
        Capture("scalar-target");
        var language = draft.Languages[0]; language.GrantedToModelGroups = "unpublished group";
        wizard.ShowSearch("unpublished group");
        var result = Assert.Single(wizard.SearchPanel.Results);
        language.Name = ""; draft.Languages.Reverse();
        var duplicate = language with { }; draft.Languages.Insert(0, duplicate);
        Assert.True(wizard.FocusSearchResult(result));
        wizard.SingleComposer.GetTextInput("language-editor-Name").SetValue("Target renamed");
        Assert.Equal("Target renamed", language.Name);
        Assert.Equal("", duplicate.Name);
        var grant = wizard.SingleComposer.GetTextInput("language-editor-GrantedToModelGroups");
        Assert.Equal("unpublished group", grant.GetText());
        Assert.True(SetupWizardViewportClip.FullyInside(grant.Bounds, grant.InsideClipBounds));
        Capture("language-target");
        var sheet = draft.SheetFields[0]; sheet.Description = "unsaved template description";
        wizard.ShowSearch("unsaved template description");
        var sheetResult = Assert.Single(wizard.SearchPanel.Results);
        sheet.Label = ""; draft.SheetFields.Reverse();
        Assert.True(wizard.FocusSearchResult(sheetResult));
        var description = wizard.SingleComposer.GetTextInput("sheet-editor-Description");
        Assert.Equal("unsaved template description", description.GetText());
        Assert.True(SetupWizardViewportClip.FullyInside(description.Bounds, description.InsideClipBounds));
        Capture("sheet-target");
        wizard.ShowSearch("chat");
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        Assert.Equal("", wizard.SearchPanel.Query);
        Assert.True(wizard.IsOpened());
        wizard.OnKeyDown(new KeyEvent { KeyCode = (int)GlKeys.Escape });
        Assert.True(wizard.IsOpened());
        Assert.All(host.RecordedEffects, effect => Assert.Equal("sound:tick", effect));
        Assert.Empty(host.UnsupportedCalls);

        void Capture(string name)
        {
            host.RenderGuiDialog(wizard, 0);
            var root = Environment.GetEnvironmentVariable("TASK6_CAPTURE_ROOT");
            if (root == null) return;
            var dir = Path.Combine(root, "scale-" + scale.ToString(System.Globalization.CultureInfo.InvariantCulture)); Directory.CreateDirectory(dir);
            host.Canvas.SavePng(Path.Combine(dir, name + ".png"));
            File.WriteAllText(Path.Combine(dir, name + ".json"), JsonSerializer.Serialize(new { width = 1280, height = 720, scale, sourceTreeHash = Environment.GetEnvironmentVariable("TASK6_SOURCE_HASH"), assemblySha256 = SetupWizardCaptureIdentity.HashFile(typeof(SetupWizardDialog).Assembly.Location), environment = host.EnvironmentIdentity, coverage = "standalone production GUI, no game or persistence acceptance" }));
        }
    }
}
