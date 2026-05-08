using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Server;

namespace thebasics.Utilities;

public static class ChatVisualPreferenceResolver
{
    private static readonly Dictionary<string, string[]> PresetLanguagePalettes = new(StringComparer.OrdinalIgnoreCase)
    {
        [ChatVisualPreferencePresets.HighContrast] = ["#FFFFFF", "#FFD700", "#00FFFF", "#FF99FF", "#00FF66", "#FF6666"],
        [ChatVisualPreferencePresets.ColorUniversal] = ["#E69F00", "#56B4E9", "#009E73", "#F0E442", "#0072B2", "#D55E00", "#CC79A7"],
        [ChatVisualPreferencePresets.Protanopia] = ["#0072B2", "#F0E442", "#56B4E9", "#CC79A7", "#999999"],
        [ChatVisualPreferencePresets.Deuteranopia] = ["#0072B2", "#F0E442", "#D55E00", "#CC79A7", "#999999"],
        [ChatVisualPreferencePresets.Tritanopia] = ["#D55E00", "#009E73", "#CC79A7", "#E69F00", "#999999"],
        [ChatVisualPreferencePresets.Monochrome] = ["#FFFFFF"],
    };

    public static string NormalizePreset(string preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return ChatVisualPreferencePresets.Default;
        }

        var normalized = preset.Trim().ToLowerInvariant();
        return IsValidPreset(normalized) ? normalized : ChatVisualPreferencePresets.Default;
    }

    public static bool IsValidPreset(string preset)
    {
        preset = preset?.Trim().ToLowerInvariant();
        return preset is ChatVisualPreferencePresets.Default
            or ChatVisualPreferencePresets.HighContrast
            or ChatVisualPreferencePresets.ColorUniversal
            or ChatVisualPreferencePresets.Protanopia
            or ChatVisualPreferencePresets.Deuteranopia
            or ChatVisualPreferencePresets.Tritanopia
            or ChatVisualPreferencePresets.Monochrome;
    }

    public static string GetLanguageColor(Language language, IServerPlayer recipient)
    {
        if (language == null)
        {
            return null;
        }

        if (recipient == null)
        {
            return language.Color;
        }

        return GetLanguageColor(language, recipient.GetChatVisualPreferences());
    }

    public static string GetLanguageColor(Language language, ChatVisualPreferences preferences)
    {
        if (language == null)
        {
            return null;
        }

        preferences ??= new ChatVisualPreferences();
        preferences.LanguageColorOverrides ??= [];
        var overrideColor = GetLanguageOverride(preferences, language);
        if (!string.IsNullOrWhiteSpace(overrideColor))
        {
            return overrideColor;
        }

        var preset = NormalizePreset(preferences.ColorPreset);
        if (preset == ChatVisualPreferencePresets.Default)
        {
            return language.Color;
        }

        if (!PresetLanguagePalettes.TryGetValue(preset, out var palette) || palette.Length == 0)
        {
            return language.Color;
        }

        return palette[StableIndex(language.Name ?? language.Prefix ?? string.Empty, palette.Length)];
    }

    public static string FormatLanguageText(string message, Language language, IServerPlayer recipient)
    {
        if (string.IsNullOrEmpty(message) || language == null)
        {
            return message;
        }

        var preferences = recipient?.GetChatVisualPreferences();

        if (preferences?.ShowLanguageLabels == true)
        {
            message = $"[{ChatHelper.EscapeMarkup(language.Name)}] {message}";
        }

        if (recipient == null)
        {
            return ChatHelper.Color(message, language.Color);
        }

        if (preferences.LanguageColorsEnabled)
        {
            message = ChatHelper.Color(message, GetLanguageColor(language, preferences));
        }

        return message;
    }

    public static string GetEmoteColor(IServerPlayer recipient, ModConfig config)
    {
        return FirstNonEmpty(recipient?.GetChatVisualPreferences().EmoteColorOverride, config.EmoteColor);
    }

    public static string GetOocColor(IServerPlayer recipient, ModConfig config)
    {
        return FirstNonEmpty(recipient?.GetChatVisualPreferences().OocColorOverride, config.OOCColor);
    }

    public static string GetGlobalOocColor(IServerPlayer recipient, ModConfig config)
    {
        return FirstNonEmpty(recipient?.GetChatVisualPreferences().GlobalOocColorOverride, config.GlobalOOCColor);
    }

    private static string GetLanguageOverride(ChatVisualPreferences preferences, Language language)
    {
        var overrides = preferences.LanguageColorOverrides;
        if (overrides == null || overrides.Count == 0)
        {
            return null;
        }

        return overrides
            .FirstOrDefault(entry => IsLanguageKey(entry?.Key, language))
            ?.Color;
    }

    private static bool IsLanguageKey(string key, Language language)
    {
        return !string.IsNullOrWhiteSpace(key) &&
            (string.Equals(key, language.Name, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(key, language.Prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string FirstNonEmpty(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static int StableIndex(string value, int count)
    {
        unchecked
        {
            var hash = 23;
            foreach (var ch in value)
            {
                hash = hash * 31 + char.ToLowerInvariant(ch);
            }

            return (hash & 0x7FFFFFFF) % count;
        }
    }
}
