#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace thebasics.ModSystems.ProximityChat.Semantics;

internal static class SemanticLanguageProgressBuilder
{
    public static SemanticLanguageProgress Build(string languageName, SemanticLanguageMemory? memory, string providerStatus, SemanticLanguageAtlasCatalog atlas)
    {
        var progress = new SemanticLanguageProgress
        {
            LanguageName = languageName,
            ProviderStatus = providerStatus,
            AtlasBucketCount = atlas?.Buckets.Count ?? 0
        };

        if (memory == null)
        {
            return progress;
        }

        AddAtlasProgress(progress, memory, atlas);
        return progress;
    }

    private static void AddAtlasProgress(SemanticLanguageProgress progress, SemanticLanguageMemory memory, SemanticLanguageAtlasCatalog? atlas)
    {
        if (atlas == null || !atlas.HasBuckets || memory.AtlasBuckets == null)
        {
            return;
        }

        var coverage = memory.AtlasBuckets
            .Select(entry => new
            {
                Entry = entry,
                Bucket = atlas.FindBucket(entry.BucketId)
            })
            .Where(entry => entry.Bucket != null && entry.Entry.Confidence > 0)
            .OrderByDescending(entry => SemanticLanguageMemoryOperations.ClampPercent(entry.Entry.Confidence))
            .ThenBy(entry => entry.Bucket!.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        progress.AtlasCoveredBucketCount = coverage.Count;
        progress.AtlasLearnedBucketCount = coverage.Count(entry => SemanticLanguageMemoryOperations.IsLearned(entry.Entry));
        progress.AtlasCoveragePercent = atlas.Buckets.Count == 0
            ? 0
            : SemanticLanguageMemoryOperations.ClampPercent((int)Math.Round(coverage.Sum(entry => SemanticLanguageMemoryOperations.ClampPercent(entry.Entry.Confidence)) / (double)atlas.Buckets.Count));
        progress.LearnedAtlasBuckets = coverage
            .Where(entry => SemanticLanguageMemoryOperations.IsLearned(entry.Entry))
            .Take(8)
            .Select(entry => entry.Bucket!.DisplayName)
            .ToArray();
        progress.InProgressAtlasBuckets = coverage
            .Where(entry => !SemanticLanguageMemoryOperations.IsLearned(entry.Entry))
            .Take(8)
            .Select(entry => $"{entry.Bucket!.DisplayName} ({SemanticLanguageMemoryOperations.ClampPercent(entry.Entry.Confidence)}%)")
            .ToArray();
    }
}
