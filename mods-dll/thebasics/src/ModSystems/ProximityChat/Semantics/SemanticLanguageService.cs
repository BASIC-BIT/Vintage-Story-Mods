#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Semantics;

public sealed class SemanticLanguageService : IDisposable
{
    private const int MaxQueuedObservations = 256;
    private const int MaxQueuedCandidatePrewarms = 512;
    private const int AtlasPhraseEmbeddingTimeoutMs = 1500;
    private const int OnDemandEmbeddingTimeoutMs = 45;

    private readonly LanguageSystem _languageSystem;
    private readonly ICoreServerAPI _api;
    private readonly SemanticLanguageLearningConfig _config;
    private readonly ConcurrentQueue<SemanticLanguageObservation> _observations = new ConcurrentQueue<SemanticLanguageObservation>();
    private readonly ConcurrentQueue<string> _candidatePrewarms = new ConcurrentQueue<string>();
    private readonly ConcurrentDictionary<string, byte> _queuedCandidatePrewarms = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
    private readonly object _workerLock = new object();
    private readonly object _prewarmWorkerLock = new object();
    private readonly object _atlasIndexLock = new object();
    private readonly CancellationTokenSource _disposeCts = new CancellationTokenSource();
    private readonly SemanticLanguageAtlasCatalog _atlas;
    private readonly System.Func<string, Language?> _languageResolver;
    private readonly System.Func<string, IServerPlayer?> _playerResolver;
    private IReadOnlyList<SemanticAtlasBucketVector> _atlasVectors = Array.Empty<SemanticAtlasBucketVector>();
    private bool _workerRunning;
    private bool _prewarmWorkerRunning;
    private int _queuedObservations;
    private int _queuedCandidatePrewarmCount;
    private ITheBasicsSemanticEmbeddingProvider? _provider;

    public SemanticLanguageService(LanguageSystem languageSystem, ICoreServerAPI api, SemanticLanguageLearningConfig config)
        : this(languageSystem, api, SemanticLanguageAtlasCatalog.LoadDefault(api), config)
    {
    }

    internal SemanticLanguageService(
        LanguageSystem languageSystem,
        ICoreServerAPI api,
        SemanticLanguageAtlasCatalog? atlas,
        SemanticLanguageLearningConfig? config = null,
        System.Func<string, Language?>? languageResolver = null,
        System.Func<string, IServerPlayer?>? playerResolver = null)
    {
        _languageSystem = languageSystem;
        _api = api;
        _atlas = atlas ?? SemanticLanguageAtlasCatalog.Empty;
        _config = config ?? new SemanticLanguageLearningConfig();
        _config.Normalize();
        _languageResolver = languageResolver ?? (name => _languageSystem?.GetLangFromText(name, false, allowHidden: true));
        _playerResolver = playerResolver ?? (playerUid => _api.GetPlayerByUID(playerUid));
    }

    public int QueuedObservationCount => Math.Max(0, Volatile.Read(ref _queuedObservations));

    public string ProviderStatus
    {
        get
        {
            if (!_config.Enabled)
            {
                return "Semantic language learning disabled.";
            }

            var provider = _provider;
            if (provider == null)
            {
                return "No semantic embedding provider registered; semantic language learning is degraded.";
            }

            return $"{provider.ProviderId} ({provider.Dimensions}d, ready={provider.IsReady}, queued={QueuedObservationCount}, atlasVectors={AtlasVectorCount})";
        }
    }

    public SemanticLanguageAtlasCatalog Atlas => _atlas;

    private int AtlasVectorCount
    {
        get
        {
            lock (_atlasIndexLock)
            {
                return _atlasVectors.Count;
            }
        }
    }

    public bool RegisterProvider(ITheBasicsSemanticEmbeddingProvider provider)
    {
        if (provider == null)
        {
            return false;
        }

        if (provider.IsReady && provider.Dimensions <= 0)
        {
            _api.Logger.Warning($"[thebasics] Semantic embedding provider '{provider.ProviderId}' reported invalid dimensions: {provider.Dimensions}.");
            return false;
        }

        _provider = provider;
        RebuildAtlasIndex(provider);
        _api.Logger.Notification($"[thebasics] Registered semantic embedding provider '{provider.ProviderId}' ({provider.Dimensions} dimensions, {AtlasVectorCount} atlas vectors). ");
        StartWorkerIfNeeded();
        return true;
    }

