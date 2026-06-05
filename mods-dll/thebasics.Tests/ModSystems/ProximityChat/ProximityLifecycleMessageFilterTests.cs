using FluentAssertions;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class ProximityLifecycleMessageFilterTests
{
    [Theory]
    [InlineData(EnumChatType.JoinLeave)]
    [InlineData(EnumChatType.Notification)]
    public void IsLifecycleMessageType_returns_true_for_vanilla_lifecycle_message_types(EnumChatType chatType)
    {
        ProximityLifecycleMessageFilter.IsLifecycleMessageType(chatType).Should().BeTrue();
    }

    [Fact]
    public void IsLifecycleMessageType_returns_false_for_player_chat()
    {
        ProximityLifecycleMessageFilter.IsLifecycleMessageType(EnumChatType.OthersMessage).Should().BeFalse();
    }

    [Fact]
    public void ShouldSendToPlayerGroup_returns_false_for_effective_proximity_group()
    {
        ProximityLifecycleMessageFilter.ConfigureForTests(42);

        var membership = new PlayerGroupMembership { GroupUid = 42 };

        ProximityLifecycleMessageFilter.ShouldSendToPlayerGroup(membership).Should().BeFalse();
    }

    [Fact]
    public void ShouldSendToPlayerGroup_returns_true_for_other_group()
    {
        ProximityLifecycleMessageFilter.ConfigureForTests(42);

        var membership = new PlayerGroupMembership { GroupUid = 7 };

        ProximityLifecycleMessageFilter.ShouldSendToPlayerGroup(membership).Should().BeTrue();
    }

    [Fact]
    public void ShouldSendPublicGeneralMessage_returns_false_when_general_is_proximity()
    {
        ProximityLifecycleMessageFilter.ShouldSendPublicGeneralMessage(
            connectedClientCount: 1,
            publicMessageThreshold: 50,
            generalIsProximity: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldSendPublicGeneralMessage_returns_true_below_threshold_when_general_is_not_proximity()
    {
        ProximityLifecycleMessageFilter.ShouldSendPublicGeneralMessage(
            connectedClientCount: 49,
            publicMessageThreshold: 50,
            generalIsProximity: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldSendPublicGeneralMessage_returns_false_at_threshold()
    {
        ProximityLifecycleMessageFilter.ShouldSendPublicGeneralMessage(
            connectedClientCount: 50,
            publicMessageThreshold: 50,
            generalIsProximity: false).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void ShouldReemitNearbyDeathMessage_follows_config_for_death_notifications(bool enabled, bool expected)
    {
        ProximityLifecycleMessageFilter.ShouldReemitNearbyDeathMessage(EnumChatType.Notification, enabled).Should().Be(expected);
    }

    [Fact]
    public void ShouldReemitNearbyDeathMessage_returns_false_for_join_leave()
    {
        ProximityLifecycleMessageFilter.ShouldReemitNearbyDeathMessage(EnumChatType.JoinLeave, enabled: true).Should().BeFalse();
    }
}
