using FluentAssertions;
using thebasics.ModSystems.ProximityChat.Transformers;

namespace thebasics.Tests.ModSystems.ProximityChat.Transformers;

public class TransformerSystemTests
{
    [Theory]
    [InlineData(2750, true)]
    [InlineData(3000, true)]
    [InlineData(3250, false)]
    public void IsWithinSignLanguageRetryWindow_IncludesFinalWindowBoundary(int elapsedMs, bool expected)
    {
        TransformerSystem.IsWithinSignLanguageRetryWindow(elapsedMs).Should().Be(expected);
    }
}
