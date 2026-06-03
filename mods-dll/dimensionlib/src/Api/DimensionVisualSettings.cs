using ProtoBuf;

namespace DimensionLib.Api;

/// <summary>
/// Explicit client/environment visual settings for a DimensionLib dimension.
/// Values with weight 0 do not affect the corresponding Vintage Story ambient channel.
/// </summary>
[ProtoContract]
public sealed class DimensionVisualSettings
{
    private DimensionSkyVisualSettings _sky = new DimensionSkyVisualSettings();
    private DimensionFogVisualSettings _fog = new DimensionFogVisualSettings();
    private DimensionAmbientVisualSettings _ambient = new DimensionAmbientVisualSettings();
    private DimensionCloudVisualSettings _clouds = new DimensionCloudVisualSettings();
    private DimensionSceneVisualSettings _scene = new DimensionSceneVisualSettings();

    [ProtoMember(1)]
    public DimensionSkyVisualSettings Sky
    {
        get => _sky;
        set => _sky = value ?? new DimensionSkyVisualSettings();
    }

    [ProtoMember(2)]
    public DimensionFogVisualSettings Fog
    {
        get => _fog;
        set => _fog = value ?? new DimensionFogVisualSettings();
    }

    [ProtoMember(3)]
    public DimensionAmbientVisualSettings Ambient
    {
        get => _ambient;
        set => _ambient = value ?? new DimensionAmbientVisualSettings();
    }

    [ProtoMember(4)]
    public DimensionCloudVisualSettings Clouds
    {
        get => _clouds;
        set => _clouds = value ?? new DimensionCloudVisualSettings();
    }

    [ProtoMember(5)]
    public DimensionSceneVisualSettings Scene
    {
        get => _scene;
        set => _scene = value ?? new DimensionSceneVisualSettings();
    }

    [ProtoMember(6)]
    public float LerpSpeed { get; set; } = 0.08f;

    public DimensionVisualSettings Clone()
    {
        return new DimensionVisualSettings
        {
            Sky = Sky.Clone(),
            Fog = Fog.Clone(),
            Ambient = Ambient.Clone(),
            Clouds = Clouds.Clone(),
            Scene = Scene.Clone(),
            LerpSpeed = LerpSpeed,
        };
    }
}

[ProtoContract]
public sealed class DimensionSkyVisualSettings
{
    private DimensionColor4 _color = new DimensionColor4(0f, 0f, 0f, 1f);

    [ProtoMember(1)]
    public bool RenderCover { get; set; }

    [ProtoMember(2)]
    public DimensionColor4 Color
    {
        get => _color;
        set => _color = value ?? new DimensionColor4(0f, 0f, 0f, 1f);
    }

    public DimensionSkyVisualSettings Clone()
    {
        return new DimensionSkyVisualSettings
        {
            RenderCover = RenderCover,
            Color = Color.Clone(),
        };
    }
}

[ProtoContract]
public sealed class DimensionFogVisualSettings
{
    private DimensionWeightedColor _color = new DimensionWeightedColor();
    private DimensionWeightedFloat _density = new DimensionWeightedFloat();
    private DimensionWeightedFloat _flatDensity = new DimensionWeightedFloat();
    private DimensionWeightedFloat _brightness = new DimensionWeightedFloat();

    [ProtoMember(1)]
    public bool SuppressVanillaCaveFog { get; set; } = true;

    [ProtoMember(2)]
    public DimensionWeightedColor Color
    {
        get => _color;
        set => _color = value ?? new DimensionWeightedColor();
    }

    [ProtoMember(3)]
    public DimensionWeightedFloat Density
    {
        get => _density;
        set => _density = value ?? new DimensionWeightedFloat();
    }

    [ProtoMember(4)]
    public DimensionWeightedFloat FlatDensity
    {
        get => _flatDensity;
        set => _flatDensity = value ?? new DimensionWeightedFloat();
    }

    [ProtoMember(5)]
    public DimensionWeightedFloat Brightness
    {
        get => _brightness;
        set => _brightness = value ?? new DimensionWeightedFloat();
    }

    public DimensionFogVisualSettings Clone()
    {
        return new DimensionFogVisualSettings
        {
            SuppressVanillaCaveFog = SuppressVanillaCaveFog,
            Color = Color.Clone(),
            Density = Density.Clone(),
            FlatDensity = FlatDensity.Clone(),
            Brightness = Brightness.Clone(),
        };
    }
}

