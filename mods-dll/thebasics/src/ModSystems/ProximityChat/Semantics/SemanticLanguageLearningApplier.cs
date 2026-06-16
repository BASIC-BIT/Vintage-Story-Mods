#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Semantics;

internal sealed class SemanticLanguageLearningApplier
{
    private readonly SemanticLanguageLearningConfig _config;
    private readonly SemanticLanguageAtlasCatalog _atlas;
    private readonly SemanticLanguageMatcher _matcher;
    private readonly System.Func<string, Language?> _languageResolver;
    private readonly System.Func<string, IServerPlayer?> _playerResolver;

    public SemanticLanguageLearningApplier(
        SemanticLanguageLearningConfig config,
        SemanticLanguageAtlasCatalog atlas,
        SemanticLanguageMatcher matcher,
        System.Func<string, Language?> languageResolver,
        System.Func<string, IServerPlayer?> playerResolver)
    {
        _config = config;
        _atlas = atlas;
        _matcher = matcher;
        _languageResolver = languageResolver;
        _playerResolver = playerResolver;
    }

    public void ApplyObservation(SemanticLanguageObservation observation, List<SemanticSpanEmbedding> embeddings)
    {
        var player = _playerResolver(observation.PlayerUid);
        var language = _languageResolver(observation.LanguageName);
        if (player == null || language == null || player.KnowsLanguage(language))
        {
            return;
        }

        var matches = SemanticLanguageMatcher.ResolveOverlaps(embeddings
            .Select(embedding => _matcher.TryMatchAtlasBucket(embedding, _config.MinimumLearningSimilarity))
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

    public void TryPromoteWholeLanguage(IServerPlayer player, Language language, SemanticLanguageMemory memory, bool notify)
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
