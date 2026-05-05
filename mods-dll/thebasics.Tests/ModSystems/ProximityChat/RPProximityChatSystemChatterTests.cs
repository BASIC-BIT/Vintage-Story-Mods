using FluentAssertions;
using thebasics.ModSystems.ProximityChat;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class RPProximityChatSystemChatterTests
{
    [Theory]
    [InlineData("walks over", 0)]
    [InlineData("walks over \"hello\"", 5)]
    [InlineData("\"hi\" and \"bye\"", 5)]
    public void CountQuotedSpeechLength_CountsOnlyQuotedSegments(string message, int expected)
    {
        RPProximityChatSystem.CountQuotedSpeechLength(message).Should().Be(expected);
    }
}
