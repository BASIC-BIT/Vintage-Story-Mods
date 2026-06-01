using System;

namespace DimensionLib.Generation.Noise;

internal static class ValueNoise2D
{
    public static double Fractal(double x, double z, int seed, int octaves, double persistence)
    {
        var amplitude = 1.0;
        var frequency = 1.0;
        var total = 0.0;
        var max = 0.0;

        for (var octave = 0; octave < octaves; octave++)
        {
            total += Smooth(x * frequency, z * frequency, seed + octave * 1009) * amplitude;
            max += amplitude;
            amplitude *= persistence;
            frequency *= 2.0;
        }

        return max == 0 ? 0 : total / max;
    }

    private static double Smooth(double x, double z, int seed)
    {
        var x0 = (int)Math.Floor(x);
        var z0 = (int)Math.Floor(z);
        var tx = SmoothStep(x - x0);
        var tz = SmoothStep(z - z0);

        var a = Value(x0, z0, seed);
        var b = Value(x0 + 1, z0, seed);
        var c = Value(x0, z0 + 1, seed);
        var d = Value(x0 + 1, z0 + 1, seed);

        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), tz);
    }

    private static double Value(int x, int z, int seed)
    {
        unchecked
        {
            var n = x * 374761393 + z * 668265263 + seed * 1442695041;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / 2147483647.0;
        }
    }

    private static double SmoothStep(double value)
    {
        return value * value * (3.0 - 2.0 * value);
    }

    private static double Lerp(double left, double right, double amount)
    {
        return left + (right - left) * amount;
    }
}
