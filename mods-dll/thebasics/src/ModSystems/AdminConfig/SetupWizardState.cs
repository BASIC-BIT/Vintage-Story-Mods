using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.Analytics;

namespace thebasics.ModSystems.AdminConfig;

public sealed class SetupWizardState
{
    private const int MaxValueLength = 4096;
    private const int MaxPayloadLength = 65536;
    private const int MaxLanguages = 256;
    private const int MaxLanguagePayloadLength = 262144;
    private readonly Dictionary<string, string> _startupValues;

    public SetupWizardState(ModConfig startup)
    {
        var snapshot = BaseBasicModSystem.CloneConfig(startup);
        _startupValues = ConfigAdminSettingRegistry.Settings
            .Where(setting => setting.ReloadBehavior == ConfigAdminReloadBehavior.RestartRequired)
            .ToDictionary(setting => setting.Key, setting => setting.GetValue(snapshot), StringComparer.OrdinalIgnoreCase);
    }

    public bool TryValidatePatch(ModConfig current, TheBasicsSetupWizardRequestMessage request, out ModConfig draft,
        out List<string> changedKeys, out List<string> conflictKeys, out List<string> errors)
    {
        draft = null;
        changedKeys = new();
        conflictKeys = new();
        errors = ValidateRequest(request).ToList();
        if (request?.Kind != SetupWizardRequestKind.Save) errors.Add("Expected a setup save request.");
        if (errors.Count > 0) return false;

        var originals = request.OriginalValues.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
        var candidate = BaseBasicModSystem.CloneConfig(current);
        foreach (var value in request.Values)
        {
            ConfigAdminSettingRegistry.TryGet(value.Key, out var setting);
            if (setting.GetValue(current) != originals[value.Key]) conflictKeys.Add(setting.Key);
        }
        var languagesEdited = request.Languages.Count > 0 || request.OriginalLanguages.Count > 0;
        var currentLanguages = LanguageConfigAdmin.BuildEntries(current);
        if (languagesEdited && !LanguageConfigAdmin.EntriesEqual(currentLanguages, request.OriginalLanguages))
            conflictKeys.Add(nameof(ModConfig.Languages));
        var fieldsEdited = request.SheetFields.Count > 0 || request.OriginalSheetFields.Count > 0;
        var currentFields = CharacterSheetFieldConfigAdmin.BuildEntries(current);
        if (fieldsEdited && !CharacterSheetFieldConfigAdmin.EntriesEqual(currentFields, request.OriginalSheetFields))
            conflictKeys.Add(nameof(ModConfig.CharacterSheetFields));
        if (conflictKeys.Count > 0)
        {
            errors.Add($"These settings changed since setup opened: {string.Join(", ", conflictKeys)}. Review your draft and save again.");
            return false;
        }

        errors.AddRange(ConfigAdminSaveWorkflow.ApplyValues(candidate, request.Values));
        if (languagesEdited) ApplyLanguagePatch(candidate, currentLanguages, request.Languages, errors);
        if (fieldsEdited) ApplySheetPatch(candidate, currentFields, request.SheetFields, errors);
        if (errors.Count == 0) errors.AddRange(ConfigAdminSettingRegistry.ValidateConfig(candidate));
        if (errors.Count > 0) return false;

        foreach (var value in request.Values)
        {
            ConfigAdminSettingRegistry.TryGet(value.Key, out var setting);
            if (setting.GetValue(current) != setting.GetValue(candidate)) changedKeys.Add(setting.Key);
        }
        if (languagesEdited && !LanguageConfigAdmin.EntriesEqual(currentLanguages, request.Languages))
            changedKeys.Add(nameof(ModConfig.Languages));
        if (fieldsEdited && !CharacterSheetFieldConfigAdmin.EntriesEqual(currentFields, CharacterSheetFieldConfigAdmin.BuildEntries(candidate)))
            changedKeys.Add(nameof(ModConfig.CharacterSheetFields));
        draft = candidate;
        return true;
    }

    public IReadOnlyList<string> GetPendingRestartKeys(ModConfig current) =>
        ConfigAdminSettingRegistry.Settings.Where(setting => _startupValues.TryGetValue(setting.Key, out var startup) && setting.GetValue(current) != startup)
            .Select(setting => setting.Key).ToArray();

    public static string ValidDestination(string destination) =>
        destination is "chat.languageeditor" or "characters.builder" ? destination : null;

    public static IReadOnlyList<string> ValidateRequest(TheBasicsSetupWizardRequestMessage request)
    {
        var errors = new List<string>();
        if (request == null) return new[] { "Missing setup request." };
        ValidateRequestHeader(request, errors);

        var values = ValidateValues(request.Values, "values", errors);
        var originals = ValidateValues(request.OriginalValues, "original values", errors);
        ValidateLanguageSnapshots(request, errors);
        errors.AddRange(ValidateSheetSnapshots(request.SheetFields, request.OriginalSheetFields));
        if (errors.Count == 0 && request.Kind == SetupWizardRequestKind.Save) errors.AddRange(ValidateSheetChoices(request.SheetFields));
        ValidateRequestContent(request, values, originals, errors);
        return errors;
    }

