using FluentAssertions;
using thebasics.ModSystems.ProximityChat.Transformers;

namespace thebasics.Tests.ModSystems.ProximityChat.Transformers;

public class TransformerSystemTests
{
    [Theory]
    [InlineData(1250, true)]
    [InlineData(1500, true)]
    [InlineData(1750, false)]
    public void IsWithinSignLanguageRetryWindow_IncludesFinalWindowBoundary(int elapsedMs, bool expected)
    {
        TransformerSystem.IsWithinSignLanguageRetryWindow(elapsedMs).Should().Be(expected);
    }
}
