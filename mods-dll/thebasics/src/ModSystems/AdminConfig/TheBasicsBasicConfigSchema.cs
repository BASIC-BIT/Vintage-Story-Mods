using System.Collections.Generic;
using BasicConfig;
using thebasics.Configs;

namespace thebasics.ModSystems.AdminConfig;

internal static class TheBasicsBasicConfigSchema
{
    public const string ConfigId = "thebasics";

    public static BasicConfigSchema<ModConfig> Build()
    {
        var builder = new BasicConfigSchemaBuilder<ModConfig>()
            .ValidateWith(ConfigAdminSettingRegistry.ValidateConfig);

        foreach (var setting in ConfigAdminSettingRegistry.Settings)
        {
            builder.Custom(
                new BasicConfigSettingMetadata(
                    setting.Key,
                    setting.Group,
                    setting.Label,
                    setting.Description,
                    ToBasicReloadBehavior(setting.ReloadBehavior)),
                ToBasicKind(setting.Kind),
                setting.GetValue,
                (config, value) => setting.TrySetValue(config, value, out var error) ? null : error,
                ToArray(setting.Options),
                ToArray(setting.OptionNames));
        }

        return builder.Build();
    }

    private static BasicConfigSettingKind ToBasicKind(ConfigAdminSettingKind kind)
    {
        return kind switch
        {
            ConfigAdminSettingKind.Boolean => BasicConfigSettingKind.Boolean,
            ConfigAdminSettingKind.Integer => BasicConfigSettingKind.Integer,
            ConfigAdminSettingKind.Decimal => BasicConfigSettingKind.Decimal,
            ConfigAdminSettingKind.Select => BasicConfigSettingKind.Select,
            _ => BasicConfigSettingKind.Text
        };
    }

    private static BasicConfigReloadBehavior ToBasicReloadBehavior(ConfigAdminReloadBehavior reloadBehavior)
    {
        return reloadBehavior == ConfigAdminReloadBehavior.Live
            ? BasicConfigReloadBehavior.Live
            : BasicConfigReloadBehavior.RestartRequired;
    }

    private static string[] ToArray(IReadOnlyList<string> values)
    {
        if (values == null || values.Count == 0)
        {
            return [];
        }

        var array = new string[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            array[i] = values[i];
        }

        return array;
    }
}
