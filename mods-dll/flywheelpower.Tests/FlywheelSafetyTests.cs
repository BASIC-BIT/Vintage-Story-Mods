namespace FlywheelPower.Tests;

public sealed class FlywheelSafetyTests
{
    [Theory]
    [InlineData(3.5f, 3.5f, 100d)]
    [InlineData(3.85f, 3.5f, 121d)]
    [InlineData(4.2f, 3.5f, 144d)]
    [InlineData(5.25f, 3.5f, 225d)]
    public void StoredEnergyRemainsVisibleAboveRatedCapacity(float speed, float safeSpeed, double expectedPercent)
    {
        Assert.Equal(expectedPercent, FlywheelSafety.GetStoredEnergyPercent(speed, safeSpeed), 2);
    }

    [Theory]
    [InlineData(3.15f, 3.5f, 1f)]
    [InlineData(3.5f, 3.5f, 1f)]
    [InlineData(3.85f, 3.5f, 1.5f)]
    [InlineData(4.2f, 3.5f, 2f)]
    public void LossesScaleProgressivelyAboveRatedSpeed(float speed, float safeSpeed, float expectedMultiplier)
    {
        Assert.Equal(expectedMultiplier, FlywheelSafety.GetOverspeedLossMultiplier(speed, safeSpeed), 3);
    }

    [Fact]
    public void OverspeedSparksBecomeMoreFrequentAndNumerous()
    {
        Assert.Equal(int.MaxValue, FlywheelSafety.GetOverspeedSparkIntervalTicks(3.5f, 3.5f));
        Assert.Equal(10, FlywheelSafety.GetOverspeedSparkIntervalTicks(3.85f, 3.5f));
        Assert.Equal(2, FlywheelSafety.GetOverspeedSparkIntervalTicks(5.25f, 3.5f));
        Assert.True(FlywheelSafety.GetOverspeedSparkQuantity(4.2f, 3.5f)
            > FlywheelSafety.GetOverspeedSparkQuantity(3.85f, 3.5f));
    }

    [Fact]
    public void OverspeedSmokeIsBoundedAndScalesWithSeverity()
    {
        Assert.Equal(int.MaxValue, FlywheelSafety.GetOverspeedSmokeIntervalTicks(3.5f, 3.5f));
        Assert.Equal(16, FlywheelSafety.GetOverspeedSmokeIntervalTicks(3.85f, 3.5f));
        Assert.Equal(6, FlywheelSafety.GetOverspeedSmokeIntervalTicks(5.25f, 3.5f));
        Assert.Equal(1.4f, FlywheelSafety.GetOverspeedSmokeQuantity(3.85f, 3.5f), 3);
        Assert.Equal(3f, FlywheelSafety.GetOverspeedSmokeQuantity(5.25f, 3.5f), 3);
    }
}
