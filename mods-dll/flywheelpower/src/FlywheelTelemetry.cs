using System;

namespace FlywheelPower;

internal enum FlywheelEnergyTrend
{
    Steady,
    Charging,
    Discharging
}

internal enum FlywheelOperatingState
{
    Idle,
    Coasting,
    CoastingUnderLoad,
    Charging,
    Discharging,
    DrivenHoldingSpeed
}

internal static class FlywheelTelemetry
{
    private const float Epsilon = 0.00001f;
    private const float SlipSpeedThreshold = 0.03f;

    internal static FlywheelEnergyTrend GetEnergyTrend(float speedChangePerSecond)
    {
        const float visibleSpeedChangePerSecond = 0.005f;
        if (speedChangePerSecond > visibleSpeedChangePerSecond)
        {
            return FlywheelEnergyTrend.Charging;
        }

        if (speedChangePerSecond < -visibleSpeedChangePerSecond)
        {
            return FlywheelEnergyTrend.Discharging;
        }

        return FlywheelEnergyTrend.Steady;
    }

    internal static FlywheelOperatingState GetOperatingState(
        float flywheelSpeed,
        float speedChangePerSecond,
        float totalNetworkTorque,
        float ownNetworkTorque,
        float totalNetworkResistance,
        float ownNetworkResistance,
        float maxTransferTorque)
    {
        if (Math.Abs(flywheelSpeed) <= 0.01f)
        {
            return FlywheelOperatingState.Idle;
        }

        float providerThreshold = Math.Max(0.001f, maxTransferTorque * 0.01f);
        float loadThreshold = Math.Max(0.001f, maxTransferTorque * 0.05f);
        bool hasExternalProvider = Math.Abs(totalNetworkTorque - ownNetworkTorque) > providerThreshold;
        bool hasExternalLoad = Math.Max(0f, totalNetworkResistance - ownNetworkResistance) > loadThreshold;

        if (!hasExternalProvider)
        {
            return hasExternalLoad
                ? FlywheelOperatingState.CoastingUnderLoad
                : FlywheelOperatingState.Coasting;
        }

        return GetEnergyTrend(speedChangePerSecond) switch
        {
            FlywheelEnergyTrend.Charging => FlywheelOperatingState.Charging,
            FlywheelEnergyTrend.Discharging => FlywheelOperatingState.Discharging,
            _ => FlywheelOperatingState.DrivenHoldingSpeed
        };
    }

    internal static float GetCouplingLoadPercent(float transferTorque, float maxTransferTorque)
    {
        if (maxTransferTorque <= Epsilon)
        {
            return 0f;
        }

        return Math.Clamp(Math.Abs(transferTorque) / maxTransferTorque * 100f, 0f, 100f);
    }

    internal static float GetSlipPercent(float flywheelSpeed, float networkSpeed, float safeSpeed)
    {
        if (safeSpeed <= Epsilon)
        {
            return 0f;
        }

        return Math.Abs(flywheelSpeed - networkSpeed) / safeSpeed * 100f;
    }

    internal static bool IsActivelySlipping(float flywheelSpeed, float networkSpeed, float safeSpeed)
    {
        return GetSlipPercent(flywheelSpeed, networkSpeed, safeSpeed) >= SlipSpeedThreshold * 100f;
    }
}
