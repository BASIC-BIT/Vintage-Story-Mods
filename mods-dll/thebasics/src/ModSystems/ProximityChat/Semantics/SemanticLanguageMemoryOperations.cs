#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace thebasics.ModSystems.ProximityChat.Semantics;

internal static class SemanticLanguageMemoryOperations
{
    public static SemanticLanguageMemory? FindLanguageMemory(SemanticLanguageMemoryStore store, string languageName)
    {
        return store.Languages?.FirstOrDefault(memory => string.Equals(memory.LanguageName, languageName, StringComparison.OrdinalIgnoreCase));
    }

    public static SemanticLanguageMemory FindOrCreateLanguageMemory(SemanticLanguageMemoryStore store, string languageName)
    {
        store.Languages ??= new List<SemanticLanguageMemory>();
        var memory = FindLanguageMemory(store, languageName);
        if (memory != null)
        {
            memory.AtlasBuckets ??= new List<SemanticLanguageAtlasBucketCoverage>();
            return memory;
        }

        memory = new SemanticLanguageMemory
        {
            LanguageName = languageName,
            AtlasBuckets = new List<SemanticLanguageAtlasBucketCoverage>()
        };
        store.Languages.Add(memory);
        return memory;
    }

    public static Dictionary<string, int> GetBucketConfidence(SemanticLanguageMemory memory)
    {
        return (memory.AtlasBuckets ?? new List<SemanticLanguageAtlasBucketCoverage>())
            .Where(bucket => bucket.Confidence > 0 && !string.IsNullOrWhiteSpace(bucket.BucketId))
            .GroupBy(bucket => bucket.BucketId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => ClampPercent(group.Max(bucket => bucket.Confidence)),
                StringComparer.OrdinalIgnoreCase);
    }

    public static SemanticLanguageAtlasBucketCoverage? FindBucketCoverage(SemanticLanguageMemory memory, string bucketId)
    {
        memory.AtlasBuckets ??= new List<SemanticLanguageAtlasBucketCoverage>();
        return memory.AtlasBuckets.FirstOrDefault(entry => string.Equals(entry.BucketId, bucketId, StringComparison.OrdinalIgnoreCase));
    }

    public static void SetBucketConfidence(
        SemanticLanguageMemory memory,
        string bucketId,
        int confidence,
        long now,
        bool promoteToLearned,
        int learnedThresholdPercent,
        out bool learnedNow)
    {
        learnedNow = false;
        memory.AtlasBuckets ??= new List<SemanticLanguageAtlasBucketCoverage>();
        var normalizedBucketId = SemanticLanguageAtlasCatalog.NormalizeIdentifier(bucketId);
        var clampedConfidence = ClampPercent(confidence);
        if (clampedConfidence <= 0)
        {
            memory.AtlasBuckets.RemoveAll(entry => string.Equals(entry.BucketId, normalizedBucketId, StringComparison.OrdinalIgnoreCase));
            return;
        }

        var existing = FindBucketCoverage(memory, normalizedBucketId);
        if (promoteToLearned && clampedConfidence >= learnedThresholdPercent)
        {
            clampedConfidence = 100;
        }

        if (existing == null)
        {
            existing = new SemanticLanguageAtlasBucketCoverage
            {
                BucketId = normalizedBucketId
            };
            memory.AtlasBuckets.Add(existing);
        }

        var wasLearned = IsLearned(existing);
        existing.Confidence = clampedConfidence;
        existing.ExposureCount = Math.Max(0, existing.ExposureCount) + 1;
        existing.LastUpdatedUnixSeconds = now;
        if (clampedConfidence >= 100 && existing.LearnedAtUnixSeconds <= 0)
        {
            existing.LearnedAtUnixSeconds = now;
        }

        learnedNow = !wasLearned && IsLearned(existing);
    }

    public static bool IsLearned(SemanticLanguageAtlasBucketCoverage coverage)
    {
        return coverage.Confidence >= 100 || coverage.LearnedAtUnixSeconds > 0;
    }

    public static int ClampPercent(int value)
    {
        return Math.Max(0, Math.Min(100, value));
    }
}
