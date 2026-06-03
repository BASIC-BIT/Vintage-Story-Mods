using System;

namespace DimensionCavern;

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
            total += Noise(x * frequency, z * frequency, seed + octave * 1013) * amplitude;
            max += amplitude;
            amplitude *= persistence;
            frequency *= 2.0;
        }

        return max <= 0.0 ? 0.0 : total / max;
    }

    private static double Noise(double x, double z, int seed)
    {
        var x0 = (int)Math.Floor(x);
        var z0 = (int)Math.Floor(z);
        var x1 = x0 + 1;
        var z1 = z0 + 1;
        var sx = Smooth(x - x0);
        var sz = Smooth(z - z0);

        var n0 = Lerp(Hash01(x0, z0, seed), Hash01(x1, z0, seed), sx);
        var n1 = Lerp(Hash01(x0, z1, seed), Hash01(x1, z1, seed), sx);
        return Lerp(n0, n1, sz);
    }

    private static double Smooth(double value)
    {
        return value * value * (3.0 - 2.0 * value);
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private static double Hash01(int x, int z, int seed)
    {
        unchecked
        {
            var hash = seed;
            hash = hash * 397 ^ x;
            hash = hash * 397 ^ z;
            hash ^= hash >> 13;
            hash *= 1274126177;
            hash ^= hash >> 16;
            return (hash & 0x7fffffff) / (double)int.MaxValue;
        }
    }
}
