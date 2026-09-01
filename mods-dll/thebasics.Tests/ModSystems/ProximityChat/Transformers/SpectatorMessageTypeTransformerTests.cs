using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Tests.Support;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat.Transformers;

public class SpectatorMessageTypeTransformerTests
{
    [Fact]
    public void PlainSpectatorSpeech_IsRejected()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_SPEECH);
        context.Message = "  hello from the sidelines  ";

        Transform(context);

        context.HasFlag(MessageContext.IS_SPEECH).Should().BeTrue();
        context.HasFlag(MessageContext.IS_OOC).Should().BeFalse();
        context.Message.Should().Be("  hello from the sidelines  ");
        context.State.Should().Be(MessageContextState.STOP);
    }

    [Fact]
    public void ExplicitSpectatorSpeech_IsRejected()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_SPEECH);
        context.SetFlag(MessageContext.IS_EXPLICIT_RANGE_COMMAND);

        Transform(context);

        context.State.Should().Be(MessageContextState.STOP);
        context.HasFlag(MessageContext.IS_OOC).Should().BeFalse();
    }

    [Fact]
    public void SpectatorEmote_IsRejected()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_EMOTE);

        Transform(context);

        context.State.Should().Be(MessageContextState.STOP);
    }

    public static IEnumerable<object[]> AllowedSpectatorMessageTypes =>
    [
        [MessageContext.IS_ENVIRONMENTAL],
        [MessageContext.IS_OOC],
        [MessageContext.IS_GLOBAL_OOC]
    ];

    [Theory]
    [MemberData(nameof(AllowedSpectatorMessageTypes))]
    public void AllowedSpectatorMessageTypes_PassThrough(string messageFlag)
    {
        var context = CreateContext(EnumGameMode.Spectator, messageFlag);
        var transformer = CreateTransformer();

        transformer.ShouldTransform(context).Should().BeFalse();
    }

    [Theory]
    [InlineData(EnumGameMode.Guest)]
    [InlineData(EnumGameMode.Survival)]
    [InlineData(EnumGameMode.Creative)]
    public void NonSpectatorSpeech_KeepsExistingBehavior(EnumGameMode gameMode)
    {
        var context = CreateContext(gameMode, MessageContext.IS_SPEECH);
        var transformer = CreateTransformer();

        transformer.ShouldTransform(context).Should().BeFalse();
        context.HasFlag(MessageContext.IS_SPEECH).Should().BeTrue();
    }

    [Fact]
    public void ConnectingSpectatorPlaceholder_KeepsExistingBehavior()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_SPEECH, EnumClientState.Connecting);
        var transformer = CreateTransformer();

        transformer.ShouldTransform(context).Should().BeFalse();
    }

    [Fact]
    public void SpectatorCannotEnterStickyEmoteMode()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_EMOTE);
        var chatSystem = new RPProximityChatSystem { Config = new ModConfig() };

        var allowed = chatSystem.IsOverrideModeAvailable(
            context.SendingPlayer,
            ChatOverrideMode.Emote,
            out var refusalLangKey);

        allowed.Should().BeFalse();
        refusalLangKey.Should().Be("thebasics:chat-spectator-embodied-message-disabled");
    }

    [Fact]
    public void DisabledProtection_RetainsNormalSpectatorRoleplayChat()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_SPEECH);
        var transformer = new SpectatorMessageTypeTransformer(new RPProximityChatSystem
        {
            Config = new ModConfig { ProtectSpectatorRoleplayChat = false }
        });

        transformer.ShouldTransform(context).Should().BeFalse();
        context.HasFlag(MessageContext.IS_SPEECH).Should().BeTrue();
    }

    [Fact]
    public void DisabledProtection_AllowsStickySpectatorEmoteMode()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_EMOTE);
        var chatSystem = new RPProximityChatSystem
        {
            Config = new ModConfig { ProtectSpectatorRoleplayChat = false }
        };

        var allowed = chatSystem.IsOverrideModeAvailable(
            context.SendingPlayer,
            ChatOverrideMode.Emote,
            out var refusalLangKey);

        allowed.Should().BeTrue();
        refusalLangKey.Should().BeNull();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void RpTextDisabled_ChatTabPipelineFollowsSpectatorProtection(bool protectedChat, bool expected)
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_SPEECH);
        context.SendingPlayer.SetRpTextEnabled(false);
        var chatSystem = new RPProximityChatSystem
        {
            Config = new ModConfig { ProtectSpectatorRoleplayChat = protectedChat }
        };

        chatSystem.ShouldProcessChatTabMessage(context.SendingPlayer).Should().Be(expected);
    }

    [Fact]
    public void ProtectedSpectatorWithRpTextDisabled_CanStillChooseGlobalOoc()
    {
        var context = CreateContext(EnumGameMode.Spectator, MessageContext.IS_GLOBAL_OOC);
        context.SendingPlayer.SetRpTextEnabled(false);
        var chatSystem = new RPProximityChatSystem
        {
            Config = new ModConfig
            {
                EnableGlobalOOC = true,
                ProtectSpectatorRoleplayChat = true
            }
        };

        var allowed = chatSystem.IsOverrideModeAvailable(
            context.SendingPlayer,
            ChatOverrideMode.GlobalOoc,
            out var refusalLangKey);

        allowed.Should().BeTrue();
        refusalLangKey.Should().BeNull();
    }

    private static void Transform(MessageContext context)
    {
        LangTestHelper.EnsureEnglish();
        var transformer = CreateTransformer();
        transformer.ShouldTransform(context).Should().BeTrue();
        transformer.Transform(context);
    }

    private static SpectatorMessageTypeTransformer CreateTransformer()
    {
        return new SpectatorMessageTypeTransformer(new RPProximityChatSystem
        {
            Config = new ModConfig { ProtectSpectatorRoleplayChat = true }
        });
    }

    private static MessageContext CreateContext(
        EnumGameMode gameMode,
        string messageFlag,
        EnumClientState connectionState = EnumClientState.Playing)
    {
        var worldData = Substitute.For<IWorldPlayerData>();
        worldData.CurrentGameMode.Returns(gameMode);
        var player = new FakeServerPlayer
        {
            WorldData = worldData,
            ConnectionState = connectionState
        };
        var context = new MessageContext
        {
            Message = "message",
            SendingPlayer = player
        };
        context.SetFlag(messageFlag);
        return context;
    }
}
