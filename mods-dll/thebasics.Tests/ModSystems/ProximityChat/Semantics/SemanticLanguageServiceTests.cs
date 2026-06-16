using System.Collections.Concurrent;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat.Semantics;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat.Semantics;

public class SemanticLanguageServiceTests
{
    [Fact]
    public void AtlasCatalog_NormalizesIdentifiersAndResolvesLabelsAliasesAndIds()
    {
        var catalog = SemanticLanguageAtlasCatalog.FromDocument(new SemanticLanguageAtlasDocument
        {
            AtlasId = " test-atlas ",
            Buckets = new List<SemanticLanguageAtlasBucket>
            {
                new SemanticLanguageAtlasBucket
                {
                    Id = " Temporal Gear ",
                    Label = "Temporal Gear",
                    Alias = "Gear of Time",
                    Family = "temporal"
                },
                new SemanticLanguageAtlasBucket
                {
                    Id = "temporal-gear",
                    Label = "Duplicate",
                    Alias = "duplicate",
                    Family = "temporal"
                }
            }
        });

        catalog.AtlasId.Should().Be("test-atlas");
        catalog.Buckets.Should().ContainSingle();
        catalog.TryResolveBucket("temporal-gear", out var byId).Should().BeTrue();
        catalog.TryResolveBucket("Gear of Time", out var byAlias).Should().BeTrue();
        catalog.TryResolveBucket("Temporal Gear", out var byLabel).Should().BeTrue();
        byId.Should().BeSameAs(byAlias);
        byAlias.Should().BeSameAs(byLabel);
    }

    [Fact]
    public void AtlasCatalog_FormatBucketEscapesVtmlSpecialCharacters()
    {
        var bucket = new SemanticLanguageAtlasBucket
        {
            Id = "danger",
            Label = "Danger <rift>",
            Alias = "rift&storm"
        };

        var formatted = SemanticLanguageAtlasCatalog.FormatBucket(bucket);

        formatted.Should().Be("Danger &lt;rift&gt; [rift&storm]");
    }

    [Fact]
    public void NormalizeSemanticLanguageMemory_ClampsAndDeduplicatesAtlasBucketCoverage()
    {
        var memory = new SemanticLanguageMemoryStore
        {
            Languages = new List<SemanticLanguageMemory>
            {
                new SemanticLanguageMemory
                {
                    LanguageName = " Tradeband ",
                    AtlasBuckets = new List<SemanticLanguageAtlasBucketCoverage>
                    {
                        new SemanticLanguageAtlasBucketCoverage { BucketId = " Temporal Gear ", Confidence = 140, ExposureCount = -2, LastUpdatedUnixSeconds = -10, LearnedAtUnixSeconds = -5 },
                        new SemanticLanguageAtlasBucketCoverage { BucketId = "temporal-gear", Confidence = 35, ExposureCount = 5, LastUpdatedUnixSeconds = 20 },
                        new SemanticLanguageAtlasBucketCoverage { BucketId = "drifters", Confidence = 0 }
                    }
                }
            }
        };

        var normalized = IServerPlayerExtensions.NormalizeSemanticLanguageMemory(memory);

        normalized.Languages.Should().ContainSingle();
        var buckets = normalized.Languages[0].AtlasBuckets;
        buckets.Should().ContainSingle();
        buckets[0].BucketId.Should().Be("temporal-gear");
        buckets[0].Confidence.Should().Be(100);
        buckets[0].ExposureCount.Should().Be(0);
        buckets[0].LastUpdatedUnixSeconds.Should().Be(0);
        buckets[0].LearnedAtUnixSeconds.Should().Be(0);
    }

    [Fact]
    public void SetAtlasBucketCoverage_UpdatesConceptProgressForResolvedBucketAlias()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();

