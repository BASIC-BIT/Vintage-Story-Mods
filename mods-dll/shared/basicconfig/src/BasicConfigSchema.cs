using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BasicConfig;

public sealed class BasicConfigSchema<TConfig>
{
    private readonly Func<TConfig, IReadOnlyList<string>> _validate;
    private readonly Dictionary<string, BasicConfigSettingDefinition<TConfig>> _settingsByKey;

    public BasicConfigSchema(IEnumerable<BasicConfigSettingDefinition<TConfig>> settings, Func<TConfig, IReadOnlyList<string>> validate = null)
    {
        Settings = (settings ?? throw new ArgumentNullException(nameof(settings))).ToList();
        _settingsByKey = Settings.ToDictionary(setting => setting.Key, StringComparer.OrdinalIgnoreCase);
        _validate = validate;
    }

    public IReadOnlyList<BasicConfigSettingDefinition<TConfig>> Settings { get; }

    public bool TryGet(string key, out BasicConfigSettingDefinition<TConfig> setting)
    {
        return _settingsByKey.TryGetValue(key ?? string.Empty, out setting);
    }

    public IReadOnlyList<string> Validate(TConfig config)
    {
        return _validate?.Invoke(config) ?? Array.Empty<string>();
    }

    public List<BasicConfigSettingValue> BuildValues(TConfig config)
    {
        return Settings.Select(setting => new BasicConfigSettingValue
        {
            Key = setting.Key,
            Value = setting.GetValue(config)
        }).ToList();
    }

    public HashSet<string> GetChangedKeys(TConfig before, TConfig after)
    {
        return Settings
            .Where(setting => !string.Equals(setting.GetValue(before), setting.GetValue(after), StringComparison.Ordinal))
            .Select(setting => setting.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public List<string> GetRestartRequiredKeys(IEnumerable<string> changedKeys)
    {
        return (changedKeys ?? Array.Empty<string>())
            .Where(key => TryGet(key, out var setting) && setting.ReloadBehavior == BasicConfigReloadBehavior.RestartRequired)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public List<string> ApplyValues(TConfig draft, IEnumerable<BasicConfigSettingValue> values)
    {
        var errors = new List<string>();
        foreach (var value in values ?? Enumerable.Empty<BasicConfigSettingValue>())
        {
            if (string.IsNullOrWhiteSpace(value?.Key) || !TryGet(value.Key, out var setting))
            {
                errors.Add($"Unknown setting: {value?.Key}");
                continue;
            }

            if (!setting.TrySetValue(draft, value.Value, out var error))
            {
                errors.Add(error);
            }
        }

        errors.AddRange(Validate(draft));
        return errors;
    }
}

public sealed class BasicConfigSettingMetadata
{
    public BasicConfigSettingMetadata(string key, string group, string label, string description, BasicConfigReloadBehavior reloadBehavior)
    {
        Key = key;
        Group = group;
        Label = label;
        Description = description;
        ReloadBehavior = reloadBehavior;
    }

    public string Key { get; }
    public string Group { get; }
    public string Label { get; }
    public string Description { get; }
    public BasicConfigReloadBehavior ReloadBehavior { get; }
}

public sealed class BasicConfigSchemaBuilder<TConfig>
{
    private readonly List<BasicConfigSettingDefinition<TConfig>> _settings = new();
    private Func<TConfig, IReadOnlyList<string>> _validate;

    public BasicConfigSchemaBuilder<TConfig> ValidateWith(Func<TConfig, IReadOnlyList<string>> validate)
    {
        _validate = validate;
        return this;
    }

    public BasicConfigSchemaBuilder<TConfig> Add(BasicConfigSettingDefinition<TConfig> definition)
    {
        _settings.Add(definition);
        return this;
    }

    public BasicConfigSchemaBuilder<TConfig> Custom(
        BasicConfigSettingMetadata metadata,
        BasicConfigSettingKind kind,
        Func<TConfig, string> get,
        Func<TConfig, string, string> set,
        string[] options = null,
        string[] optionNames = null)
    {
        metadata = RequireMetadata(metadata);
        return Add(new BasicConfigSettingDefinition<TConfig>(new BasicConfigSettingDefinitionOptions<TConfig>
        {
            Key = metadata.Key,
            Group = metadata.Group,
            Label = metadata.Label,
            Description = metadata.Description,
            Kind = kind,
            ReloadBehavior = metadata.ReloadBehavior,
            GetValue = get,
            SetValue = set,
            Options = options,
            OptionNames = optionNames
        }));
    }

    public BasicConfigSchemaBuilder<TConfig> Bool(BasicConfigSettingMetadata metadata, Func<TConfig, bool> get, Action<TConfig, bool> set)
    {
        metadata = RequireMetadata(metadata);
        return Custom(metadata, BasicConfigSettingKind.Boolean,
            config => BasicConfigSettingDefinition<TConfig>.FormatBool(get(config)),
            (config, value) =>
            {
                if (!BasicConfigSettingDefinition<TConfig>.TryParseBool(value, out var parsed))
                {
                    return $"{metadata.Key} must be true/false or 1/0.";
                }

                set(config, parsed);
                return null;
            });
    }

    public BasicConfigSchemaBuilder<TConfig> Int(BasicConfigSettingMetadata metadata, Func<TConfig, int> get, Action<TConfig, int> set, int min, int max)
    {
        metadata = RequireMetadata(metadata);
        return Custom(metadata, BasicConfigSettingKind.Integer,
            config => get(config).ToString(CultureInfo.InvariantCulture),
            (config, value) =>
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) || parsed < min || parsed > max)
                {
                    return $"{metadata.Key} must be a whole number from {min} to {max}.";
                }

                set(config, parsed);
                return null;
            });
    }

