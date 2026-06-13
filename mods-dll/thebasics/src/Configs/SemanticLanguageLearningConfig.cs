using System;
using ProtoBuf;

namespace thebasics.Configs;

[ProtoContract]
public class SemanticLanguageLearningConfig
{
    [ProtoMember(1)]
    public bool Enabled { get; set; } = true;

    [ProtoMember(2)]
    public int LearnedThresholdPercent { get; set; } = 85;

    [ProtoMember(3)]
    public int LearningRatePercent { get; set; } = 3;

    [ProtoMember(4)]
    public float MinimumLearningSimilarity { get; set; } = 0.72f;

    [ProtoMember(5)]
    public float MinimumComprehensionSimilarity { get; set; } = 0.68f;

    [ProtoMember(6)]
    public int MaxSpanWords { get; set; } = 5;

    [ProtoMember(7)]
    public int MaxSpansPerMessage { get; set; } = 24;

    [ProtoMember(8)]
    public int MaxBucketProgressPerMessage { get; set; } = 6;

    [ProtoMember(9)]
    public int MaxRealtimeEmbeddingsPerMessage { get; set; } = 4;

    [ProtoMember(10)]
    public int MaxAtlasExamplesPerBucket { get; set; } = 6;

    [ProtoMember(11)]
    public bool NotifyLearnedConcepts { get; set; } = true;

    [ProtoMember(12)]
    public int MaxChunkWords { get; set; } = 32;

    [ProtoMember(13)]
    public int ChunkOverlapWords { get; set; } = 6;

    [ProtoMember(14)]
    public int MaxRealtimeChunkEmbeddingsPerMessage { get; set; } = 16;

    [ProtoMember(15)]
    public int MaxFineChunksPerMessage { get; set; } = 6;

    [ProtoMember(16)]
    public int MaxRealtimeSpanEmbeddingsPerChunk { get; set; } = 4;

    [ProtoMember(17)]
    public float MinimumChunkRoutingSimilarity { get; set; } = 0.35f;

    [ProtoMember(18)]
    public bool EnableWholeLanguagePromotion { get; set; } = true;

    [ProtoMember(19)]
    public int WholeLanguageLearnedBucketPercent { get; set; } = 70;

    [ProtoMember(20)]
    public int WholeLanguageMinimumLearnedBuckets { get; set; } = 12;

    [ProtoMember(21)]
    public bool NotifyWholeLanguageLearned { get; set; } = true;

    public void Normalize()
    {
        NormalizeLearningThresholds();
        NormalizeSpanBudgets();
        NormalizeChunkRouting();
        NormalizeWholeLanguagePromotion();
    }

    private void NormalizeLearningThresholds()
    {
        LearnedThresholdPercent = ClampPercent(LearnedThresholdPercent <= 0 ? 85 : LearnedThresholdPercent);
        LearningRatePercent = ClampPercent(LearningRatePercent <= 0 ? 3 : LearningRatePercent);
        MinimumLearningSimilarity = ClampSimilarity(MinimumLearningSimilarity <= 0 ? 0.72f : MinimumLearningSimilarity);
        MinimumComprehensionSimilarity = ClampSimilarity(MinimumComprehensionSimilarity <= 0 ? 0.68f : MinimumComprehensionSimilarity);
        MinimumChunkRoutingSimilarity = ClampSimilarity(MinimumChunkRoutingSimilarity <= 0 ? 0.35f : MinimumChunkRoutingSimilarity);
    }

    private void NormalizeSpanBudgets()
    {
        MaxSpanWords = MaxSpanWords <= 0 ? 5 : MaxSpanWords;
        MaxSpansPerMessage = MaxSpansPerMessage <= 0 ? 24 : MaxSpansPerMessage;
        MaxBucketProgressPerMessage = MaxBucketProgressPerMessage <= 0 ? 6 : ClampPercent(MaxBucketProgressPerMessage);
        MaxRealtimeEmbeddingsPerMessage = MaxRealtimeEmbeddingsPerMessage < 0 ? 0 : MaxRealtimeEmbeddingsPerMessage;
        MaxAtlasExamplesPerBucket = MaxAtlasExamplesPerBucket <= 0 ? 6 : MaxAtlasExamplesPerBucket;
    }

    private void NormalizeChunkRouting()
    {
        MaxChunkWords = MaxChunkWords <= 0 ? 32 : Math.Max(MaxSpanWords, MaxChunkWords);
        ChunkOverlapWords = ChunkOverlapWords <= 0 ? 6 : ChunkOverlapWords;
        ChunkOverlapWords = Math.Max(0, Math.Min(ChunkOverlapWords, Math.Max(0, MaxChunkWords - 1)));
        MaxRealtimeChunkEmbeddingsPerMessage = MaxRealtimeChunkEmbeddingsPerMessage <= 0 ? 16 : MaxRealtimeChunkEmbeddingsPerMessage;
        MaxFineChunksPerMessage = MaxFineChunksPerMessage <= 0 ? 6 : MaxFineChunksPerMessage;
        MaxRealtimeSpanEmbeddingsPerChunk = MaxRealtimeSpanEmbeddingsPerChunk < 0 ? 0 : MaxRealtimeSpanEmbeddingsPerChunk;
    }

    private void NormalizeWholeLanguagePromotion()
    {
        WholeLanguageLearnedBucketPercent = ClampPercent(WholeLanguageLearnedBucketPercent <= 0 ? 70 : WholeLanguageLearnedBucketPercent);
        WholeLanguageMinimumLearnedBuckets = WholeLanguageMinimumLearnedBuckets <= 0 ? 12 : WholeLanguageMinimumLearnedBuckets;
    }

    private static int ClampPercent(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 100 ? 100 : value;
    }

    private static float ClampSimilarity(float value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 1 ? 1 : value;
    }
}
