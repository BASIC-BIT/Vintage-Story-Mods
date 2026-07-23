using System;
using System.Collections.Generic;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.ModSystems.ProximityChat;

internal static class LanguageCatalog
{
    public static List<Language> GetAll(ModConfig config, bool allowBabble, bool includeHidden = true, bool includeSign = true)
    {
        var languages = new List<Language>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var language in config?.Languages ?? Array.Empty<Language>())
        {
            if (!IsUsableConfiguredLanguage(language, includeHidden) ||
                names.Contains(language.Name) ||
                prefixes.Contains(language.Prefix))
            {
                continue;
            }

            names.Add(language.Name);
            prefixes.Add(language.Prefix);
            languages.Add(language);
        }

        if (includeSign)
        {
            languages.Add(LanguageSystem.SignLanguage);
        }

        if (allowBabble)
        {
            languages.Add(LanguageSystem.BabbleLang);
        }

        return languages;
    }

    public static Dictionary<string, Language> GetReconciliationMap(ModConfig config)
    {
        var languagesByName = new Dictionary<string, Language>(StringComparer.OrdinalIgnoreCase);
        foreach (var language in GetAll(config, allowBabble: false))
        {
            languagesByName[language.Name] = language;
        }

        return languagesByName;
    }

    private static bool IsUsableConfiguredLanguage(Language language, bool includeHidden)
    {
        return language != null &&
               !string.IsNullOrWhiteSpace(language.Name) &&
               !string.IsNullOrWhiteSpace(language.Prefix) &&
               (includeHidden || !language.Hidden) &&
               !IsReservedBuiltIn(language);
    }

    private static bool IsReservedBuiltIn(Language language)
    {
        return IsSameNameOrPrefix(language, LanguageSystem.SignLanguage) ||
               IsSameNameOrPrefix(language, LanguageSystem.BabbleLang);
    }

    private static bool IsSameNameOrPrefix(Language language, Language builtIn)
    {
        return string.Equals(language.Name, builtIn.Name, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(language.Prefix, builtIn.Prefix, StringComparison.OrdinalIgnoreCase);
    }
}
