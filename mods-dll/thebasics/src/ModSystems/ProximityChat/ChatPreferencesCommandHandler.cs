using System;
using System.Collections.Generic;
using System.Drawing;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat;

internal sealed class ChatPreferencesCommandHandler
{
    private static readonly HashSet<string> OnValues = new(StringComparer.OrdinalIgnoreCase) { "on", "true", "yes", "1" };
    private static readonly HashSet<string> OffValues = new(StringComparer.OrdinalIgnoreCase) { "off", "false", "no", "0" };

    private readonly ModConfig _config;
    private readonly System.Func<string, Language> _resolveLanguage;
    private readonly Dictionary<string, PreferenceCommand> _commands;

    public ChatPreferencesCommandHandler(ModConfig config, System.Func<string, Language> resolveLanguage)
    {
        _config = config ?? new ModConfig();
        _resolveLanguage = resolveLanguage ?? (_ => null);
        _commands = BuildCommandMap();
    }

    public TextCommandResult Handle(IServerPlayer player, string setting, string value, string extra)
    {
        var preferences = player.GetChatVisualPreferences();
        var normalizedSetting = setting?.ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedSetting) || normalizedSetting == "status")
        {
            return Success(BuildChatPreferencesStatus(preferences));
        }

        if (normalizedSetting == "help")
        {
            return Success(Lang.Get("thebasics:chatprefs-help"));
        }

        return _commands.TryGetValue(normalizedSetting, out var command)
            ? command(player, preferences, value, extra)
            : Error(Lang.Get("thebasics:chatprefs-usage"));
    }

    private Dictionary<string, PreferenceCommand> BuildCommandMap()
    {
        var commands = new Dictionary<string, PreferenceCommand>(StringComparer.OrdinalIgnoreCase);
        AddAliases(commands, SetLanguageColors, "languagecolors", "langcolors");
        AddAliases(commands, SetLanguageLabels, "labels", "languagelabels");
        AddAliases(commands, SetNicknameColors, "nickcolors", "nicknamecolors");
        AddAliases(commands, SetColorPreset, "preset");
        AddAliases(commands, SetLanguageColorOverride, "langcolor", "languagecolor");
        AddAliases(commands, SetOocColorOverride, "ooccolor");
        AddAliases(commands, SetGlobalOocColorOverride, "gooccolor", "globalooccolor");
        AddAliases(commands, SetEmoteColorOverride, "emotecolor");
        AddAliases(commands, ResetPreferences, "reset");
        return commands;
    }

    private static void AddAliases(Dictionary<string, PreferenceCommand> commands, PreferenceCommand command, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            commands[alias] = command;
        }
    }

    private static string BuildChatPreferencesStatus(ChatVisualPreferences preferences)
    {
        return Lang.Get(
            "thebasics:chatprefs-status",
            ChatHelper.OnOff(preferences.LanguageColorsEnabled),
            ChatHelper.OnOff(preferences.ShowLanguageLabels),
            preferences.ColorPreset,
            ChatHelper.OnOff(preferences.NicknameColorsEnabled));
    }

    private static TextCommandResult SetLanguageColors(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra)
    {
        return SetBooleanPreference(player, preferences, value, pref => pref.LanguageColorsEnabled, (pref, enabled) => pref.LanguageColorsEnabled = enabled, "thebasics:chatprefs-langcolor-status", "thebasics:chatprefs-langcolor-set");
    }

    private static TextCommandResult SetLanguageLabels(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra)
    {
        return SetBooleanPreference(player, preferences, value, pref => pref.ShowLanguageLabels, (pref, enabled) => pref.ShowLanguageLabels = enabled, "thebasics:chatprefs-labels-status", "thebasics:chatprefs-labels-set");
    }

    private static TextCommandResult SetNicknameColors(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra)
    {
        return SetBooleanPreference(player, preferences, value, pref => pref.NicknameColorsEnabled, (pref, enabled) => pref.NicknameColorsEnabled = enabled, "thebasics:chatprefs-nickcolors-status", "thebasics:chatprefs-nickcolors-set");
    }

    private static TextCommandResult SetBooleanPreference(
        IServerPlayer player,
        ChatVisualPreferences preferences,
        string value,
        System.Func<ChatVisualPreferences, bool> getter,
        System.Action<ChatVisualPreferences, bool> setter,
        string statusKey,
        string successKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Success(Lang.Get(statusKey, ChatHelper.OnOff(getter(preferences))));
        }

        if (!TryParseOnOff(value, out var enabled))
        {
            return Error(Lang.Get("thebasics:chatprefs-error-onoff"));
        }

        setter(preferences, enabled);
        player.SetChatVisualPreferences(preferences);
        return Success(Lang.Get(successKey, ChatHelper.OnOff(enabled)));
    }

    private static TextCommandResult SetColorPreset(IServerPlayer player, ChatVisualPreferences preferences, string preset, string extra)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return Success(Lang.Get("thebasics:chatprefs-preset-status", preferences.ColorPreset));
        }

        if (!ChatVisualPreferenceResolver.IsValidPreset(preset))
        {
            return Error(Lang.Get("thebasics:chatprefs-error-preset", GetPresetList()));
        }

        preferences.ColorPreset = ChatVisualPreferenceResolver.NormalizePreset(preset);
        player.SetChatVisualPreferences(preferences);
        return Success(Lang.Get("thebasics:chatprefs-preset-set", preferences.ColorPreset));
    }

    private TextCommandResult SetLanguageColorOverride(IServerPlayer player, ChatVisualPreferences preferences, string languageIdentifier, string colorValue)
    {
        if (!TryResolveLanguage(languageIdentifier, out var language, out var error))
        {
            return error;
        }

        if (string.IsNullOrWhiteSpace(colorValue))
        {
            return Success(Lang.Get("thebasics:chatprefs-langcolor-override-status", ChatHelper.LangIdentifier(language, player), FormatColorSetting(ChatVisualPreferenceResolver.GetLanguageColor(language, player))));
        }

        EnsureLanguageColorOverrides(preferences).RemoveAll(entry => MatchesLanguage(entry, language));
        if (!AddLanguageColorOverride(preferences, language, colorValue, out error))
        {
            return error;
        }

        player.SetChatVisualPreferences(preferences);
        return Success(Lang.Get("thebasics:chatprefs-langcolor-override-set", ChatHelper.LangIdentifier(language, player), FormatColorSetting(ChatVisualPreferenceResolver.GetLanguageColor(language, player))));
    }

    private bool TryResolveLanguage(string languageIdentifier, out Language language, out TextCommandResult error)
    {
        language = null;
        error = null;
        if (string.IsNullOrWhiteSpace(languageIdentifier))
        {
            error = Error(Lang.Get("thebasics:chatprefs-langcolor-usage"));
            return false;
        }

        language = _resolveLanguage(languageIdentifier);
        if (language == null)
        {
            error = Error(Lang.Get("thebasics:lang-error-invalid", languageIdentifier));
            return false;
        }

        return true;
    }

    private static List<ColorOverrideEntry> EnsureLanguageColorOverrides(ChatVisualPreferences preferences)
    {
        preferences.LanguageColorOverrides ??= new List<ColorOverrideEntry>();
        return preferences.LanguageColorOverrides;
    }

    private static bool MatchesLanguage(ColorOverrideEntry entry, Language language)
    {
        return string.Equals(entry?.Key, language.Name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry?.Key, language.Prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AddLanguageColorOverride(ChatVisualPreferences preferences, Language language, string colorValue, out TextCommandResult error)
    {
        error = null;
        if (string.Equals(colorValue, "default", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!TryNormalizeColor(colorValue, out var color))
        {
            error = Error(Lang.Get("thebasics:chat-error-invalid-color"));
            return false;
        }

        preferences.LanguageColorOverrides.Add(new ColorOverrideEntry { Key = language.Name, Color = color });
        return true;
    }

    private static TextCommandResult SetOocColorOverride(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra)
    {
        return SetColorOverride(player, preferences, value, pref => pref.OocColorOverride, (pref, color) => pref.OocColorOverride = color, "thebasics:chatprefs-ooccolor-status", "thebasics:chatprefs-ooccolor-set");
    }

    private TextCommandResult SetGlobalOocColorOverride(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra)
    {
        return _config.EnableGlobalOOC
            ? SetColorOverride(player, preferences, value, pref => pref.GlobalOocColorOverride, (pref, color) => pref.GlobalOocColorOverride = color, "thebasics:chatprefs-gooccolor-status", "thebasics:chatprefs-gooccolor-set")
            : Error(Lang.Get("thebasics:chatprefs-gooc-disabled"));
    }

    private static TextCommandResult SetEmoteColorOverride(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra)
    {
        return SetColorOverride(player, preferences, value, pref => pref.EmoteColorOverride, (pref, color) => pref.EmoteColorOverride = color, "thebasics:chatprefs-emotecolor-status", "thebasics:chatprefs-emotecolor-set");
    }

    private static TextCommandResult SetColorOverride(
        IServerPlayer player,
        ChatVisualPreferences preferences,
        string colorValue,
        System.Func<ChatVisualPreferences, string> getter,
        System.Action<ChatVisualPreferences, string> setter,
        string statusKey,
        string successKey)
    {
        if (string.IsNullOrWhiteSpace(colorValue))
        {
            return Success(Lang.Get(statusKey, FormatColorSetting(getter(preferences))));
        }

        if (!TryResolveColorOverride(colorValue, out var color, out var error))
        {
            return error;
        }

        setter(preferences, color);
        player.SetChatVisualPreferences(preferences);
        return Success(Lang.Get(successKey, FormatColorSetting(color)));
    }

    private static bool TryResolveColorOverride(string colorValue, out string color, out TextCommandResult error)
    {
        color = null;
        error = null;
        if (string.Equals(colorValue, "default", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryNormalizeColor(colorValue, out color))
        {
            return true;
        }

        error = Error(Lang.Get("thebasics:chat-error-invalid-color"));
        return false;
    }

    private static TextCommandResult ResetPreferences(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra)
    {
        player.ClearChatVisualPreferences();
        return Success(Lang.Get("thebasics:chatprefs-reset"));
    }

    private static string FormatColorSetting(string color)
    {
        return string.IsNullOrWhiteSpace(color) ? "default" : ChatHelper.Color(color, color);
    }

    private static bool TryNormalizeColor(string colorValue, out string color)
    {
        color = null;
        try
        {
            color = ColorToHex(ColorTranslator.FromHtml(colorValue));
            return !string.IsNullOrWhiteSpace(color) && !color.Contains('<') && !color.Contains('>');
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseOnOff(string value, out bool enabled)
    {
        var normalized = value?.Trim();
        enabled = OnValues.Contains(normalized);
        return enabled || OffValues.Contains(normalized);
    }

    private static string GetPresetList()
    {
        return string.Join(", ", ChatVisualPreferencePresets.Default, ChatVisualPreferencePresets.HighContrast, ChatVisualPreferencePresets.ColorUniversal, ChatVisualPreferencePresets.Protanopia, ChatVisualPreferencePresets.Deuteranopia, ChatVisualPreferencePresets.Tritanopia, ChatVisualPreferencePresets.Monochrome);
    }

    private static string ColorToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static TextCommandResult Success(string message)
    {
        return new TextCommandResult { Status = EnumCommandStatus.Success, StatusMessage = message };
    }

    private static TextCommandResult Error(string message)
    {
        return new TextCommandResult { Status = EnumCommandStatus.Error, StatusMessage = message };
    }

    private delegate TextCommandResult PreferenceCommand(IServerPlayer player, ChatVisualPreferences preferences, string value, string extra);
}
