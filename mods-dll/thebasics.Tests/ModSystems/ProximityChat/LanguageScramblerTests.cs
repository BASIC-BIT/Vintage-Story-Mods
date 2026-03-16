using FluentAssertions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class LanguageScramblerTests
{
    private static readonly Language TestLanguage = new(
        "Elvish", "Ancient tongue", "elv",
        new[] { "la", "el", "na", "ri", "ta", "si", "mo", "du" },
        "#00ff00");

    [Fact]
    public void ScrambleMessage_ProducesOutput()
    {
        var result = LanguageScrambler.ScrambleMessage("Hello world", TestLanguage);
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ScrambleMessage_IsDeterministic()
    {
        // Same input + same language = same output (seeded by word hash)
        var result1 = LanguageScrambler.ScrambleMessage("Hello world", TestLanguage);
        var result2 = LanguageScrambler.ScrambleMessage("Hello world", TestLanguage);
        result1.Should().Be(result2);
    }

    [Fact]
    public void ScrambleMessage_DifferentInput_ProducesDifferentOutput()
    {
        var result1 = LanguageScrambler.ScrambleMessage("Hello", TestLanguage);
        var result2 = LanguageScrambler.ScrambleMessage("Goodbye", TestLanguage);
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void ScrambleMessage_PreservesCapitalization()
    {
        var result = LanguageScrambler.ScrambleMessage("Hello", TestLanguage);
        result[0].Should().Be(char.ToUpper(result[0]));
    }

    [Fact]
    public void ScrambleMessage_PreservesPunctuation()
    {
        var result = LanguageScrambler.ScrambleMessage("Hello, world!", TestLanguage);
        result.Should().Contain(",");
        result.Should().Contain("!");
    }

    [Fact]
    public void ScrambleMessage_PreservesSpaces()
    {
        var result = LanguageScrambler.ScrambleMessage("Hello world", TestLanguage);
        result.Should().Contain(" ");
    }

    [Fact]
    public void ScrambleMessage_UsesSyllablesFromLanguage()
    {
        var simpleLang = new Language("Simple", "", "sim",
            new[] { "ba" }, "#000");

        var result = LanguageScrambler.ScrambleMessage("Hello", simpleLang);
        // With only one syllable "ba", the output should be all "ba" repeated
        result.ToLower().Replace("b", "").Replace("a", "").Should().BeEmpty();
    }

    [Fact]
    public void ScrambleMessage_HandlesMultipleWords()
    {
        var result = LanguageScrambler.ScrambleMessage("The quick brown fox", TestLanguage);
        // Should still have spaces between words
        result.Split(' ').Length.Should().Be(4);
    }

    [Fact]
    public void ScrambleMessage_EmptyMessage_ReturnsEmpty()
    {
        var result = LanguageScrambler.ScrambleMessage("", TestLanguage);
        result.Should().BeEmpty();
    }
}
