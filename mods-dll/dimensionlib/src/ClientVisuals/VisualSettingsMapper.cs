using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DimensionLib.ClientVisuals;

internal static class VisualSettingsMapper
{
    public static AmbientModifier CreateModifier(DimensionVisualSettings settings, VisualTuningState tuning)
    {
        settings ??= new DimensionVisualSettings();
        tuning ??= new VisualTuningState();
        var fog = settings.Fog;
        var ambient = settings.Ambient;
        var clouds = settings.Clouds;
        var scene = settings.Scene;

        return new AmbientModifier
        {
            FogColor = WeightedFloatArray.New(
                new[] { tuning.Get("fogred", fog.Color.Value.Red), tuning.Get("foggreen", fog.Color.Value.Green), tuning.Get("fogblue", fog.Color.Value.Blue) },
                tuning.Get("fogweight", fog.Color.Weight)),
            AmbientColor = WeightedFloatArray.New(
                new[] { tuning.Get("ambientred", ambient.Color.Value.Red), tuning.Get("ambientgreen", ambient.Color.Value.Green), tuning.Get("ambientblue", ambient.Color.Value.Blue) },
                tuning.Get("ambientweight", ambient.Color.Weight)),
            FogDensity = WeightedFloat.New(tuning.Get("fogdensity", fog.Density.Value), tuning.Get("fogdensityweight", fog.Density.Weight)),
            FlatFogDensity = WeightedFloat.New(tuning.Get("flatfogdensity", fog.FlatDensity.Value), tuning.Get("flatfogdensityweight", fog.FlatDensity.Weight)),
            CloudDensity = WeightedFloat.New(tuning.Get("clouddensity", clouds.Density.Value), tuning.Get("clouddensityweight", clouds.Density.Weight)),
            CloudBrightness = WeightedFloat.New(tuning.Get("cloudbrightness", clouds.Brightness.Value), tuning.Get("cloudbrightnessweight", clouds.Brightness.Weight)),
            SceneBrightness = WeightedFloat.New(tuning.Get("scenebrightness", scene.Brightness.Value), tuning.Get("scenebrightnessweight", scene.Brightness.Weight)),
            FogBrightness = WeightedFloat.New(tuning.Get("fogbrightness", fog.Brightness.Value), tuning.Get("fogbrightnessweight", fog.Brightness.Weight)),
            LerpSpeed = WeightedFloat.New(tuning.Get("lerpspeed", settings.LerpSpeed), 1f),
        };
    }

    public static Vec3f GetSkyColor(DimensionVisualSettings settings, VisualTuningState tuning)
    {
        settings ??= new DimensionVisualSettings();
        tuning ??= new VisualTuningState();
        return new Vec3f(
            tuning.Get("skyred", settings.Sky.Color.Red),
            tuning.Get("skygreen", settings.Sky.Color.Green),
            tuning.Get("skyblue", settings.Sky.Color.Blue));
    }

    public static float GetSkyAlpha(DimensionVisualSettings settings, VisualTuningState tuning)
    {
        settings ??= new DimensionVisualSettings();
        tuning ??= new VisualTuningState();
        return tuning.Get("skyalpha", settings.Sky.Color.Alpha);
    }

    public static Vec3f GetLightLiftColor(DimensionVisualSettings settings, VisualTuningState tuning)
    {
        settings ??= new DimensionVisualSettings();
        tuning ??= new VisualTuningState();
        return new Vec3f(
            tuning.Get("liftred", settings.Scene.LightLift.Red),
            tuning.Get("liftgreen", settings.Scene.LightLift.Green),
            tuning.Get("liftblue", settings.Scene.LightLift.Blue));
    }
}
