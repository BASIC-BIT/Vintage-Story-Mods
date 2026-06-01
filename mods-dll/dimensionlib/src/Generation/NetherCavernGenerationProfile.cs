namespace DimensionLib.Generation;

internal sealed class NetherCavernGenerationProfile
{
    public static readonly NetherCavernGenerationProfile Default = new NetherCavernGenerationProfile();

    public int SpawnPlateauRadius { get; } = 5;
    public int SpawnPlateauBlendDistance { get; } = 10;
    public double SpawnPlateauDropPerBlock { get; } = 1.8;
    public int SpawnMarkerRadius { get; } = 4;

    public int FloorBaseY { get; } = 50;
    public double FloorBasinAmplitude { get; } = 20.0;
    public double FloorRoughAmplitude { get; } = 9.0;
    public double FloorShelfAmplitude { get; } = 8.0;
    public int FloorMinY { get; } = 42;
    public int FloorSpawnClearance { get; } = 4;

    public double FloorBasinScale { get; } = 90.0;
    public int FloorBasinSeedOffset { get; } = 101;
    public double FloorBasinPersistence { get; } = 0.52;
    public double FloorRoughScale { get; } = 24.0;
    public int FloorRoughSeedOffset { get; } = 131;
    public double FloorRoughPersistence { get; } = 0.6;
    public double FloorShelfScale { get; } = 46.0;
    public int FloorShelfSeedOffset { get; } = 141;
    public double FloorShelfPersistence { get; } = 0.58;

    public int CeilingBaseY { get; } = 94;
    public double CeilingDomeAmplitude { get; } = 24.0;
    public double CeilingTeethAmplitude { get; } = 14.0;
    public double CeilingSqueezeThreshold { get; } = 0.58;
    public double CeilingSqueezeNormalizer { get; } = 0.42;
    public double CeilingSqueezeAmplitude { get; } = 32.0;
    public int CeilingMinimumOpenHeight { get; } = 18;
    public int CeilingClampMinimumHeight { get; } = 14;
    public int CeilingMapTopClearance { get; } = 96;
    public int CeilingSpawnMaxOffset { get; } = 68;
    public int CeilingCoverDepth { get; } = 64;

    public double CeilingDomeScale { get; } = 110.0;
    public int CeilingDomeSeedOffset { get; } = 151;
    public double CeilingDomePersistence { get; } = 0.55;
    public double CeilingTeethScale { get; } = 34.0;
    public int CeilingTeethSeedOffset { get; } = 181;
    public double CeilingTeethPersistence { get; } = 0.6;
    public double CeilingSqueezeScale { get; } = 72.0;
    public int CeilingSqueezeSeedOffset { get; } = 191;
    public double CeilingSqueezePersistence { get; } = 0.57;

    public int SurfaceRoughDepth { get; } = 2;
    public int CeilingRoughDepth { get; } = 2;
    public int ColumnRoughDepth { get; } = 2;
    public int CeilingSpikeRoughDepth { get; } = 3;

    public int LavaMaxFloorY { get; } = 62;
    public double LavaSpawnClearRadius { get; } = 16.0;
    public double LavaPoolScale { get; } = 42.0;
    public int LavaPoolSeedOffset { get; } = 211;
    public double LavaPoolPersistence { get; } = 0.58;
    public double LavaPoolThreshold { get; } = 0.5;
    public int LavaDepthBase { get; } = 3;
    public double LavaDepthAmplitude { get; } = 3.0;
    public double LavaDepthScale { get; } = 18.0;
    public int LavaDepthSeedOffset { get; } = 233;
    public double LavaDepthPersistence { get; } = 0.5;

    public double FloorSpikeSpawnClearRadius { get; } = 12.0;
    public int SpikeMinimumOpenHeight { get; } = 18;
    public double FloorSpikeScale { get; } = 13.0;
    public int FloorSpikeSeedOffset { get; } = 251;
    public double FloorSpikePersistence { get; } = 0.55;
    public double FloorSpikeThreshold { get; } = 0.77;
    public double FloorSpikeThresholdRange { get; } = 0.23;
    public int FloorSpikeHeadroom { get; } = 8;
    public int FloorSpikeBaseHeight { get; } = 2;
    public double FloorSpikeMaxExtraHeight { get; } = 16.0;

    public double CeilingSpikeScale { get; } = 15.0;
    public int CeilingSpikeSeedOffset { get; } = 271;
    public double CeilingSpikePersistence { get; } = 0.55;
    public double CeilingSpikeThreshold { get; } = 0.68;
    public double CeilingSpikeThresholdRange { get; } = 0.32;
    public int CeilingSpikeHeadroom { get; } = 8;
    public int CeilingSpikeBaseDepth { get; } = 3;
    public double CeilingSpikeMaxExtraDepth { get; } = 20.0;

    public int LavaFallMinimumOpenHeight { get; } = 22;
    public double LavaFallSpawnClearRadius { get; } = 20.0;
    public double LavaFallFissureScale { get; } = 19.0;
    public int LavaFallFissureSeedOffset { get; } = 291;
    public double LavaFallFissurePersistence { get; } = 0.62;
    public double LavaFallFissureThreshold { get; } = 0.92;
    public double LavaFallSourceScale { get; } = 53.0;
    public int LavaFallSourceSeedOffset { get; } = 307;
    public double LavaFallSourcePersistence { get; } = 0.55;
    public double LavaFallSourceThreshold { get; } = 0.68;

    public double ColumnSpawnClearRadius { get; } = 14.0;
    public double ColumnThreshold { get; } = 0.66;
    public double ColumnSolidThreshold { get; } = 0.82;
    public double ColumnBroadScale { get; } = 52.0;
    public int ColumnBroadSeedOffset { get; } = 311;
    public double ColumnBroadPersistence { get; } = 0.57;
    public double ColumnVeinScale { get; } = 29.0;
    public int ColumnVeinSeedOffset { get; } = 331;
    public double ColumnVeinPersistence { get; } = 0.62;
    public double ColumnDetailScale { get; } = 11.0;
    public int ColumnDetailSeedOffset { get; } = 353;
    public double ColumnDetailPersistence { get; } = 0.5;
    public double ColumnBroadWeight { get; } = 0.55;
    public double ColumnVeinWeight { get; } = 0.35;
    public double ColumnDetailWeight { get; } = 0.1;
}
