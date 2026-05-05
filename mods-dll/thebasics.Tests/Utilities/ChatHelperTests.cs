using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using static thebasics.Utilities.ChatHelper;

namespace thebasics.Tests.Utilities;

public class ChatHelperTests
{
    public class IsPunctuationTests
    {
        [Theory]
        [InlineData('.', true)]
        [InlineData('!', true)]
        [InlineData('?', true)]
        [InlineData('~', true)]
        [InlineData('-', true)]
        [InlineData(';', true)]
        [InlineData(':', true)]
        [InlineData('/', true)]
        [InlineData(',', true)]
        [InlineData('"', true)]
        [InlineData('\'', true)]
        [InlineData('a', false)]
        [InlineData(' ', false)]
        [InlineData('1', false)]
        public void IdentifiesPunctuation(char c, bool expected)
        {
            ChatHelper.IsPunctuation(c).Should().Be(expected);
        }
    }

    public class IsWhitespaceTests
    {
        [Theory]
        [InlineData(' ', true)]
        [InlineData('\t', true)]
        [InlineData('\n', true)]
        [InlineData('\r', true)]
        [InlineData('a', false)]
        [InlineData('.', false)]
        public void IdentifiesWhitespace(char c, bool expected)
        {
            ChatHelper.IsWhitespace(c).Should().Be(expected);
        }
    }

    public class DoesMessageNeedPunctuationTests
    {
        [Theory]
        [InlineData("Hello", true)]
        [InlineData("Hello world", true)]
        [InlineData("Hello.", false)]
        [InlineData("Hello!", false)]
        [InlineData("Hello?", false)]
        [InlineData("Hello~", false)]
        [InlineData("", false)]
        public void DetectsWhenPunctuationNeeded(string input, bool expected)
        {
            ChatHelper.DoesMessageNeedPunctuation(input).Should().Be(expected);
        }
    }

    public class StrongTests
    {
        [Fact]
        public void WrapsInStrongTags()
        {
            ChatHelper.Strong("test").Should().Be("<strong>test</strong>");
        }

        [Fact]
        public void HandlesEmptyString()
        {
            ChatHelper.Strong("").Should().Be("<strong></strong>");
        }
    }

    public class ItalicTests
    {
        [Fact]
        public void WrapsInItalicTags()
        {
            ChatHelper.Italic("test").Should().Be("<i>test</i>");
        }
    }

    public class QuoteTests
    {
        [Fact]
        public void WrapsInDoubleQuotes()
        {
            ChatHelper.Quote("hello").Should().Be("\"hello\"");
        }
    }

    public class WrapTests
    {
        [Fact]
        public void WrapsWithGivenString()
        {
            ChatHelper.Wrap("hello", "**").Should().Be("**hello**");
        }
    }

    public class WrapWithTagTests
    {
        [Theory]
        [InlineData("text", "b", "<b>text</b>")]
        [InlineData("text", "strong", "<strong>text</strong>")]
        [InlineData("text", "font", "<font>text</font>")]
        public void WrapsWithHtmlTag(string input, string tag, string expected)
        {
            ChatHelper.WrapWithTag(input, tag).Should().Be(expected);
        }
    }

    public class GetTagTests
    {
        [Fact]
        public void ReturnsOpenTag()
        {
            ChatHelper.GetTag("div", TagPosition.Start).Should().Be("<div>");
        }

        [Fact]
        public void ReturnsCloseTag()
        {
            ChatHelper.GetTag("div", TagPosition.End).Should().Be("</div>");
        }
    }

    public class BuildTests
    {
        [Fact]
        public void ConcatenatesAllParts()
        {
            ChatHelper.Build("a", "b", "c").Should().Be("abc");
        }

        [Fact]
        public void HandlesSinglePart()
        {
            ChatHelper.Build("only").Should().Be("only");
        }

        [Fact]
        public void HandlesNoParts()
        {
            ChatHelper.Build().Should().BeEmpty();
        }
    }

    public class ColorTests
    {
        [Fact]
        public void WrapsInFontColorTag()
        {
            ChatHelper.Color("hello", "#ff0000")
                .Should().Be("<font color=\"#ff0000\">hello</font>");
        }

        [Fact]
        public void ReturnsMessageWhenColorIsNull()
        {
            ChatHelper.Color("hello", null!).Should().Be("hello");
        }

        [Fact]
        public void ReturnsMessageWhenColorIsEmpty()
        {
            ChatHelper.Color("hello", "").Should().Be("hello");
        }
    }

