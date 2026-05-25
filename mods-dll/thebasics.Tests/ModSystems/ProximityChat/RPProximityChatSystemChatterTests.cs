using FluentAssertions;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class RPProximityChatSystemChatterTests
{
    [Theory]
    [InlineData("walks over", 0)]
    [InlineData("walks over \"hello\"", 5)]
    [InlineData("\"hi\" and \"bye\"", 5)]
    public void CountQuotedSpeechLength_CountsOnlyQuotedSegments(string message, int expected)
    {
        RPProximityChatSystem.CountQuotedSpeechLength(message).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(24, 2)]
    [InlineData(25, 3)]
    [InlineData(80, 3)]
    [InlineData(81, 4)]
    public void GetNoteCount_UsesConfiguredLengthBuckets(int speechLength, int expected)
    {
        ChatterMessagePlanner.GetNoteCount(speechLength).Should().Be(expected);
    }

    [Fact]
    public void TryGetSpeechLength_UsesFullSpeechOutsideProseMode()
    {
        var context = CreateSpeechContext("hello there");
        var config = CreateConfig(ProximityChatPresentationModes.StandardRoleplay);

        ChatterMessagePlanner.TryGetSpeechLength(context, config, out var speechLength).Should().BeTrue();
        speechLength.Should().Be("hello there".Length);
    }

    [Fact]
    public void TryGetSpeechLength_UsesQuotedSpeechInProseMode()
    {
        var context = CreateSpeechContext("Alice says \"hello\" and waves");
        var config = CreateConfig(ProximityChatPresentationModes.Prose);

        ChatterMessagePlanner.TryGetSpeechLength(context, config, out var speechLength).Should().BeTrue();
        speechLength.Should().Be(5);
    }

    [Fact]
    public void TryGetSpeechLength_SkipsSilentSignLanguage()
    {
        var context = CreateSpeechContext("hello");
        context.SetMetadata(MessageContext.LANGUAGE, LanguageSystem.SignLanguage);

        ChatterMessagePlanner.TryGetSpeechLength(context, CreateConfig(), out _).Should().BeFalse();
    }

    [Fact]
    public void TryGetSpeechLength_UsesQuotedSpeechInEmotes()
    {
        var context = new MessageContext
        {
            Message = "Alice waves and says \"hello\"."
        };
        context.SetFlag(MessageContext.IS_EMOTE);

        ChatterMessagePlanner.TryGetSpeechLength(context, CreateConfig(), out var speechLength).Should().BeTrue();
        speechLength.Should().Be(5);
    }

    [Fact]
    public void ForRecipient_AppliesSelfVolumeMultiplierOnlyForSender()
    {
        var message = new ChatterSoundMessage
        {
            EntityId = 10,
            TalkType = 2,
            NoteCount = 3,
            Volume = 0.8f,
            Pitch = 1.1f
        };

        ChatterMessagePlanner.ForRecipient(message, isSelf: false, selfVolumeMultiplier: 0.25f)
            .Should().BeSameAs(message);

        var selfMessage = ChatterMessagePlanner.ForRecipient(message, isSelf: true, selfVolumeMultiplier: 0.25f);
        selfMessage.Should().NotBeSameAs(message);
        selfMessage.EntityId.Should().Be(message.EntityId);
        selfMessage.TalkType.Should().Be(message.TalkType);
        selfMessage.NoteCount.Should().Be(message.NoteCount);
        selfMessage.Volume.Should().BeApproximately(0.2f, 0.0001f);
        selfMessage.Pitch.Should().Be(message.Pitch);
    }

    [Theory]
    [InlineData(ChatTypingIndicatorState.None, true, ChatTypingIndicatorState.Typing)]
    [InlineData(ChatTypingIndicatorState.None, false, ChatTypingIndicatorState.None)]
    [InlineData(ChatTypingIndicatorState.Typing, false, ChatTypingIndicatorState.Typing)]
    [InlineData(ChatTypingIndicatorState.ChatOpenComposing, true, ChatTypingIndicatorState.ChatOpenComposing)]
    public void NormalizeTypingIndicatorState_PreservesStateAndSupportsLegacyIsTypingFallback(
        ChatTypingIndicatorState state,
        bool isTyping,
        ChatTypingIndicatorState expected)
    {
        RPProximityChatSystem.NormalizeTypingIndicatorState(state, isTyping).Should().Be(expected);
    }

    private static MessageContext CreateSpeechContext(string speechText)
    {
        var context = new MessageContext();
        context.SetFlag(MessageContext.IS_SPEECH);
        context.SetSpeechText(speechText);
        return context;
    }

    private static ModConfig CreateConfig(string presentationMode = ProximityChatPresentationModes.StandardRoleplay)
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        config.ProximityChatPresentationMode = presentationMode;
        return config;
    }
}
