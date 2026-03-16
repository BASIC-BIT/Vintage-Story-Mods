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
}
