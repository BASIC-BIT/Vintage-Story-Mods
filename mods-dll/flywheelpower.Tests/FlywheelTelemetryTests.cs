namespace FlywheelPower.Tests;

public sealed class FlywheelTelemetryTests
{
    [Fact]
    public void EnergyTrendFollowsObservableWheelSpeedChange()
    {
        Assert.Equal(FlywheelEnergyTrend.Charging, FlywheelTelemetry.GetEnergyTrend(0.02f));
        Assert.Equal(FlywheelEnergyTrend.Discharging, FlywheelTelemetry.GetEnergyTrend(-0.02f));
        Assert.Equal(FlywheelEnergyTrend.Steady, FlywheelTelemetry.GetEnergyTrend(0.005f));
        Assert.Equal(FlywheelEnergyTrend.Steady, FlywheelTelemetry.GetEnergyTrend(-0.005f));
    }

    [Fact]
    public void NoExternalProviderIsReportedAsCoasting()
    {
        FlywheelOperatingState state = FlywheelTelemetry.GetOperatingState(
            flywheelSpeed: 1.54f,
            speedChangePerSecond: -0.002f,
            totalNetworkTorque: 0.007f,
            ownNetworkTorque: 0.007f,
            totalNetworkResistance: 0.003f,
            ownNetworkResistance: 0.003f,
            maxTransferTorque: 0.35f);

        Assert.Equal(FlywheelOperatingState.Coasting, state);
    }

    [Fact]
    public void ResistanceWithoutAProviderIsReportedAsCoastingUnderLoad()
    {
        FlywheelOperatingState state = FlywheelTelemetry.GetOperatingState(
            flywheelSpeed: 2.03f,
            speedChangePerSecond: -0.06f,
            totalNetworkTorque: 0.17f,
            ownNetworkTorque: 0.17f,
            totalNetworkResistance: 0.12f,
            ownNetworkResistance: 0.004f,
            maxTransferTorque: 0.18f);

        Assert.Equal(FlywheelOperatingState.CoastingUnderLoad, state);
    }

    [Fact]
    public void ExternalProviderUsesActualWheelEnergyTrend()
    {
        Assert.Equal(FlywheelOperatingState.Charging, WithExternalProvider(0.06f));
        Assert.Equal(FlywheelOperatingState.Discharging, WithExternalProvider(-0.06f));
        Assert.Equal(FlywheelOperatingState.DrivenHoldingSpeed, WithExternalProvider(0f));
    }

    private static FlywheelOperatingState WithExternalProvider(float speedChange)
    {
        return FlywheelTelemetry.GetOperatingState(
            flywheelSpeed: 2f,
            speedChangePerSecond: speedChange,
            totalNetworkTorque: 0.2f,
            ownNetworkTorque: 0.05f,
            totalNetworkResistance: 0.04f,
            ownNetworkResistance: 0.004f,
            maxTransferTorque: 0.35f);
    }

    [Fact]
    public void CouplingLoadIsAUsefulBoundedPercentage()
    {
        Assert.Equal(0f, FlywheelTelemetry.GetCouplingLoadPercent(0f, 0.35f));
        Assert.Equal(50f, FlywheelTelemetry.GetCouplingLoadPercent(0.175f, 0.35f), 3);
        Assert.Equal(100f, FlywheelTelemetry.GetCouplingLoadPercent(0.7f, 0.35f));
    }

    [Fact]
    public void SlipRequiresMeaningfulRelativeSpeed()
    {
        Assert.False(FlywheelTelemetry.IsActivelySlipping(1f, 0.95f, 3.5f));
        Assert.True(FlywheelTelemetry.IsActivelySlipping(2f, 0f, 3.5f));
    }

    [Fact]
    public void SpeedMismatchIsNormalizedAgainstSafeSpeed()
    {
        Assert.Equal(50f, FlywheelTelemetry.GetSlipPercent(2f, 0f, 4f), 3);
        Assert.Equal(250f, FlywheelTelemetry.GetSlipPercent(10f, 0f, 4f));
    }
}
