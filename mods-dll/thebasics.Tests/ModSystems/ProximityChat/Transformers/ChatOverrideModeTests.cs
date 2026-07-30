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

        // OOC delivery re-checks OOCTogglePermission, and NSubstitute denies privileges by default.
        player.HasPrivilege(Arg.Any<string>()).Returns(true);

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
        // The stale-override rejections resolve a lang key, which throws if no translation service
        // has been registered. Without this the class passes or fails on test ordering alone.
        LangTestHelper.EnsureEnglish();

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
    public void StickyGlobalOocRejectsTheLineWhenGlobalOocDisabled()
    {
        // An admin can disable global OOC after a player has already parked in it. Downgrading the
        // line to in-character speech would publish something the player believed was going to an
        // out-of-character channel, so reject it exactly as an explicit (( )) prefix would.
        var context = Parse(CreateConfig(enableGlobalOoc: false), CreatePlayer(ChatOverrideMode.GlobalOoc), "brb, kid woke up");

        context.State.Should().Be(MessageContextState.STOP);
        context.HasFlag(MessageContext.IS_SPEECH).Should().BeFalse();
        context.HasFlag(MessageContext.IS_GLOBAL_OOC).Should().BeFalse();
    }

    [Fact]
    public void StaleOverrideRejectsTheLineWhenRpChatIsDisabled()
    {
        // Same hazard as the disabled-global-OOC case: downgrading to speech would publish a line
        // the player meant for an out-of-character channel. Reject it once, clear the mode, and let
        // the next line through as ordinary chat.
        var config = CreateConfig();
        config.DisableRPChat = true;

        var context = Parse(config, CreatePlayer(ChatOverrideMode.Ooc), "brb, wife's calling");

        context.State.Should().Be(MessageContextState.STOP);
        context.HasFlag(MessageContext.IS_SPEECH).Should().BeFalse();
        context.HasFlag(MessageContext.IS_OOC).Should().BeFalse();
    }

    [Fact]
    public void ExplicitRangeCommandBeatsAGlobalOocOverride()
    {
        // A range command names a range and global OOC has none, so honouring the override would
        // turn "/w he's lying" into a server-wide broadcast of a line the player chose /w for.
        LangTestHelper.EnsureEnglish();
        var transformer = new PlayerChatTransformer(new RPProximityChatSystem { Config = CreateConfig() });
        var context = new MessageContext
        {
            Message = "he's lying about the trade",
            SendingPlayer = CreatePlayer(ChatOverrideMode.GlobalOoc)
        };
        context.SetFlag(MessageContext.IS_PLAYER_CHAT);
        context.SetFlag(MessageContext.IS_EXPLICIT_RANGE_COMMAND);

        var result = transformer.Transform(context);

        result.HasFlag(MessageContext.IS_GLOBAL_OOC).Should().BeFalse();
        result.HasFlag(MessageContext.IS_SPEECH).Should().BeTrue();
    }

    [Fact]
    public void PlainLineStillHonoursAGlobalOocOverride()
    {
        // The bypass must be scoped to explicit range commands, not applied to ordinary typing.
        var context = Parse(CreateConfig(), CreatePlayer(ChatOverrideMode.GlobalOoc), "anyone around?");

        context.HasFlag(MessageContext.IS_GLOBAL_OOC).Should().BeTrue();
    }

    [Fact]
    public void StickyOocIsRejectedWhenTheOocToggleIsTurnedOffLive()
    {
        // AllowOOCToggle gates entry, so it has to gate delivery too, or an admin flipping it off
        // mid-event leaves everyone already parked in OOC posting OOC indefinitely.
        var config = CreateConfig();
        config.AllowOOCToggle = false;

        var context = Parse(config, CreatePlayer(ChatOverrideMode.Ooc), "still chatting");

        context.State.Should().Be(MessageContextState.STOP);
        context.HasFlag(MessageContext.IS_OOC).Should().BeFalse();
    }

    [Fact]
    public void StickyOocIsRejectedWhenThePlayerLosesTheTogglePrivilege()
    {
        // Roles change at runtime with no config edit, so the privilege that gates entry has to be
        // re-checked on delivery. Otherwise a demoted player keeps posting OOC indefinitely.
        var player = CreatePlayer(ChatOverrideMode.Ooc);
        player.HasPrivilege(Arg.Any<string>()).Returns(false);

        var context = Parse(CreateConfig(), player, "still chatting");

        context.State.Should().Be(MessageContextState.STOP);
        context.HasFlag(MessageContext.IS_OOC).Should().BeFalse();
    }

    [Fact]
    public void EmoteOverrideSurvivesLosingTheOocTogglePrivilege()
    {
        // The OOC gates must not leak onto emote mode, which is deliberately ungated.
        var player = CreatePlayer(ChatOverrideMode.Emote);
        player.HasPrivilege(Arg.Any<string>()).Returns(false);

        var context = Parse(CreateConfig(), player, "waves");

        context.HasFlag(MessageContext.IS_EMOTE).Should().BeTrue();
        context.State.Should().Be(MessageContextState.CONTINUE);
    }

    [Fact]
    public void GlobalOocPrefixIsRefusedWhenRpChatIsDisabled()
    {
        // The ((( ))) prefix used to check EnableGlobalOOC on its own, so it kept broadcasting
        // server-wide while every sibling path refused. It now asks the same predicate.
        var config = CreateConfig();
        config.DisableRPChat = true;

        var context = Parse(config, CreatePlayer(), "(((server restart soon)))");

        context.State.Should().Be(MessageContextState.STOP);
        context.HasFlag(MessageContext.IS_GLOBAL_OOC).Should().BeFalse();
    }

    [Fact]
    public void GlobalOocPrefixStillWorksWhenAllGatesAllowIt()
    {
        var context = Parse(CreateConfig(), CreatePlayer(), "(((server restart soon)))");

        context.HasFlag(MessageContext.IS_GLOBAL_OOC).Should().BeTrue();
        context.State.Should().Be(MessageContextState.CONTINUE);
    }

    [Fact]
    public void StaleTypeRejectionReportsBothTheResetAndTheDroppedLine()
    {
        // The gate's refusal copy alone explains why the type is unavailable but not that the line
        // was dropped or the type reset, so a player would retry blindly.
        LangTestHelper.EnsureEnglish();
        var config = CreateConfig();
        config.AllowOOCToggle = false;
        var sent = new List<string>();
        var player = CreatePlayer(ChatOverrideMode.Ooc);
        player.When(p => p.SendMessage(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<EnumChatType>(), Arg.Any<string>()))
            .Do(call => sent.Add(call.ArgAt<string>(1)));

        Parse(config, player, "still chatting");

        sent.Should().ContainSingle().Which.Should().Be("thebasics:chat-type-reset-dropped-message");
    }

    [Fact]
    public void RefusedPrefixDoesNotClaimTheTypeWasReset()
    {
        // The prefix path refuses without touching the stored type, so claiming a reset would lie.
        LangTestHelper.EnsureEnglish();
        var config = CreateConfig(enableGlobalOoc: false);
        var sent = new List<string>();
        var player = CreatePlayer();
        player.When(p => p.SendMessage(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<EnumChatType>(), Arg.Any<string>()))
            .Do(call => sent.Add(call.ArgAt<string>(1)));

        Parse(config, player, "((server restart soon))");

        sent.Should().ContainSingle().Which.Should().Be("thebasics:chat-message-not-sent");
    }

    [Fact]
    public void PlainSpeechIsUnaffectedWhenRpChatIsDisabled()
    {
        var config = CreateConfig();
        config.DisableRPChat = true;

        var context = Parse(config, CreatePlayer(), "hello");

        context.State.Should().Be(MessageContextState.CONTINUE);
        context.HasFlag(MessageContext.IS_SPEECH).Should().BeTrue();
    }

    [Fact]
    public void StaleOverrideIsClearedWhenRpChatIsDisabled()
    {
        // DisableRPChat needs a restart to change. Skipping the override without clearing it means
        // flipping the setting back off later drops the player into a channel they never rejoined.
        var config = CreateConfig();
        config.DisableRPChat = true;
        var player = CreatePlayer(ChatOverrideMode.GlobalOoc);
        byte[]? written = null;
        player.When(p => p.SetModdata(OverrideKey, Arg.Any<byte[]>()))
            .Do(call => written = call.ArgAt<byte[]>(1));

        Parse(config, player, "hello");

        written.Should().NotBeNull();
        SerializerUtil.Deserialize(written, ChatOverrideMode.GlobalOoc).Should().Be(ChatOverrideMode.None);
    }

    [Fact]
    public void StickyGlobalOocIsClearedNotMaskedWhenGlobalOocDisabled()
    {
        // Masking alone would leave the stored value intact, so re-enabling global OOC later would
        // silently drop the player back into a server-wide channel on their next ordinary line.
        var player = CreatePlayer(ChatOverrideMode.GlobalOoc);
        byte[]? written = null;
        player.When(p => p.SetModdata(OverrideKey, Arg.Any<byte[]>()))
            .Do(call => written = call.ArgAt<byte[]>(1));

        Parse(CreateConfig(enableGlobalOoc: false), player, "hello");

        written.Should().NotBeNull("the stale override must be persisted as cleared, not just masked");
        SerializerUtil.Deserialize(written, ChatOverrideMode.GlobalOoc).Should().Be(ChatOverrideMode.None);
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