    public BasicConfigSchemaBuilder<TConfig> Decimal(BasicConfigSettingMetadata metadata, Func<TConfig, double> get, Action<TConfig, double> set, double min, double max)
    {
        metadata = RequireMetadata(metadata);
        return Custom(metadata, BasicConfigSettingKind.Decimal,
            config => BasicConfigSettingDefinition<TConfig>.FormatDecimal(get(config)),
            (config, value) =>
            {
                if (!BasicConfigSettingDefinition<TConfig>.TryParseDecimal(value, out var parsed) || parsed < min || parsed > max)
                {
                    return $"{metadata.Key} must be a number from {min.ToString(CultureInfo.InvariantCulture)} to {max.ToString(CultureInfo.InvariantCulture)}.";
                }

                set(config, parsed);
                return null;
            });
    }

    public BasicConfigSchemaBuilder<TConfig> Text(BasicConfigSettingMetadata metadata, Func<TConfig, string> get, Action<TConfig, string> set, Func<string, string> validate = null)
    {
        metadata = RequireMetadata(metadata);
        return Custom(metadata, BasicConfigSettingKind.Text,
            config => get(config) ?? string.Empty,
            (config, value) =>
            {
                value ??= string.Empty;
                var error = validate?.Invoke(value);
                if (error != null)
                {
                    return error;
                }

                set(config, value);
                return null;
            });
    }

    public BasicConfigSchemaBuilder<TConfig> Select(BasicConfigSettingMetadata metadata, Func<TConfig, string> get, Action<TConfig, string> set, string[] options, string[] optionNames = null)
    {
        metadata = RequireMetadata(metadata);
        return Custom(metadata, BasicConfigSettingKind.Select,
            config => get(config) ?? options[0],
            (config, value) =>
            {
                if (!options.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    return $"{metadata.Key} must be one of: {string.Join(", ", options)}.";
                }

                set(config, options.First(option => option.Equals(value, StringComparison.OrdinalIgnoreCase)));
                return null;
            }, options, optionNames ?? options);
    }

    public BasicConfigSchema<TConfig> Build()
    {
        return new BasicConfigSchema<TConfig>(_settings, _validate);
    }

    private static BasicConfigSettingMetadata RequireMetadata(BasicConfigSettingMetadata metadata)
    {
        return metadata ?? throw new ArgumentNullException(nameof(metadata));
    }
}
