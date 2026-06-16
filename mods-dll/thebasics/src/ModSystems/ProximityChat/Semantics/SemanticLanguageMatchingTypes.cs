#nullable enable

using System.Collections.Generic;

namespace thebasics.ModSystems.ProximityChat.Semantics;

internal sealed class SemanticLanguageObservation
{
    public SemanticLanguageObservation(string playerUid, string languageName, string message)
    {
        PlayerUid = playerUid;
        LanguageName = languageName;
        Message = message;
    }

    public string PlayerUid { get; }

    public string LanguageName { get; }

    public string Message { get; }
}

internal sealed class SemanticCandidateSpan
{
    public SemanticCandidateSpan(int startIndex, int endIndex, string text)
    {
        StartIndex = startIndex;
        EndIndex = endIndex;
        Text = text;
    }

    public int StartIndex { get; }

    public int EndIndex { get; }

    public string Text { get; }
}

internal sealed class SemanticSpanEmbedding
{
    public SemanticSpanEmbedding(SemanticCandidateSpan span, float[] vector)
    {
        StartIndex = span.StartIndex;
        EndIndex = span.EndIndex;
        Text = span.Text;
        Vector = vector;
    }

    public int StartIndex { get; }

    public int EndIndex { get; }

    public string Text { get; }

    public float[] Vector { get; }
}

internal sealed class SemanticAtlasBucketVector
{
    public SemanticAtlasBucketVector(string bucketId, string phrase, float[] vector)
    {
        BucketId = bucketId;
        Phrase = phrase;
        Vector = vector;
    }

    public string BucketId { get; }

    public string Phrase { get; }

    public float[] Vector { get; }
}

internal sealed class SemanticBucketSpanMatch
{
    public SemanticBucketSpanMatch(int startIndex, int endIndex, string text, string bucketId, float similarity)
    {
        StartIndex = startIndex;
        EndIndex = endIndex;
        Text = text;
        BucketId = bucketId;
        Similarity = similarity;
    }

    public int StartIndex { get; }

    public int EndIndex { get; }

    public int WordCount => EndIndex - StartIndex;
    public string Text { get; }
    public string BucketId { get; }
    public float Similarity { get; }
}

internal sealed class SpanMatchOptions
{
    public SpanMatchOptions(
        float minimumSimilarity,
        IEnumerable<string>? allowedBucketIds,
        bool allowOnDemandEmbeddings,
        int maxOnDemandEmbeddings,
        int startTokenIndex = 0,
        int? endTokenIndex = null,
        ISet<string>? priorityTokens = null)
    {
        MinimumSimilarity = minimumSimilarity;
        AllowedBucketIds = allowedBucketIds;
        AllowOnDemandEmbeddings = allowOnDemandEmbeddings;
        MaxOnDemandEmbeddings = maxOnDemandEmbeddings;
        StartTokenIndex = startTokenIndex;
        EndTokenIndex = endTokenIndex;
        PriorityTokens = priorityTokens;
    }

    public float MinimumSimilarity { get; }

    public IEnumerable<string>? AllowedBucketIds { get; }

    public bool AllowOnDemandEmbeddings { get; }

    public int MaxOnDemandEmbeddings { get; }

    public int StartTokenIndex { get; }

    public int? EndTokenIndex { get; }

    public ISet<string>? PriorityTokens { get; }
}

internal sealed class WordToken
{
    public WordToken(string text, int originalWordIndex)
    {
        Text = text;
        OriginalWordIndex = originalWordIndex;
    }

    public string Text { get; }

    public int OriginalWordIndex { get; }
}
