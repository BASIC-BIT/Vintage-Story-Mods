using FlywheelPower;

namespace FlywheelPower.Tests;

public sealed class FlywheelModelDimensionsTests
{
    [Fact]
    public void FullSizeWheelLeavesHalfBlockClearanceInsideFootprint()
    {
        float wheelDiameter = FlywheelModelDimensions.WheelOuterRadius * 2f;
        float edgeClearance = (FlywheelModelDimensions.FootprintDiameter - wheelDiameter) / 2f;

        Assert.Equal(2f, wheelDiameter);
        Assert.Equal(0.5f, edgeClearance);
    }

    [Fact]
    public void FullSizeWheelUsesOneEighthBlockTotalDepth()
    {
        Assert.Equal(0.125f, FlywheelModelDimensions.WheelHalfThickness * 2f);
    }

    [Fact]
    public void CoreRemainsClearOfAxleAndFitsInsideWheel()
    {
        Assert.True(FlywheelModelDimensions.ShaftClearanceRadius > FlywheelModelDimensions.AxleRadius);
        Assert.True(FlywheelModelDimensions.HubInnerRadius > FlywheelModelDimensions.ShaftClearanceRadius);
        Assert.True(FlywheelModelDimensions.HubOuterRadius > FlywheelModelDimensions.HubInnerRadius);
        Assert.True(FlywheelModelDimensions.CouplingPlateOuterRadius >= FlywheelModelDimensions.HubOuterRadius);
        Assert.True(FlywheelModelDimensions.CouplingPlateOuterRadius < FlywheelModelDimensions.WheelOuterRadius);
    }
}
