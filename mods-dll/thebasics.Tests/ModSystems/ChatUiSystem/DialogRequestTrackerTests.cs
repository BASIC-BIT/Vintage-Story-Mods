using FluentAssertions;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class DialogRequestTrackerTests
{
    [Fact]
    public void UntrackedPush_DoesNotConsumePendingRequestOrItsTimeout()
    {
        var tracker = new DialogRequestTracker();
        Action expire = null!;
        var failures = 0;
        var id = tracker.Begin(() => failures++, (callback, _) => expire = callback);
        tracker.Accept(0).Should().BeFalse();
        expire();
        failures.Should().Be(1);
        tracker.Accept(id).Should().BeFalse();
        tracker.Accept(0).Should().BeTrue();
    }

    [Fact]
    public void MissingReply_ReleasesDraftAndRejectsLateReplyAfterRetry()
    {
        var tracker = new DialogRequestTracker();
        var draft = new DialogDraftState("loaded");
        Action expire = null!;
        draft.TryBeginRequest("first").Should().BeTrue();
        var first = tracker.Begin(draft.CancelRequest, (callback, delay) => { expire = callback; delay.Should().Be(30000); });
        expire();
        draft.IsDirty("newer").Should().BeTrue();
        draft.TryBeginRequest("newer").Should().BeTrue();
        var second = tracker.Begin(draft.CancelRequest, (_, _) => { });
        expire();
        tracker.Accept(first).Should().BeFalse();
        draft.TryBeginRequest("another").Should().BeFalse();
        tracker.Accept(second).Should().BeTrue();
    }

    [Fact]
    public void CompletedRequest_TimeoutDoesNotCancelLaterRequest()
    {
        var tracker = new DialogRequestTracker();
        Action expire = null!;
        var failures = 0;
        var first = tracker.Begin(() => failures++, (callback, _) => expire = callback);
        tracker.Accept(first).Should().BeTrue();
        var second = tracker.Begin(() => failures++, (_, _) => { });
        expire();
        failures.Should().Be(0);
        tracker.Accept(second).Should().BeTrue();
    }
}
