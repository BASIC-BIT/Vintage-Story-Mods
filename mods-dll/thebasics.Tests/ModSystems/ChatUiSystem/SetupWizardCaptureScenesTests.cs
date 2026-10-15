using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using TheBasics.GuiPreview;
using thebasics.Models;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardCaptureScenesTests
{
    [VisualTheory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    public void PrivilegeCaptureOpensSuggestionsWithoutChangingOrSavingTheDraft(double scale)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000, scale);
        var values = SetupWizardCaptureScenes.DefaultValues();
        var draft = new SetupWizardDraft(values);
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "qa-run", true,
            () => saves++, null!, null!, layoutOnly: true, captureOnly: true);
        wizard.SetSession(new TheBasicsSetupWizardResultMessage
        {
            Kind = SetupWizardResultKind.Open, Success = true, RunId = "qa-run", IsDedicated = true,
            Values = values,
            PrivilegeSuggestions = [new() { Code = "custom.server", Description = "Manage custom server tools" }]
        });

        SetupWizardCaptureScenes.Show(wizard, "wizard-privileges", 0.75);

        Assert.Equal("server.history", wizard.CurrentStepId);
        var picker = Assert.IsType<GuiElementPrivilegePicker>(wizard.SingleComposer.GetElement("ChatHistoryPermission"));
        Assert.True(picker.IsSuggestionsOpen);
        Assert.Equal(values.Single(value => value.Key == "ChatHistoryPermission").Value, picker.Value);
        Assert.False(draft.IsDirty);
        Assert.False(draft.IsSavePending);
        Assert.True(wizard.IsCaptureOnly);
        Assert.Equal(0, saves);
    }

    [VisualTheory]
    [InlineData("wizard-map-visibility", "0", "0", "1000")]
    [InlineData("wizard-map-managed", "1", "0", "80")]
    [InlineData("wizard-map-hidden", "1", "1", "80")]
    public void MapCapturesKeepEachPolicyInAnIsolatedDraft(string scenario, string managed, string hidden, string range)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var values = SetupWizardCaptureScenes.DefaultValues();
        var draft = new SetupWizardDraft(values);
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "qa-run", true,
            () => saves++, null!, null!, layoutOnly: true, captureOnly: true);

        SetupWizardCaptureScenes.Show(wizard, scenario, 0.75);

        Assert.Equal("map.visibility", wizard.CurrentStepId);
        Assert.Equal(managed, draft.Get("ManageMapPlayerVisibility"));
        Assert.Equal(hidden, draft.Get("MapHideOtherPlayers"));
        Assert.Equal(range, draft.Get("MapPlayerRenderDistance"));
        Assert.Equal(values.Single(value => value.Key == "EnableChatter").Value, draft.Get("EnableChatter"));
        Assert.False(draft.IsSavePending);
        Assert.True(wizard.IsCaptureOnly);
        Assert.Equal(0, saves);
    }

    [VisualTheory]
    [InlineData("wizard-appearance-bubbles", "appearance.bubbles")]
    [InlineData("wizard-appearance-names", "appearance.names")]
    [InlineData("wizard-map-visibility", "map.visibility")]
    [InlineData("wizard-characters-sheets", "characters.sheets")]
    [InlineData("wizard-roleplay-dice", "roleplay.dice")]
    [InlineData("wizard-server-history", "server.history")]
    [InlineData("wizard-server-stats", "server.stats")]
    public void AddedBranchCapturesOpenTheRequestedPageWithoutSaving(string scenario, string pageId)
    {
        using var host = new PreviewHost(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
            Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 1600, 1000);
        var values = SetupWizardCaptureScenes.DefaultValues();
        var draft = new SetupWizardDraft(values);
        draft.Set("EnableChatter", draft.Get("EnableChatter") == "1" ? "0" : "1");
        var saves = 0;
        using var wizard = new SetupWizardDialog(host.Api, draft, "qa-run", true,
            () => saves++, null!, null!, layoutOnly: true, captureOnly: true);

        SetupWizardCaptureScenes.Show(wizard, scenario, 0.75);

        Assert.Equal(pageId, wizard.CurrentStepId);
        Assert.True(SetupWizardCatalog.TryGetPage(pageId, out _));
        Assert.Equal(values.Select(value => (value.Key, value.Value)),
            draft.Values.Select(value => (value.Key, value.Value)));
        Assert.False(draft.IsDirty);
        Assert.False(draft.IsSavePending);
        Assert.True(wizard.IsCaptureOnly);
        Assert.Equal(0, saves);
    }
}
