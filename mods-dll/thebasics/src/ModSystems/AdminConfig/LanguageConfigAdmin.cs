using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.AdminConfig;

public static class LanguageConfigAdmin
{
    private static readonly Regex PrefixPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
    private static readonly Regex HexColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private static readonly string[] ReservedNames = [LanguageSystem.BabbleLang.Name, LanguageSystem.SignLanguage.Name];
    private static readonly string[] ReservedPrefixes = [LanguageSystem.BabbleLang.Prefix, LanguageSystem.SignLanguage.Prefix];

    public static List<LanguageConfigEntryMessage> BuildEntries(ModConfig config)
    {
        return (config?.Languages ?? Array.Empty<Language>())
            .Select(language => new LanguageConfigEntryMessage
            {
                OriginalName = language.Name,
                Name = language.Name,
                Description = language.Description,
                Prefix = language.Prefix,
                Syllables = FormatArray(language.Syllables),
                Color = language.Color,
                Default = language.Default,
                Hidden = language.Hidden,
                GrantedToClasses = FormatArray(language.GrantedToClasses),
                GrantedToModels = FormatArray(language.GrantedToModels),
                GrantedToModelGroups = FormatArray(language.GrantedToModelGroups),
                GrantedToTraits = FormatArray(language.GrantedToTraits)
            })
            .ToList();
    }

    public static bool TryApplyEntries(ModConfig config, IEnumerable<LanguageConfigEntryMessage> entries, out List<string> errors)
    {
        errors = ValidateEntries(entries, out var languages);
        if (errors.Count > 0)
        {
            return false;
        }

        config.Languages = languages;
        return true;
    }

    public static List<string> ValidateEntries(IEnumerable<LanguageConfigEntryMessage> entries, out List<Language> languages)
    {
        languages = new List<Language>();
        var errors = new List<string>();
        var rows = (entries ?? Array.Empty<LanguageConfigEntryMessage>()).ToList();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rows.Count == 0)
        {
            errors.Add("At least one language must be configured.");
            return errors;
        }

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 1;
            var entry = rows[index] ?? new LanguageConfigEntryMessage();
            var name = NormalizeText(entry.Name);
            var description = NormalizeText(entry.Description);
            var prefix = NormalizeText(entry.Prefix);
            var color = NormalizeText(entry.Color);
            var syllables = ParseArray(entry.Syllables);
            var classGrants = ParseArray(entry.GrantedToClasses);
            var modelGrants = ParseArray(entry.GrantedToModels);
            var modelGroupGrants = ParseArray(entry.GrantedToModelGroups);
            var traitGrants = ParseArray(entry.GrantedToTraits);
            var label = string.IsNullOrWhiteSpace(name) ? $"row {rowNumber}" : $"language '{name}'";

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add($"Language row {rowNumber} must have a name.");
            }
            else
            {
                if (ContainsVtmlControlCharacters(name))
                {
                    errors.Add($"{label} name cannot contain '<' or '>'.");
                }

                if (ReservedNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"{label} uses a reserved language name.");
                }

                if (!names.Add(name))
                {
                    errors.Add($"Language name '{name}' is duplicated.");
                }
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                errors.Add($"{label} must have a description.");
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                errors.Add($"{label} must have a prefix.");
            }
            else
            {
                if (!PrefixPattern.IsMatch(prefix))
                {
                    errors.Add($"{label} prefix may only contain letters, numbers, and underscore.");
                }

                if (ReservedPrefixes.Contains(prefix, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"{label} uses a reserved language prefix.");
                }

                if (!prefixes.Add(prefix))
                {
                    errors.Add($"Language prefix '{prefix}' is duplicated.");
                }
            }

            if (!HexColorPattern.IsMatch(color))
            {
                errors.Add($"{label} color must be a hex color like #E9DDCE.");
            }

            if (syllables.Length == 0)
            {
                errors.Add($"{label} must have at least one syllable.");
            }
            else if (syllables.Any(ContainsVtmlControlCharacters))
            {
                errors.Add($"{label} syllables cannot contain '<' or '>'.");
            }

            languages.Add(new Language(
                name,
                description,
                prefix,
                syllables,
                color,
                entry.Default,
                entry.Hidden,
                classGrants,
                modelGrants,
                modelGroupGrants,
                traitGrants));
        }

        if (!languages.Any(language => language.Default))
        {
            errors.Add("At least one language must be marked as a default language.");
        }

        return errors;
    }

    public static IReadOnlyDictionary<string, string> BuildRenameMap(IEnumerable<LanguageConfigEntryMessage> entries)
    {
        return (entries ?? Array.Empty<LanguageConfigEntryMessage>())
            .Where(entry => !string.IsNullOrWhiteSpace(entry?.OriginalName) && !string.IsNullOrWhiteSpace(entry.Name))
            .Select(entry => new
            {
                Original = entry.OriginalName.Trim(),
                Current = entry.Name.Trim()
            })
            .Where(entry => !entry.Original.Equals(entry.Current, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Original, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Current, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeText(string value)
    {
        return (value ?? string.Empty).Trim();
    }

    private static string[] ParseArray(string value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
