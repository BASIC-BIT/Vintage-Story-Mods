using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using Vintagestory.API.Common;
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
        context.SendingPlayer.PlayerName.Returns("AccountName");
        context.SetMetadata(MessageContext.FORMATTED_NAME, "Alice");
        context.SetMetadata(MessageContext.LANGUAGE, config.Languages[0]);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Normal);

        transformer.Transform(context);

        context.Message.Should().Be("[AccountName] <font color=\"#FF55FF\">hello</font>");
    }

    [Fact]
    public void EnvironmentMessageTransformer_AttributesToPlayerName_WhenConfigured()
    {
        var config = CreateConfig();
        config.AttributeFreeformMessagesToPlayerName = true;

        var transformer = new EnvironmentMessageTransformer(CreateChatSystem(config));
        var context = CreateEnvironmentalContext("door creaks");
        context.SendingPlayer.PlayerName.Returns("AccountName");

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
        context.SendingPlayer.PlayerName.Returns("AccountName");

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
        context.SendingPlayer.PlayerName.Returns("AccountName");
        context.SendingPlayer.Entity.Returns(CreateEntityPlayer(42));
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
    public void SpeechBubbleClientDataTransformer_UsesUnattributedEnvironmentBubbleBase()
    {
        var config = CreateConfig();
        config.AttributeFreeformMessagesToPlayerName = true;

        var transformer = new SpeechBubbleClientDataTransformer(CreateChatSystem(config));
        var context = CreateEnvironmentalContext("[AccountName] <i>door creaks</i>");
        context.SendingPlayer.Entity.Returns(CreateEntityPlayer(42));
        context.SetMetadata(MessageContext.BUBBLE_TEXT_BASE, "<i>door creaks</i>");

        transformer.Transform(context);

        var clientData = context.GetMetadata<string>("clientData");
        clientData.Should().Contain("from:42,msg");
        clientData.Should().NotContain("AccountName");
        clientData.Should().Contain("&lt;i&gt;door creaks&lt;/i&gt;");
    }

    [Fact]
    public void SpeechBubbleClientDataTransformer_ShouldNotTransform_WhenBubbleModeIsVanilla()
    {
        var transformer = new SpeechBubbleClientDataTransformer(CreateChatSystem(new ModConfig
        {
            OverheadChatBubbleMode = OverheadChatBubbleModes.Vanilla
        }));
        var context = CreateSpeechContext("hello");

        transformer.ShouldTransform(context).Should().BeFalse();
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

    private static MessageContext CreateSpeechContext(string message)
    {
        var player = Substitute.For<IServerPlayer>();
        player.GetModdata(Arg.Any<string>()).Returns((byte[])null!);

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
        var player = Substitute.For<IServerPlayer>();

        var context = new MessageContext
        {
            Message = message,
            SendingPlayer = player
        };
        context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
        return context;
    }

    private static EntityPlayer CreateEntityPlayer(long entityId)
    {
        var entity = (EntityPlayer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(EntityPlayer));
        entity.EntityId = entityId;
        return entity;
    }
}
