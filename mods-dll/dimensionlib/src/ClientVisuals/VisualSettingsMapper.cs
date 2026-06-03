using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DimensionLib.ClientVisuals;

internal static class VisualSettingsMapper
{
    public static AmbientModifier CreateModifier(DimensionVisualSettings settings)
    {
        settings ??= new DimensionVisualSettings();
        var fog = settings.Fog;
        var ambient = settings.Ambient;
        var clouds = settings.Clouds;
        var scene = settings.Scene;

        return new AmbientModifier
        {
            FogColor = WeightedFloatArray.New(
                new[] { fog.Color.Value.Red, fog.Color.Value.Green, fog.Color.Value.Blue },
                fog.Color.Weight),
            AmbientColor = WeightedFloatArray.New(
                new[] { ambient.Color.Value.Red, ambient.Color.Value.Green, ambient.Color.Value.Blue },
                ambient.Color.Weight),
            FogDensity = WeightedFloat.New(fog.Density.Value, fog.Density.Weight),
            FlatFogDensity = WeightedFloat.New(fog.FlatDensity.Value, fog.FlatDensity.Weight),
            CloudDensity = WeightedFloat.New(clouds.Density.Value, clouds.Density.Weight),
            CloudBrightness = WeightedFloat.New(clouds.Brightness.Value, clouds.Brightness.Weight),
            SceneBrightness = WeightedFloat.New(scene.Brightness.Value, scene.Brightness.Weight),
            FogBrightness = WeightedFloat.New(fog.Brightness.Value, fog.Brightness.Weight),
            LerpSpeed = WeightedFloat.New(settings.LerpSpeed, 1f),
        };
    }

    public static Vec3f GetSkyColor(DimensionVisualSettings settings)
    {
        settings ??= new DimensionVisualSettings();
        return new Vec3f(
            settings.Sky.Color.Red,
            settings.Sky.Color.Green,
            settings.Sky.Color.Blue);
    }

    public static float GetSkyAlpha(DimensionVisualSettings settings)
    {
        settings ??= new DimensionVisualSettings();
        return settings.Sky.Color.Alpha;
    }

    public static Vec3f GetLightLiftColor(DimensionVisualSettings settings)
    {
        settings ??= new DimensionVisualSettings();
        return new Vec3f(
            settings.Scene.LightLift.Red,
            settings.Scene.LightLift.Green,
            settings.Scene.LightLift.Blue);
    }
}
