#nullable enable

using System;
using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Semantics;

public sealed class SemanticLanguageProgress
{
    public string LanguageName { get; set; } = string.Empty;

    public string ProviderStatus { get; set; } = string.Empty;

    public int AtlasBucketCount { get; set; }

    public int AtlasCoveredBucketCount { get; set; }

    public int AtlasLearnedBucketCount { get; set; }

    public int AtlasCoveragePercent { get; set; }

    public IReadOnlyList<string> LearnedAtlasBuckets { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> InProgressAtlasBuckets { get; set; } = Array.Empty<string>();
}
