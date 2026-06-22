using System;
using System.Collections.Generic;
using System.Globalization;

namespace BasicConfig;

public interface IBasicConfigSettingDefinition
{
    string Key { get; }
    string Group { get; }
    string Label { get; }
    string Description { get; }
    BasicConfigSettingKind Kind { get; }
    BasicConfigReloadBehavior ReloadBehavior { get; }
    IReadOnlyList<string> Options { get; }
    IReadOnlyList<string> OptionNames { get; }
}

public sealed class BasicConfigSettingDefinition<TConfig> : IBasicConfigSettingDefinition
{
    private readonly Func<TConfig, string> _getValue;
    private readonly Func<TConfig, string, string> _setValue;

    public BasicConfigSettingDefinition(BasicConfigSettingDefinitionOptions<TConfig> options)
    {
        options = options ?? throw new ArgumentNullException(nameof(options));
        Key = Require(options.Key, nameof(options.Key));
        Group = options.Group ?? string.Empty;
        Label = options.Label ?? Key;
        Description = options.Description ?? string.Empty;
        Kind = options.Kind;
        ReloadBehavior = options.ReloadBehavior;
        _getValue = options.GetValue ?? throw new ArgumentException("GetValue is required.", nameof(options));
        _setValue = options.SetValue ?? throw new ArgumentException("SetValue is required.", nameof(options));
        Options = options.Options ?? Array.Empty<string>();
        OptionNames = options.OptionNames ?? Options;
    }

    public string Key { get; }
    public string Group { get; }
    public string Label { get; }
    public string Description { get; }
    public BasicConfigSettingKind Kind { get; }
    public BasicConfigReloadBehavior ReloadBehavior { get; }
    public IReadOnlyList<string> Options { get; }
    public IReadOnlyList<string> OptionNames { get; }

    public string GetValue(TConfig config)
    {
        return _getValue(config) ?? string.Empty;
    }

    public bool TrySetValue(TConfig config, string value, out string error)
    {
        error = _setValue(config, value ?? string.Empty);
        return error == null;
    }

    public static string FormatBool(bool value)
    {
        return value ? "1" : "0";
    }

    public static bool TryParseBool(string value, out bool parsed)
    {
        if (value == "1")
        {
            parsed = true;
            return true;
        }

        if (value == "0")
        {
            parsed = false;
            return true;
        }

        return bool.TryParse(value, out parsed);
    }

    public static string FormatDecimal(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParseDecimal(string value, out double parsed)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
               && !double.IsNaN(parsed)
               && !double.IsInfinity(parsed);
    }

    private static string Require(string value, string name)
    {
        return string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value;
    }
}

public sealed class BasicConfigSettingDefinitionOptions<TConfig>
{
    public string Key { get; set; }
    public string Group { get; set; }
    public string Label { get; set; }
    public string Description { get; set; }
    public BasicConfigSettingKind Kind { get; set; }
    public BasicConfigReloadBehavior ReloadBehavior { get; set; }
    public Func<TConfig, string> GetValue { get; set; }
    public Func<TConfig, string, string> SetValue { get; set; }
    public string[] Options { get; set; }
    public string[] OptionNames { get; set; }
}