        var updated = service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 80, out var bucket, out var errorCode);
        var progress = service.BuildProgress(player, language);

        updated.Should().BeTrue(errorCode);
        bucket.Should().NotBeNull();
        bucket!.Id.Should().Be("temporal-gear");
        progress.AtlasBucketCount.Should().Be(2);
        progress.AtlasCoveredBucketCount.Should().Be(1);
        progress.AtlasLearnedBucketCount.Should().Be(0);
        progress.AtlasCoveragePercent.Should().Be(40);
        progress.InProgressAtlasBuckets.Should().ContainSingle(entry => entry.Contains("Temporal Gear (80%)", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SetAtlasBucketCoverage_AtThresholdPromotesBucketToLearned()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();

        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 85, out _, out _).Should().BeTrue();

        var progress = service.BuildProgress(player, language);
        var memory = player.GetSemanticLanguageMemory().Languages.Single().AtlasBuckets.Single();
        memory.Confidence.Should().Be(100);
        memory.LearnedAtUnixSeconds.Should().BeGreaterThan(0);
        progress.AtlasLearnedBucketCount.Should().Be(1);
        progress.LearnedAtlasBuckets.Should().ContainSingle("Temporal Gear");
    }

    [Fact]
    public void SetAtlasBucketCoverage_ZeroClearsBucketCoverage()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();

        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 80, out _, out _).Should().BeTrue();
        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 0, out _, out _).Should().BeTrue();

        var progress = service.BuildProgress(player, language);
        progress.AtlasBucketCount.Should().Be(2);
        progress.AtlasCoveredBucketCount.Should().Be(0);
        progress.AtlasCoveragePercent.Should().Be(0);
    }

    [Fact]
    public void BuildComprehensionPlan_RequiresProviderEvenWhenBucketCoverageExists()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();

        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 80, out _, out _).Should().BeTrue();

        service.BuildComprehensionPlan(player, language, "temporal gear").Should().BeNull();
    }

    [Fact]
    public void BuildComprehensionPlan_UsesEmbeddingMatchedBucketCoverageForPhrase()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();
        service.RegisterProvider(CreateProvider());
        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 80, out _, out _).Should().BeTrue();

        var plan = service.BuildComprehensionPlan(player, language, "temporal gear");

        plan.Should().NotBeNull();
        plan!.GetPercent(0).Should().Be(80);
        plan.GetPercent(1).Should().Be(80);
    }

    [Fact]
    public void BuildComprehensionPlan_PreservesOriginalWordIndexesWhenIgnoringShortFillerWords()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();
        service.RegisterProvider(CreateProvider());
        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 85, out _, out _).Should().BeTrue();

        var plan = service.BuildComprehensionPlan(player, language, "I have a temporal gear");

        plan.Should().NotBeNull();
        plan!.GetPercent(0).Should().Be(0);
        plan.GetPercent(1).Should().Be(0);
        plan.GetPercent(2).Should().Be(0);
        plan.GetPercent(3).Should().Be(100);
        plan.GetPercent(4).Should().Be(100);
    }

    [Fact]
    public void BuildComprehensionPlan_RoutesLongRunOnMessageToLateLearnedConcept()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService(new SemanticLanguageLearningConfig
        {
            MaxChunkWords = 6,
            ChunkOverlapWords = 1,
            MaxRealtimeChunkEmbeddingsPerMessage = 1,
            MaxFineChunksPerMessage = 1,
            MaxRealtimeSpanEmbeddingsPerChunk = 1,
            MaxRealtimeEmbeddingsPerMessage = 1
        });
        service.RegisterProvider(CreateProvider());
        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 85, out _, out _).Should().BeTrue();

        var message = "alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo temporal gear";
        var plan = service.BuildComprehensionPlan(player, language, message);

        plan.Should().NotBeNull();
        plan!.GetPercent(10).Should().Be(0);
        plan.GetPercent(11).Should().Be(100);
        plan.GetPercent(12).Should().Be(100);
    }

    [Fact]
    public void BuildComprehensionPlan_UsesLearnedBucketForEmbeddingMatchedParaphrase()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();
        service.RegisterProvider(CreateProvider());
        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 85, out _, out _).Should().BeTrue();

        var plan = service.BuildComprehensionPlan(player, language, "weird gear from ruins");

        plan.Should().NotBeNull();
        plan!.GetPercent(0).Should().Be(100);
        plan.GetPercent(1).Should().Be(100);
        plan.GetPercent(2).Should().Be(100);
        plan.GetPercent(3).Should().Be(100);
    }

    [Fact]
    public void BuildComprehensionPlan_DoesNotUseUnmatchedBucketCoverage()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();
        service.RegisterProvider(CreateProvider());
        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 80, out _, out _).Should().BeTrue();

        var plan = service.BuildComprehensionPlan(player, language, "temporal storm");

        plan.Should().BeNull();
    }

    [Fact]
    public void ObserveMessageForRecipient_RoutesLongRunOnMessageToLateLearnedConcept()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var api = CreateApi(player);
        var service = CreateService(new SemanticLanguageLearningConfig
        {
            LearningRatePercent = 100,
            MaxBucketProgressPerMessage = 100,
            MaxChunkWords = 6,
            ChunkOverlapWords = 1,
            MaxRealtimeChunkEmbeddingsPerMessage = 1,
            MaxFineChunksPerMessage = 1,
            MaxRealtimeSpanEmbeddingsPerChunk = 1,
            MaxRealtimeEmbeddingsPerMessage = 1
        }, api, player);
        service.RegisterProvider(CreateProvider());

        service.ObserveMessageForRecipient(new MessageContext
        {
            ReceivingPlayer = player,
            Message = "alpha bravo charlie delta echo foxtrot golf hotel india juliet kilo temporal gear",
            Metadata = new Dictionary<string, object> { [MessageContext.LANGUAGE] = language },
            Flags = new Dictionary<string, bool> { [MessageContext.IS_SPEECH] = true }
        });

        SpinWait.SpinUntil(() =>
        {
            var memory = player.GetSemanticLanguageMemory().Languages.SingleOrDefault();
            return memory?.AtlasBuckets.Any(bucket => bucket.BucketId == "temporal-gear" && bucket.Confidence == 100) == true;
        }, TimeSpan.FromSeconds(2)).Should().BeTrue();
    }

    [Fact]
    public void ObserveMessageForRecipient_DoesNotLearnFromSpeakerReceivingOwnMessage()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService(new SemanticLanguageLearningConfig
        {
            LearningRatePercent = 100,
            MaxBucketProgressPerMessage = 100
        }, CreateApi(player), player);
        service.RegisterProvider(CreateProvider());

        service.ObserveMessageForRecipient(new MessageContext
        {
            SendingPlayer = player,
            ReceivingPlayer = player,
            Message = "temporal gear",
            Metadata = new Dictionary<string, object> { [MessageContext.LANGUAGE] = language },
            Flags = new Dictionary<string, bool> { [MessageContext.IS_SPEECH] = true }
        });

        player.GetSemanticLanguageMemory().Languages.Should().BeEmpty();
    }

    [Fact]
    public void ObserveMessageForRecipient_RespectsLearningObservationCooldown()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        player.SetSemanticLanguageMemory(new SemanticLanguageMemoryStore
        {
            Languages = new List<SemanticLanguageMemory>
            {
                new SemanticLanguageMemory
                {
                    LanguageName = language.Name,
                    LastLearningObservationUnixSeconds = now,
                    AtlasBuckets = new List<SemanticLanguageAtlasBucketCoverage>
                    {
                        new SemanticLanguageAtlasBucketCoverage { BucketId = "temporal-gear", Confidence = 10, LastUpdatedUnixSeconds = now }
                    }
                }
            }
        });
        var service = CreateService(new SemanticLanguageLearningConfig
        {
            MinimumSecondsBetweenLearningObservations = 60,
            MinimumSecondsBetweenBucketLearning = 0,
            LearningRatePercent = 100,
            MaxBucketProgressPerMessage = 100
        }, CreateApi(player), player);
        service.RegisterProvider(CreateProvider());

        service.ObserveMessageForRecipient(new MessageContext
        {
            ReceivingPlayer = player,
            Message = "drifters",
            Metadata = new Dictionary<string, object> { [MessageContext.LANGUAGE] = language },
            Flags = new Dictionary<string, bool> { [MessageContext.IS_SPEECH] = true }
        });

        SpinWait.SpinUntil(() => service.QueuedObservationCount == 0, TimeSpan.FromSeconds(2)).Should().BeTrue();
        var memory = player.GetSemanticLanguageMemory().Languages.Single();
        memory.AtlasBuckets.Should().NotContain(bucket => bucket.BucketId == "drifters");
    }

    [Fact]
    public void ObserveMessageForRecipient_RespectsBucketLearningCooldown()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        player.SetSemanticLanguageMemory(new SemanticLanguageMemoryStore
        {
            Languages = new List<SemanticLanguageMemory>
            {
                new SemanticLanguageMemory
                {
                    LanguageName = language.Name,
                    AtlasBuckets = new List<SemanticLanguageAtlasBucketCoverage>
                    {
                        new SemanticLanguageAtlasBucketCoverage { BucketId = "temporal-gear", Confidence = 10, LastUpdatedUnixSeconds = now }
                    }
                }
            }
        });
        var service = CreateService(new SemanticLanguageLearningConfig
        {
            MinimumSecondsBetweenLearningObservations = 0,
            MinimumSecondsBetweenBucketLearning = 60,
            LearningRatePercent = 100,
            MaxBucketProgressPerMessage = 100
        }, CreateApi(player), player);
        service.RegisterProvider(CreateProvider());

        service.ObserveMessageForRecipient(new MessageContext
        {
            ReceivingPlayer = player,
            Message = "temporal gear",
            Metadata = new Dictionary<string, object> { [MessageContext.LANGUAGE] = language },
            Flags = new Dictionary<string, bool> { [MessageContext.IS_SPEECH] = true }
        });

        SpinWait.SpinUntil(() => service.QueuedObservationCount == 0, TimeSpan.FromSeconds(2)).Should().BeTrue();
        var bucket = player.GetSemanticLanguageMemory().Languages.Single().AtlasBuckets.Single(entry => entry.BucketId == "temporal-gear");
        bucket.Confidence.Should().Be(10);
    }

    [Fact]
    public void SetAtlasBucketCoverage_PromotesWholeLanguageWhenEnoughBucketsAreLearned()
    {
        var language = CreateLanguage();
        var player = CreatePlayer();
        var service = CreateService();

        service.TrySetAtlasBucketCoverage(player, language, "temporal-gear", 85, out _, out _).Should().BeTrue();
        player.KnowsLanguage(language).Should().BeFalse();

        service.TrySetAtlasBucketCoverage(player, language, "drifters", 85, out _, out _).Should().BeTrue();

        player.KnowsLanguage(language).Should().BeTrue();
        player.GetSemanticLanguageMemory().Languages.Should().BeEmpty();
    }

    private static Language CreateLanguage()
    {
        return new Language(
            "Tradeband",
            "A common language for trade",
            "tr",
            new[] { "feng", "tar" },
            "#D4A96A",
            false,
            false);
    }

    private static SemanticLanguageService CreateService(SemanticLanguageLearningConfig? config = null, ICoreServerAPI? api = null, IServerPlayer? player = null)
    {
        var language = CreateLanguage();
        return new SemanticLanguageService(
            null!,
            api ?? CreateApi(),
            CreateAtlas(),
            config,
            name => string.Equals(name, language.Name, StringComparison.OrdinalIgnoreCase) ? language : null,
            playerUid => player != null && string.Equals(playerUid, player.PlayerUID, StringComparison.OrdinalIgnoreCase) ? player : null);
    }

    private static ICoreServerAPI CreateApi(params IServerPlayer[] players)
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Logger.Returns(Substitute.For<ILogger>());
        var eventApi = Substitute.For<IServerEventAPI>();
        api.Event.Returns(eventApi);
        eventApi.When(call => call.EnqueueMainThreadTask(Arg.Any<Action>(), Arg.Any<string>()))
            .Do(call => call.Arg<Action>()());
        return api;
    }

    private static SemanticLanguageAtlasCatalog CreateAtlas()
    {
        return SemanticLanguageAtlasCatalog.FromDocument(new SemanticLanguageAtlasDocument
        {
            AtlasId = "test-atlas",
            Buckets = new List<SemanticLanguageAtlasBucket>
            {
                new SemanticLanguageAtlasBucket
                {
                    Id = "temporal-gear",
                    Label = "Temporal Gear",
                    Alias = "temporal-gear",
                    Family = "temporal-lore",
                    Examples = new List<string> { "large temporal gear", "strange gear from the ruins" }
                },
                new SemanticLanguageAtlasBucket
                {
                    Id = "drifters",
                    Label = "Drifters",
                    Alias = "drifters",
                    Family = "hostile-creatures"
                }
            }
        });
    }

    private static FakeEmbeddingProvider CreateProvider()
    {
        return new FakeEmbeddingProvider(new Dictionary<string, float[]>
        {
            ["temporal gear"] = new[] { 1f, 0f },
            ["large temporal gear"] = new[] { 1f, 0f },
            ["strange gear from the ruins"] = new[] { 1f, 0f },
            ["weird gear from ruins"] = new[] { 1f, 0f },
            ["kilo temporal gear"] = new[] { 1f, 0f },
            ["drifters"] = new[] { 0f, 1f },
            ["temporal storm"] = new[] { 0f, 1f }
        });
    }

    private static IServerPlayer CreatePlayer()
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns("player-1");
        player.PlayerName.Returns("Alice");
        var modData = new Dictionary<string, byte[]>();
        player.GetModdata(Arg.Any<string>()).Returns(call => modData.TryGetValue(call.Arg<string>(), out var value) ? value : null);
        player.When(call => call.SetModdata(Arg.Any<string>(), Arg.Any<byte[]>()))
            .Do(call => modData[call.ArgAt<string>(0)] = call.ArgAt<byte[]>(1));
        player.When(call => call.RemoveModdata(Arg.Any<string>()))
            .Do(call => modData.Remove(call.ArgAt<string>(0)));
        return player;
    }

    private sealed class FakeEmbeddingProvider : ITheBasicsSemanticEmbeddingProvider
    {
        private readonly Dictionary<string, float[]> _vectors;
        private readonly ConcurrentDictionary<string, float[]> _cache = new ConcurrentDictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);

        public FakeEmbeddingProvider(Dictionary<string, float[]> vectors)
        {
            _vectors = vectors;
        }

        public string ProviderId => "fake";

        public int Dimensions => 2;

        public bool IsReady => true;

        public ValueTask<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
        {
            var key = Normalize(text);
            if (!_vectors.TryGetValue(key, out var vector))
            {
                return ValueTask.FromResult<float[]?>(null);
            }

            _cache[key] = vector;
            return ValueTask.FromResult<float[]?>(vector);
        }

        public bool TryGetCachedEmbedding(string text, out float[] embedding)
        {
            return _cache.TryGetValue(Normalize(text), out embedding!);
        }

        private static string Normalize(string text)
        {
            return string.Join(" ", (text ?? string.Empty).Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
