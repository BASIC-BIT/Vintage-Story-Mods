using FluentAssertions;
using thebasics.Utilities;

namespace thebasics.Tests.Utilities;

public class VtmlUtilsTests
{
    public class EscapeVtml
    {
        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("<b>bold</b>", "&lt;b&gt;bold&lt;/b&gt;")]
        [InlineData("a < b > c", "a &lt; b &gt; c")]
        [InlineData("<script>alert('xss')</script>", "&lt;script&gt;alert('xss')&lt;/script&gt;")]
        public void EscapesAngleBrackets(string input, string expected)
        {
            VtmlUtils.EscapeVtml(input).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsInputWhenNullOrEmpty(string? input)
        {
            VtmlUtils.EscapeVtml(input!).Should().Be(input);
        }

        [Fact]
        public void DoesNotEscapeAmpersands()
        {
            // VS doesn't escape & in chat messages
            VtmlUtils.EscapeVtml("rock & roll").Should().Be("rock & roll");
        }

        [Fact]
        public void DoesNotEscapeQuotes()
        {
            VtmlUtils.EscapeVtml("he said \"hello\"").Should().Be("he said \"hello\"");
        }
    }

    public class UnescapeVtml
    {
        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("&lt;b&gt;bold&lt;/b&gt;", "<b>bold</b>")]
        [InlineData("non&nbsp;breaking", "non breaking")]
        public void UnescapesEntities(string input, string expected)
        {
            VtmlUtils.UnescapeVtml(input).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsInputWhenNullOrEmpty(string? input)
        {
            VtmlUtils.UnescapeVtml(input!).Should().Be(input);
        }
    }

    public class EscapeUnescapeRoundTrip
    {
        [Theory]
        [InlineData("hello world")]
        [InlineData("<b>bold</b>")]
        [InlineData("a < b > c")]
        [InlineData("no special chars")]
        public void RoundTripsCorrectly(string original)
        {
            var escaped = VtmlUtils.EscapeVtml(original);
            var unescaped = VtmlUtils.UnescapeVtml(escaped);
            unescaped.Should().Be(original);
        }
    }

    public class StripUserVtmlTags
    {
        [Theory]
        [InlineData("<b>bold</b>", "bold")]
        [InlineData("<strong>bold</strong>", "bold")]
        [InlineData("<foo>Hi</foo>", "Hi")]
        [InlineData("&lt;b&gt;bold&lt;/b&gt;", "bold")]
        [InlineData("&lt;foo&gt;Hi&lt;/foo&gt;", "Hi")]
        [InlineData("a < b > c", "a < b > c")]
        [InlineData("a &lt; b &gt; c", "a &lt; b &gt; c")]
        public void RemovesRawAndEscapedTagsOnly(string input, string expected)
        {
            VtmlUtils.StripUserVtmlTags(input).Should().Be(expected);
        }
    }

    public class UnescapeRenderableVtmlTags
    {
        [Theory]
        [InlineData("&lt;font color=\"#fff\"&gt;Hi&lt;/font&gt;", "<font color=\"#fff\">Hi</font>")]
        [InlineData("&lt;i&gt;Hi&lt;/i&gt;", "<i>Hi</i>")]
        [InlineData("&lt;strong&gt;Hi&lt;/strong&gt;", "<strong>Hi</strong>")]
        [InlineData("&lt;b&gt;Hi&lt;/b&gt;", "&lt;b&gt;Hi&lt;/b&gt;")]
        [InlineData("a &lt; b &gt; c", "a &lt; b &gt; c")]
        public void UnescapesOnlyTrustedRenderableTags(string input, string expected)
        {
            VtmlUtils.UnescapeRenderableVtmlTags(input).Should().Be(expected);
        }
    }

