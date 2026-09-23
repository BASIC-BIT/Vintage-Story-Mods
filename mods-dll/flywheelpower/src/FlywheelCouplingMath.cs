using System;
using Vintagestory.API.MathTools;

namespace FlywheelPower;

internal readonly record struct FlywheelStep(float Speed, float TransferTorque, float LossTorque);

internal readonly record struct FlywheelStepParameters(
    float Inertia,
    float CouplingStrength,
    float CouplingEngagement,
    float MaxTransferTorque,
    float BaseBearingLoss,
    float ViscousBearingLoss,
    float WindageLoss,
    float SafeSpeed);

internal static class FlywheelCouplingMath
{
    private const float Epsilon = 0.00001f;

    internal static float DampNetworkSpeed(
        float filteredNetworkSpeed,
        float currentNetworkSpeed,
        bool hasFilteredSample,
        float dt,
        float dampingSeconds)
    {
        if (!float.IsFinite(currentNetworkSpeed))
        {
            return 0f;
        }

        if (!hasFilteredSample || !float.IsFinite(filteredNetworkSpeed))
        {
            return currentNetworkSpeed;
        }

        if (dt <= 0f || dampingSeconds <= Epsilon)
        {
            return currentNetworkSpeed;
        }

        // Vintage Story advances its mechanical network in coarse discrete steps. At high
        // gear ratios that explicit solver can chatter between several speeds. A short,
        // time-based low-pass models torsional compliance at the hub and prevents those
        // numerical half-cycles from alternately charging and discharging the wheel.
        float alpha = 1f - MathF.Exp(-dt / dampingSeconds);
        return filteredNetworkSpeed + (currentNetworkSpeed - filteredNetworkSpeed)
            * GameMath.Clamp(alpha, 0f, 1f);
    }

    internal static FlywheelStep Step(
        float flywheelSpeed,
        float networkSpeed,
        FlywheelStepParameters parameters,
        float dt)
    {
        float transferTorque = GameMath.Clamp(
            parameters.CouplingStrength * parameters.CouplingEngagement * (flywheelSpeed - networkSpeed),
            -parameters.MaxTransferTorque,
            parameters.MaxTransferTorque);

        if (parameters.Inertia <= Epsilon || dt <= 0f)
        {
            return new FlywheelStep(flywheelSpeed, transferTorque, 0f);
        }

        float nextSpeed = flywheelSpeed - transferTorque / parameters.Inertia * dt;
        if (!float.IsFinite(nextSpeed))
        {
            nextSpeed = 0f;
        }

        float speedAbs = Math.Abs(nextSpeed);
        float lossTorque = GetLossTorque(
            speedAbs,
            parameters.BaseBearingLoss,
            parameters.ViscousBearingLoss,
            parameters.WindageLoss,
            parameters.SafeSpeed);
        float speedLoss = lossTorque / parameters.Inertia * dt;
        nextSpeed = Math.Sign(nextSpeed) * Math.Max(0f, speedAbs - speedLoss);
        return new FlywheelStep(nextSpeed, transferTorque, lossTorque);
    }

    internal static float GetLossTorque(
        float speedAbs,
        float baseBearingLoss,
        float viscousBearingLoss,
        float windageLoss,
        float safeSpeed)
    {
        if (speedAbs < Epsilon)
        {
            return 0f;
        }

        float baseLoss = baseBearingLoss
            + viscousBearingLoss * speedAbs
            + windageLoss * speedAbs * speedAbs;
        return baseLoss * FlywheelSafety.GetOverspeedLossMultiplier(speedAbs, safeSpeed);
    }
}
