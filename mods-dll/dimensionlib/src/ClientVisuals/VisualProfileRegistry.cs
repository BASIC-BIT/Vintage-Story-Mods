using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DimensionLib.ClientVisuals;

internal static class VisualProfileRegistry
{
    public static readonly Vec3f DefaultNetherSkyColor = new Vec3f(0.035f, 0.0035f, 0.002f);
    public static readonly Vec3f DefaultNetherLightLiftColor = new Vec3f(0.85f, 0.42f, 0.24f);
    public static readonly Vec3f DefaultPocketVoidSkyColor = new Vec3f(0.012f, 0.013f, 0.015f);
    public static readonly Vec3f DefaultPocketVoidLightLiftColor = new Vec3f(0.60f, 0.62f, 0.66f);

    public static AmbientModifier CreateModifier(string profileId, VisualTuningState tuning)
    {
        switch (profileId)
        {
            case DimensionVisualProfileIds.OppositeDay:
                return new AmbientModifier
                {
                    FogColor = WeightedFloatArray.New(new[] { 0.12f, 0.14f, 0.28f }, 0.65f),
                    AmbientColor = WeightedFloatArray.New(new[] { 0.2f, 0.22f, 0.42f }, 0.55f),
                    FogDensity = WeightedFloat.New(0.026f, 0.45f),
                    FlatFogDensity = WeightedFloat.New(0.025f, 0.45f),
                    CloudDensity = WeightedFloat.New(0.9f, 0.5f),
                    CloudBrightness = WeightedFloat.New(0.35f, 0.35f),
                    LerpSpeed = WeightedFloat.New(0.08f, 1f),
                };

            case DimensionVisualProfileIds.NetherCavern:
                return new AmbientModifier
                {
                    FogColor = WeightedFloatArray.New(new[] { tuning.Get("fogred", 0.24f), tuning.Get("foggreen", 0.045f), tuning.Get("fogblue", 0.018f) }, tuning.Get("fogweight", 0.16f)),
                    AmbientColor = WeightedFloatArray.New(new[] { tuning.Get("ambientred", 0.74f), tuning.Get("ambientgreen", 0.34f), tuning.Get("ambientblue", 0.2f) }, tuning.Get("ambientweight", 0.48f)),
                    FogDensity = WeightedFloat.New(tuning.Get("fogdensity", 0.0016f), tuning.Get("fogdensityweight", 0.16f)),
                    FlatFogDensity = WeightedFloat.New(tuning.Get("flatfogdensity", 0f), tuning.Get("flatfogdensityweight", 0f)),
                    CloudDensity = WeightedFloat.New(0.0f, 0.7f),
                    CloudBrightness = WeightedFloat.New(0.0f, 0.7f),
                    SceneBrightness = WeightedFloat.New(tuning.Get("scenebrightness", 1.0f), tuning.Get("scenebrightnessweight", 0.45f)),
                    FogBrightness = WeightedFloat.New(tuning.Get("fogbrightness", 0.95f), tuning.Get("fogbrightnessweight", 0.2f)),
                    LerpSpeed = WeightedFloat.New(0.08f, 1f),
                };

            case DimensionVisualProfileIds.PocketVoid:
                return new AmbientModifier
                {
                    FogColor = WeightedFloatArray.New(new[] { 0.018f, 0.02f, 0.024f }, 0.45f),
                    AmbientColor = WeightedFloatArray.New(new[] { 0.58f, 0.60f, 0.64f }, 0.70f),
                    FogDensity = WeightedFloat.New(0.0f, 1.0f),
                    FlatFogDensity = WeightedFloat.New(0.0f, 1.0f),
                    CloudDensity = WeightedFloat.New(0.0f, 0.8f),
                    CloudBrightness = WeightedFloat.New(0.0f, 0.8f),
                    SceneBrightness = WeightedFloat.New(1.05f, 0.45f),
                    FogBrightness = WeightedFloat.New(0.65f, 0.35f),
                    LerpSpeed = WeightedFloat.New(0.08f, 1f),
                };

            default:
                return new AmbientModifier
                {
                    FogColor = WeightedFloatArray.New(new[] { 0.32f, 0.38f, 0.58f }, 0.55f),
                    AmbientColor = WeightedFloatArray.New(new[] { 0.42f, 0.45f, 0.62f }, 0.35f),
                    FogDensity = WeightedFloat.New(0.018f, 0.35f),
                    FlatFogDensity = WeightedFloat.New(0.02f, 0.35f),
                    CloudDensity = WeightedFloat.New(0.75f, 0.45f),
                    CloudBrightness = WeightedFloat.New(0.6f, 0.3f),
                    LerpSpeed = WeightedFloat.New(0.08f, 1f),
                };
        }
    }

    public static Vec3f GetDefaultSkyColor(string profileId)
    {
        return profileId == DimensionVisualProfileIds.PocketVoid ? DefaultPocketVoidSkyColor : DefaultNetherSkyColor;
    }

    public static Vec3f GetDefaultLightLiftColor(string profileId)
    {
        return profileId == DimensionVisualProfileIds.PocketVoid ? DefaultPocketVoidLightLiftColor : DefaultNetherLightLiftColor;
    }
}
