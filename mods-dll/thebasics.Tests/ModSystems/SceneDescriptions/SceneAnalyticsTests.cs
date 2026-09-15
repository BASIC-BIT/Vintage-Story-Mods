using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneAnalyticsTests
{
    [Fact]
    public void PropertiesExcludeAuthoredContentAndIdentity()
    {
        var props = SceneAnalytics.Properties(new SceneDescriptionData { Title = "secret", Body = "private", AuthorUid = "uid", AuthorName = "name", Display = SceneDescriptionDisplay.AlwaysNearby, LockItemCode = "lock", ShowBodyInBubble = true });
        props.Should().BeEquivalentTo(new Dictionary<string, object> { ["scene_content"] = "written", ["scene_display_mode"] = "always_nearby", ["scene_locked"] = true, ["scene_body_shown"] = true });
    }
    [Fact]
    public void DwellRequiresContinuousVisibleSecondAndCooldown()
    {
        var gate = new SceneObservationGate();
        gate.Observe("a", 0).Should().BeFalse();
        gate.Observe("a", 251).Should().BeFalse();
        for (long now = 501; now <= 1001; now += 250) gate.Observe("a", now).Should().BeFalse();
        gate.Observe("a", 1251).Should().BeTrue();
        gate.Observe("a", 1501).Should().BeFalse();
        for (long now = 60251; now < 61251; now += 250) gate.Observe("a", now).Should().BeFalse();
        gate.Observe("a", 61251).Should().BeTrue();
    }
    [Fact]
    public void ObservationStateIsBoundedAndReaderActionsAreSeparate()
    {
        var gate = new SceneObservationGate();
        gate.Accept("a", 0, 30000).Should().BeTrue();
        gate.Accept("a", 29999, 30000).Should().BeFalse();
        gate.Accept("a", 30000, 30000).Should().BeTrue();
        for (int i = 0; i < 5000; i++) gate.Observe("marker" + i, i);
        gate.Count.Should().BeLessThanOrEqualTo(SceneObservationGate.MaxEntries);
    }
    [Theory]
    [InlineData("", "", "empty")]
    [InlineData("", "new", "first_content")]
    [InlineData("old", "new", "edit")]
    public void SaveKindsDescribeResult(string before, string after, string expected) => SceneAnalytics.SaveKind(new() { Body = before }, new() { Body = after }).Should().Be(expected);
    [Fact]
    public void DisconnectedObservationNeverQueuesForLaterConsent()
    {
        var channel = NSubstitute.Substitute.For<Vintagestory.API.Client.IClientNetworkChannel>();
        var api = NSubstitute.Substitute.For<Vintagestory.API.Client.ICoreClientAPI>();
        using var safe = new thebasics.Utilities.Network.SafeClientNetworkChannel(channel, api);
        safe.TrySendPacketWithoutQueue(new SceneObservationMessage()).Should().BeFalse();
        safe.PendingActionCount.Should().Be(0);
    }

    [Theory]
    [InlineData(SceneDescriptionDisplay.AlwaysNearby, 7, false, true)]
    [InlineData(SceneDescriptionDisplay.AlwaysNearby, 7.1, false, false)]
    [InlineData(SceneDescriptionDisplay.WhenTargeted, 2, false, false)]
    [InlineData(SceneDescriptionDisplay.WhenTargeted, 2, true, true)]
    [InlineData(SceneDescriptionDisplay.OnInteraction, 2, true, false)]
    public void BubbleValidationUsesServerVisibility(SceneDescriptionDisplay display, double distance, bool targeted, bool expected) =>
        SceneAnalyticsObserver.ObservationAllowed(new() { Title = "text", Display = display }, distance, true, targeted).Should().Be(expected);
    [Theory]
    [InlineData(8, true)]
    [InlineData(8.01, false)]
    [InlineData(double.NaN, false)]
    public void ReaderValidationRejectsOutOfReach(double distance, bool expected) =>
        SceneAnalyticsObserver.ObservationAllowed(new(), distance, false, false).Should().Be(expected); [Theory]
    [InlineData(0.5f, 0, 0, true)]
    [InlineData(0.49f, 10, 10, false)]
    [InlineData(1f, -1, 10, false)]
    [InlineData(1f, 100, 10, false)]
    [InlineData(1f, 10, 100, false)]
    [InlineData(1f, double.NaN, 10, false)]
    public void DwellQualifiesOnlyVisibleBubbleCenters(float opacity, double x, double y, bool expected) =>
        SceneAnalytics.VisibleFrame(opacity, x, y, 100, 100).Should().Be(expected);
}