    public class NormalizeVtmlForRendering
    {
        [Theory]
        [InlineData("<b>bold</b>", "bold")]
        [InlineData("<B>bold</B>", "bold")]
        [InlineData("<foo>bold</foo>", "bold")]
        [InlineData("<strong>bold</strong>", "<strong>bold</strong>")]
        [InlineData("<FONT color=\"#fff\">bold</FONT>", "<font color=\"#fff\">bold</font>")]
        [InlineData("<br>", "<br>")]
        [InlineData("<box>bold</box>", "bold")]
        public void KeepsOnlySupportedRendererTags(string input, string expected)
        {
            VtmlUtils.NormalizeVtmlForRendering(input).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsInputWhenNullOrEmpty(string? input)
        {
            VtmlUtils.NormalizeVtmlForRendering(input!).Should().Be(input);
        }
    }

    public class ContainsVtmlSpecialChars
    {
        [Theory]
        [InlineData("<tag>", true)]
        [InlineData("no tags", false)]
        [InlineData("a < b", true)]
        [InlineData("a > b", true)]
        [InlineData("", false)]
        public void DetectsAngleBrackets(string input, bool expected)
        {
            VtmlUtils.ContainsVtmlSpecialChars(input).Should().Be(expected);
        }

        [Fact]
        public void ReturnsFalseForNull()
        {
            VtmlUtils.ContainsVtmlSpecialChars(null!).Should().BeFalse();
        }
    }

    /// <summary>
    /// StripVtmlTags tests are skipped in environments without VintagestoryAPI.dll
    /// because the method signature references ILogger, causing assembly load at JIT time.
    /// These tests should be enabled once the VS DLL is available in the test runner.
    /// </summary>
    public class StripVtmlTags
    {
        // StripVtmlTags references ILogger (from VintagestoryAPI) in its signature,
        // so even the regex fallback path triggers assembly loading.
        // These are tested manually or in integration tests with the VS DLL present.
    }

    public class ContainsVtmlCriticalChars
    {
        [Fact]
        public void DelegatesToContainsVtmlSpecialChars()
        {
            // These should return the same result
            var input = "<test>";
            VtmlUtils.ContainsVtmlCriticalChars(input)
                .Should().Be(VtmlUtils.ContainsVtmlSpecialChars(input));
        }
    }

    /// <summary>
    /// A token wider than the bubble is clipped by VS unless it happens to start a line,
    /// so long tokens are split before the text reaches the engine.
    /// </summary>
    public class BreakLongTokens
    {
        // Fixed-width stand-in for Cairo: every char is 10px wide.
        private const double CharWidthPx = 10;
        private const double MaxWidthPx = 100;

        private static readonly Func<string, double> Measure = text => text.Length * CharWidthPx;

        private static string Break(string vtml, double maxWidthPx = MaxWidthPx)
        {
            return VtmlUtils.BreakLongTokens(vtml, Measure, maxWidthPx);
        }

        [Fact]
        public void SplitsLongTokenInTheMiddleOfAMessage()
        {
            // The reported bug: "Check this <url>" clipped the url instead of wrapping it.
            Break("Check this aaaaaaaaaaaaaaaaaaaaaaaaa")
                .Should().Be("Check this aaaaaaaaaa\naaaaaaaaaa\naaaaa");
        }

        [Fact]
        public void SplitsLongTokenAtTheStart()
        {
            // Already wrapped correctly by the engine; must not regress.
            Break("aaaaaaaaaaaaaaaaaaaaaaaaa tail")
                .Should().Be("aaaaaaaaaa\naaaaaaaaaa\naaaaa tail");
        }

        [Fact]
        public void SplitsLongTokenAtTheEnd()
        {
            Break("tail aaaaaaaaaaaaaaaaaaaaaaaaa")
                .Should().Be("tail aaaaaaaaaa\naaaaaaaaaa\naaaaa");
        }

        [Theory]
        [InlineData("Just a normal sentence here.")]
        [InlineData("short\nlines\tand  spacing preserved")]
        [InlineData("<i>italic</i> and <font color=\"#ff0000\">colored</font>")]
        public void LeavesMessagesWithoutLongTokensUntouched(string vtml)
        {
            Break(vtml).Should().Be(vtml);
        }

        [Theory]
        [InlineData(10)] // exactly the limit
        [InlineData(9)]  // just under
        public void LeavesTokensUpToTheLimitUntouched(int tokenLength)
        {
            var vtml = "hi " + new string('a', tokenLength);

            Break(vtml).Should().Be(vtml);
        }

