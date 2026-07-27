using System;
using Newtonsoft.Json;
using Vintagestory.API.Server;

namespace BasicConfig;

public sealed class BasicConfigStore<TConfig> where TConfig : class, new()
{
    private readonly ICoreServerAPI _api;
    private readonly string _configName;
    private readonly string _displayName;
    private readonly Action<TConfig> _normalize;
    private bool _loggedLoadFailure;
    private bool _loggedRepair;

    public BasicConfigStore(ICoreServerAPI api, string configName, string displayName, Action<TConfig> normalize)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _configName = configName ?? throw new ArgumentNullException(nameof(configName));
        _displayName = displayName ?? configName;
        _normalize = normalize;
    }

    public TConfig Current { get; private set; }

    public TConfig GetOrLoad()
    {
        return Current ??= LoadFromDisk();
    }

    public TConfig Reload()
    {
        var loaded = LoadFromDisk();
        if (Current == null)
        {
            Current = loaded;
        }
        else
        {
            CopyValues(loaded, Current);
        }

        return Current;
    }

    public void Save()
    {
        if (Current != null)
        {
            _api.StoreModConfig(Current, _configName);
        }
    }

    public TConfig Clone(TConfig source)
    {
        var json = JsonConvert.SerializeObject(source);
        var clone = JsonConvert.DeserializeObject<TConfig>(json) ?? new TConfig();
        Normalize(clone);
        return clone;
    }

    public void CopyValues(TConfig source, TConfig target)
    {
        var json = JsonConvert.SerializeObject(source);
        JsonConvert.PopulateObject(json, target, new JsonSerializerSettings
        {
            ObjectCreationHandling = ObjectCreationHandling.Replace
        });
        Normalize(target);
    }

    private TConfig LoadFromDisk()
    {
        try
        {
            var loaded = _api.LoadModConfig<TConfig>(_configName);
            if (loaded == null)
            {
                _api.Server.LogNotification($"{_displayName}: non-existent modconfig at 'ModConfig/{_configName}', creating default...");
                loaded = new TConfig();
            }

            Normalize(loaded);
            _api.StoreModConfig(loaded, _configName);
            return loaded;
        }
        catch (Exception ex)
        {
            var repaired = TryRepairJsonStringConfig();
            if (repaired != null)
            {
                return repaired;
            }

            if (!_loggedLoadFailure)
            {
                _loggedLoadFailure = true;
                _api.Server.LogError($"{_displayName}: Failed to load mod config '{_configName}'. Using defaults. (Exception type: {ex.GetType().Name})");
            }

            var fallback = new TConfig();
            Normalize(fallback);
            return fallback;
        }
    }

    private TConfig TryRepairJsonStringConfig()
    {
        try
        {
            var maybeJsonString = _api.LoadModConfig<string>(_configName);
            if (string.IsNullOrWhiteSpace(maybeJsonString) || !maybeJsonString.TrimStart().StartsWith('{'))
            {
                return null;
            }

            var repaired = JsonConvert.DeserializeObject<TConfig>(maybeJsonString);
            if (repaired == null)
            {
                return null;
            }

            Normalize(repaired);
            _api.StoreModConfig(repaired, _configName);
            if (!_loggedRepair)
            {
                _loggedRepair = true;
                _api.Server.LogWarning($"{_displayName}: Repaired malformed config file '{_configName}' (was JSON string). Saved corrected config.");
            }

            return repaired;
        }
        catch
        {
            return null;
        }
    }

    private void Normalize(TConfig config)
    {
        _normalize?.Invoke(config);
    }
}
