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
    private List<CharacterSheetFieldConfigEntryMessage> _originalSheetFields;
    private List<CharacterSheetFieldConfigEntryMessage> _submittedSheetFields;
    private List<CharacterSheetFieldConfigEntryMessage> _unconfirmedSheetFields;
    private bool _hasSheetSnapshot;
    private long _requestId;
    private string _runId;
    private List<LanguageConfigEntryMessage> _originalLanguages;
    private List<LanguageConfigEntryMessage> _submittedLanguages;
    private Dictionary<LanguageConfigEntryMessage, string> _submittedLanguageNames;
    private Dictionary<string, string> _unconfirmedValues;
    private List<LanguageConfigEntryMessage> _unconfirmedLanguages;
    private Dictionary<LanguageConfigEntryMessage, string> _unconfirmedLanguageNames;

    public SetupWizardDraft(IEnumerable<ConfigAdminSettingValue> values, IEnumerable<LanguageConfigEntryMessage> languages = null, IEnumerable<CharacterSheetFieldConfigEntryMessage> fields = null)
    {
        SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(fields);
        _hasSheetSnapshot = SheetFields.Count > 0;
        _originalSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(SheetFields);
        Languages = LanguageConfigAdmin.CloneEntries(languages);
        _originalLanguages = LanguageConfigAdmin.CloneEntries(Languages);
        foreach (var value in values ?? Enumerable.Empty<ConfigAdminSettingValue>())
        {
            if (value?.Key == null || !SetupWizardCatalog.IsSettingKey(value.Key) || !ConfigAdminSettingRegistry.TryGet(value.Key, out var setting)) continue;
            _values[setting.Key] = _originals[setting.Key] = value.Value ?? string.Empty;
        }
    }

    public string Get(string key) => _values.TryGetValue(key, out var value) ? value : string.Empty;
    public string GetOriginal(string key) => _originals.TryGetValue(key, out var value) ? value : string.Empty;
    public List<LanguageConfigEntryMessage> Languages { get; private set; }
    public bool LanguagesChanged => !LanguageConfigAdmin.EntriesEqual(Languages, _originalLanguages);
    public IReadOnlyList<LanguageConfigEntryMessage> OriginalLanguages => LanguageConfigAdmin.CloneEntries(_originalLanguages);
    public List<CharacterSheetFieldConfigEntryMessage> SheetFields { get; private set; }
    public IReadOnlyList<CharacterSheetFieldConfigEntryMessage> OriginalSheetFields => CharacterSheetFieldConfigAdmin.CloneEntries(_originalSheetFields);
    public bool SheetFieldsChanged => !CharacterSheetFieldConfigAdmin.EntriesEqual(SetupWizardState.NormalizeSheetDraft(SheetFields), SetupWizardState.NormalizeSheetDraft(_originalSheetFields));
    public string SheetFieldsUnavailableReason => !_hasSheetSnapshot
        ? "This server did not provide a character sheet template. Update the server to edit fields here."
        : SetupWizardState.ValidateSheetSnapshots(_originalSheetFields, _originalSheetFields).FirstOrDefault();
    public bool CanEditSheetFields => SheetFieldsUnavailableReason == null;
    public bool IsDirty => ChangedKeys.Count > 0;
    public bool IsSavePending => _submitted != null;
    public bool HasUnconfirmedSave => _unconfirmedValues != null;
    public IReadOnlyList<ConfigAdminSettingValue> Values => Snapshot(_values);
    public IReadOnlyList<string> ChangedKeys => _values.Where(pair => pair.Value != _originals[pair.Key]).Select(pair => pair.Key)
        .Concat(LanguagesChanged ? new[] { "Languages" } : Array.Empty<string>())
        .Concat(SheetFieldsChanged ? new[] { "CharacterSheetFields" } : Array.Empty<string>()).ToArray();

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

    public void RefreshLanguages(IEnumerable<LanguageConfigEntryMessage> languages)
    {
        if (languages == null || LanguagesChanged) return;
        Languages = LanguageConfigAdmin.CloneEntries(languages);
        _originalLanguages = LanguageConfigAdmin.CloneEntries(Languages);
    }

    public void RefreshSheetFields(IEnumerable<CharacterSheetFieldConfigEntryMessage> fields)
    {
        var snapshot = CharacterSheetFieldConfigAdmin.CloneEntries(fields);
        _hasSheetSnapshot = snapshot.Count > 0;
        if (!_hasSheetSnapshot || SheetFieldsChanged) return;
        SheetFields = snapshot;
        _originalSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(SheetFields);
    }

    public void ReplaceAfterReload(TheBasicsSetupWizardResultMessage result)
    {
        if (IsSavePending || HasUnconfirmedSave) throw new InvalidOperationException("Check the pending save before reloading.");
        if (!result.Success) return;
        _values.Clear();
        _originals.Clear();
        RefreshValues(result.Values);
        Languages = LanguageConfigAdmin.CloneEntries(result.Languages);
        _originalLanguages = LanguageConfigAdmin.CloneEntries(Languages);
        SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(result.SheetFields);
        _originalSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(SheetFields);
        _hasSheetSnapshot = SheetFields.Count > 0;
    }

    public void ResetSheetFields()
    {
        if (IsSavePending || HasUnconfirmedSave) throw new InvalidOperationException("Check the pending save before resetting field edits.");
        SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(_originalSheetFields);
    }

    public void ResetLanguages()
    {
        if (IsSavePending || HasUnconfirmedSave) throw new InvalidOperationException("Check the pending save before resetting language edits.");
        Languages = LanguageConfigAdmin.CloneEntries(_originalLanguages);
    }

    public TheBasicsSetupWizardRequestMessage CreateSaveRequest(long requestId, string runId)
    {
        if (_submitted != null || HasUnconfirmedSave) throw new InvalidOperationException("The previous setup save must be confirmed first.");
        var changedKeys = ChangedKeys.Where(key => key != "Languages" && key != "CharacterSheetFields").ToArray();
        var languagesChanged = LanguagesChanged;
        var fieldsChanged = SheetFieldsChanged;
        if (fieldsChanged && !CanEditSheetFields) throw new InvalidOperationException(SheetFieldsUnavailableReason);
        if (fieldsChanged && SetupWizardState.ValidateSheetSnapshots(SheetFields, _originalSheetFields).FirstOrDefault() is { } fieldError)
            throw new InvalidOperationException(fieldError);
        if (fieldsChanged && SetupWizardState.ValidateSheetChoices(SheetFields).FirstOrDefault() is { } choiceError)
            throw new InvalidOperationException(choiceError);
        _submittedSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(SheetFields);
        _submitted = new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase);
        _submittedLanguages = LanguageConfigAdmin.CloneEntries(Languages);
        _submittedLanguageNames = LanguageConfigDialog.CaptureSubmittedNames(Languages);
        _requestId = requestId;
        _runId = runId;
        return new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save,
            RequestId = requestId,
            RunId = runId,
            Values = changedKeys.Select(key => new ConfigAdminSettingValue { Key = key, Value = _submitted[key] }).ToList(),
            OriginalValues = changedKeys.Select(key => new ConfigAdminSettingValue { Key = key, Value = _originals[key] }).ToList(),
            Languages = languagesChanged ? LanguageConfigAdmin.CloneEntries(_submittedLanguages) : new(),
            OriginalLanguages = languagesChanged ? LanguageConfigAdmin.CloneEntries(_originalLanguages) : new(),
            SheetFields = fieldsChanged ? SetupWizardState.NormalizeSheetDraft(_submittedSheetFields) : new(),
            OriginalSheetFields = fieldsChanged ? CharacterSheetFieldConfigAdmin.CloneEntries(_originalSheetFields) : new()
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
        if (result.Languages?.Count > 0)
        {
            if (LanguageConfigAdmin.EntriesEqual(Languages, _submittedLanguages) &&
                (result.Success || LanguageConfigAdmin.EntriesEqual(_submittedLanguages, _originalLanguages)))
                Languages = LanguageConfigAdmin.CloneEntries(result.Languages);
            else if (result.Success)
                LanguageConfigDialog.RebaseSavedNames(Languages, _submittedLanguageNames, result.Languages);
            _originalLanguages = LanguageConfigAdmin.CloneEntries(result.Languages);
        }
        MergeSheetSnapshot(result.SheetFields, _submittedSheetFields, result.Success);
        CancelRequest();
        return true;
    }

    public void CancelRequest()
    {
        _submitted = null;
        _submittedSheetFields = null;
        _submittedLanguages = null;
        _submittedLanguageNames = null;
        _requestId = 0;
        _runId = null;
    }

    public void MarkSaveTimedOut()
    {
        if (_submitted == null) return;
        _unconfirmedValues = _submitted;
        _unconfirmedSheetFields = _submittedSheetFields;
        _unconfirmedLanguages = _submittedLanguages;
        _unconfirmedLanguageNames = !LanguageConfigAdmin.EntriesEqual(_submittedLanguages, _originalLanguages) ? _submittedLanguageNames : null;
        CancelRequest();
    }

    public bool MergeSaveReadback(TheBasicsSetupWizardResultMessage result)
    {
        if (!HasUnconfirmedSave || result?.Kind != SetupWizardResultKind.Open || !result.Success) return false;
        if (!CharacterSheetFieldConfigAdmin.EntriesEqual(_unconfirmedSheetFields, _originalSheetFields) &&
            (result.SheetFields == null || result.SheetFields.Count == 0)) return false;
        foreach (var value in result.Values ?? Enumerable.Empty<ConfigAdminSettingValue>())
        {
            if (value?.Key == null || !SetupWizardCatalog.IsSettingKey(value.Key) || !ConfigAdminSettingRegistry.TryGet(value.Key, out var setting)) continue;
            var key = setting.Key;
            if (!_values.TryGetValue(key, out var current) ||
                _unconfirmedValues.TryGetValue(key, out var submitted) && current == submitted && submitted == GetOriginal(key))
                _values[key] = value.Value ?? string.Empty;
            _originals[key] = value.Value ?? string.Empty;
        }
        if (result.Languages?.Count > 0)
        {
            var normalized = new thebasics.Configs.ModConfig();
            var matchesSubmitted = LanguageConfigAdmin.TryApplyEntries(normalized, _unconfirmedLanguages, out _) &&
                LanguageConfigAdmin.EntriesEqual(LanguageConfigAdmin.BuildEntries(normalized), result.Languages);
            if (LanguageConfigAdmin.EntriesEqual(Languages, _unconfirmedLanguages) &&
                (_unconfirmedLanguageNames == null || matchesSubmitted))
                Languages = LanguageConfigAdmin.CloneEntries(result.Languages);
            else if (_unconfirmedLanguageNames != null)
                LanguageConfigDialog.RebaseSavedNames(Languages, _unconfirmedLanguageNames, result.Languages);
            _originalLanguages = LanguageConfigAdmin.CloneEntries(result.Languages);
        }
        var normalizedFields = new thebasics.Configs.ModConfig();
        var fieldsMatch = CharacterSheetFieldConfigAdmin.TryApplyEntries(normalizedFields, SetupWizardState.NormalizeSheetDraft(_unconfirmedSheetFields), out _) &&
            CharacterSheetFieldConfigAdmin.EntriesEqual(CharacterSheetFieldConfigAdmin.BuildEntries(normalizedFields), result.SheetFields);
        MergeSheetSnapshot(result.SheetFields, _unconfirmedSheetFields, fieldsMatch, rebaseIdentities: true);
        _unconfirmedSheetFields = null;
        _unconfirmedValues = null;
        _unconfirmedLanguages = null;
        _unconfirmedLanguageNames = null;
        return true;
    }

    private void MergeSheetSnapshot(List<CharacterSheetFieldConfigEntryMessage> saved,
        List<CharacterSheetFieldConfigEntryMessage> submitted, bool success, bool rebaseIdentities = false)
    {
        if (saved == null || saved.Count == 0) return;
        if (CharacterSheetFieldConfigAdmin.EntriesEqual(SheetFields, submitted) &&
            (success || CharacterSheetFieldConfigAdmin.EntriesEqual(submitted, _originalSheetFields)))
            SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(saved);
        else if (success || rebaseIdentities)
        {
            for (var index = 0; index < SheetFields.Count; index++)
            {
                var entry = SheetFields[index];
                if (entry == null) continue;
                var id = entry.Id;
                var persisted = saved.FirstOrDefault(field => field.Id == id);
                var sent = submitted.FirstOrDefault(field => field.Id == id);
                if (persisted == null || sent == null) continue;
                if (success && CharacterSheetFieldConfigAdmin.EntriesEqual(new[] { entry }, new[] { sent }))
                    SheetFields[index] = persisted with { };
                else entry.OriginalId = persisted.OriginalId;
            }
        }
        _hasSheetSnapshot = true;
        _originalSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(saved);
    }

    private static List<ConfigAdminSettingValue> Snapshot(IEnumerable<KeyValuePair<string, string>> values) =>
        values.Select(pair => new ConfigAdminSettingValue { Key = pair.Key, Value = pair.Value }).ToList();
}
