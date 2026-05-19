using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.CharacterSheets.Models;

namespace thebasics.ModSystems.AdminConfig;

public static class CharacterSheetFieldConfigAdmin
{
    private const int MaxLabelLength = 100;
    private const int MaxDescriptionLength = 280;
    private const int MaxFieldLength = 5000;
    private const int MaxEditorRows = 16;
    private static readonly Regex FieldIdPattern = new("^[A-Za-z0-9_-]+$", RegexOptions.Compiled);
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        CharacterSheetFieldTypes.String,
        CharacterSheetFieldTypes.LongString,
        CharacterSheetFieldTypes.Number,
        CharacterSheetFieldTypes.Option
    };
    private static readonly HashSet<string> SupportedVisibilities = new(StringComparer.OrdinalIgnoreCase)
    {
        CharacterSheetFieldVisibilities.Public,
        CharacterSheetFieldVisibilities.Nearby,
        CharacterSheetFieldVisibilities.Self,
        CharacterSheetFieldVisibilities.Admin
    };
    private static readonly HashSet<string> SupportedBindTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        string.Empty,
        "thebasics.fullName",
        "thebasics.nickname"
    };

    public static List<CharacterSheetFieldConfigEntryMessage> BuildEntries(ModConfig config)
    {
        return (config?.CharacterSheetFields ?? Array.Empty<CharacterSheetFieldDefinition>())
            .Select(field => new CharacterSheetFieldConfigEntryMessage
            {
                OriginalId = NormalizeExistingId(field.Id),
                Id = NormalizeExistingId(field.Id),
                Label = NormalizeText(field.Label),
                Description = NormalizeText(field.Description),
                Type = NormalizeType(field.Type),
                Optional = field.Optional,
                Options = FormatArray(field.Options),
                BindTo = NormalizeBind(field.BindTo),
                MaxLength = Math.Max(0, field.MaxLength).ToString(),
                Visibility = NormalizeVisibility(field.Visibility),
                ShowInLook = field.ShowInLook,
                EditorRows = Math.Max(0, field.EditorRows).ToString(),
                LayoutSection = ResolveLayoutSection(field),
                Width = CharacterSheetFieldWidths.Normalize(field.Width)
            })
            .ToList();
    }

    public static bool TryApplyEntries(ModConfig config, IEnumerable<CharacterSheetFieldConfigEntryMessage> entries, out List<string> errors)
    {
        errors = ValidateEntries(entries, out var fields);
        if (errors.Count > 0)
        {
            return false;
        }

        config.CharacterSheetFields = fields;
        return true;
    }

    public static List<string> ValidateEntries(IEnumerable<CharacterSheetFieldConfigEntryMessage> entries, out List<CharacterSheetFieldDefinition> fields)
    {
        fields = new List<CharacterSheetFieldDefinition>();
        var errors = new List<string>();
        var rows = (entries ?? Array.Empty<CharacterSheetFieldConfigEntryMessage>()).ToList();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var binds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rows.Count == 0)
        {
            errors.Add("At least one character sheet field must be configured.");
            return errors;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 1;
            var entry = rows[index] ?? new CharacterSheetFieldConfigEntryMessage();
            var originalId = NormalizeExistingId(entry.OriginalId);
            var submittedId = NormalizeExistingId(entry.Id);
            var label = NormalizeText(entry.Label);
            var description = NormalizeText(entry.Description);
            var type = NormalizeRawLower(entry.Type);
            var options = ParseArray(entry.Options);
            var bindTo = NormalizeText(entry.BindTo);
            var maxLength = ParseNonNegativeInt(entry.MaxLength, out var maxLengthError);
            var visibility = NormalizeRawLower(entry.Visibility);
            var editorRows = ParseNonNegativeInt(entry.EditorRows, out var editorRowsError);
            var layoutSection = NormalizeLayoutSection(entry.LayoutSection);
            var width = CharacterSheetFieldWidths.Normalize(entry.Width);
            var fieldId = ResolveFieldId(entry, label);
            var labelText = string.IsNullOrWhiteSpace(label) ? $"row {rowNumber}" : $"field '{label}'";
            var rowErrorCount = errors.Count;

            ValidateId(errors, ids, originalId, submittedId, fieldId, rowNumber, labelText);
            ValidateLabel(errors, label, labelText, rowNumber);
            ValidateDescription(errors, description, labelText);
            ValidateType(errors, type, labelText);
            ValidateOptions(errors, type, options, labelText);
            ValidateBind(errors, binds, bindTo, type, labelText);
            ValidateLength(errors, maxLength, maxLengthError, labelText);
            ValidateEditorRows(errors, type, editorRows, editorRowsError, labelText);
            ValidateVisibility(errors, visibility, labelText);
            ValidateLayout(errors, entry.LayoutSection, labelText);
            ValidateWidth(errors, entry.Width, labelText);

            if (errors.Count != rowErrorCount)
            {
                continue;
            }

            fields.Add(new CharacterSheetFieldDefinition
            {
                Id = originalId.Length > 0 ? originalId : fieldId,
                Label = label,
                Description = description,
                Type = NormalizeType(type),
                Optional = entry.Optional,
                Options = string.Equals(type, CharacterSheetFieldTypes.Option, StringComparison.OrdinalIgnoreCase) ? options : Array.Empty<string>(),
                BindTo = NormalizeBind(bindTo),
                MaxLength = maxLength,
                Visibility = NormalizeVisibility(visibility),
                ShowInLook = entry.ShowInLook,
                EditorRows = string.Equals(type, CharacterSheetFieldTypes.LongString, StringComparison.OrdinalIgnoreCase) ? Math.Clamp(editorRows, 0, MaxEditorRows) : 0,
                LayoutSection = layoutSection,
                Width = width
            });
        }

        return errors;
    }

    public static string GenerateSuggestedId(string label)
    {
        var characters = (label ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();

        var slug = new string(characters);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-");
        }

        return string.IsNullOrWhiteSpace(slug.Trim('-')) ? "field" : slug.Trim('-');
    }

    private static void ValidateId(List<string> errors, HashSet<string> ids, string originalId, string submittedId, string fieldId, int rowNumber, string labelText)
    {
        if (string.IsNullOrWhiteSpace(fieldId))
        {
            errors.Add($"Character sheet field row {rowNumber} must have a key.");
            return;
        }

        if (ContainsVtmlControlCharacters(fieldId))
        {
            errors.Add($"{labelText} key cannot contain '<' or '>'.");
        }

        if (!FieldIdPattern.IsMatch(fieldId))
        {
            errors.Add($"{labelText} key may only contain letters, numbers, dash, and underscore.");
        }

        if (!string.IsNullOrWhiteSpace(originalId) && !submittedId.Equals(originalId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{labelText} cannot rename saved field keys. Add a new field instead.");
        }

        if (!ids.Add(originalId.Length > 0 ? originalId : fieldId))
        {
            errors.Add($"Character sheet field key '{fieldId}' is duplicated.");
        }
    }

    private static void ValidateLabel(List<string> errors, string label, string labelText, int rowNumber)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            errors.Add($"Character sheet field row {rowNumber} must have a label.");
            return;
        }

        if (label.Length > MaxLabelLength)
        {
            errors.Add($"{labelText} label must be {MaxLabelLength} characters or fewer.");
        }

        if (ContainsVtmlControlCharacters(label))
        {
            errors.Add($"{labelText} label cannot contain '<' or '>'.");
        }
    }

    private static void ValidateDescription(List<string> errors, string description, string labelText)
    {
        if (description.Length > MaxDescriptionLength)
        {
            errors.Add($"{labelText} description must be {MaxDescriptionLength} characters or fewer.");
        }

        if (ContainsVtmlControlCharacters(description))
        {
            errors.Add($"{labelText} description cannot contain '<' or '>'.");
        }
    }

    private static void ValidateType(List<string> errors, string type, string labelText)
    {
        if (!SupportedTypes.Contains(type))
        {
            errors.Add($"{labelText} type must be one of: {string.Join(", ", SupportedTypes.OrderBy(value => value, StringComparer.Ordinal))}.");
        }
    }

    private static void ValidateOptions(List<string> errors, string type, string[] options, string labelText)
    {
        if (type != CharacterSheetFieldTypes.Option)
        {
            return;
        }

        if (options.Length == 0)
        {
            errors.Add($"{labelText} must have at least one option.");
            return;
        }

        if (options.Any(ContainsVtmlControlCharacters))
        {
            errors.Add($"{labelText} options cannot contain '<' or '>'.");
        }
    }

    private static void ValidateBind(List<string> errors, HashSet<string> binds, string bindTo, string type, string labelText)
    {
        if (!SupportedBindTargets.Contains(bindTo))
        {
            errors.Add($"{labelText} bind target must be blank, thebasics.fullName, or thebasics.nickname.");
            return;
        }

        if (string.IsNullOrWhiteSpace(bindTo))
        {
            return;
        }

        if (type != CharacterSheetFieldTypes.String)
        {
            errors.Add($"{labelText} bind target requires a string field.");
        }

        if (!binds.Add(bindTo))
        {
            errors.Add($"Bind target '{bindTo}' is already used by another character sheet field.");
        }
    }

    private static void ValidateLength(List<string> errors, int maxLength, string parseError, string labelText)
    {
        if (parseError != null)
        {
            errors.Add($"{labelText} max length must be a whole number.");
            return;
        }

        if (maxLength < 0 || maxLength > MaxFieldLength)
        {
            errors.Add($"{labelText} max length must be from 0 to {MaxFieldLength}.");
        }
    }

    private static void ValidateEditorRows(List<string> errors, string type, int editorRows, string parseError, string labelText)
    {
        if (parseError != null)
        {
            errors.Add($"{labelText} editor rows must be a whole number.");
            return;
        }

        if (editorRows < 0 || editorRows > MaxEditorRows)
        {
            errors.Add($"{labelText} editor rows must be from 0 to {MaxEditorRows}.");
        }

        if (editorRows > 0 && type != CharacterSheetFieldTypes.LongString)
        {
            errors.Add($"{labelText} editor rows only apply to long text fields.");
        }
    }

    private static void ValidateVisibility(List<string> errors, string visibility, string labelText)
    {
        if (!SupportedVisibilities.Contains(visibility))
        {
            errors.Add($"{labelText} visibility must be one of: {string.Join(", ", SupportedVisibilities.OrderBy(value => value, StringComparer.Ordinal))}.");
        }
    }

    private static void ValidateLayout(List<string> errors, string layoutSection, string labelText)
    {
        var normalized = NormalizeText(layoutSection).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized) && normalized != CharacterSheetLayoutSections.HeaderSide && normalized != CharacterSheetLayoutSections.Body)
        {
            errors.Add($"{labelText} layout section must be header-side or body.");
        }
    }

    private static void ValidateWidth(List<string> errors, string width, string labelText)
    {
        var normalized = NormalizeText(width).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalized) && normalized != CharacterSheetFieldWidths.Full && normalized != CharacterSheetFieldWidths.Half)
        {
            errors.Add($"{labelText} width must be full or half.");
        }
    }

    private static string ResolveFieldId(CharacterSheetFieldConfigEntryMessage entry, string label)
    {
        var originalId = NormalizeExistingId(entry.OriginalId);
        if (!string.IsNullOrWhiteSpace(originalId))
        {
            return originalId;
        }

        var id = NormalizeText(entry.Id);
        return string.IsNullOrWhiteSpace(id) ? GenerateSuggestedId(label) : id;
    }

    private static string NormalizeText(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string NormalizeExistingId(string value)
    {
        return NormalizeText(value);
    }

    private static string NormalizeRawLower(string value)
    {
        return NormalizeText(value).ToLowerInvariant();
    }

    private static string NormalizeType(string value)
    {
        var normalized = NormalizeText(value).ToLowerInvariant();
        return SupportedTypes.Contains(normalized) ? normalized : CharacterSheetFieldTypes.String;
    }

    private static string NormalizeVisibility(string value)
    {
        var normalized = NormalizeText(value).ToLowerInvariant();
        return SupportedVisibilities.Contains(normalized) ? normalized : CharacterSheetFieldVisibilities.Public;
    }

    private static string NormalizeBind(string value)
    {
        var normalized = NormalizeText(value);
        return SupportedBindTargets.Contains(normalized) ? normalized : string.Empty;
    }

    private static string NormalizeLayoutSection(string value)
    {
        return CharacterSheetLayoutSections.Normalize(value);
    }

    private static string ResolveLayoutSection(CharacterSheetFieldDefinition field)
    {
        return CharacterSheetLayoutSections.Normalize(field.LayoutSection);
    }

    private static string[] ParseArray(string value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ParseNonNegativeInt(string value, out string error)
    {
        var normalized = NormalizeText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = null;
            return 0;
        }

        if (!int.TryParse(normalized, out var parsed))
        {
            error = "invalid";
            return 0;
        }

        error = null;
        return parsed;
    }

    private static string FormatArray(IEnumerable<string> values)
    {
        return string.Join(", ", values ?? Array.Empty<string>());
    }

    private static bool ContainsVtmlControlCharacters(string value)
    {
        return (value ?? string.Empty).Contains('<') || (value ?? string.Empty).Contains('>');
    }
}
