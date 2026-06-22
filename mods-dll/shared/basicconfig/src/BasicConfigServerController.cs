using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Server;

namespace BasicConfig;

public sealed class BasicConfigServerController<TConfig> where TConfig : class, new()
{
    private readonly string _configId;
    private readonly string _displayName;
    private readonly Func<TConfig> _getConfig;
    private readonly Func<TConfig> _reloadConfig;
    private readonly Action _saveConfig;
    private readonly Func<TConfig, TConfig> _cloneConfig;
    private readonly Action<TConfig, TConfig> _copyConfig;
    private readonly BasicConfigSchema<TConfig> _schema;
    private readonly IServerNetworkChannel _channel;
    private readonly Func<IServerPlayer, bool> _canEdit;
    private readonly Func<TConfig, IList<string>> _getReviewedKeys;
    private readonly Action<TConfig, IList<string>> _setReviewedKeys;
    private readonly Action<IReadOnlySet<string>> _afterChanged;

    public BasicConfigServerController(BasicConfigServerControllerOptions<TConfig> options)
    {
        options = RequireOptions(options);
        _configId = RequireOption(options.ConfigId, "ConfigId");
        _displayName = ResolveDisplayName(options.DisplayName, _configId);
        var store = ValidateStoreOrDelegates(options);

        _getConfig = ResolveGetConfig(options, store);
        _reloadConfig = ResolveReloadConfig(options, store);
        _saveConfig = ResolveSaveConfig(options, store);
        _cloneConfig = ResolveCloneConfig(options, store);
        _copyConfig = ResolveCopyConfig(options, store);
        _schema = RequireOption(options.Schema, "Schema");
        _channel = RequireOption(options.Channel, "Channel");
        _canEdit = ResolveCanEdit(options.CanEdit);
        _getReviewedKeys = options.GetReviewedKeys;
        _setReviewedKeys = options.SetReviewedKeys;
        _afterChanged = options.AfterChanged;
    }

    private static BasicConfigServerControllerOptions<TConfig> RequireOptions(BasicConfigServerControllerOptions<TConfig> options)
    {
        return options ?? throw new ArgumentNullException(nameof(options));
    }

    private static TValue RequireOption<TValue>(TValue value, string optionName) where TValue : class
    {
        return value ?? throw new ArgumentException($"{optionName} is required.", nameof(value));
    }

    private static string ResolveDisplayName(string displayName, string configId)
    {
        return displayName ?? configId;
    }

    private static BasicConfigStore<TConfig> ValidateStoreOrDelegates(BasicConfigServerControllerOptions<TConfig> options)
    {
        if (options.Store != null)
        {
            return options.Store;
        }

        if (options.GetConfig != null && options.ReloadConfig != null && options.SaveConfig != null && options.CloneConfig != null && options.CopyConfig != null)
        {
            return null;
        }

        throw new ArgumentException("Provide either Store or all config delegate options.", nameof(options));
    }

    private static Func<TConfig> ResolveGetConfig(BasicConfigServerControllerOptions<TConfig> options, BasicConfigStore<TConfig> store)
    {
        return options.GetConfig ?? store.GetOrLoad;
    }

    private static Func<TConfig> ResolveReloadConfig(BasicConfigServerControllerOptions<TConfig> options, BasicConfigStore<TConfig> store)
    {
        return options.ReloadConfig ?? store.Reload;
    }

    private static Action ResolveSaveConfig(BasicConfigServerControllerOptions<TConfig> options, BasicConfigStore<TConfig> store)
    {
        return options.SaveConfig ?? store.Save;
    }

    private static Func<TConfig, TConfig> ResolveCloneConfig(BasicConfigServerControllerOptions<TConfig> options, BasicConfigStore<TConfig> store)
    {
        return options.CloneConfig ?? store.Clone;
    }

    private static Action<TConfig, TConfig> ResolveCopyConfig(BasicConfigServerControllerOptions<TConfig> options, BasicConfigStore<TConfig> store)
    {
        return options.CopyConfig ?? store.CopyValues;
    }

    private static Func<IServerPlayer, bool> ResolveCanEdit(Func<IServerPlayer, bool> canEdit)
    {
        return canEdit ?? (_ => false);
    }

    public TConfig Config => _getConfig();

    public void SendOpen(IServerPlayer player, string statusMessage = null)
    {
        if (player == null)
        {
            return;
        }

        if (!_canEdit(player))
        {
            SendResult(player, false, $"You do not have permission to edit {_displayName} config.", Array.Empty<string>());
            return;
        }

        _channel.SendPacket(new BasicConfigOpenMessage
        {
            ConfigId = _configId,
            Values = _schema.BuildValues(Config),
            ReviewedKeys = GetReviewedKeys(Config).ToList(),
            StatusMessage = statusMessage
        }, player);
    }