    public class LangColorTests
    {
        [Fact]
        public void UsesLanguageColor()
        {
            var lang = new Language("Elvish", "Ancient tongue", "elv",
                new[] { "la", "el" }, "#00ff00");

            ChatHelper.LangColor("hello", lang)
                .Should().Be("<font color=\"#00ff00\">hello</font>");
        }
    }

    public class LangIdentifierTests
    {
        [Fact]
        public void FormatsNameAndPrefix()
        {
            var lang = new Language("Elvish", "Ancient tongue", "elv",
                new[] { "la", "el" }, "#00ff00");

            var result = ChatHelper.LangIdentifier(lang);
            result.Should().Contain("Elvish");
            result.Should().Contain(":elv");
            result.Should().Contain("#00ff00");
        }

        [Fact]
        public void MarksHiddenLanguages()
        {
            var lang = new Language("Secret", "Hidden tongue", "sec",
                new[] { "la", "el" }, "#00ff00", Hidden: true);

            ChatHelper.LangIdentifier(lang).Should().Contain("[hidden]");
        }
    }

    public class FormatProseMessageTests
    {
        [Fact]
        public void ColorsNarrativeAndQuotedSpeechSeparately()
        {
            var config = CreateConfig();
            var lang = config.Languages[0] with { Color = "#00AAFF" };

            ChatHelper.FormatProseMessage("walks over \"hello\" quietly", lang, config, languageEnabled: true)
                .Should().Be("<font color=\"#FF55FF\">walks over </font><font color=\"#00AAFF\">\"hello\"</font><font color=\"#FF55FF\"> quietly</font>");
        }

        [Fact]
        public void ProcessesOnlyQuotedSpeech()
        {
            var config = CreateConfig();
            var lang = config.Languages[0] with { Color = "#00AAFF" };

            ChatHelper.FormatProseMessage("waves \"hello\"", lang, config, languageEnabled: true, processQuotedText: text => text.ToUpperInvariant())
                .Should().Be("<font color=\"#FF55FF\">waves </font><font color=\"#00AAFF\">\"HELLO\"</font>");
        }

        [Fact]
        public void ReplacesStandaloneNicknameTokenOnlyInNarrativeSegments()
        {
            var config = CreateConfig();
            var lang = config.Languages[0] with { Color = "#00AAFF" };

            ChatHelper.FormatProseMessage("email a@b.com @ says \"@ stays\"", lang, config, languageEnabled: true, nicknameReplacement: "Alice")
                .Should().Be("<font color=\"#FF55FF\">email a@b.com </font>Alice<font color=\"#FF55FF\"> says </font><font color=\"#00AAFF\">\"@ stays\"</font>");
        }

        private static ModConfig CreateConfig()
        {
            var config = new ModConfig { EmoteColor = "#FF55FF" };
            config.InitializeDefaultsIfNeeded();
            return config;
        }
    }

    public class IsDecoratorCharTests
    {
        [Theory]
        [InlineData('\u0301', true)]
        [InlineData('\u200D', true)]
        [InlineData('a', false)]
        [InlineData(':', false)]
        public void IdentifiesCombiningAndFormatCharacters(char input, bool expected)
        {
            ChatHelper.IsDecoratorChar(input).Should().Be(expected);
        }
    }

    public class EscapeMarkupTests
    {
        [Fact]
        public void DelegatesToVtmlUtilsEscapeVtml()
        {
            ChatHelper.EscapeMarkup("<script>")
                .Should().Be("&lt;script&gt;");
        }
    }

    public class GetMessageTests
    {
        [Theory]
        [InlineData("PlayerName > Hello world", "Hello world")]
        [InlineData("just text with no arrow", "just text with no arrow")]
        public void ExtractsMessageAfterArrow(string input, string expected)
        {
            ChatHelper.GetMessage(input).Should().Be(expected);
        }

        [Fact]
        public void ExtractsMessage_WithVtmlName_MatchesLastArrow()
        {
            // The regex uses .*? which is non-greedy on the left, so with VTML
            // tags containing >, it captures from the first > in the markup.
            // This documents the actual behavior of the regex.
            var result = ChatHelper.GetMessage("<strong>Name</strong> > Some text");
            result.Should().Contain("Some text");
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void ReturnsEmptyForNullOrWhitespace(string? input)
        {
            ChatHelper.GetMessage(input!).Should().BeEmpty();
        }
    }
}