    private static void ApplyLanguagePatch(ModConfig candidate, List<LanguageConfigEntryMessage> currentLanguages,
        List<LanguageConfigEntryMessage> entries, List<string> errors)
    {
        var originalNames = currentLanguages.Select(entry => entry.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var submittedOriginals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.OriginalName)))
        {
            if (!originalNames.Contains(entry.OriginalName) || !submittedOriginals.Add(entry.OriginalName))
                errors.Add("A language's original name is missing or duplicated. Reopen setup and try again.");
        }
        errors.AddRange(LanguageConfigAdmin.ValidateEntries(entries, out var languages));
        if (!LanguageConfigAdmin.EntriesEqual(currentLanguages, entries)) candidate.Languages = languages;
    }

    private static void ApplySheetPatch(ModConfig candidate, List<CharacterSheetFieldConfigEntryMessage> currentFields,
        List<CharacterSheetFieldConfigEntryMessage> entries, List<string> errors)
    {
        var existingIds = currentFields.Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var claimedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var originalId = entry.OriginalId?.Trim() ?? string.Empty;
            if (originalId.Length > 0 && (!existingIds.Contains(originalId) || !claimedIds.Add(originalId)))
                errors.Add("A character sheet field's original key is missing or duplicated. Reopen setup and try again.");
            if (originalId.Length == 0 && existingIds.Contains(entry.Id?.Trim() ?? string.Empty))
                errors.Add("An existing character sheet field must retain its original key.");
        }
        errors.AddRange(CharacterSheetFieldConfigAdmin.ValidateEntries(NormalizeSheetDraft(entries), out var fields));
        candidate.CharacterSheetFields = fields;
    }

    private static void ValidateRequestHeader(TheBasicsSetupWizardRequestMessage request, List<string> errors)
    {
        if ((request.Destination?.Length ?? 0) > 64) errors.Add("Setup destination is too long.");
        if (!Enum.IsDefined(request.Kind)) errors.Add("Unknown setup request kind.");
        if (request.CaptureOnly && request.Kind != SetupWizardRequestKind.Open) errors.Add("Only open requests may use QA capture mode.");
        if (request.RequestId < 0 || request.Kind == SetupWizardRequestKind.Save && request.RequestId == 0) errors.Add("Invalid setup request ID.");
        ValidateJourneyFields(request, errors);
    }

    private static void ValidateJourneyFields(TheBasicsSetupWizardRequestMessage request, List<string> errors)
    {
        if ((request.RunId?.Length ?? 0) > 64 || (request.RunId?.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_') ?? false)) errors.Add("Invalid setup run ID.");
        if ((request.StepId?.Length ?? 0) > 64 || (request.ChoiceId?.Length ?? 0) > 64 || (request.JourneyAction?.Length ?? 0) > 64) errors.Add("Setup journey fields are too long.");
    }

    private static void ValidateLanguageSnapshots(TheBasicsSetupWizardRequestMessage request, List<string> errors)
    {
        var languagePayloadLength = 0;
        ValidateLanguages(request.Languages, "languages", errors, ref languagePayloadLength);
        ValidateLanguages(request.OriginalLanguages, "original languages", errors, ref languagePayloadLength);
        if (languagePayloadLength > MaxLanguagePayloadLength) errors.Add("Setup language payload is too long.");
    }

    private static void ValidateRequestContent(TheBasicsSetupWizardRequestMessage request, HashSet<string> values,
        HashSet<string> originals, List<string> errors)
    {
        if (request.Kind != SetupWizardRequestKind.Save && ((request.SheetFields?.Count ?? 0) > 0 || (request.OriginalSheetFields?.Count ?? 0) > 0))
            errors.Add("Only save requests may contain character sheet fields.");
        if (request.Kind == SetupWizardRequestKind.Save)
        {
            if (!values.SetEquals(originals)) errors.Add("Every changed setting needs exactly one original value.");
        }
        else if (values.Count > 0 || originals.Count > 0) errors.Add("Only save requests may contain setting values.");
        if (request.Kind != SetupWizardRequestKind.Save && ((request.Languages?.Count ?? 0) > 0 || (request.OriginalLanguages?.Count ?? 0) > 0))
            errors.Add("Only save requests may contain language values.");
        if (request.Kind == SetupWizardRequestKind.Track &&
            !AnalyticsService.IsValidSetupWizardJourney(request.RunId, 1, request.StepId, request.JourneyAction, request.ChoiceId)) errors.Add("Unknown setup journey step, action, or choice.");
    }

    // Inactive editor height stays in the local draft; persisted non-long-text fields use zero rows.
    public static List<CharacterSheetFieldConfigEntryMessage> NormalizeSheetDraft(IEnumerable<CharacterSheetFieldConfigEntryMessage> fields)
    {
        var copy = CharacterSheetFieldConfigAdmin.CloneEntries(fields);
        foreach (var entry in copy)
            if (entry != null)
            {
                if (!string.Equals(entry.Type, "longstring", StringComparison.OrdinalIgnoreCase)) entry.EditorRows = "0";
                if (!string.Equals(entry.Type, "option", StringComparison.OrdinalIgnoreCase)) entry.Options = "";
            }
        return copy;
    }

    public static IReadOnlyList<string> ValidateSheetChoices(IEnumerable<CharacterSheetFieldConfigEntryMessage> fields)
    {
        var errors = new List<string>();
        foreach (var field in fields ?? Enumerable.Empty<CharacterSheetFieldConfigEntryMessage>())
        {
            if (!string.Equals(field?.Type, "option", StringComparison.OrdinalIgnoreCase)) continue;
            var options = (field.Options ?? "").Split(',').Select(value => value.Trim()).ToArray();
            if (options.Any(string.IsNullOrWhiteSpace)) errors.Add("Choices must not be empty.");
            if (options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length) errors.Add("Choices must be different.");
        }
        return errors;
    }

    public static IReadOnlyList<string> ValidateSheetSnapshots(IReadOnlyList<CharacterSheetFieldConfigEntryMessage> fields,
        IReadOnlyList<CharacterSheetFieldConfigEntryMessage> originals)
    {
        var errors = new List<string>();
        long length = 0;
        foreach (var snapshot in new[] { fields, originals })
        {
            if (snapshot == null) { errors.Add("Missing setup character sheet fields."); continue; }
            if (snapshot.Count > 256) { errors.Add("Character sheet editing supports at most 256 fields per snapshot."); continue; }
            foreach (var entry in snapshot)
            {
                if (entry == null) { errors.Add("Missing character sheet field."); continue; }
                foreach (var value in new[] { entry.OriginalId, entry.Id, entry.Label, entry.Description, entry.Type, entry.Options,
                    entry.BindTo, entry.MaxLength, entry.Visibility, entry.EditorRows, entry.LayoutSection, entry.Width })
                {
                    if (value == null || value.Length > 8192) errors.Add("A character sheet field value is missing or longer than 8192 characters.");
                    length += value?.Length ?? 0;
                }
            }
        }
        if (length > 1048576) errors.Add("Character sheet editing supports at most 1 MiB of text across both snapshots.");
        return errors;
    }

    private static void ValidateLanguages(IReadOnlyList<LanguageConfigEntryMessage> languages, string name, List<string> errors, ref int payloadLength)
    {
        if (languages == null)
        {
            errors.Add($"Missing setup {name}.");
            return;
        }
        if (languages.Count > MaxLanguages)
        {
            errors.Add($"Too many setup {name}.");
            return;
        }
        foreach (var entry in languages)
        {
            if (entry == null)
            {
                errors.Add($"Missing language in setup {name}.");
                continue;
            }
            foreach (var value in new[] { entry.OriginalName, entry.Name, entry.Description, entry.Prefix, entry.Syllables, entry.Color,
                         entry.GrantedToClasses, entry.GrantedToModels, entry.GrantedToModelGroups, entry.GrantedToTraits })
            {
                if ((value?.Length ?? 0) > MaxValueLength) errors.Add($"A setup {name} value is too long.");
                payloadLength += value?.Length ?? 0;
            }
        }
    }

    private static HashSet<string> ValidateValues(IReadOnlyList<ConfigAdminSettingValue> values, string name, List<string> errors)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (values == null)
        {
            errors.Add($"Missing setup {name}.");
            return keys;
        }
        if (values.Count > SetupWizardCatalog.Pages.Sum(page => page.SettingKeys.Count))
        {
            errors.Add($"Too many setup {name}.");
            return keys;
        }
        var length = 0;
        foreach (var value in values)
        {
            if (value?.Key == null || value.Key.Length > 128 || !SetupWizardCatalog.IsSettingKey(value.Key))
            {
                errors.Add($"Unknown setup setting in {name}.");
                continue;
            }
            if (!keys.Add(value.Key)) errors.Add($"Duplicate setup setting in {name}: {value.Key}.");
            if (value.Value == null || value.Value.Length > MaxValueLength) errors.Add($"Setup value is missing or too long: {value.Key}.");
            length += value.Key.Length + (value.Value?.Length ?? 0);
        }
        if (length > MaxPayloadLength) errors.Add($"Setup {name} payload is too long.");
        return keys;
    }
}

public sealed class SetupWizardInvitationState
{
    private readonly HashSet<string> _ready = new(StringComparer.Ordinal);
    private readonly HashSet<string> _playing = new(StringComparer.Ordinal);
    private readonly HashSet<string> _offered = new(StringComparer.Ordinal);

    public void MarkClientReady(string uid) => _ready.Add(uid);
    public void MarkPlaying(string uid) => _playing.Add(uid);
    public bool TryOffer(string uid, bool alreadySeen, bool authorized) =>
        authorized && !alreadySeen && _ready.Contains(uid) && _playing.Contains(uid) && _offered.Add(uid);

    public void Clear(string uid)
    {
        _ready.Remove(uid);
        _playing.Remove(uid);
        _offered.Remove(uid);
    }
}
