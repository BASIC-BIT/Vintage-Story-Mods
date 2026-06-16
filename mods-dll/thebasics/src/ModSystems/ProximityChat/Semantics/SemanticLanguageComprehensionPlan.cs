#nullable enable

using System;

namespace thebasics.ModSystems.ProximityChat.Semantics;

public sealed class SemanticLanguageComprehensionPlan
{
    public SemanticLanguageComprehensionPlan(int[] wordComprehension)
    {
        WordComprehension = wordComprehension ?? Array.Empty<int>();
    }

    public int[] WordComprehension { get; }

    public bool HasScores => WordComprehension.Length > 0;

    public int GetPercent(int wordIndex)
    {
        if (wordIndex < 0 || wordIndex >= WordComprehension.Length)
        {
            return 0;
        }

        return Math.Max(0, Math.Min(100, WordComprehension[wordIndex]));
    }
}
