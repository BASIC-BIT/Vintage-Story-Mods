#nullable enable

using System;

namespace thebasics.ModSystems.ProximityChat.Semantics;

internal static class SemanticVectorMath
{
    public static float[]? Normalize(float[]? vector)
    {
        if (vector == null || vector.Length == 0)
        {
            return null;
        }

        var magnitudeSquared = 0f;
        for (var index = 0; index < vector.Length; index++)
        {
            magnitudeSquared += vector[index] * vector[index];
        }

        if (magnitudeSquared <= 0f || float.IsNaN(magnitudeSquared) || float.IsInfinity(magnitudeSquared))
        {
            return null;
        }

        var magnitude = MathF.Sqrt(magnitudeSquared);
        var normalized = new float[vector.Length];
        for (var index = 0; index < vector.Length; index++)
        {
            normalized[index] = vector[index] / magnitude;
        }

        return normalized;
    }

    public static float CosineSimilarity(float[] left, float[] right)
    {
        if (left == null || right == null || left.Length == 0 || left.Length != right.Length)
        {
            return 0f;
        }

        var dot = 0f;
        for (var index = 0; index < left.Length; index++)
        {
            dot += left[index] * right[index];
        }

        return dot;
    }
}
