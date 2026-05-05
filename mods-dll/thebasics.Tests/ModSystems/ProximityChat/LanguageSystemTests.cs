using System.Reflection;
using FluentAssertions;
using thebasics.ModSystems.ProximityChat;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class LanguageSystemTests
{
    [Fact]
    public void ScrambleAsGestures_PreservesConfiguredNameWords()
    {
        var method = typeof(LanguageSystem).GetMethod(
            "ScrambleAsGestures",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();
        var result = (string)method!.Invoke(
            null,
            new object[]
            {
                "Hello Steve",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "steve" }
            })!;

        result.Should().Contain("Steve");
        result.Should().NotContain("Hello");
    }
}
