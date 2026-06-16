#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using thebasics.Configs;

namespace thebasics.ModSystems.ProximityChat.Semantics;

internal sealed class SemanticLanguageMatcher
{
    private readonly SemanticLanguageLearningConfig _config;
    private readonly SemanticLanguageAtlasCatalog _atlas;
    private readonly Func<IReadOnlyList<SemanticAtlasBucketVector>> _getAtlasVectors;
    private readonly Action<string> _enqueueCandidatePrewarm;

    public SemanticLanguageMatcher(
        SemanticLanguageLearningConfig config,
        SemanticLanguageAtlasCatalog atlas,
        Func<IReadOnlyList<SemanticAtlasBucketVector>> getAtlasVectors,
        Action<string> enqueueCandidatePrewarm)
    {
        _config = config;
        _atlas = atlas;
        _getAtlasVectors = getAtlasVectors;
        _enqueueCandidatePrewarm = enqueueCandidatePrewarm;
    }

    public async Task<List<SemanticSpanEmbedding>> EmbedObservationAsync(
        ITheBasicsSemanticEmbeddingProvider provider,
        string message,
        CancellationToken cancellationToken)
    {
        var tokens = SemanticTextCandidateBuilder.Tokenize(message).ToList();
        if (tokens.Count == 0)
        {
            return new List<SemanticSpanEmbedding>();
        }

        var priorityTokens = GetBucketHintTokens(_atlas.Buckets.Select(bucket => bucket.Id));
        if (tokens.Count <= _config.MaxChunkWords)
        {
            return await EmbedCandidateSpansAsync(
                provider,
                tokens,
                0,
                tokens.Count,
                _config.MaxSpansPerMessage,
                priorityTokens,
                cancellationToken).ConfigureAwait(false);
        }

        var routedChunks = await RouteRelevantChunksAsync(
            provider,
            tokens,
            allowedBucketIds: null,
            priorityTokens,
            _config.MaxRealtimeChunkEmbeddingsPerMessage,
            cancellationToken).ConfigureAwait(false);
        if (routedChunks.Count == 0)
        {
            return await EmbedCandidateSpansAsync(
                provider,
                tokens,
                0,
                tokens.Count,
                _config.MaxSpansPerMessage,
                priorityTokens,
                cancellationToken).ConfigureAwait(false);
        }

        var embeddings = new List<SemanticSpanEmbedding>();
        foreach (var chunk in routedChunks)
        {
            embeddings.AddRange(await EmbedCandidateSpansAsync(
                provider,
                tokens,
                chunk.StartIndex,
                chunk.EndIndex,
                _config.MaxRealtimeSpanEmbeddingsPerChunk,
                priorityTokens,
                cancellationToken).ConfigureAwait(false));
        }

        return embeddings;
    }

    public List<SemanticBucketSpanMatch> MatchMessageSpansForComprehension(
        ITheBasicsSemanticEmbeddingProvider provider,
        List<WordToken> tokens,
        IEnumerable<string> allowedBucketIds)
    {
        var priorityTokens = GetBucketHintTokens(allowedBucketIds);
        if (tokens.Count <= _config.MaxChunkWords)
        {
            return MatchMessageSpans(
                provider,
                tokens,
                new SpanMatchOptions(
                    _config.MinimumComprehensionSimilarity,
                    allowedBucketIds,
                    priorityTokens: priorityTokens));
        }

        var routedChunks = RouteRelevantChunks(
            provider,
            tokens,
            allowedBucketIds,
            priorityTokens);
        if (routedChunks.Count == 0)
        {
            return MatchMessageSpans(
                provider,
                tokens,
                new SpanMatchOptions(
                    _config.MinimumComprehensionSimilarity,
                    allowedBucketIds,
                    priorityTokens: priorityTokens));
        }

        var matches = new List<SemanticBucketSpanMatch>();
        foreach (var chunk in routedChunks)
        {
            matches.AddRange(MatchMessageSpans(
                provider,
                tokens,
                new SpanMatchOptions(
                    _config.MinimumComprehensionSimilarity,
                    allowedBucketIds,
                    startTokenIndex: chunk.StartIndex,
                    endTokenIndex: chunk.EndIndex,
                    priorityTokens)));
        }

        return ResolveOverlaps(matches, preferLongerMatches: false);
    }

