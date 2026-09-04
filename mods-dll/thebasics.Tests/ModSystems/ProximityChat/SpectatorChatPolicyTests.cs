using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using thebasics.Tests.Support;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class SpectatorChatPolicyTests
{
    [Fact]
    public void ActiveSpectator_SuppressesEntityAttachedCues()
    {
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Playing);

        SpectatorChatPolicy.IsActiveSpectator(player).Should().BeTrue();
        SpectatorChatPolicy.ShouldEmitEntityAttachedCues(player).Should().BeFalse();
    }

    [Fact]
    public void ConnectingSpectatorPlaceholder_DoesNotApplySpectatorPolicy()
    {
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Connecting);
        var config = new ModConfig
        {
            AllowSpectatorPlacedEnvironmentalMessages = false,
            UseNicknameInOOC = true,
            UseNicknameInSpectatorOOC = false
        };

        SpectatorChatPolicy.IsActiveSpectator(player).Should().BeFalse();
        SpectatorChatPolicy.ShouldEmitEntityAttachedCues(player).Should().BeTrue();
        SpectatorChatPolicy.CanPlaceEnvironmentalMessage(player, config).Should().BeTrue();
        SpectatorChatPolicy.ShouldProtectRoleplayChat(player, config).Should().BeFalse();
        SpectatorChatPolicy.UseNicknameInLocalOoc(player, config).Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void PlacedEnvironmentalMessages_FollowSpectatorSetting(bool allowed, bool expected)
    {
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Playing);
        var config = new ModConfig { AllowSpectatorPlacedEnvironmentalMessages = allowed };

        SpectatorChatPolicy.CanPlaceEnvironmentalMessage(player, config).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void LocalOocNickname_FollowsSpectatorSpecificSetting(bool useNickname, bool expected)
    {
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Playing);
        var config = new ModConfig
        {
            UseNicknameInOOC = !useNickname,
            UseNicknameInSpectatorOOC = useNickname
        };

        SpectatorChatPolicy.UseNicknameInLocalOoc(player, config).Should().Be(expected);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void RoleplayChatProtection_FollowsSpectatorSetting(bool protectedChat, bool expected)
    {
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Playing);
        var config = new ModConfig { ProtectSpectatorRoleplayChat = protectedChat };

        SpectatorChatPolicy.ShouldProtectRoleplayChat(player, config).Should().Be(expected);
    }

    [Theory]
    [InlineData(EnumGameMode.Guest)]
    [InlineData(EnumGameMode.Survival)]
    [InlineData(EnumGameMode.Creative)]
    public void NonSpectatorModes_KeepExistingChatBehavior(EnumGameMode gameMode)
    {
        var player = CreatePlayer(gameMode, EnumClientState.Playing);
        var config = new ModConfig
        {
            UseNicknameInOOC = true,
            UseNicknameInSpectatorOOC = false,
            AllowSpectatorPlacedEnvironmentalMessages = false,
            ProtectSpectatorRoleplayChat = true
        };

        SpectatorChatPolicy.UseNicknameInLocalOoc(player, config).Should().BeTrue();
        SpectatorChatPolicy.ShouldEmitEntityAttachedCues(player).Should().BeTrue();
        SpectatorChatPolicy.CanPlaceEnvironmentalMessage(player, config).Should().BeTrue();
        SpectatorChatPolicy.ShouldProtectRoleplayChat(player, config).Should().BeFalse();
    }

    private static FakeServerPlayer CreatePlayer(EnumGameMode gameMode, EnumClientState connectionState)
    {
        var worldData = Substitute.For<IWorldPlayerData>();
        worldData.CurrentGameMode.Returns(gameMode);
        return new FakeServerPlayer
        {
            WorldData = worldData,
            ConnectionState = connectionState
        };
    }
}
