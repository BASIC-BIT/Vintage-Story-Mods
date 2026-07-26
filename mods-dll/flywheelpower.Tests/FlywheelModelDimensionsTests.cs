using FlywheelPower;

namespace FlywheelPower.Tests;

public sealed class FlywheelModelDimensionsTests
{
    [Fact]
    public void FullSizeWheelLeavesOneBlockClearanceInsideFootprint()
    {
        float wheelDiameter = FlywheelModelDimensions.WheelOuterRadius * 2f;
        float edgeClearance = (FlywheelModelDimensions.FootprintDiameter - wheelDiameter) / 2f;

        Assert.Equal(1f, wheelDiameter);
        Assert.Equal(1f, edgeClearance);
    }

    [Fact]
    public void FullSizeWheelUsesOneSixteenthBlockTotalDepth()
    {
        Assert.Equal(0.0625f, FlywheelModelDimensions.WheelHalfThickness * 2f);
        Assert.Equal(0.09f, FlywheelModelDimensions.HubHalfThickness * 2f);
        Assert.Equal(0.01f, FlywheelModelDimensions.CouplingPlateThickness);
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
