using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DimensionLib.ClientVisuals;

internal sealed class VisualTuningState
{
    private static readonly HashSet<string> AllowedKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "ambientblue",
        "ambientgreen",
        "ambientred",
        "ambientweight",
        "flatfogdensity",
        "flatfogdensityweight",
        "fogblue",
        "fogbrightness",
        "fogbrightnessweight",
        "fogdensity",
        "fogdensityweight",
        "foggreen",
        "fogred",
        "fogweight",
        "liftblue",
        "liftgreen",
        "liftmax",
        "liftmult",
        "liftred",
        "minlight",
        "scenebrightness",
        "scenebrightnessweight",
        "skyalpha",
        "skyblue",
        "skygreen",
        "skyred",
    };

    private readonly Dictionary<string, float> _values = new Dictionary<string, float>(StringComparer.Ordinal);

    public void Reset()
    {
        _values.Clear();
    }

    public float Get(string key, float fallback)
    {
        return _values.TryGetValue(NormalizeKey(key), out var value) ? value : fallback;
    }

    public bool TrySet(string key, float value)
    {
        key = NormalizeKey(key);
        if (!AllowedKeys.Contains(key))
        {
            return false;
        }

        _values[key] = ClampValue(key, value);
        return true;
    }

    public bool ApplyPreset(string presetId)
    {
        switch (NormalizeKey(presetId))
        {
            case "default":
                Reset();
                return true;
            case "clear":
                Reset();
                TrySet("fogdensity", 0f);
                TrySet("flatfogdensity", 0f);
                TrySet("fogweight", 0f);
                TrySet("fogdensityweight", 0f);
                TrySet("flatfogdensityweight", 0f);
                TrySet("minlight", 0f);
                TrySet("skyalpha", 0.25f);
                return true;
            case "thin":
                Reset();
                TrySet("fogdensity", 0.0004f);
                TrySet("flatfogdensity", 0f);
                TrySet("fogweight", 0.04f);
                TrySet("fogdensityweight", 0.02f);
                TrySet("minlight", 0f);
                TrySet("skyalpha", 0.4f);
                return true;
            default:
                return false;
        }
    }

    public string DescribeOverrides()
    {
        return _values.Count == 0
            ? "none"
            : string.Join(",", _values.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}={pair.Value.ToString("0.####", CultureInfo.InvariantCulture)}"));
    }

    private static string NormalizeKey(string key)
    {
        return (key ?? string.Empty).Trim().ToLowerInvariant().Replace("-", string.Empty).Replace("_", string.Empty);
    }

    private static float ClampValue(string key, float value)
    {
        if (key.EndsWith("red", StringComparison.Ordinal) || key.EndsWith("green", StringComparison.Ordinal) || key.EndsWith("blue", StringComparison.Ordinal) || key.EndsWith("alpha", StringComparison.Ordinal) || key.EndsWith("weight", StringComparison.Ordinal))
        {
            return Clamp(value, 0f, 1f);
        }

        switch (key)
        {
            case "fogdensity":
            case "flatfogdensity":
                return Clamp(value, 0f, 0.05f);
            case "minlight":
            case "liftmax":
                return Clamp(value, 0f, 0.8f);
            case "liftmult":
                return Clamp(value, 0f, 4f);
            case "scenebrightness":
            case "fogbrightness":
                return Clamp(value, 0f, 2f);
            default:
                return value;
        }
    }

    private static float Clamp(float value, float min, float max)
    {
        return value < min ? min : value > max ? max : value;
    }
}
