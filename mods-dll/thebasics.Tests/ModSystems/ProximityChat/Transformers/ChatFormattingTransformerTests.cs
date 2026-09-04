using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat.Transformers;

public class ChatFormattingTransformerTests
{
    [Fact]
    public void AutoCapitalization_ShouldNotTransform_WhenTextNormalizationDisabled()
    {
        var transformer = new AutoCapitalizationTransformer(CreateChatSystem(new ModConfig
        {
            NormalizeProximityChatText = false
        }));
        var context = CreateSpeechContext("hello");

        transformer.ShouldTransform(context).Should().BeFalse();
    }

    [Fact]
    public void AutoPunctuation_ShouldNotTransform_WhenTextNormalizationDisabled()
    {
        var transformer = new AutoPunctuationTransformer(CreateChatSystem(new ModConfig
        {
            NormalizeProximityChatText = false
        }));
        var context = CreateSpeechContext("hello");
        context.SetFlag(MessageContext.IS_ROLEPLAY);

        transformer.ShouldTransform(context).Should().BeFalse();
    }

    [Fact]
    public void WrapSpeechQuotes_UsesConfiguredQuoteDelimiter()
    {
        var config = CreateConfig();

        ChatHelper.WrapSpeechQuotes("hello", config.Languages[0], config, languageEnabled: true)
            .Should().Be("\"hello\"");
    }

    [Theory]
    [InlineData("StandardRoleplay", "Alice says <font color=\"#00AAFF\">\"hello\"</font>")]
    [InlineData("SimpleSpeech", "Alice: <font color=\"#00AAFF\">\"hello\"</font>")]
    [InlineData("PlainProximity", "Alice: <font color=\"#00AAFF\">hello</font>")]
    [InlineData("Prose", "<font color=\"#FF55FF\">hello</font>")]
    public void ICSpeechFormatTransformer_FormatsSpeechByPresentationMode(string presentationMode, string expected)
    {
        var config = CreateConfig();
        config.EmoteColor = "#FF55FF";
        config.Languages[0] = config.Languages[0] with { Color = "#00AAFF" };
        config.ProximityChatPresentationMode = presentationMode;
        config.ProximityChatModeVerbs[ProximityChatMode.Normal] = ["says"];

        var transformer = new ICSpeechFormatTransformer(CreateChatSystem(config));
        var context = CreateSpeechContext("hello");
        context.SetMetadata(MessageContext.FORMATTED_NAME, "Alice");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        context.Message.Should().Be(expected);
    }

    [Fact]
    public void ICSpeechFormatTransformer_ReplacesStandaloneProseNicknameToken()
    {
        var config = CreateConfig();
        config.EmoteColor = "#FF55FF";
        config.Languages[0] = config.Languages[0] with { Color = "#00AAFF" };
        config.ProximityChatPresentationMode = ProximityChatPresentationModes.Prose;

        var transformer = new ICSpeechFormatTransformer(CreateChatSystem(config));
        var context = CreateSpeechContext("@ waves \"hello\"");
        context.SetMetadata(MessageContext.FORMATTED_NAME, "<strong>Alice</strong>");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        context.Message.Should().Be("<strong>Alice</strong><font color=\"#FF55FF\"> waves </font><font color=\"#00AAFF\">\"hello\"</font>");
    }

    [Fact]
    public void ICSpeechFormatTransformer_FormatsProseInsideDistanceFontSizeWrapper()
    {
        var config = CreateConfig();
        config.EmoteColor = "#FF55FF";
        config.Languages[0] = config.Languages[0] with { Color = "#00AAFF" };
        config.ProximityChatPresentationMode = ProximityChatPresentationModes.Prose;

        var transformer = new ICSpeechFormatTransformer(CreateChatSystem(config));
        var context = CreateSpeechContext("<font size=\"16\">@ walks over \"hello\"</font>");
        context.SetMetadata(MessageContext.FORMATTED_NAME, "Alice");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        context.Message.Should().Be("<font size=\"16\">Alice<font color=\"#FF55FF\"> walks over </font><font color=\"#00AAFF\">\"hello\"</font></font>");
    }

