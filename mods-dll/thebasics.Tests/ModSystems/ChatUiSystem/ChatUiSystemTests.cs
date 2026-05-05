using FluentAssertions;
using thebasics.Configs;
using ChatUiModSystem = thebasics.ModSystems.ChatUiSystem.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class ChatUiSystemTests
{
    [Theory]
    [InlineData(50, 30, 30)]
    [InlineData(20, 30, 20)]
    [InlineData(30, 0, 0)]
    [InlineData(-1, 30, 0)]
    public void GetEffectiveTypingIndicatorRange_CapsAtNametagRange(
        int typingRange,
        int nametagRange,
        int expectedRange)
    {
        var config = new ModConfig
        {
            TypingIndicatorMaxRange = typingRange,
            NametagRenderRange = nametagRange,
        };

        var range = ChatUiModSystem.GetEffectiveTypingIndicatorRange(config);

        range.Should().Be(expectedRange);
    }
}
