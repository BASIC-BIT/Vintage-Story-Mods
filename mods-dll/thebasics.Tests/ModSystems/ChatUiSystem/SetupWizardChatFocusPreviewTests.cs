using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SetupWizardChatFocusPreviewTests
{
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public void OpeningUsesTheEditableDefault(bool proximityDefault, int selected)
    {
        var draft = Draft();
        draft.Set("ProximityChatAsDefault", proximityDefault ? "1" : "0");
        Assert.Equal(selected, At(draft, 0.75).SelectedTabIndex);
    }

    [Theory]
    [InlineData(true, true, true, 1)]
    [InlineData(false, true, true, 0)]
    [InlineData(false, false, true, 1)]
    [InlineData(false, true, false, 1)]
    public void IncomingGeneralNotificationNeedsBothGamePreferencesAndRespectsNearbyProtection(
        bool prevent, bool autoOpenSelected, bool autoChat, int selected)
    {
        var draft = Draft();
        draft.Set("PreventProximityChannelSwitching", prevent ? "1" : "0");
        var state = SetupWizardChatFocusPreview.At(draft, 1.25, autoOpenSelected, autoChat);
        Assert.Equal(selected, state.SelectedTabIndex);
        Assert.Contains("notice", state.StageText, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NearbySpeechDoesNotSwitchAwayFromChosenGeneral(bool prevent)
    {
        var draft = Draft();
        draft.Set("PreventProximityChannelSwitching", prevent ? "1" : "0");
        var state = At(draft, 4.25);
        Assert.Equal(0, state.SelectedTabIndex);
        Assert.DoesNotContain("Meet me", state.Messages);
        Assert.Contains("does not switch", state.StageText);
        var chosen = SetupWizardChatFocusPreview.At(draft, 4.25, true, selectedTabIndex: 1);
        Assert.Contains("Meet me", chosen.Messages);
        Assert.Equal("You choose Proximity in the paused example.", chosen.StageText);
    }

    [Theory]
    [InlineData(true, true, 0)]
    [InlineData(false, true, 1)]
    [InlineData(true, false, 0)]
    [InlineData(false, false, 0)]
    public void ReopeningRemembersTheManualChoiceOrUsesTheConfiguredDefault(bool remember, bool proximityDefault, int selected)
    {
        var draft = Draft();
        draft.Set("PreserveDefaultChatChoice", remember ? "1" : "0");
        draft.Set("ProximityChatAsDefault", proximityDefault ? "1" : "0");
        Assert.False(At(draft, 6.25).IsOpen);
        Assert.Empty(At(draft, 6.25).Messages);
        var reopened = At(draft, 7.25);
        Assert.True(reopened.IsOpen);
        Assert.Equal(selected, reopened.SelectedTabIndex);
    }

    [Fact]
    public void GeneralReplacementHasOneTabAndReceivesNearbySpeechThere()
    {
        var draft = Draft();
        draft.Set("UseGeneralChannelAsProximityChat", "1");
        foreach (double time in new[] { 0.75, 1.25, 2.25, 4.25, 6.25, 7.25 })
        {
            Assert.Equal(0, At(draft, time).SelectedTabIndex);
            Assert.Equal(0, SetupWizardChatFocusPreview.At(draft, time, true, selectedTabIndex: 1).SelectedTabIndex);
        }
        Assert.Contains("Meet me", At(draft, 4.25).Messages);
    }

    [Fact]
    public void ExactBoundariesReplayWithoutSavedStateOrDraftMutations()
    {
        var draft = Draft();
        var before = draft.Values.Select(value => (value.Key, value.Value)).ToArray();
        foreach (var (time, phase) in new[] { (0d, 0), (0.999, 0), (1d, 1), (2d, 2), (4d, 3), (6d, 4), (7d, 5), (9.999, 5), (10d, 0) })
            Assert.Equal(phase, At(draft, time).PhaseIndex);
        Assert.Equal(At(draft, 7.25), At(draft, 17.25));
        Assert.Equal(At(draft, 0), At(draft, double.NaN));
        Assert.Equal(At(draft, 0), At(draft, double.PositiveInfinity));
        Assert.Equal(At(draft, 0), At(draft, -1));
        Assert.Equal(before, draft.Values.Select(value => (value.Key, value.Value)).ToArray());
        Assert.Empty(draft.ChangedKeys);
        Assert.False(draft.IsSavePending);
    }

    private static SetupWizardChatFocusPreview.State At(SetupWizardDraft draft, double time) =>
        SetupWizardChatFocusPreview.At(draft, time, true);

    private static SetupWizardDraft Draft() => new(SetupWizardCaptureScenes.DefaultValues());
}