    [Fact]
    public void ICSpeechFormatTransformer_AttributesProseToPlayerName_WhenConfigured()
    {
        var config = CreateConfig();
        config.EmoteColor = "#FF55FF";
        config.ProximityChatPresentationMode = ProximityChatPresentationModes.Prose;
        config.AttributeFreeformMessagesToPlayerName = true;

        var transformer = new ICSpeechFormatTransformer(CreateChatSystem(config));
        var context = CreateSpeechContext("hello");
        ((FakeServerPlayer)context.SendingPlayer).PlayerName = "AccountName";
        context.SetMetadata(MessageContext.FORMATTED_NAME, "Alice");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        context.Message.Should().Be("[AccountName] <font color=\"#FF55FF\">hello</font>");
    }

    [Fact]
    public void ICSpeechFormatTransformer_ObfuscatesQuotedProseTextByDistance()
    {
        var config = CreateConfig();
        config.EmoteColor = "#FF55FF";
        config.Languages[0] = config.Languages[0] with { Color = "#00AAFF" };
        config.ProximityChatPresentationMode = ProximityChatPresentationModes.Prose;
        config.ProximityChatModeDistances[ProximityChatMode.Normal] = 10;
        config.ProximityChatModeObfuscationRanges[ProximityChatMode.Normal] = 0;

        var transformer = new ICSpeechFormatTransformer(
            CreateChatSystem(config),
            distanceObfuscationSystem: CreateDistanceObfuscationSystem(config));
        var context = CreateSpeechContext("walks over \"hello there!\"");
        ((FakeServerPlayer)context.SendingPlayer).Entity = CreateEntityPlayer(1, x: 0);
        context.ReceivingPlayer = new FakeServerPlayer { Entity = CreateEntityPlayer(2, x: 10) };
        context.SetMetadata(MessageContext.FORMATTED_NAME, "Alice");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        context.Message.Should().Be("<font color=\"#FF55FF\">walks over </font><font color=\"#00AAFF\">\"***** *****!\"</font>");
    }

    [Fact]
    public void EnvironmentMessageTransformer_AttributesToPlayerName_WhenConfigured()
    {
        var config = CreateConfig();
        config.AttributeFreeformMessagesToPlayerName = true;

        var transformer = new EnvironmentMessageTransformer(CreateChatSystem(config));
        var context = CreateEnvironmentalContext("door creaks");
        ((FakeServerPlayer)context.SendingPlayer).PlayerName = "AccountName";

        transformer.Transform(context);

        context.Message.Should().Be("[AccountName] <i>door creaks</i>");
    }

    [Fact]
    public void EnvironmentMessageTransformer_StoresUnattributedBubbleText_WhenAttributionConfigured()
    {
        var config = CreateConfig();
        config.AttributeFreeformMessagesToPlayerName = true;

        var transformer = new EnvironmentMessageTransformer(CreateChatSystem(config));
        var context = CreateEnvironmentalContext("door creaks");
        ((FakeServerPlayer)context.SendingPlayer).PlayerName = "AccountName";

        transformer.Transform(context);

        context.GetMetadata<string>(MessageContext.BUBBLE_TEXT_BASE).Should().Be("<i>door creaks</i>");
    }

    [Fact]
    public void SpeechBubbleClientDataTransformer_DoesNotAttributeProseBubble_WhenConfigured()
    {
        var config = CreateConfig();
        config.AttributeFreeformMessagesToPlayerName = true;
        config.EmoteColor = "#FF55FF";
        config.Languages[0] = config.Languages[0] with { Color = "#00AAFF" };
        config.ProximityChatPresentationMode = ProximityChatPresentationModes.Prose;

        var transformer = new SpeechBubbleClientDataTransformer(CreateChatSystem(config));
        var context = CreateSpeechContext("walks over \"hello\"");
        ((FakeServerPlayer)context.SendingPlayer).PlayerName = "AccountName";
        ((FakeServerPlayer)context.SendingPlayer).Entity = CreateEntityPlayer(42);
        context.SetMetadata(MessageContext.FORMATTED_NAME, "Alice");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        var clientData = context.GetMetadata<string>("clientData");
        clientData.Should().Contain("from:42,msg:");
        clientData.Should().NotContain("AccountName");
        clientData.Should().Contain("walks over");
        clientData.Should().Contain("hello");
    }

