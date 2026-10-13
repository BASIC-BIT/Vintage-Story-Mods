using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ChatUiSystem;
using thebasics.Tests.GuiPreview;
using TheBasics.GuiPreview;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

[Collection("Standalone GUI")]
public class SetupWizardCaptureScenesTests
{
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