    public void OnSaveMessage(IServerPlayer player, BasicConfigSaveMessage message)
    {
        if (!string.Equals(message?.ConfigId, _configId, StringComparison.Ordinal))
        {
            return;
        }

        if (player == null || !_canEdit(player))
        {
            SendResult(player, false, $"You do not have permission to edit {_displayName} config.", Array.Empty<string>());
            return;
        }

        if (message.ReloadFromDisk)
        {
            var changed = ReloadAndApply();
            SendResult(player, true, $"Reloaded {_displayName} config from disk. Changed settings: {changed.Count}.", changed);
            return;
        }

        var draft = _cloneConfig(Config);
        var errors = _schema.ApplyValues(draft, message.Values);
        if (errors.Count > 0)
        {
            SendResult(player, false, string.Join("\n", errors), Array.Empty<string>());
            return;
        }

        if (message.MarkReviewedKeys?.Count > 0)
        {
            MarkReviewedKeys(draft, message.MarkReviewedKeys);
        }

        var changedKeys = _schema.GetChangedKeys(Config, draft);
        _copyConfig(draft, Config);
        _saveConfig();
        _afterChanged?.Invoke(changedKeys);
        SendResult(player, true, BuildSaveMessage(changedKeys), changedKeys);
    }

    public HashSet<string> ReloadAndApply()
    {
        var before = _cloneConfig(Config);
        _reloadConfig();
        var changedKeys = _schema.GetChangedKeys(before, Config);
        _afterChanged?.Invoke(changedKeys);
        return changedKeys;
    }

    private void SendResult(IServerPlayer player, bool success, string message, IReadOnlyCollection<string> changedKeys)
    {
        if (player == null)
        {
            return;
        }

        changedKeys ??= Array.Empty<string>();
        var restartRequired = _schema.GetRestartRequiredKeys(changedKeys);
        var liveApplied = changedKeys.Where(key => !restartRequired.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList();

        _channel.SendPacket(new BasicConfigResultMessage
        {
            ConfigId = _configId,
            Success = success,
            Message = message,
            Values = _schema.BuildValues(Config),
            ReviewedKeys = GetReviewedKeys(Config).ToList(),
            LiveAppliedKeys = liveApplied,
            RestartRequiredKeys = restartRequired
        }, player);
    }

    private string BuildSaveMessage(IReadOnlyCollection<string> changedKeys)
    {
        var restartRequired = _schema.GetRestartRequiredKeys(changedKeys);
        return restartRequired.Count == 0
            ? $"Saved {_displayName} config. Live-applied settings: {changedKeys.Count}."
            : $"Saved {_displayName} config. Restart required for: {string.Join(", ", restartRequired)}.";
    }

    private IReadOnlyList<string> GetReviewedKeys(TConfig config)
    {
        return _getReviewedKeys?.Invoke(config)?.ToList() ?? new List<string>();
    }

    private void MarkReviewedKeys(TConfig draft, IEnumerable<string> keys)
    {
        if (_setReviewedKeys == null)
        {
            return;
        }

        var reviewed = new HashSet<string>(GetReviewedKeys(draft), StringComparer.OrdinalIgnoreCase);
        foreach (var key in keys?.Where(key => _schema.TryGet(key, out _)) ?? Enumerable.Empty<string>())
        {
            reviewed.Add(key);
        }

        _setReviewedKeys(draft, reviewed.OrderBy(key => key, StringComparer.OrdinalIgnoreCase).ToList());
    }
}

public sealed class BasicConfigServerControllerOptions<TConfig> where TConfig : class, new()
{
    public string ConfigId { get; set; }
    public string DisplayName { get; set; }
    public BasicConfigStore<TConfig> Store { get; set; }
    public Func<TConfig> GetConfig { get; set; }
    public Func<TConfig> ReloadConfig { get; set; }
    public Action SaveConfig { get; set; }
    public Func<TConfig, TConfig> CloneConfig { get; set; }
    public Action<TConfig, TConfig> CopyConfig { get; set; }
    public BasicConfigSchema<TConfig> Schema { get; set; }
    public IServerNetworkChannel Channel { get; set; }
    public Func<IServerPlayer, bool> CanEdit { get; set; }
    public Func<TConfig, IList<string>> GetReviewedKeys { get; set; }
    public Action<TConfig, IList<string>> SetReviewedKeys { get; set; }
    public Action<IReadOnlySet<string>> AfterChanged { get; set; }
}
