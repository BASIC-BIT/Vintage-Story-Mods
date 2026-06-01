using ProtoBuf;

namespace DimensionLib.Api;

/// <summary>
/// Explicit client/environment visual settings for a DimensionLib dimension.
/// Values with weight 0 do not affect the corresponding Vintage Story ambient channel.
/// </summary>
[ProtoContract]
public sealed class DimensionVisualSettings
{
    [ProtoMember(1)]
    public bool SuppressVanillaCaveFog { get; set; } = true;

    [ProtoMember(2)]
    public bool RenderSkyCover { get; set; }

    [ProtoMember(3)]
    public float SkyRed { get; set; }

    [ProtoMember(4)]
    public float SkyGreen { get; set; }

    [ProtoMember(5)]
    public float SkyBlue { get; set; }

    [ProtoMember(6)]
    public float SkyAlpha { get; set; } = 1f;

    [ProtoMember(7)]
    public float FogRed { get; set; }

    [ProtoMember(8)]
    public float FogGreen { get; set; }

    [ProtoMember(9)]
    public float FogBlue { get; set; }

    [ProtoMember(10)]
    public float FogColorWeight { get; set; }

    [ProtoMember(11)]
    public float AmbientRed { get; set; }

    [ProtoMember(12)]
    public float AmbientGreen { get; set; }

    [ProtoMember(13)]
    public float AmbientBlue { get; set; }

    [ProtoMember(14)]
    public float AmbientColorWeight { get; set; }

    [ProtoMember(15)]
    public float FogDensity { get; set; }

    [ProtoMember(16)]
    public float FogDensityWeight { get; set; }

    [ProtoMember(17)]
    public float FlatFogDensity { get; set; }

    [ProtoMember(18)]
    public float FlatFogDensityWeight { get; set; }

    [ProtoMember(19)]
    public float CloudDensity { get; set; }

    [ProtoMember(20)]
    public float CloudDensityWeight { get; set; }

    [ProtoMember(21)]
    public float CloudBrightness { get; set; }

    [ProtoMember(22)]
    public float CloudBrightnessWeight { get; set; }

    [ProtoMember(23)]
    public float SceneBrightness { get; set; }

    [ProtoMember(24)]
    public float SceneBrightnessWeight { get; set; }

    [ProtoMember(25)]
    public float FogBrightness { get; set; }

    [ProtoMember(26)]
    public float FogBrightnessWeight { get; set; }

    [ProtoMember(27)]
    public float MinimumSceneLight { get; set; }

    [ProtoMember(28)]
    public float LightLiftRed { get; set; } = 1f;

    [ProtoMember(29)]
    public float LightLiftGreen { get; set; } = 1f;

    [ProtoMember(30)]
    public float LightLiftBlue { get; set; } = 1f;

    [ProtoMember(31)]
    public int AmbientBlockLightFloor { get; set; }

    [ProtoMember(32)]
    public int AmbientSunlightFloor { get; set; }

    [ProtoMember(33)]
    public int AmbientLightMinYOffset { get; set; }

    [ProtoMember(34)]
    public int AmbientLightMaxYOffset { get; set; } = int.MaxValue;

    [ProtoMember(35)]
    public float LerpSpeed { get; set; } = 0.08f;

    public DimensionVisualSettings Clone()
    {
        return (DimensionVisualSettings)MemberwiseClone();
    }
}
