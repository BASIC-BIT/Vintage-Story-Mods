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
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Semantics;

public sealed class SemanticLanguageService : IDisposable
{
    private const int MaxQueuedObservations = 256;
    private const int MaxQueuedCandidatePrewarms = 512;
    private const int AtlasPhraseEmbeddingTimeoutMs = 1500;

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
    private readonly SemanticLanguageMatcher _matcher;
    private readonly SemanticLanguageLearningApplier _learningApplier;
    private IReadOnlyList<SemanticAtlasBucketVector> _atlasVectors = Array.Empty<SemanticAtlasBucketVector>();
    private int _atlasIndexBuildVersion;
    private int _atlasIndexBuilding;
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
        var resolvedLanguageResolver = languageResolver ?? (name => _languageSystem?.GetLangFromText(name, false, allowHidden: true));
        var resolvedPlayerResolver = playerResolver ?? (playerUid => _api.GetPlayerByUID(playerUid));
        _matcher = new SemanticLanguageMatcher(_config, _atlas, GetAtlasVectors, EnqueueCandidatePrewarm);
        _learningApplier = new SemanticLanguageLearningApplier(_config, _atlas, _matcher, resolvedLanguageResolver, resolvedPlayerResolver);
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

            return $"{provider.ProviderId} ({provider.Dimensions}d, ready={provider.IsReady}, queued={QueuedObservationCount}, atlasVectors={AtlasVectorCount}, atlasIndexing={IsAtlasIndexBuilding})";
        }
    }

    public SemanticLanguageAtlasCatalog Atlas => _atlas;

    internal bool HasReadyAtlasIndex => HasAtlasVectors();

    private bool IsAtlasIndexBuilding => Volatile.Read(ref _atlasIndexBuilding) != 0;

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
        StartAtlasIndexBuild(provider);
        _api.Logger.Notification($"[thebasics] Registered semantic embedding provider '{provider.ProviderId}' ({provider.Dimensions} dimensions; semantic atlas index building in background). ");
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
        var matches = _matcher.MatchMessageSpansForComprehension(provider, tokens, bucketConfidence.Keys);
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
        _learningApplier.TryPromoteWholeLanguage(player, language, memory, notify: false);
        bucket = resolvedBucket;
        return true;
    }

    public void Dispose()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }

    private void StartAtlasIndexBuild(ITheBasicsSemanticEmbeddingProvider provider)
    {
        var buildVersion = Interlocked.Increment(ref _atlasIndexBuildVersion);
        SetAtlasVectors(Array.Empty<SemanticAtlasBucketVector>());
        if (!_config.Enabled || !_atlas.HasBuckets || !provider.IsReady)
        {
            Volatile.Write(ref _atlasIndexBuilding, 0);
            return;
        }

        Volatile.Write(ref _atlasIndexBuilding, 1);
        Task.Run(() => RebuildAtlasIndexAsync(provider, buildVersion, _disposeCts.Token));
    }

    private async Task RebuildAtlasIndexAsync(ITheBasicsSemanticEmbeddingProvider provider, int buildVersion, CancellationToken cancellationToken)
    {
        var vectors = new List<SemanticAtlasBucketVector>();
        try
        {
            await EmbedAtlasBucketsAsync(provider, vectors, cancellationToken).ConfigureAwait(false);
            if (IsCurrentAtlasBuild(provider, buildVersion))
            {
                SetAtlasVectors(vectors);
                _api.Logger.Notification($"[thebasics] Semantic atlas index ready with {vectors.Count} vectors for provider '{provider.ProviderId}'.");
                StartWorkerIfNeeded();
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _ = ex;
        }
        catch (Exception ex)
        {
            _api.Logger.Warning($"[thebasics] Failed to build semantic atlas index for provider '{provider.ProviderId}': {ex.Message}");
        }
        finally
        {
            if (Volatile.Read(ref _atlasIndexBuildVersion) == buildVersion)
            {
                Volatile.Write(ref _atlasIndexBuilding, 0);
            }
        }
    }

    private async Task EmbedAtlasBucketsAsync(ITheBasicsSemanticEmbeddingProvider provider, List<SemanticAtlasBucketVector> vectors, CancellationToken cancellationToken)
    {
        foreach (var bucket in _atlas.Buckets)
        {
            foreach (var phrase in GetAtlasBucketEmbeddingPhrases(bucket))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var vector = await TryEmbedAtlasPhraseAsync(provider, phrase, cancellationToken).ConfigureAwait(false);
                if (vector != null)
                {
                    vectors.Add(new SemanticAtlasBucketVector(bucket.Id, phrase, vector));
                }
            }
        }
    }

    private bool IsCurrentAtlasBuild(ITheBasicsSemanticEmbeddingProvider provider, int buildVersion)
    {
        return ReferenceEquals(_provider, provider) && Volatile.Read(ref _atlasIndexBuildVersion) == buildVersion && !_disposeCts.IsCancellationRequested;
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

    private async ValueTask<float[]?> TryEmbedAtlasPhraseAsync(ITheBasicsSemanticEmbeddingProvider provider, string phrase, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            return null;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(AtlasPhraseEmbeddingTimeoutMs);
            return SemanticVectorMath.Normalize(await provider.EmbedAsync(phrase, timeout.Token).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
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

                var embeddings = await _matcher.EmbedObservationAsync(provider, observation.Message, _disposeCts.Token).ConfigureAwait(false);
                if (embeddings.Count > 0)
                {
                    OnMain(() => _learningApplier.ApplyObservation(observation, embeddings));
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

    private static bool IsProviderReady(ITheBasicsSemanticEmbeddingProvider? provider)
    {
        return provider != null && provider.IsReady;
    }

    private static bool HasComprehensionInputs(IServerPlayer player, Language language, string message)
    {
        return player != null && language != null && !string.IsNullOrWhiteSpace(message);
    }

}