    [Fact]
    public void SpeechBubbleClientDataTransformer_ObfuscatesQuotedProseTextByDistance()
    {
        var config = CreateConfig();
        config.EmoteColor = "#FF55FF";
        config.Languages[0] = config.Languages[0] with { Color = "#00AAFF" };
        config.ProximityChatPresentationMode = ProximityChatPresentationModes.Prose;
        config.ProximityChatModeDistances[ProximityChatMode.Normal] = 10;
        config.ProximityChatModeObfuscationRanges[ProximityChatMode.Normal] = 0;

        var transformer = new SpeechBubbleClientDataTransformer(
            CreateChatSystem(config),
            distanceObfuscationSystem: CreateDistanceObfuscationSystem(config));
        var context = CreateSpeechContext("walks over \"hello there!\"");
        ((FakeServerPlayer)context.SendingPlayer).Entity = CreateEntityPlayer(1, x: 0);
        context.ReceivingPlayer = new FakeServerPlayer { Entity = CreateEntityPlayer(2, x: 10) };
        context.SetMetadata(MessageContext.FORMATTED_NAME, "Alice");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        var clientData = context.GetMetadata<string>("clientData");
        clientData.Should().Contain("walks over");
        clientData.Should().Contain("***** *****!");
        clientData.Should().NotContain("hello there");
    }

    [Fact]
    public void SpeechBubbleClientDataTransformer_UsesUnattributedEnvironmentBubbleBase()
    {
        var config = CreateConfig();
        config.AttributeFreeformMessagesToPlayerName = true;

        var transformer = new SpeechBubbleClientDataTransformer(CreateChatSystem(config));
        var context = CreateEnvironmentalContext("[AccountName] <i>door creaks</i>");
        ((FakeServerPlayer)context.SendingPlayer).Entity = CreateEntityPlayer(42);
        context.SetMetadata(MessageContext.BUBBLE_TEXT_BASE, "<i>door creaks</i>");

        transformer.Transform(context);

        var clientData = context.GetMetadata<string>("clientData");
        clientData.Should().Contain("from:42,msg");
        clientData.Should().NotContain("AccountName");
        clientData.Should().Contain("&lt;i&gt;door creaks&lt;/i&gt;");
    }

    [Fact]
    public void SpeechBubbleClientDataTransformer_EmitsPlainPayload_WhenBubbleModeIsVanilla()
    {
        var transformer = new SpeechBubbleClientDataTransformer(CreateChatSystem(new ModConfig
        {
            OverheadChatBubbleMode = OverheadChatBubbleModes.Vanilla
        }));
        var context = CreateSpeechContext("<font color=\"#00AAFF\">hello</font>");
        ((FakeServerPlayer)context.SendingPlayer).Entity = CreateEntityPlayer(42);

        transformer.ShouldTransform(context).Should().BeTrue();
        transformer.Transform(context);

        context.GetMetadata<string>("clientData").Should().Be("from:42,msg:hello");
    }

    [Fact]
    public void SpeechBubbleClientDataTransformer_RemovesClientData_WhenBubbleModeIsOff()
    {
        var transformer = new SpeechBubbleClientDataTransformer(CreateChatSystem(new ModConfig
        {
            OverheadChatBubbleMode = OverheadChatBubbleModes.Off
        }));
        var context = CreateSpeechContext("hello");
        context.SetMetadata("clientData", "from:1,msg:hello");

        transformer.ShouldTransform(context).Should().BeTrue();
        transformer.Transform(context);

        context.HasMetadata("clientData").Should().BeFalse();
    }

    [Fact]
    public void OverheadChatBubbleMode_UsesLegacyDisableFlag_WhenNewModeMissing()
    {
        OverheadChatBubbleModes.Normalize(string.Empty, legacyDisableRpOverheadBubbles: true)
            .Should().Be(OverheadChatBubbleModes.Vanilla);
    }