    public SemanticBucketSpanMatch? TryMatchAtlasBucket(
        SemanticSpanEmbedding embedding,
        float minimumSimilarity,
        ISet<string>? allowedBucketIds = null)
    {
        SemanticAtlasBucketVector? bestVector = null;
        var bestSimilarity = 0f;
        foreach (var atlasVector in _getAtlasVectors())
        {
            if (allowedBucketIds?.Contains(atlasVector.BucketId) == false)
            {
                continue;
            }

            var similarity = SemanticVectorMath.CosineSimilarity(embedding.Vector, atlasVector.Vector);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestVector = atlasVector;
            }
        }

        return bestVector != null && bestSimilarity >= minimumSimilarity
            ? new SemanticBucketSpanMatch(embedding.StartIndex, embedding.EndIndex, embedding.Text, bestVector.BucketId, bestSimilarity)
            : null;
    }

    public static List<SemanticBucketSpanMatch> ResolveOverlaps(IEnumerable<SemanticBucketSpanMatch> matches, bool preferLongerMatches = true)
    {
        var selected = new List<SemanticBucketSpanMatch>();
        foreach (var match in matches
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => preferLongerMatches ? -match.WordCount : match.WordCount))
        {
            if (selected.Any(existing => match.StartIndex < existing.EndIndex && existing.StartIndex < match.EndIndex))
            {
                continue;
            }

            selected.Add(match);
        }

        return selected.OrderBy(match => match.StartIndex).ToList();
    }

    private async Task<List<SemanticSpanEmbedding>> EmbedCandidateSpansAsync(
        ITheBasicsSemanticEmbeddingProvider provider,
        List<WordToken> tokens,
        int startTokenIndex,
        int endTokenIndex,
        int maxSpans,
        ISet<string>? priorityTokens,
        CancellationToken cancellationToken)
    {
        var embeddings = new List<SemanticSpanEmbedding>();
        foreach (var candidate in SemanticTextCandidateBuilder.BuildCandidateSpans(
            tokens,
            _config.MaxSpanWords,
            maxSpans,
            startTokenIndex,
            endTokenIndex,
            priorityTokens))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vector = await provider.EmbedAsync(candidate.Text, cancellationToken).ConfigureAwait(false);
            vector = SemanticVectorMath.Normalize(vector);
            if (vector != null)
            {
                embeddings.Add(new SemanticSpanEmbedding(candidate, vector));
            }
        }

        return embeddings;
    }

    private List<SemanticBucketSpanMatch> RouteRelevantChunks(
        ITheBasicsSemanticEmbeddingProvider provider,
        List<WordToken> tokens,
        IEnumerable<string>? allowedBucketIds,
        ISet<string>? priorityTokens)
    {
        var allowed = allowedBucketIds == null
            ? null
            : new HashSet<string>(allowedBucketIds, StringComparer.OrdinalIgnoreCase);
        var routed = new List<SemanticBucketSpanMatch>();
        foreach (var chunk in SemanticTextCandidateBuilder.BuildTokenChunks(tokens, _config.MaxChunkWords, _config.ChunkOverlapWords, priorityTokens))
        {
            var chunkVector = GetCachedCandidateVector(provider, chunk.Text);
            if (chunkVector == null)
            {
                continue;
            }

            var match = TryMatchAtlasBucket(
                new SemanticSpanEmbedding(chunk, chunkVector),
                _config.MinimumChunkRoutingSimilarity,
                allowed);
            if (match != null)
            {
                routed.Add(match);
            }
        }

        return SelectRoutedChunks(routed);
    }

    private async Task<List<SemanticBucketSpanMatch>> RouteRelevantChunksAsync(
        ITheBasicsSemanticEmbeddingProvider provider,
        List<WordToken> tokens,
        IEnumerable<string>? allowedBucketIds,
        ISet<string>? priorityTokens,
        int maxEmbeddings,
        CancellationToken cancellationToken)
    {
        var allowed = allowedBucketIds == null
            ? null
            : new HashSet<string>(allowedBucketIds, StringComparer.OrdinalIgnoreCase);
        var routed = new List<SemanticBucketSpanMatch>();
        var embeddingsUsed = 0;
        foreach (var chunk in SemanticTextCandidateBuilder.BuildTokenChunks(tokens, _config.MaxChunkWords, _config.ChunkOverlapWords, priorityTokens))
        {
            if (embeddingsUsed >= maxEmbeddings)
            {
                break;
            }

            cancellationToken.ThrowIfCancellationRequested();
            embeddingsUsed++;
            var chunkVector = SemanticVectorMath.Normalize(await provider.EmbedAsync(chunk.Text, cancellationToken).ConfigureAwait(false));
            if (chunkVector == null)
            {
                continue;
            }

            var match = TryMatchAtlasBucket(
                new SemanticSpanEmbedding(chunk, chunkVector),
                _config.MinimumChunkRoutingSimilarity,
                allowed);
            if (match != null)
            {
                routed.Add(match);
            }
        }

        return SelectRoutedChunks(routed);
    }

    private List<SemanticBucketSpanMatch> SelectRoutedChunks(IEnumerable<SemanticBucketSpanMatch> routed)
    {
        var selected = new List<SemanticBucketSpanMatch>();
        foreach (var chunk in routed
            .OrderByDescending(chunk => chunk.Similarity)
            .ThenBy(chunk => chunk.StartIndex))
        {
            if (selected.Any(existing => chunk.StartIndex < existing.EndIndex && existing.StartIndex < chunk.EndIndex))
            {
                continue;
            }

            selected.Add(chunk);
            if (selected.Count >= _config.MaxFineChunksPerMessage)
            {
                break;
            }
        }

        return selected.OrderBy(chunk => chunk.StartIndex).ToList();
    }

    private List<SemanticBucketSpanMatch> MatchMessageSpans(
        ITheBasicsSemanticEmbeddingProvider provider,
        List<WordToken> tokens,
        SpanMatchOptions options)
    {
        var allowed = options.AllowedBucketIds == null
            ? null
            : new HashSet<string>(options.AllowedBucketIds, StringComparer.OrdinalIgnoreCase);
        var matches = new List<SemanticBucketSpanMatch>();
        foreach (var candidate in SemanticTextCandidateBuilder.BuildCandidateSpans(
            tokens,
            _config.MaxSpanWords,
            _config.MaxSpansPerMessage,
            options.StartTokenIndex,
            options.EndTokenIndex,
            options.PriorityTokens))
        {
            var candidateVector = GetCachedCandidateVector(provider, candidate.Text);
            if (candidateVector == null)
            {
                continue;
            }

            var match = TryMatchAtlasBucket(new SemanticSpanEmbedding(candidate, candidateVector), options.MinimumSimilarity, allowed);
            if (match == null)
            {
                continue;
            }

            matches.Add(match);
        }

        return ResolveOverlaps(matches, preferLongerMatches: false);
    }

    private float[]? GetCachedCandidateVector(ITheBasicsSemanticEmbeddingProvider provider, string candidate)
    {
        if (provider.TryGetCachedEmbedding(candidate, out var cachedVector))
        {
            return SemanticVectorMath.Normalize(cachedVector);
        }

        _enqueueCandidatePrewarm(candidate);
        return null;
    }

    private ISet<string> GetBucketHintTokens(IEnumerable<string> bucketIds)
    {
        var hints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bucketId in bucketIds)
        {
            var bucket = _atlas.FindBucket(bucketId);
            if (bucket == null)
            {
                continue;
            }

            AddHintTokens(hints, bucket.Id.Replace('-', ' '));
            AddHintTokens(hints, bucket.Alias.Replace('-', ' '));
            AddHintTokens(hints, bucket.Label);
            foreach (var example in bucket.Examples ?? new List<string>())
            {
                AddHintTokens(hints, example);
            }
        }

        return hints;
    }

    private static void AddHintTokens(ISet<string> hints, string text)
    {
        foreach (var token in SemanticTextCandidateBuilder.Tokenize(text))
        {
            hints.Add(token.Text.ToLowerInvariant());
        }
    }
}
