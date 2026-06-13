using System.Reflection;
using FluentAssertions;
using thebasics.ModSystems.ProximityChat;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class LanguageSystemTests
{
    [Fact]
    public void TryParseAtlasBucketAndSkill_AcceptsBucketLabelsWithSpaces()
    {
        var parsed = LanguageSystem.TryParseAtlasBucketAndSkill("Temporal Gear 80", out var bucketIdentifier, out var skill);

        parsed.Should().BeTrue();
        bucketIdentifier.Should().Be("Temporal Gear");
        skill.Should().Be(80);
    }

    [Theory]
    [InlineData("temporal-gear")]
    [InlineData("temporal-gear -1")]
    [InlineData("temporal-gear 101")]
    [InlineData("temporal-gear many")]
    public void TryParseAtlasBucketAndSkill_RejectsMissingOrInvalidSkill(string value)
    {
        LanguageSystem.TryParseAtlasBucketAndSkill(value, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void ScrambleAsGestures_PreservesConfiguredNameWords()
    {
        var method = typeof(LanguageSystem).GetMethod(
            "ScrambleAsGesturesWithWordPredicate",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        Func<string, int, bool>? shouldPreserveWord = null;
        var result = (string)method!.Invoke(
            null,
            new object?[]
            {
                "Hello Steve",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "steve" },
                shouldPreserveWord
            })!;

        result.Should().Contain("Steve");
        result.Should().NotContain("Hello");
    }
}
