using System;
using DimensionLib.Api;

namespace DimensionLib.Lighting;

internal sealed class DimensionLightPolicy
{
    private const float NetherCavernMinimumSceneLight = 0.08f;
    private const int NetherCavernAmbientBlockLightFloor = 7;
    private const int NetherCavernAmbientSunlightFloor = 2;
    private const int NetherCavernAmbientLightMinYOffset = -48;
    private const int NetherCavernAmbientLightMaxYOffset = 128;

    private DimensionLightPolicy(float minimumSceneLight, int blocklightFloor, int sunlightFloor, int minYOffset, int maxYOffset)
    {
        MinimumSceneLight = minimumSceneLight;
        BlocklightFloor = blocklightFloor;
        SunlightFloor = sunlightFloor;
        MinYOffset = minYOffset;
        MaxYOffset = maxYOffset;
    }

    public float MinimumSceneLight { get; }

    public int BlocklightFloor { get; }

    public int SunlightFloor { get; }

    public int MinYOffset { get; }

    public int MaxYOffset { get; }

    public static DimensionLightPolicy For(Dimension dimension)
    {
        if (dimension == null)
        {
            return None;
        }

        var basePolicy = string.Equals(dimension.VisualProfileId, DimensionVisualProfileIds.NetherCavern, StringComparison.Ordinal)
            ? NetherCavern
            : None;

        if (dimension.MinimumSceneLight > 0f)
        {
            return new DimensionLightPolicy(
                ClampFloat(dimension.MinimumSceneLight, 0f, 0.8f),
                basePolicy.BlocklightFloor,
                basePolicy.SunlightFloor,
                basePolicy.MinYOffset,
                basePolicy.MaxYOffset);
        }

        return basePolicy;
    }

    public static DimensionLightPolicy None { get; } = new DimensionLightPolicy(0f, 0, 0, 0, int.MaxValue);

    public static DimensionLightPolicy NetherCavern { get; } = new DimensionLightPolicy(
        NetherCavernMinimumSceneLight,
        NetherCavernAmbientBlockLightFloor,
        NetherCavernAmbientSunlightFloor,
        NetherCavernAmbientLightMinYOffset,
        NetherCavernAmbientLightMaxYOffset);

    private static float ClampFloat(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