        [Fact]
        public void SplitsTokenOneCharOverTheLimit()
        {
            Break("hi " + new string('a', 11))
                .Should().Be("hi aaaaaaaaaa\na");
        }

        [Fact]
        public void CopiesTagsVerbatimAndDoesNotCountThemAsTokenWidth()
        {
            // The tag is far wider than the limit in characters but contributes no rendered
            // width, and the short text runs around it stay whole.
            Break("<font color=\"#ff0000\">abc</font>def")
                .Should().Be("<font color=\"#ff0000\">abc</font>def");
        }

        [Fact]
        public void SplitsALongTokenBetweenTags()
        {
            Break("<i>aaaaaaaaaaaaaaa</i>")
                .Should().Be("<i>aaaaaaaaaa\naaaaa</i>");
        }

        [Fact]
        public void NeverSplitsInsideAnEntity()
        {
            // "&lt;" is decoded to "<" by VtmlParser; splitting it would render as literal text.
            Break("aaaaaaaa&lt;&lt;&lt;bbbb")
                .Should().Be("aaaaaaaa\n&lt;&lt;\n&lt;bbbb");
        }

        [Fact]
        public void NeverSplitsASurrogatePair()
        {
            var emoji = "\U0001F600"; // one code point, two chars

            Break(new string('a', 9) + emoji + new string('a', 9))
                .Should().Be("aaaaaaaaa\n" + emoji + "aaaaaaaa\na");
        }

        [Fact]
        public void NeverStrandsACombiningMark()
        {
            // "e" + U+0301 is one grapheme cluster; splitting it leaves a floating accent.
            var accented = "e\u0301"; // decomposed: "e" plus combining acute

            Break(new string('a', 9) + accented + new string('a', 4))
                .Should().Be("aaaaaaaaa\n" + accented + "aaaa");
        }

        [Fact]
        public void KeepsAtLeastOneUnitPerLineWhenNothingFits()
        {
            // Degenerate limit: must still terminate and preserve every character.
            Break("abcdef", maxWidthPx: 1).Should().Be("a\nb\nc\nd\ne\nf");
        }

        [Fact]
        public void CopiesAnUnterminatedTagVerbatim()
        {
            Break("hello <font color=\"#fff\" aaaaaaaaaaaaaaa")
                .Should().Be("hello <font color=\"#fff\" aaaaaaaaaaaaaaa");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsInputWhenNullOrEmpty(string? vtml)
        {
            VtmlUtils.BreakLongTokens(vtml!, Measure, MaxWidthPx).Should().Be(vtml);
        }

        [Fact]
        public void ReturnsInputWhenMeasureIsMissing()
        {
            VtmlUtils.BreakLongTokens("aaaaaaaaaaaaaaa", null!, MaxWidthPx)
                .Should().Be("aaaaaaaaaaaaaaa");
        }

        [Fact]
        public void ReturnsInputWhenMaxWidthIsNotPositive()
        {
            VtmlUtils.BreakLongTokens("aaaaaaaaaaaaaaa", Measure, 0)
                .Should().Be("aaaaaaaaaaaaaaa");
        }
    }

    /// <summary>
    /// Cheap gate deciding whether a tag-free bubble needs the wrapping renderer at all.
    /// </summary>
    public class HasUnbrokenRun
    {
        [Theory]
        [InlineData("aaaa", 4, true)]
        [InlineData("aaa", 4, false)]
        [InlineData("aaa aaa", 4, false)]           // breaks reset the run
        [InlineData("aaa\naaaa", 4, true)]
        [InlineData("hi aaaa hi", 4, true)]         // the run may sit anywhere
        [InlineData("aa\taa", 4, false)]
        [InlineData("", 4, false)]
        [InlineData(null, 4, false)]
        [InlineData("aaaa", 0, false)]
        public void DetectsRunsOfNonBreakingCharacters(string? text, int minLength, bool expected)
        {
            VtmlUtils.HasUnbrokenRun(text!, minLength).Should().Be(expected);
        }
    }
}