    [Theory]
    [InlineData(false, "AccountName")]
    [InlineData(true, "SpectatorNick")]
    public void NameTransformer_LocalOocUsesSpectatorNicknameSetting(bool useNickname, string expectedName)
    {
        var config = CreateConfig();
        config.BoldNicknames = false;
        config.ApplyColorsToNicknames = false;
        config.ApplyColorsToPlayerNames = false;
        config.UseNicknameInOOC = true;
        config.UseNicknameInSpectatorOOC = useNickname;
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Playing);
        player.SetNickname("SpectatorNick");
        var context = new MessageContext { SendingPlayer = player };
        context.SetFlag(MessageContext.IS_OOC);

        new NameTransformer(CreateChatSystem(config)).Transform(context);

        context.GetMetadata<string>(MessageContext.FORMATTED_NAME).Should().Be(expectedName);
    }

    [Fact]
    public void NameTransformer_GlobalOocStillUsesExistingGlobalSettingForSpectator()
    {
        var config = CreateConfig();
        config.BoldNicknames = false;
        config.ApplyColorsToNicknames = false;
        config.UseNicknameInGlobalOOC = true;
        config.UseNicknameInSpectatorOOC = false;
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Playing);
        player.SetNickname("SpectatorNick");
        var context = new MessageContext { SendingPlayer = player };
        context.SetFlag(MessageContext.IS_GLOBAL_OOC);

        new NameTransformer(CreateChatSystem(config)).Transform(context);

        context.GetMetadata<string>(MessageContext.FORMATTED_NAME).Should().Be("SpectatorNick");
    }

    [Fact]
    public void PlacedEnvironmentTransformer_RejectsDisallowedSpectatorPlacement()
    {
        LangTestHelper.EnsureEnglish();
        var config = CreateConfig();
        config.AllowSpectatorPlacedEnvironmentalMessages = false;
        var player = CreatePlayer(EnumGameMode.Spectator, EnumClientState.Playing);
        var chatSystem = CreateChatSystem(config);
        chatSystem.ProximityChatId = 23;
        var context = new MessageContext { SendingPlayer = player };
        context.SetFlag(MessageContext.IS_PLACED_ENVIRONMENTAL);

        new PlacedEnvironmentTransformer(chatSystem).Transform(context);

        context.State.Should().Be(MessageContextState.STOP);
        var sentMessage = player.SentMessages.Should().ContainSingle().Which;
        sentMessage.GroupId.Should().Be(23);
        sentMessage.Message.Should().Be("thebasics:chat-spectator-env-placement-disabled");
        sentMessage.ChatType.Should().Be(EnumChatType.CommandError);
    }

    private static RPProximityChatSystem CreateChatSystem(ModConfig config)
    {
        config.InitializeDefaultsIfNeeded();
        return new RPProximityChatSystem { Config = config };
    }

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    private static DistanceObfuscationSystem CreateDistanceObfuscationSystem(ModConfig config)
    {
        return new DistanceObfuscationSystem(null, null, config);
    }

    private static MessageContext CreateSpeechContext(string message)
    {
        var player = new FakeServerPlayer();

        var context = new MessageContext
        {
            Message = message,
            SendingPlayer = player
        };
        context.SetFlag(MessageContext.IS_SPEECH);
        return context;
    }

    private static MessageContext CreateEnvironmentalContext(string message)
    {
        var player = new FakeServerPlayer();

        var context = new MessageContext
        {
            Message = message,
            SendingPlayer = player
        };
        context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
        return context;
    }

    private static EntityPlayer CreateEntityPlayer(long entityId, double x = 0, double y = 0, double z = 0)
    {
        var entity = (EntityPlayer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
        entity.EntityId = entityId;
        var posField = typeof(Entity).GetField("<Pos>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        posField!.SetValue(entity, new EntityPos(x, y, z));
        return entity;
    }

    private static FakeServerPlayer CreatePlayer(EnumGameMode gameMode, EnumClientState connectionState)
    {
        var worldData = Substitute.For<IWorldPlayerData>();
        worldData.CurrentGameMode.Returns(gameMode);
        return new FakeServerPlayer("player-1", "AccountName")
        {
            WorldData = worldData,
            ConnectionState = connectionState
        };
    }
}
