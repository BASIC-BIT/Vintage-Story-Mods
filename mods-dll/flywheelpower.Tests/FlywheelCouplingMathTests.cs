namespace FlywheelPower.Tests;

public sealed class FlywheelCouplingMathTests
{
    [Fact]
    public void FasterWheelCannotHoldAConstantSpeedAboveItsNetwork()
    {
        const float networkSpeed = 1.8f;
        float wheelSpeed = 2.17f;

        for (int stepIndex = 0; stepIndex < 300; stepIndex++)
        {
            float previousSpeed = wheelSpeed;
            FlywheelStep step = FullSizeStep(wheelSpeed, networkSpeed);
            wheelSpeed = step.Speed;

            if (previousSpeed > networkSpeed)
            {
                Assert.True(wheelSpeed < previousSpeed);
                Assert.True(step.TransferTorque > 0f);
            }
        }

        Assert.InRange(wheelSpeed, networkSpeed - 0.02f, networkSpeed + 0.01f);

        for (int stepIndex = 0; stepIndex < 900; stepIndex++)
        {
            wheelSpeed = FullSizeStep(wheelSpeed, networkSpeed).Speed;
        }

        Assert.InRange(wheelSpeed, 1.75f, networkSpeed);
    }

    [Fact]
    public void SlowerWheelChargesTowardAConstantPoweredNetwork()
    {
        FlywheelStep step = FullSizeStep(1.5f, 1.8f);

        Assert.True(step.Speed > 1.5f);
        Assert.True(step.TransferTorque < 0f);
    }

    [Fact]
    public void DisconnectedWheelAlwaysLosesSpeed()
    {
        FlywheelStep step = FullSizeStep(2f, 0f);

        Assert.True(step.Speed < 2f);
        Assert.True(step.TransferTorque > 0f);
        Assert.True(step.LossTorque > 0f);
    }

    [Fact]
    public void DampedNetworkSpeedSuppressesAHighRatioTwoCycle()
    {
        const float lowSolverSample = 1.76f;
        const float highSolverSample = 3.28f;
        float filteredSpeed = 0f;
        bool hasFilteredSample = false;
        float minimumSettledSpeed = float.MaxValue;
        float maximumSettledSpeed = float.MinValue;

        for (int stepIndex = 0; stepIndex < 100; stepIndex++)
        {
            float rawSpeed = stepIndex % 2 == 0 ? highSolverSample : lowSolverSample;
            filteredSpeed = FlywheelCouplingMath.DampNetworkSpeed(
                filteredSpeed,
                rawSpeed,
                hasFilteredSample,
                dt: 0.1f,
                dampingSeconds: 0.5f);
            hasFilteredSample = true;

            if (stepIndex >= 80)
            {
                minimumSettledSpeed = Math.Min(minimumSettledSpeed, filteredSpeed);
                maximumSettledSpeed = Math.Max(maximumSettledSpeed, filteredSpeed);
            }
        }

        Assert.InRange(filteredSpeed, 2.35f, 2.7f);
        Assert.True(maximumSettledSpeed - minimumSettledSpeed < 0.2f);
    }

    [Fact]
    public void SlowerCompactWheelChargesAcrossAlternatingSolverSamples()
    {
        float wheelSpeed = 2.07f;
        float filteredNetworkSpeed = 0f;
        bool hasFilteredSample = false;

        for (int stepIndex = 0; stepIndex < 20; stepIndex++)
        {
            float rawNetworkSpeed = stepIndex % 2 == 0 ? 3.28f : 1.76f;
            filteredNetworkSpeed = FlywheelCouplingMath.DampNetworkSpeed(
                filteredNetworkSpeed,
                rawNetworkSpeed,
                hasFilteredSample,
                dt: 0.1f,
                dampingSeconds: 0.5f);
            FlywheelStep step = FlywheelCouplingMath.Step(
                wheelSpeed,
                filteredNetworkSpeed,
                inertia: 0.452f,
                couplingStrength: 0.55f,
                couplingEngagement: 1f,
                maxTransferTorque: 0.18f,
                baseBearingLoss: 0.001f,
                viscousBearingLoss: 0.003f,
                windageLoss: 0.0015f,
                safeSpeed: 4.5f,
                dt: 0.1f);

            wheelSpeed = step.Speed;
            hasFilteredSample = true;
        }

        Assert.True(wheelSpeed > 2.4f);
        Assert.True(wheelSpeed < 2.55f);
    }

    private static FlywheelStep FullSizeStep(float wheelSpeed, float networkSpeed)
    {
        return FlywheelCouplingMath.Step(
            wheelSpeed,
            networkSpeed,
            inertia: 7.934f,
            couplingStrength: 0.8f,
            couplingEngagement: 1f,
            maxTransferTorque: 0.35f,
            baseBearingLoss: 0.001f,
            viscousBearingLoss: 0.003f,
            windageLoss: 0.0015f,
            safeSpeed: 3.5f,
            dt: 0.1f);
    }
}
