using FluentAssertions;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class DialogDraftStateTests
{
    [Fact]
    public void Refresh_PreservesUnsavedDraft()
    {
        var state = new DialogDraftState("loaded");
        state.ApplyResponse("typing", "server", true, false).Should().BeTrue();
        state.IsDirty("typing").Should().BeTrue();
    }

    [Fact]
    public void SaveReply_PreservesLaterEdits_AndUpdatesBaseline()
    {
        var state = new DialogDraftState("loaded");
        state.TryBeginRequest("saved").Should().BeTrue();
        state.ApplyResponse("new edits", "saved", true).Should().BeTrue();
        state.IsDirty("loaded").Should().BeTrue();
        state.IsDirty("saved").Should().BeFalse();
        state.TryBeginRequest("new edits").Should().BeTrue();
        state.ApplyResponse("new edits", "normalized edits", true).Should().BeFalse();
    }

    [Fact]
    public void Failure_PreservesSubmittedDraft_AndOriginalBaseline()
    {
        var state = new DialogDraftState("loaded");
        state.TryBeginRequest("saved");
        state.ApplyResponse("saved", "invalid", false).Should().BeTrue();
        state.IsDirty("saved").Should().BeTrue();
        state.IsDirty("loaded").Should().BeFalse();
    }

    [Fact]
    public void BackgroundRefresh_DoesNotConsumePendingSave()
    {
        var state = new DialogDraftState("loaded");
        state.TryBeginRequest("saved");
        state.ApplyResponse("saved", "background", true, false).Should().BeTrue();
        state.TryBeginRequest("second save").Should().BeFalse();
        state.ApplyResponse("saved", "normalized", true).Should().BeFalse();
    }

    [Fact]
    public void CleanRefresh_AcceptsServerSnapshot()
    {
        var state = new DialogDraftState("loaded");
        state.ApplyResponse("loaded", "server", true, false).Should().BeFalse();
        state.IsDirty("server").Should().BeFalse();
    }

    [Theory]
    [InlineData("discarded draft", false)]
    [InlineData("typed after reload", true)]
    public void ExplicitReload_DiscardsOnlyTheDraftPresentWhenRequested(string current, bool preserve)
    {
        var state = new DialogDraftState("loaded");
        state.TryBeginRequest("discarded draft");
        state.ApplyResponse(current, "disk", true).Should().Be(preserve);
    }

    [Fact]
    public void RecoveryRefresh_AlwaysPreservesCurrentDraftAndUpdatesBaseline()
    {
        var state = new DialogDraftState("loaded");
        state.TryBeginRequest("newer draft");

        state.ApplyResponse("newer draft", "authoritative", true, preserveCurrent: true).Should().BeTrue();

        state.IsDirty("newer draft").Should().BeTrue();
        state.IsDirty("authoritative").Should().BeFalse();
        state.TryBeginRequest("retry").Should().BeTrue();
    }

    [Fact]
    public void Failure_AllowsRetry()
    {
        var state = new DialogDraftState("loaded");
        state.TryBeginRequest("invalid");
        state.ApplyResponse("invalid", "loaded", false);
        state.TryBeginRequest("corrected").Should().BeTrue();
        state.ApplyResponse("corrected", "corrected", true).Should().BeFalse();
        state.IsDirty("corrected").Should().BeFalse();
    }
}
