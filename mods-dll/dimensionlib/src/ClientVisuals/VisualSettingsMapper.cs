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

        return new AmbientModifier
        {
            FogColor = WeightedFloatArray.New(new[] { tuning.Get("fogred", settings.FogRed), tuning.Get("foggreen", settings.FogGreen), tuning.Get("fogblue", settings.FogBlue) }, tuning.Get("fogweight", settings.FogColorWeight)),
            AmbientColor = WeightedFloatArray.New(new[] { tuning.Get("ambientred", settings.AmbientRed), tuning.Get("ambientgreen", settings.AmbientGreen), tuning.Get("ambientblue", settings.AmbientBlue) }, tuning.Get("ambientweight", settings.AmbientColorWeight)),
            FogDensity = WeightedFloat.New(tuning.Get("fogdensity", settings.FogDensity), tuning.Get("fogdensityweight", settings.FogDensityWeight)),
            FlatFogDensity = WeightedFloat.New(tuning.Get("flatfogdensity", settings.FlatFogDensity), tuning.Get("flatfogdensityweight", settings.FlatFogDensityWeight)),
            CloudDensity = WeightedFloat.New(tuning.Get("clouddensity", settings.CloudDensity), tuning.Get("clouddensityweight", settings.CloudDensityWeight)),
            CloudBrightness = WeightedFloat.New(tuning.Get("cloudbrightness", settings.CloudBrightness), tuning.Get("cloudbrightnessweight", settings.CloudBrightnessWeight)),
            SceneBrightness = WeightedFloat.New(tuning.Get("scenebrightness", settings.SceneBrightness), tuning.Get("scenebrightnessweight", settings.SceneBrightnessWeight)),
            FogBrightness = WeightedFloat.New(tuning.Get("fogbrightness", settings.FogBrightness), tuning.Get("fogbrightnessweight", settings.FogBrightnessWeight)),
            LerpSpeed = WeightedFloat.New(tuning.Get("lerpspeed", settings.LerpSpeed), 1f),
        };
    }

    public static Vec3f GetSkyColor(DimensionVisualSettings settings, VisualTuningState tuning)
    {
        settings ??= new DimensionVisualSettings();
        tuning ??= new VisualTuningState();
        return new Vec3f(
            tuning.Get("skyred", settings.SkyRed),
            tuning.Get("skygreen", settings.SkyGreen),
            tuning.Get("skyblue", settings.SkyBlue));
    }

    public static float GetSkyAlpha(DimensionVisualSettings settings, VisualTuningState tuning)
    {
        settings ??= new DimensionVisualSettings();
        tuning ??= new VisualTuningState();
        return tuning.Get("skyalpha", settings.SkyAlpha);
    }

    public static Vec3f GetLightLiftColor(DimensionVisualSettings settings, VisualTuningState tuning)
    {
        settings ??= new DimensionVisualSettings();
        tuning ??= new VisualTuningState();
        return new Vec3f(
            tuning.Get("liftred", settings.LightLiftRed),
            tuning.Get("liftgreen", settings.LightLiftGreen),
            tuning.Get("liftblue", settings.LightLiftBlue));
    }
}
