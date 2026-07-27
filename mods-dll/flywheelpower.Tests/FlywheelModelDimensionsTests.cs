using FlywheelPower;

namespace FlywheelPower.Tests;

public sealed class FlywheelModelDimensionsTests
{
    [Fact]
    public void FullSizeWheelLeavesSevenTenthsBlockClearanceInsideFootprint()
    {
        float wheelDiameter = FlywheelModelDimensions.WheelOuterRadius * 2f;
        float edgeClearance = (FlywheelModelDimensions.FootprintDiameter - wheelDiameter) / 2f;

        Assert.Equal(1.6f, wheelDiameter);
        Assert.Equal(0.7f, edgeClearance);
    }

    [Fact]
    public void FullSizeWheelUsesReinforcedDepthProfile()
    {
        Assert.Equal(0.1875f, FlywheelModelDimensions.WheelHalfThickness * 2f);
        Assert.Equal(0.27f, FlywheelModelDimensions.HubHalfThickness * 2f);
        Assert.Equal(0.03f, FlywheelModelDimensions.CouplingPlateThickness);
    }

    [Fact]
    public void BearingRingCloselyFitsAxleAndStepsIntoHub()
    {
        Assert.True(FlywheelModelDimensions.ShaftClearanceRadius > FlywheelModelDimensions.AxleRadius);
        Assert.InRange(
            FlywheelModelDimensions.ShaftClearanceRadius - FlywheelModelDimensions.AxleRadius,
            0.001f,
            0.005f);
        Assert.True(FlywheelModelDimensions.BearingOuterRadius > FlywheelModelDimensions.ShaftClearanceRadius);
        Assert.True(FlywheelModelDimensions.HubOuterRadius > FlywheelModelDimensions.BearingOuterRadius);
        Assert.True(FlywheelModelDimensions.BearingHalfThickness > FlywheelModelDimensions.HubHalfThickness);
        Assert.Equal(FlywheelModelDimensions.HubOuterRadius, FlywheelModelDimensions.CoupledInnerRadius);
        Assert.True(FlywheelModelDimensions.CouplingPlateOuterRadius >= FlywheelModelDimensions.HubOuterRadius);
        Assert.True(FlywheelModelDimensions.CouplingPlateOuterRadius < FlywheelModelDimensions.WheelOuterRadius);
    }
}
