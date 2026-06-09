using FluentAssertions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class HeritageLanguageSystemTests
{
    [Fact]
    public void GetClassLanguagesToRemove_ExcludesLanguageStillGrantedByCurrentClass()
    {
        var shared = CreateLanguage("Shared", grantedToClasses: ["oldclass", "newclass"]);
        var oldOnly = CreateLanguage("OldOnly", grantedToClasses: ["oldclass"]);
        var newOnly = CreateLanguage("NewOnly", grantedToClasses: ["newclass"]);

        var toRemove = HeritageLanguageSystem.GetClassLanguagesToRemove(
            [shared, oldOnly, newOnly],
            previousClassCode: "oldclass",
            currentClassCode: "newclass",
            currentTraits: [],
            currentModelCode: string.Empty,
            currentModelGroupCode: string.Empty,
            includeModelBindings: false);

        toRemove.Select(language => language.Name).Should().BeEquivalentTo(["OldOnly"]);
    }

    [Theory]
    [InlineData("game:tailor", "game:tailor")]
    [InlineData("tailor", "game:tailor")]
    [InlineData("game:*", "game:tailor")]
    [InlineData("*tailor", "game:tailor")]
    public void IsLanguageGrantedByClass_MatchesClassCodeVariantsAndWildcards(string binding, string classCode)
    {
        var language = CreateLanguage("ClassTongue", grantedToClasses: [binding]);

        HeritageLanguageSystem.IsLanguageGrantedByClass(language, classCode).Should().BeTrue();
    }

    [Fact]
    public void IsLanguageGrantedByClass_DoesNotMatchUnrelatedClass()
    {
        var language = CreateLanguage("ClassTongue", grantedToClasses: ["game:tailor"]);

        HeritageLanguageSystem.IsLanguageGrantedByClass(language, "game:malefactor").Should().BeFalse();
    }

    [Theory]
    [InlineData("lrc:firebeards-and-broadbeams", "Firebeards And Broadbeams")]
    [InlineData("lrc:firebeards_and_broadbeams", "Firebeards And Broadbeams")]
    [InlineData("game:wolf.tailored", "Wolf Tailored")]
    [InlineData("lrc:firebeardsandbroadbeams", "Firebeardsandbroadbeams")]
    public void FormatModelCodeFallback_StripsDomainAndFormatsSeparators(string code, string expected)
    {
        HeritageLanguageSystem.FormatModelCodeFallback(code).Should().Be(expected);
    }

    [Fact]
    public void GetPlayerModelLibModelName_PrefersConfiguredModelName()
    {
        HeritageLanguageSystem.GetPlayerModelLibModelName("lrc:bnumenorean", "Black Numenorean")
            .Should().Be("Black Numenorean");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetPlayerModelLibModelName_FallsBackWhenConfiguredNameIsMissing(string? configuredName)
    {
        HeritageLanguageSystem.GetPlayerModelLibModelName("lrc:firebeards-and-broadbeams", configuredName)
            .Should().Be("Firebeards And Broadbeams");
    }

    [Fact]
    public void GetModelGroupDisplayName_FallsBackToFormattedCode()
    {
        HeritageLanguageSystem.GetModelGroupDisplayName("lrc:lost-races")
            .Should().Be("Lost Races");
    }

    private static Language CreateLanguage(string name, string[] grantedToClasses)
    {
        return new Language(
            name,
            $"{name} language",
            name.ToLowerInvariant(),
            ["qa"],
            "#FFFFFF",
            GrantedToClasses: grantedToClasses);
    }
}
