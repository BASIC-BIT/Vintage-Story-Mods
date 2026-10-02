using System;

namespace FlywheelPower;

internal static class FlywheelSafety
{
    private const float Epsilon = 0.00001f;
    private const float OverspeedLossSlope = 5f;
    private const int SlowestOverspeedSparkIntervalTicks = 12;
    private const int FastestOverspeedSparkIntervalTicks = 2;
    private const int SlowestOverspeedSmokeIntervalTicks = 18;
    private const int FastestOverspeedSmokeIntervalTicks = 6;

    internal static float GetRatedSpeedRatio(float speed, float safeSpeed)
    {
        if (!float.IsFinite(speed) || !float.IsFinite(safeSpeed) || safeSpeed <= Epsilon)
        {
            return 0f;
        }

        return Math.Abs(speed) / safeSpeed;
    }

    internal static double GetStoredEnergyPercent(float speed, float safeSpeed)
    {
        double ratio = GetRatedSpeedRatio(speed, safeSpeed);
        return ratio * ratio * 100d;
    }

    internal static float GetOverspeedLossMultiplier(float speed, float safeSpeed)
    {
        float overspeed = Math.Max(0f, GetRatedSpeedRatio(speed, safeSpeed) - 1f);
        return 1f + OverspeedLossSlope * overspeed;
    }

    internal static int GetOverspeedSparkIntervalTicks(float speed, float safeSpeed)
    {
        float overspeed = GetRatedSpeedRatio(speed, safeSpeed) - 1f;
        if (overspeed <= 0f)
        {
            return int.MaxValue;
        }

        int interval = (int)MathF.Round(SlowestOverspeedSparkIntervalTicks - overspeed * 20f);
        return Math.Clamp(interval, FastestOverspeedSparkIntervalTicks, SlowestOverspeedSparkIntervalTicks);
    }

    internal static float GetOverspeedSparkQuantity(float speed, float safeSpeed)
    {
        float overspeed = Math.Max(0f, GetRatedSpeedRatio(speed, safeSpeed) - 1f);
        return Math.Clamp(4f + overspeed * 16f, 4f, 12f);
    }

    internal static int GetOverspeedSmokeIntervalTicks(float speed, float safeSpeed)
    {
        float overspeed = GetRatedSpeedRatio(speed, safeSpeed) - 1f;
        if (overspeed <= 0f)
        {
            return int.MaxValue;
        }

        int interval = (int)MathF.Round(SlowestOverspeedSmokeIntervalTicks - overspeed * 24f);
        return Math.Clamp(interval, FastestOverspeedSmokeIntervalTicks, SlowestOverspeedSmokeIntervalTicks);
    }

    internal static float GetOverspeedSmokeQuantity(float speed, float safeSpeed)
    {
        float overspeed = Math.Max(0f, GetRatedSpeedRatio(speed, safeSpeed) - 1f);
        return Math.Clamp(1f + overspeed * 4f, 1f, 3f);
    }
}
