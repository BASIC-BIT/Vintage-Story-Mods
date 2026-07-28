using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.ProximityChat.Transformers;

/// <summary>
/// Covers the sticky chat override axis: a persistent message kind that sits alongside, and stays
/// independent of, the range axis (<see cref="ProximityChatMode"/>).
/// </summary>
public class ChatOverrideModeTests
{
    private const string OverrideKey = "BASIC_CHAT_OVERRIDE_MODE";
    private const string LegacyEmoteKey = "BASIC_EMOTEMODE";

    private static IServerPlayer CreatePlayer(ChatOverrideMode? overrideMode = null, bool? legacyEmoteMode = null)
    {
        var player = Substitute.For<IServerPlayer>();
        player.GetModdata(Arg.Any<string>()).Returns((byte[])null!);

        if (overrideMode.HasValue)
        {
            player.GetModdata(OverrideKey).Returns(SerializerUtil.Serialize(overrideMode.Value));
        }

        if (legacyEmoteMode.HasValue)
        {
            player.GetModdata(LegacyEmoteKey).Returns(SerializerUtil.Serialize(legacyEmoteMode.Value));
        }

        return player;
    }

    private static ModConfig CreateConfig(bool enableGlobalOoc = true)
    {
        var config = new ModConfig { EnableGlobalOOC = enableGlobalOoc };
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    private static MessageContext Parse(ModConfig config, IServerPlayer player, string message)
    {
        var transformer = new PlayerChatTransformer(new RPProximityChatSystem { Config = config });
        var context = new MessageContext
        {
            Message = message,
            SendingPlayer = player
        };
        context.SetFlag(MessageContext.IS_PLAYER_CHAT);

        return transformer.Transform(context);
    }

    [Fact]
    public void PlainLineIsSpeechWhenNoOverrideSet()
    {
        var context = Parse(CreateConfig(), CreatePlayer(), "hello there");

        context.HasFlag(MessageContext.IS_SPEECH).Should().BeTrue();
        context.HasFlag(MessageContext.IS_OOC).Should().BeFalse();
    }

    [Fact]
    public void StickyOocRoutesPlainLineToOoc()
    {
        var context = Parse(CreateConfig(), CreatePlayer(ChatOverrideMode.Ooc), "brb food");

        context.HasFlag(MessageContext.IS_OOC).Should().BeTrue();
        context.Message.Should().Be("brb food");
    }

    [Fact]
    public void StickyGlobalOocRoutesPlainLineToGlobalOoc()
    {
        var context = Parse(CreateConfig(), CreatePlayer(ChatOverrideMode.GlobalOoc), "server restart soon");

        context.HasFlag(MessageContext.IS_GLOBAL_OOC).Should().BeTrue();
        context.Message.Should().Be("server restart soon");
    }

    [Fact]
    public void StickyEmoteRoutesPlainLineToEmote()
    {
        var context = Parse(CreateConfig(), CreatePlayer(ChatOverrideMode.Emote), "waves slowly");

        context.HasFlag(MessageContext.IS_EMOTE).Should().BeTrue();
    }

    [Fact]
    public void ExplicitPrefixOverridesStickyModeForOneLine()
    {
        // A player parked in OOC can still emote for a single line with the emote prefix.
        var context = Parse(CreateConfig(), CreatePlayer(ChatOverrideMode.Ooc), "*waves*");

        context.HasFlag(MessageContext.IS_EMOTE).Should().BeTrue();
        context.HasFlag(MessageContext.IS_OOC).Should().BeFalse();
    }

    [Fact]
    public void StickyOocDoesNotStripTrailingDelimiterCharacters()
    {
        // The line carries no delimiters, so a message that merely ends in ')' must survive intact.
        var context = Parse(CreateConfig(), CreatePlayer(ChatOverrideMode.Ooc), "nice one (lol)");

        context.HasFlag(MessageContext.IS_OOC).Should().BeTrue();
        context.Message.Should().Be("nice one (lol)");
    }

    [Fact]
    public void StickyGlobalOocFallsBackToSpeechWhenGlobalOocDisabled()
    {
        // An admin can disable global OOC after a player has already parked in it. Their lines
        // should degrade to speech, not get rejected on every message.
        var context = Parse(CreateConfig(enableGlobalOoc: false), CreatePlayer(ChatOverrideMode.GlobalOoc), "hello");

        context.HasFlag(MessageContext.IS_SPEECH).Should().BeTrue();
        context.HasFlag(MessageContext.IS_GLOBAL_OOC).Should().BeFalse();
        context.State.Should().Be(MessageContextState.CONTINUE);
    }

    [Fact]
    public void LegacyEmoteModeMigratesIntoOverrideAxis()
    {
        var player = CreatePlayer(legacyEmoteMode: true);

        player.GetChatOverrideMode().Should().Be(ChatOverrideMode.Emote);
        player.GetEmoteMode().Should().BeTrue();
    }

    [Fact]
    public void OverrideModeWinsOverLegacyEmoteKey()
    {
        var player = CreatePlayer(ChatOverrideMode.Ooc, legacyEmoteMode: true);

        player.GetChatOverrideMode().Should().Be(ChatOverrideMode.Ooc);
        player.GetEmoteMode().Should().BeFalse();
    }

    [Fact]
    public void GetOocEnabledReflectsOverrideAxis()
    {
        CreatePlayer(ChatOverrideMode.Ooc).GetOOCEnabled().Should().BeTrue();
        CreatePlayer(ChatOverrideMode.Emote).GetOOCEnabled().Should().BeFalse();
        CreatePlayer().GetOOCEnabled().Should().BeFalse();
    }
}
