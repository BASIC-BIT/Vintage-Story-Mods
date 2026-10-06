using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;

namespace thebasics.ModSystems.ChatUiSystem;

public sealed class SetupWizardDraft
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _originals = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _submitted;
    private long _requestId;
    private string _runId;

    public SetupWizardDraft(IEnumerable<ConfigAdminSettingValue> values)
    {
        foreach (var value in values ?? Enumerable.Empty<ConfigAdminSettingValue>())
        {
            if (value?.Key == null || !SetupWizardCatalog.IsSettingKey(value.Key) || !ConfigAdminSettingRegistry.TryGet(value.Key, out var setting)) continue;
            _values[setting.Key] = _originals[setting.Key] = value.Value ?? string.Empty;
        }
    }

    public string Get(string key) => _values.TryGetValue(key, out var value) ? value : string.Empty;
    public string GetOriginal(string key) => _originals.TryGetValue(key, out var value) ? value : string.Empty;
    public bool IsDirty => ChangedKeys.Count > 0;
    public IReadOnlyList<ConfigAdminSettingValue> Values => Snapshot(_values);
    public IReadOnlyList<string> ChangedKeys => _values.Where(pair => pair.Value != _originals[pair.Key]).Select(pair => pair.Key).ToArray();

    public void Set(string key, string value)
    {
        if (key == null || !_values.ContainsKey(key)) throw new ArgumentException("Setting is not part of this setup draft.", nameof(key));
        _values[key] = value ?? string.Empty;
    }

    public void RefreshValues(IEnumerable<ConfigAdminSettingValue> values)
    {
        foreach (var value in values ?? Enumerable.Empty<ConfigAdminSettingValue>())
        {
            if (value?.Key == null || !SetupWizardCatalog.IsSettingKey(value.Key) || !ConfigAdminSettingRegistry.TryGet(value.Key, out var setting)) continue;
            var key = setting.Key;
            if (_values.TryGetValue(key, out var current) && current != GetOriginal(key)) continue;
            _values[key] = _originals[key] = value.Value ?? string.Empty;
        }
    }

    public TheBasicsSetupWizardRequestMessage CreateSaveRequest(long requestId, string runId)
    {
        if (_submitted != null) throw new InvalidOperationException("A setup save is already pending.");
        var changedKeys = ChangedKeys;
        _submitted = new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
        _requestId = requestId;
        _runId = runId;
        return new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save,
            RequestId = requestId,
            RunId = runId,
            Values = changedKeys.Select(key => new ConfigAdminSettingValue { Key = key, Value = _submitted[key] }).ToList(),
            OriginalValues = changedKeys.Select(key => new ConfigAdminSettingValue { Key = key, Value = _originals[key] }).ToList()
        };
    }

    public bool MergeSaveResult(TheBasicsSetupWizardResultMessage result)
    {
        if (_submitted == null || result == null || result.Kind != SetupWizardResultKind.SaveResult || result.RequestId != _requestId || result.RunId != _runId) return false;
        foreach (var value in result.Values ?? Enumerable.Empty<ConfigAdminSettingValue>())
        {
            if (value?.Key == null || !SetupWizardCatalog.IsSettingKey(value.Key) || !ConfigAdminSettingRegistry.TryGet(value.Key, out var setting)) continue;
            var key = setting.Key;
            if (!_values.TryGetValue(key, out var current) ||
                (_submitted.TryGetValue(key, out var submitted) && current == submitted &&
                 (result.Success || submitted == GetOriginal(key)))) _values[key] = value.Value ?? string.Empty;
            _originals[key] = value.Value ?? string.Empty;
        }
        CancelRequest();
        return true;
    }

    public void CancelRequest()
    {
        _submitted = null;
        _requestId = 0;
        _runId = null;
    }

    private static List<ConfigAdminSettingValue> Snapshot(IEnumerable<KeyValuePair<string, string>> values) =>
        values.Select(pair => new ConfigAdminSettingValue { Key = pair.Key, Value = pair.Value }).ToList();
}
