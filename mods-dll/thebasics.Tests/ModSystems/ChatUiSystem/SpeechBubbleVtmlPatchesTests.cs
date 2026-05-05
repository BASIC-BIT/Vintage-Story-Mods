using FluentAssertions;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.Tests.ModSystems.ChatUiSystem;

public class SpeechBubbleVtmlPatchesTests
{
    [Theory]
    [InlineData(2, 3500, 3500)]
    [InlineData(20, 3500, 4500)]
    [InlineData(2, 0, 2700)]
    public void CalculateReceivedTimeForMinimumDuration_UsesConfiguredFloor(int messageLength, int minimumDurationMs, int expectedVisibleMs)
    {
        const long nowMs = 10_000;
        var vanillaDurationMs = 3500 + 100 * (messageLength - 10);

        var receivedTime = SpeechBubbleVtmlPatches.CalculateReceivedTimeForMinimumDuration(
            nowMs,
            messageLength,
            minimumDurationMs);

        (receivedTime + vanillaDurationMs - nowMs).Should().Be(expectedVisibleMs);
    }
}