[ProtoContract]
public sealed class DimensionAmbientVisualSettings
{
    private DimensionWeightedColor _color = new DimensionWeightedColor();

    [ProtoMember(1)]
    public DimensionWeightedColor Color
    {
        get => _color;
        set => _color = value ?? new DimensionWeightedColor();
    }

    public DimensionAmbientVisualSettings Clone()
    {
        return new DimensionAmbientVisualSettings
        {
            Color = Color.Clone(),
        };
    }
}

[ProtoContract]
public sealed class DimensionCloudVisualSettings
{
    private DimensionWeightedFloat _density = new DimensionWeightedFloat();
    private DimensionWeightedFloat _brightness = new DimensionWeightedFloat();

    [ProtoMember(1)]
    public DimensionWeightedFloat Density
    {
        get => _density;
        set => _density = value ?? new DimensionWeightedFloat();
    }

    [ProtoMember(2)]
    public DimensionWeightedFloat Brightness
    {
        get => _brightness;
        set => _brightness = value ?? new DimensionWeightedFloat();
    }

    public DimensionCloudVisualSettings Clone()
    {
        return new DimensionCloudVisualSettings
        {
            Density = Density.Clone(),
            Brightness = Brightness.Clone(),
        };
    }
}

[ProtoContract]
public sealed class DimensionSceneVisualSettings
{
    private DimensionWeightedFloat _brightness = new DimensionWeightedFloat();
    private DimensionColor3 _lightLift = new DimensionColor3(1f, 1f, 1f);

    [ProtoMember(1)]
    public DimensionWeightedFloat Brightness
    {
        get => _brightness;
        set => _brightness = value ?? new DimensionWeightedFloat();
    }

    [ProtoMember(2)]
    public float MinimumLight { get; set; }

    [ProtoMember(3)]
    public DimensionColor3 LightLift
    {
        get => _lightLift;
        set => _lightLift = value ?? new DimensionColor3(1f, 1f, 1f);
    }

    public DimensionSceneVisualSettings Clone()
    {
        return new DimensionSceneVisualSettings
        {
            Brightness = Brightness.Clone(),
            MinimumLight = MinimumLight,
            LightLift = LightLift.Clone(),
        };
    }
}

[ProtoContract]
public sealed class DimensionWeightedColor
{
    private DimensionColor3 _value = new DimensionColor3();

    public DimensionWeightedColor()
    {
    }

    public DimensionWeightedColor(DimensionColor3 value, float weight)
    {
        Value = value;
        Weight = weight;
    }

    [ProtoMember(1)]
    public DimensionColor3 Value
    {
        get => _value;
        set => _value = value ?? new DimensionColor3();
    }

    [ProtoMember(2)]
    public float Weight { get; set; }

    public DimensionWeightedColor Clone()
    {
        return new DimensionWeightedColor(Value.Clone(), Weight);
    }
}

[ProtoContract]
public sealed class DimensionWeightedFloat
{
    public DimensionWeightedFloat()
    {
    }

    public DimensionWeightedFloat(float value, float weight)
    {
        Value = value;
        Weight = weight;
    }

    [ProtoMember(1)]
    public float Value { get; set; }

    [ProtoMember(2)]
    public float Weight { get; set; }

    public DimensionWeightedFloat Clone()
    {
        return new DimensionWeightedFloat(Value, Weight);
    }
}

[ProtoContract]
public sealed class DimensionColor3
{
    public DimensionColor3()
    {
    }

    public DimensionColor3(float red, float green, float blue)
    {
        Red = red;
        Green = green;
        Blue = blue;
    }

    [ProtoMember(1)]
    public float Red { get; set; }

    [ProtoMember(2)]
    public float Green { get; set; }

    [ProtoMember(3)]
    public float Blue { get; set; }

    public DimensionColor3 Clone()
    {
        return new DimensionColor3(Red, Green, Blue);
    }
}

[ProtoContract]
public sealed class DimensionColor4
{
    public DimensionColor4()
    {
        Alpha = 1f;
    }

    public DimensionColor4(float red, float green, float blue, float alpha = 1f)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    [ProtoMember(1)]
    public float Red { get; set; }

    [ProtoMember(2)]
    public float Green { get; set; }

    [ProtoMember(3)]
    public float Blue { get; set; }

    [ProtoMember(4)]
    public float Alpha { get; set; }

    public DimensionColor4 Clone()
    {
        return new DimensionColor4(Red, Green, Blue, Alpha);
    }
}
