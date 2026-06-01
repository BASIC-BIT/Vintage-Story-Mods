using System;
using DimensionLib.Api;

namespace DimensionLib.Lighting;

internal sealed class DimensionLightPolicy
{
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

        var settings = dimension.VisualSettings;
        if (settings != null)
        {
            return new DimensionLightPolicy(
                ClampFloat(settings.MinimumSceneLight, 0f, 0.8f),
                ClampInt(settings.AmbientBlockLightFloor, 0, 31),
                ClampInt(settings.AmbientSunlightFloor, 0, 31),
                settings.AmbientLightMinYOffset,
                settings.AmbientLightMaxYOffset < settings.AmbientLightMinYOffset ? settings.AmbientLightMinYOffset : settings.AmbientLightMaxYOffset);
        }

        return None;
    }

    public static DimensionLightPolicy None { get; } = new DimensionLightPolicy(0f, 0, 0, 0, int.MaxValue);

    private static float ClampFloat(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }

    private static int ClampInt(int value, int min, int max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