    public void ObserveMessageForRecipient(MessageContext context)
    {
        if (!CanObserve(context, out var language))
        {
            return;
        }

        foreach (var segment in GetObservableSegments(context!))
        {
            EnqueueObservation(context!.ReceivingPlayer.PlayerUID, language.Name, segment);
        }
    }

    public SemanticLanguageComprehensionPlan? BuildComprehensionPlan(IServerPlayer player, Language language, string message)
    {
        if (!TryGetComprehensionInputs(player, language, message, out var provider, out var tokens, out var originalWordCount, out var bucketConfidence))
        {
            return null;
        }

        var scores = BuildComprehensionScores(provider!, tokens, originalWordCount, bucketConfidence);

        return scores.Any(score => score > 0)
            ? new SemanticLanguageComprehensionPlan(scores)
            : null;
    }

    private bool CanObserve(MessageContext? context, out Language language)
    {
        language = null!;
        if (!_config.Enabled || !IsProviderReady(_provider) || !HasAtlasVectors() || context?.ReceivingPlayer == null)
        {
            return false;
        }

        if (IsSpeakerRecipient(context))
        {
            return false;
        }

        if (!context.TryGetMetadata(MessageContext.LANGUAGE, out language) || language == null)
        {
            return false;
        }

        return !context.ReceivingPlayer.KnowsLanguage(language) &&
            !string.Equals(language.Name, LanguageSystem.BabbleLang.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpeakerRecipient(MessageContext context)
    {
        return context.SendingPlayer != null &&
            string.Equals(context.SendingPlayer.PlayerUID, context.ReceivingPlayer.PlayerUID, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetComprehensionInputs(
        IServerPlayer player,
        Language language,
        string message,
        out ITheBasicsSemanticEmbeddingProvider? provider,
        out List<WordToken> tokens,
        out int originalWordCount,
        out Dictionary<string, int> bucketConfidence)
    {
        provider = _provider;
        tokens = new List<WordToken>();
        originalWordCount = 0;
        bucketConfidence = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!_config.Enabled || !IsProviderReady(provider) || !HasAtlasVectors() || !HasComprehensionInputs(player, language, message))
        {
            return false;
        }

        var memory = SemanticLanguageMemoryOperations.FindLanguageMemory(player.GetSemanticLanguageMemory(), language.Name);
        if (memory?.AtlasBuckets == null || memory.AtlasBuckets.Count == 0)
        {
            return false;
        }

        bucketConfidence = SemanticLanguageMemoryOperations.GetBucketConfidence(memory);
        tokens = SemanticTextCandidateBuilder.TokenizeWithOriginalWordCount(message, out originalWordCount);
        return bucketConfidence.Count > 0 && tokens.Count > 0;
    }

    private int[] BuildComprehensionScores(
        ITheBasicsSemanticEmbeddingProvider provider,
        List<WordToken> tokens,
        int originalWordCount,
        Dictionary<string, int> bucketConfidence)
    {
        var scores = new int[originalWordCount];
        var matches = MatchMessageSpansForComprehension(provider, tokens, bucketConfidence.Keys);
        foreach (var match in matches)
        {
            if (bucketConfidence.TryGetValue(match.BucketId, out var confidence))
            {
                ApplyMatchScore(scores, tokens, match, confidence);
            }
        }

        return scores;
    }

    private static void ApplyMatchScore(int[] scores, List<WordToken> tokens, SemanticBucketSpanMatch match, int confidence)
    {
        for (var index = match.StartIndex; index < match.EndIndex && index < tokens.Count; index++)
        {
            var originalWordIndex = tokens[index].OriginalWordIndex;
            if (originalWordIndex >= 0 && originalWordIndex < scores.Length)
            {
                scores[originalWordIndex] = Math.Max(scores[originalWordIndex], confidence);
            }
        }
    }

    public SemanticLanguageProgress BuildProgress(IServerPlayer player, Language language)
    {
        var languageName = language?.Name ?? string.Empty;
        var memory = player == null || language == null
            ? null
            : SemanticLanguageMemoryOperations.FindLanguageMemory(player.GetSemanticLanguageMemory(), language.Name);

        return SemanticLanguageProgressBuilder.Build(languageName, memory, ProviderStatus, _atlas);
    }

    public bool TrySetAtlasBucketCoverage(IServerPlayer player, Language language, string bucketIdentifier, int confidence, out SemanticLanguageAtlasBucket? bucket, out string errorCode)
    {
        bucket = null;
        errorCode = string.Empty;
        if (player == null || language == null || string.IsNullOrWhiteSpace(bucketIdentifier))
        {
            errorCode = "invalid";
            return false;
        }

        if (!_atlas.HasBuckets)
        {
            errorCode = "unavailable";
            return false;
        }

        if (!_atlas.TryResolveBucket(bucketIdentifier, out var resolvedBucket))
        {
            errorCode = "bucket-not-found";
            return false;
        }

        var store = player.GetSemanticLanguageMemory();
        var memory = SemanticLanguageMemoryOperations.FindOrCreateLanguageMemory(store, language.Name);
        SemanticLanguageMemoryOperations.SetBucketConfidence(memory, resolvedBucket.Id, confidence, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), promoteToLearned: true, learnedThresholdPercent: _config.LearnedThresholdPercent, out _);
        player.SetSemanticLanguageMemory(store);
        TryPromoteWholeLanguage(player, language, memory, notify: false);
        bucket = resolvedBucket;
        return true;
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    private void RebuildAtlasIndex(ITheBasicsSemanticEmbeddingProvider provider)
    {
        var vectors = new List<SemanticAtlasBucketVector>();
        if (!_config.Enabled || !_atlas.HasBuckets || !provider.IsReady)
        {
            SetAtlasVectors(vectors);
            return;
        }

        foreach (var bucket in _atlas.Buckets)
        {
            foreach (var phrase in GetAtlasBucketEmbeddingPhrases(bucket))
            {
                var vector = TryEmbedAtlasPhrase(provider, phrase);
                if (vector != null)
                {
                    vectors.Add(new SemanticAtlasBucketVector(bucket.Id, phrase, vector));
                }
            }
        }

        SetAtlasVectors(vectors);
    }

    private void SetAtlasVectors(IReadOnlyList<SemanticAtlasBucketVector> vectors)
    {
        lock (_atlasIndexLock)
        {
            _atlasVectors = vectors;
        }
    }

    private IReadOnlyList<SemanticAtlasBucketVector> GetAtlasVectors()
    {
        lock (_atlasIndexLock)
        {
            return _atlasVectors;
        }
    }

    private bool HasAtlasVectors()
    {
        return AtlasVectorCount > 0;
    }

    private float[]? TryEmbedAtlasPhrase(ITheBasicsSemanticEmbeddingProvider provider, string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return null;
        }

        try
        {
            using var timeout = new CancellationTokenSource(AtlasPhraseEmbeddingTimeoutMs);
            return SemanticVectorMath.Normalize(provider.EmbedAsync(phrase, timeout.Token).AsTask().GetAwaiter().GetResult());
        }
        catch (Exception ex) when (ex is OperationCanceledException || ex is InvalidOperationException)
        {
            _api.Logger.Warning($"[thebasics] Failed to embed semantic atlas phrase '{phrase}': {ex.Message}");
            return null;
        }
    }

    private IEnumerable<string> GetAtlasBucketEmbeddingPhrases(SemanticLanguageAtlasBucket bucket)
    {
        var phrases = new List<string>
        {
            bucket.Label,
            bucket.Alias,
            bucket.Alias.Replace('-', ' '),
            bucket.Id.Replace('-', ' ')
        };

        phrases.AddRange((bucket.Examples ?? new List<string>()).Take(_config.MaxAtlasExamplesPerBucket));
        return phrases
            .Select(SemanticTextCandidateBuilder.NormalizeText)
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
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

    private void EnqueueObservation(string playerUid, string languageName, string message)
    {
        if (string.IsNullOrWhiteSpace(playerUid) || string.IsNullOrWhiteSpace(languageName) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (Interlocked.Increment(ref _queuedObservations) > MaxQueuedObservations)
        {
            Interlocked.Decrement(ref _queuedObservations);
            return;
        }

        _observations.Enqueue(new SemanticLanguageObservation(playerUid, languageName, message));
        StartWorkerIfNeeded();
    }

    private void EnqueueCandidatePrewarm(string candidate)
    {
        var normalized = SemanticTextCandidateBuilder.NormalizeText(candidate);
        if (string.IsNullOrWhiteSpace(normalized) || !_queuedCandidatePrewarms.TryAdd(normalized, 0))
        {
            return;
        }

        if (Interlocked.Increment(ref _queuedCandidatePrewarmCount) > MaxQueuedCandidatePrewarms)
        {
            _queuedCandidatePrewarms.TryRemove(normalized, out _);
            Interlocked.Decrement(ref _queuedCandidatePrewarmCount);
            return;
        }

        _candidatePrewarms.Enqueue(normalized);
        StartPrewarmWorkerIfNeeded();
    }

    private void StartWorkerIfNeeded()
    {
        lock (_workerLock)
        {
            if (_workerRunning)
            {
                return;
            }

            _workerRunning = true;
        }

        Task.Run(ProcessQueueAsync);
    }

    private void StartPrewarmWorkerIfNeeded()
    {
        lock (_prewarmWorkerLock)
        {
            if (_prewarmWorkerRunning)
            {
                return;
            }

            _prewarmWorkerRunning = true;
        }

        Task.Run(ProcessCandidatePrewarmQueueAsync);
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            while (!_disposeCts.IsCancellationRequested && _observations.TryDequeue(out var observation))
            {
                Interlocked.Decrement(ref _queuedObservations);
                var provider = _provider;
                if (provider == null || !provider.IsReady || !HasAtlasVectors())
                {
                    continue;
                }

                var embeddings = await EmbedObservationAsync(provider, observation, _disposeCts.Token).ConfigureAwait(false);
                if (embeddings.Count > 0)
                {
                    OnMain(() => ApplyObservation(observation, embeddings));
                }
            }
        }
        catch (OperationCanceledException ex) when (_disposeCts.IsCancellationRequested)
        {
            _ = ex;
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Semantic language observation worker failed: {ex.Message}");
        }
        finally
        {
            lock (_workerLock)
            {
                _workerRunning = false;
            }

            if (!_observations.IsEmpty && !_disposeCts.IsCancellationRequested)
            {
                StartWorkerIfNeeded();
            }
        }
    }

    private async Task ProcessCandidatePrewarmQueueAsync()
    {
        try
        {
            while (!_disposeCts.IsCancellationRequested && _candidatePrewarms.TryDequeue(out var candidate))
            {
                Interlocked.Decrement(ref _queuedCandidatePrewarmCount);
                _queuedCandidatePrewarms.TryRemove(candidate, out _);

                var provider = _provider;
                if (provider == null || !provider.IsReady || provider.TryGetCachedEmbedding(candidate, out _))
                {
                    continue;
                }

                await provider.EmbedAsync(candidate, _disposeCts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException ex) when (_disposeCts.IsCancellationRequested)
        {
            _ = ex;
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Semantic language candidate prewarm worker failed: {ex.Message}");
        }
        finally
        {
            lock (_prewarmWorkerLock)
            {
                _prewarmWorkerRunning = false;
            }

            if (!_candidatePrewarms.IsEmpty && !_disposeCts.IsCancellationRequested)
            {
                StartPrewarmWorkerIfNeeded();
            }
        }
    }

    private async Task<List<SemanticSpanEmbedding>> EmbedObservationAsync(
        ITheBasicsSemanticEmbeddingProvider provider,
        SemanticLanguageObservation observation,
        CancellationToken cancellationToken)
    {
        var tokens = SemanticTextCandidateBuilder.Tokenize(observation.Message).ToList();
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

    private void ApplyObservation(SemanticLanguageObservation observation, List<SemanticSpanEmbedding> embeddings)
    {
        var player = _playerResolver(observation.PlayerUid);
        var language = _languageResolver(observation.LanguageName);
        if (player == null || language == null || player.KnowsLanguage(language))
        {
            return;
        }

        var matches = ResolveOverlaps(embeddings
            .Select(embedding => TryMatchAtlasBucket(embedding, _config.MinimumLearningSimilarity))
            .Where(match => match != null)
            .Cast<SemanticBucketSpanMatch>());
        if (matches.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var store = player.GetSemanticLanguageMemory();
        var memory = SemanticLanguageMemoryOperations.FindOrCreateLanguageMemory(store, language.Name);
        if (IsLearningObservationOnCooldown(memory, now))
        {
            return;
        }

        var learnedBuckets = ApplyBucketLearning(memory, matches, now, out var updatedAnyBucket);
        if (!updatedAnyBucket)
        {
            return;
        }

        memory.LastLearningObservationUnixSeconds = now;
        player.SetSemanticLanguageMemory(store);
        NotifyLearnedBuckets(player, language, learnedBuckets);
        TryPromoteWholeLanguage(player, language, memory, notify: true);
    }

    private bool IsLearningObservationOnCooldown(SemanticLanguageMemory memory, long now)
    {
        return _config.MinimumSecondsBetweenLearningObservations > 0 &&
            memory.LastLearningObservationUnixSeconds > 0 &&
            now - memory.LastLearningObservationUnixSeconds < _config.MinimumSecondsBetweenLearningObservations;
    }

    private List<SemanticLanguageAtlasBucket> ApplyBucketLearning(SemanticLanguageMemory memory, IEnumerable<SemanticBucketSpanMatch> matches, long now, out bool updatedAnyBucket)
    {
        var learnedBuckets = new List<SemanticLanguageAtlasBucket>();
        updatedAnyBucket = false;
        foreach (var match in matches
            .GroupBy(match => match.BucketId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(match => match.Similarity).First()))
        {
            var existing = SemanticLanguageMemoryOperations.FindBucketCoverage(memory, match.BucketId);
            if (IsBucketLearningOnCooldown(existing, now))
            {
                continue;
            }

            var gain = CalculateLearningGain(match.Similarity);
            var current = existing?.Confidence ?? 0;
            var target = Math.Min(100, current + gain);
            SemanticLanguageMemoryOperations.SetBucketConfidence(memory, match.BucketId, target, now, promoteToLearned: true, learnedThresholdPercent: _config.LearnedThresholdPercent, out var learnedNow);
            updatedAnyBucket = true;
            if (learnedNow)
            {
                var bucket = _atlas.FindBucket(match.BucketId);
                if (bucket != null)
                {
                    learnedBuckets.Add(bucket);
                }
            }
        }

        return learnedBuckets;
    }

    private bool IsBucketLearningOnCooldown(SemanticLanguageAtlasBucketCoverage? existing, long now)
    {
        return _config.MinimumSecondsBetweenBucketLearning > 0 &&
            existing?.LastUpdatedUnixSeconds > 0 &&
            now - existing.LastUpdatedUnixSeconds < _config.MinimumSecondsBetweenBucketLearning;
    }

    private int CalculateLearningGain(float similarity)
    {
        var rawGain = (int)Math.Round(Math.Max(0.25f, similarity) * _config.LearningRatePercent);
        return Math.Max(1, Math.Min(_config.MaxBucketProgressPerMessage, rawGain));
    }

    private void NotifyLearnedBuckets(IServerPlayer player, Language language, IEnumerable<SemanticLanguageAtlasBucket> learnedBuckets)
    {
        if (!_config.NotifyLearnedConcepts)
        {
            return;
        }

        foreach (var bucket in learnedBuckets)
        {
            player.SendMessage(
                GlobalConstants.CurrentChatGroup,
                Lang.Get(
                    "thebasics:lang-semantic-learned-concept",
                    FormatPlainLanguageIdentifier(language),
                    VtmlUtils.EscapeVtml(bucket.DisplayName)),
                EnumChatType.Notification);
        }
    }

    private void TryPromoteWholeLanguage(IServerPlayer player, Language language, SemanticLanguageMemory memory, bool notify)
    {
        if (!ShouldPromoteWholeLanguage(player, language, memory))
        {
            return;
        }

        player.AddLanguage(language);
        if (notify && _config.NotifyWholeLanguageLearned)
        {
            player.SendMessage(
                GlobalConstants.CurrentChatGroup,
                Lang.Get("thebasics:lang-semantic-learned-language", FormatPlainLanguageIdentifier(language)),
                EnumChatType.Notification);
        }
    }

    private bool ShouldPromoteWholeLanguage(IServerPlayer player, Language language, SemanticLanguageMemory memory)
    {
        if (!_config.EnableWholeLanguagePromotion || player == null || language == null || memory == null || player.KnowsLanguage(language) || !_atlas.HasBuckets)
        {
            return false;
        }

        var requiredLearnedBuckets = GetRequiredWholeLanguageLearnedBucketCount();
        return requiredLearnedBuckets > 0 && CountLearnedAtlasBuckets(memory) >= requiredLearnedBuckets;
    }

    private int GetRequiredWholeLanguageLearnedBucketCount()
    {
        var atlasBucketCount = _atlas.Buckets.Count;
        if (atlasBucketCount <= 0)
        {
            return 0;
        }

        var percentRequirement = (int)Math.Ceiling(atlasBucketCount * (_config.WholeLanguageLearnedBucketPercent / 100.0));
        return Math.Min(atlasBucketCount, Math.Max(_config.WholeLanguageMinimumLearnedBuckets, percentRequirement));
    }

    private int CountLearnedAtlasBuckets(SemanticLanguageMemory memory)
    {
        return (memory.AtlasBuckets ?? new List<SemanticLanguageAtlasBucketCoverage>())
            .Where(SemanticLanguageMemoryOperations.IsLearned)
            .Select(bucket => bucket.BucketId)
            .Where(bucketId => _atlas.FindBucket(bucketId) != null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private void OnMain(Action action)
    {
        try
        {
            _api.Event.EnqueueMainThreadTask(action, "thebasics-language-semantics");
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Failed to enqueue semantic language update: {ex.Message}");
        }
    }

    private static IEnumerable<string> GetObservableSegments(MessageContext context)
    {
        if (context.HasFlag(MessageContext.IS_SPEECH))
        {
            yield return context.Message ?? string.Empty;
            yield break;
        }

        if (!context.HasFlag(MessageContext.IS_EMOTE) || string.IsNullOrWhiteSpace(context.Message))
        {
            yield break;
        }

        var splitMessage = context.Message.Trim().Split('"');
        for (var index = 1; index < splitMessage.Length; index += 2)
        {
            if (!string.IsNullOrWhiteSpace(splitMessage[index]))
            {
                yield return splitMessage[index];
            }
        }
    }

    private List<SemanticBucketSpanMatch> MatchMessageSpansForComprehension(
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
                    true,
                    _config.MaxRealtimeEmbeddingsPerMessage,
                    priorityTokens: priorityTokens));
        }

        var routedChunks = RouteRelevantChunks(
            provider,
            tokens,
            allowedBucketIds,
            priorityTokens,
            _config.MaxRealtimeChunkEmbeddingsPerMessage);
        if (routedChunks.Count == 0)
        {
            return MatchMessageSpans(
                provider,
                tokens,
                new SpanMatchOptions(
                    _config.MinimumComprehensionSimilarity,
                    allowedBucketIds,
                    true,
                    _config.MaxRealtimeEmbeddingsPerMessage,
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
                    true,
                    _config.MaxRealtimeSpanEmbeddingsPerChunk,
                    chunk.StartIndex,
                    chunk.EndIndex,
                    priorityTokens)));
        }

        return ResolveOverlaps(matches, preferLongerMatches: false);
    }

    private List<SemanticBucketSpanMatch> RouteRelevantChunks(
        ITheBasicsSemanticEmbeddingProvider provider,
        List<WordToken> tokens,
        IEnumerable<string>? allowedBucketIds,
        ISet<string>? priorityTokens,
        int maxOnDemandEmbeddings)
    {
        var allowed = allowedBucketIds == null
            ? null
            : new HashSet<string>(allowedBucketIds, StringComparer.OrdinalIgnoreCase);
        var routed = new List<SemanticBucketSpanMatch>();
        var onDemandEmbeddingsUsed = 0;
        foreach (var chunk in SemanticTextCandidateBuilder.BuildTokenChunks(tokens, _config.MaxChunkWords, _config.ChunkOverlapWords, priorityTokens))
        {
            var chunkVector = GetCandidateVector(
                provider,
                chunk.Text,
                allowOnDemandEmbedding: true,
                ref onDemandEmbeddingsUsed,
                maxOnDemandEmbeddings);
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
        var onDemandEmbeddingsUsed = 0;
        foreach (var candidate in SemanticTextCandidateBuilder.BuildCandidateSpans(
            tokens,
            _config.MaxSpanWords,
            _config.MaxSpansPerMessage,
            options.StartTokenIndex,
            options.EndTokenIndex,
            options.PriorityTokens))
        {
            var candidateVector = GetCandidateVector(
                provider,
                candidate.Text,
                options.AllowOnDemandEmbeddings,
                ref onDemandEmbeddingsUsed,
                options.MaxOnDemandEmbeddings);
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

    private SemanticBucketSpanMatch? TryMatchAtlasBucket(
        SemanticSpanEmbedding embedding,
        float minimumSimilarity,
        ISet<string>? allowedBucketIds = null)
    {
        SemanticAtlasBucketVector? bestVector = null;
        var bestSimilarity = 0f;
        foreach (var atlasVector in GetAtlasVectors())
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

    private static List<SemanticBucketSpanMatch> ResolveOverlaps(IEnumerable<SemanticBucketSpanMatch> matches, bool preferLongerMatches = true)
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

    private float[]? GetCandidateVector(
        ITheBasicsSemanticEmbeddingProvider provider,
        string candidate,
        bool allowOnDemandEmbedding,
        ref int onDemandEmbeddingsUsed,
        int maxOnDemandEmbeddings)
    {
        if (provider.TryGetCachedEmbedding(candidate, out var cachedVector))
        {
            return SemanticVectorMath.Normalize(cachedVector);
        }

        EnqueueCandidatePrewarm(candidate);
        if (!allowOnDemandEmbedding || onDemandEmbeddingsUsed >= maxOnDemandEmbeddings)
        {
            return null;
        }

        onDemandEmbeddingsUsed++;
        return TryEmbedCandidateWithinBudget(provider, candidate);
    }

    private static float[]? TryEmbedCandidateWithinBudget(ITheBasicsSemanticEmbeddingProvider provider, string candidate)
    {
        using var timeout = new CancellationTokenSource(OnDemandEmbeddingTimeoutMs);
        var task = provider.EmbedAsync(candidate, timeout.Token).AsTask();
        try
        {
            if (task.Wait(OnDemandEmbeddingTimeoutMs))
            {
                return SemanticVectorMath.Normalize(task.Result);
            }
        }
        catch (AggregateException ex)
        {
            if (ex.InnerExceptions.All(inner => inner is OperationCanceledException))
            {
                return null;
            }

            throw;
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        return null;
    }

    private static bool IsProviderReady(ITheBasicsSemanticEmbeddingProvider? provider)
    {
        return provider != null && provider.IsReady;
    }

    private static bool HasComprehensionInputs(IServerPlayer player, Language language, string message)
    {
        return player != null && language != null && !string.IsNullOrWhiteSpace(message);
    }

    private static string FormatPlainLanguageIdentifier(Language language)
    {
        if (language == null)
        {
            return string.Empty;
        }

        var hiddenMarker = language.Hidden ? " [hidden]" : string.Empty;
        return VtmlUtils.EscapeVtml($"{language.Name} (:{language.Prefix}){hiddenMarker}");
    }

}
