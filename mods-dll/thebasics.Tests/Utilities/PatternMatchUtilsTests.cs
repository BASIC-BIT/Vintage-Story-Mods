using FluentAssertions;
using thebasics.Utilities;

namespace thebasics.Tests.Utilities;

public class PatternMatchUtilsTests
{
    [Theory]
    [InlineData("hello", "*", true)]
    [InlineData("hello", "hello", true)]
    [InlineData("hello", "HELLO", true)]       // case-insensitive default
    [InlineData("hello", "h*", true)]
    [InlineData("hello", "*lo", true)]
    [InlineData("hello", "h*lo", true)]
    [InlineData("hello", "h?llo", true)]
    [InlineData("hello", "?????", true)]
    [InlineData("hello", "world", false)]
    [InlineData("hello", "hell", false)]        // not a substring match
    [InlineData("hello", "helloo", false)]
    [InlineData("hello", "h*z", false)]
    [InlineData("hello", "?", false)]           // too short
    public void WildcardMatches_WithVariousPatterns_ReturnsExpected(
        string input, string pattern, bool expected)
    {
        PatternMatchUtils.WildcardMatches(input, pattern).Should().Be(expected);
    }

    [Theory]
    [InlineData("hello", "HELLO", false, false)]  // case-sensitive: no match
    [InlineData("HELLO", "HELLO", false, true)]    // case-sensitive: exact match
    [InlineData("Hello", "h*", false, false)]      // case-sensitive: H != h
    [InlineData("Hello", "H*", false, true)]
    public void WildcardMatches_CaseSensitive_RespectsCase(
        string input, string pattern, bool ignoreCase, bool expected)
    {
        PatternMatchUtils.WildcardMatches(input, pattern, ignoreCase).Should().Be(expected);
    }

    [Theory]
    [InlineData("hello", "")]
    [InlineData("hello", null)]
    public void WildcardMatches_EmptyOrNullPattern_ReturnsFalse(string input, string? pattern)
    {
        PatternMatchUtils.WildcardMatches(input, pattern!).Should().BeFalse();
    }

    [Fact]
    public void WildcardMatches_NullInput_WithWildcard_ReturnsTrue()
    {
        // null input is treated as empty string, which matches "*"
        PatternMatchUtils.WildcardMatches(null!, "*").Should().BeTrue();
    }

    [Fact]
    public void WildcardMatches_NullInput_WithLiteralPattern_ReturnsFalse()
    {
        PatternMatchUtils.WildcardMatches(null!, "hello").Should().BeFalse();
    }

    [Fact]
    public void WildcardMatches_PatternWithWhitespace_IsTrimmed()
    {
        PatternMatchUtils.WildcardMatches("hello", "  hello  ").Should().BeTrue();
    }

    [Fact]
    public void WildcardMatches_SamePatternTwice_UsesCache()
    {
        // Call twice with same pattern to exercise cache path
        PatternMatchUtils.WildcardMatches("test1", "t*").Should().BeTrue();
        PatternMatchUtils.WildcardMatches("test2", "t*").Should().BeTrue();
    }
}
