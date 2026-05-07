using System;
using System.Collections.Generic;
using System.Globalization;
using thebasics.Configs;

namespace thebasics.ModSystems.AdminConfig;

public sealed class ConfigAdminSettingDefinition
{
    private readonly Func<ModConfig, string> _getValue;
    private readonly Func<ModConfig, string, string> _setValue;

    public ConfigAdminSettingDefinition(ConfigAdminSettingDefinitionOptions definition)
    {
        Key = definition.Key;
        Group = definition.Group;
        Label = definition.Label;
        Description = definition.Description;
        Kind = definition.Kind;
        ReloadBehavior = definition.ReloadBehavior;
        _getValue = definition.GetValue;
        _setValue = definition.SetValue;
        Options = definition.Options ?? Array.Empty<string>();
        OptionNames = definition.OptionNames ?? Options;
    }

    public string Key { get; }
    public string Group { get; }
    public string Label { get; }
    public string Description { get; }
    public ConfigAdminSettingKind Kind { get; }
    public ConfigAdminReloadBehavior ReloadBehavior { get; }
    public IReadOnlyList<string> Options { get; }
    public IReadOnlyList<string> OptionNames { get; }

    public string GetValue(ModConfig config)
    {
        return _getValue(config);
    }

    public bool TrySetValue(ModConfig config, string value, out string error)
    {
        error = _setValue(config, value);
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
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }
}

public sealed class ConfigAdminSettingDefinitionOptions
{
    public string Key { get; set; }
    public string Group { get; set; }
    public string Label { get; set; }
    public string Description { get; set; }
    public ConfigAdminSettingKind Kind { get; set; }
    public ConfigAdminReloadBehavior ReloadBehavior { get; set; }
    public Func<ModConfig, string> GetValue { get; set; }
    public Func<ModConfig, string, string> SetValue { get; set; }
    public string[] Options { get; set; }
    public string[] OptionNames { get; set; }
}
