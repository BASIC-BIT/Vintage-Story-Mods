using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Tests.Support;
using thebasics.Utilities;

namespace thebasics.Tests.Utilities;

public class ChatHelperVerbTests
{
    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    public class IsQuestionTests
    {
        [Theory]
        [InlineData("Where are you?", true)]
        [InlineData("Where are you?  ", true)]
        [InlineData("Where are you?!", true)]
        [InlineData("Wait, what?!?", true)]
        [InlineData("Really?!  ", true)]
        [InlineData("Look out!", false)]
        [InlineData("Well...", false)]
        [InlineData("Hmm,", false)]
        [InlineData("Hello.", false)]
        [InlineData("Hello", false)]
        [InlineData("?", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        [InlineData(null, false)]
        public void DetectsTrailingQuestionMark(string? input, bool expected)
        {
            ChatHelper.IsQuestion(input!).Should().Be(expected);
        }
    }

    public class GetProximityChatVerbTests
    {
        [Theory]
        [InlineData(ProximityChatMode.Normal)]
        [InlineData(ProximityChatMode.Whisper)]
        [InlineData(ProximityChatMode.Yell)]
        public void UsesQuestionVerbWhenMessageEndsInQuestionMark(ProximityChatMode mode)
        {
            var config = CreateConfig();

            var verb = ChatHelper.GetProximityChatVerb(null, mode, config, "Are you there?");

            verb.Should().Be("asks");
        }

        [Theory]
        [InlineData(ProximityChatMode.Normal)]
        [InlineData(ProximityChatMode.Whisper)]
        [InlineData(ProximityChatMode.Yell)]
        public void UsesModeVerbForStatements(ProximityChatMode mode)
        {
            var config = CreateConfig();

            var verb = ChatHelper.GetProximityChatVerb(null, mode, config, "I am here.");

            verb.Should().BeOneOf(config.ProximityChatModeVerbs[mode]);
        }

        [Fact]
        public void FallsBackToModeVerbsWhenQuestionVerbsMissingForMode()
        {
            var config = CreateConfig();
            config.ProximityChatModeQuestionVerbs.Remove(ProximityChatMode.Yell);

            var verb = ChatHelper.GetProximityChatVerb(null, ProximityChatMode.Yell, config, "Anyone there?");

            verb.Should().BeOneOf(config.ProximityChatModeVerbs[ProximityChatMode.Yell]);
        }

        [Fact]
        public void FallsBackToModeVerbsWhenQuestionVerbsEmptyForMode()
        {
            var config = CreateConfig();
            config.ProximityChatModeQuestionVerbs[ProximityChatMode.Normal] = [];

            var verb = ChatHelper.GetProximityChatVerb(null, ProximityChatMode.Normal, config, "Anyone there?");

            verb.Should().BeOneOf(config.ProximityChatModeVerbs[ProximityChatMode.Normal]);
        }

        [Theory]
        [InlineData(ProximityChatMode.Whisper, new[] { "whispers", "mumbles", "mutters" })]
        [InlineData(ProximityChatMode.Yell, new[] { "yells", "shouts", "exclaims" })]
        [InlineData(ProximityChatMode.Normal, new[] { "says", "states", "mentions" })]
        public void FallsBackToTheModesRealDefaultVerbsWhenOmitted(ProximityChatMode mode, string[] expected)
        {
            // The old fallback used the enum name, rendering as `Alice normal "Hello"`.
            var config = CreateConfig();
            config.ProximityChatModeVerbs.Remove(mode);
            config.ProximityChatModeQuestionVerbs.Remove(mode);

            ChatHelper.GetProximityChatVerb(null, mode, config, "Hello.").Should().BeOneOf(expected);
        }

        [Fact]
        public void FallsBackToDefaultVerbsWhenTheListIsPresentButBlank()
        {
            var config = CreateConfig();
            config.ProximityChatModeVerbs[ProximityChatMode.Yell] = ["", "   "];

            ChatHelper.GetProximityChatVerb(null, ProximityChatMode.Yell, config, "Hello.")
                .Should().BeOneOf("yells", "shouts", "exclaims");
        }

        [Fact]
        public void SignLanguageOverridesQuestionVerbs()
        {
            LangTestHelper.EnsureEnglish();
            var config = CreateConfig();

            var verb = ChatHelper.GetProximityChatVerb(LanguageSystem.SignLanguage, ProximityChatMode.Normal, config, "Are you there?");

            verb.Should().Be("thebasics:chat-sign-verb");
        }

        [Fact]
        public void BabbleOverridesQuestionVerbs()
        {
            LangTestHelper.EnsureEnglish();
            var config = CreateConfig();
            config.ProximityChatModeBabbleVerb = "gurgles";

            var verb = ChatHelper.GetProximityChatVerb(LanguageSystem.BabbleLang, ProximityChatMode.Normal, config, "Are you there?");

            verb.Should().Be("gurgles");
        }

        [Fact]
        public void ResolvedVerbIsReusedSoEveryRecipientSeesTheSameOne()
        {
            // Verb lists are a random pick. Resolving per recipient showed two players standing
            // side by side different verbs for the same line, and a third one in the chat log.
            var config = CreateConfig();
            config.ProximityChatModeVerbs[ProximityChatMode.Normal] = ["says", "states", "mentions", "remarks", "observes"];

            var context = new MessageContext { Message = "hello." };
            context.SetFlag(MessageContext.IS_SPEECH);
            context.SetMetadata(MessageContext.SPEECH_VERB, "states");

            var picks = Enumerable
                .Range(0, 50)
                .Select(_ => TransformerSystem.GetResolvedSpeechVerb(context, null, ProximityChatMode.Normal, config))
                .Distinct()
                .ToList();

            picks.Should().ContainSingle().Which.Should().Be("states");
        }

        [Fact]
        public void ResolutionFallsBackWhenNoVerbWasStashed()
        {
            var config = CreateConfig();
            var context = new MessageContext { Message = "anyone there?" };
            context.SetFlag(MessageContext.IS_SPEECH);

            TransformerSystem.GetResolvedSpeechVerb(context, null, ProximityChatMode.Normal, config)
                .Should().Be("asks");
        }

        [Fact]
        public void LanguageOverridesAreIgnoredWhenLanguageSystemDisabled()
        {
            var config = CreateConfig();
            config.EnableLanguageSystem = false;

            var verb = ChatHelper.GetProximityChatVerb(LanguageSystem.SignLanguage, ProximityChatMode.Normal, config, "Are you there?");

            verb.Should().Be("asks");
        }
    }
}
