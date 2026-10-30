using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat;
using thebasics.Utilities;

namespace thebasics.ModSystems.ChatUiSystem;

public static class SetupWizardLanguagePreview
{
    private static readonly ModConfig Defaults = CreateDefaults();

    public sealed record Sample(string Understood, string Unfamiliar, IReadOnlyList<string> Errors)
    {
        public bool IsValid => Errors.Count == 0;
    }

    public static Sample Build(LanguageConfigEntryMessage entry, string sampleText = "Meet me by the gate.", ModConfig config = null)
    {
        // A row can be previewed without being the collection's default language.
        var errors = LanguageConfigAdmin.ValidateEntries([entry == null ? null : entry with { Default = true }], out var languages);
        if (errors.Count > 0)
            return new(string.Empty, string.Empty, errors.Select(VtmlUtils.EscapeVtml).ToArray());

        config ??= Defaults;
        var language = languages[0];
        var enabled = config.EnableLanguageSystem && !config.DisableRPChat;
        var mode = ProximityChatPresentationModes.Normalize(config.ProximityChatPresentationMode);
        var name = config.DisableRPChat ? "PipPlayer" : "Pip";

        string Format(bool unfamiliar)
        {
            var words = sampleText ?? string.Empty;
            if (enabled && unfamiliar) words = LanguageScrambler.ScrambleMessage(words, language);
            words = VtmlUtils.EscapeVtml(words);
            if (enabled && unfamiliar && words.Length > 0) words = ChatHelper.Italic(words);
            if (ProximityChatPresentationModes.UsesSpeechQuotes(mode) || mode == ProximityChatPresentationModes.Prose)
                words = ChatHelper.WrapSpeechQuotes(words, language, config, enabled);
            if (enabled) words = ChatVisualPreferenceResolver.FormatLanguageText(words, language, null);
            return mode switch
            {
                ProximityChatPresentationModes.SimpleSpeech or ProximityChatPresentationModes.PlainProximity => name + ": " + words,
                ProximityChatPresentationModes.Prose => ChatHelper.FormatProseMessage(name + " waves.", language, config, enabled) + " " + words,
                _ => name + " says " + words
            };
        }

        return new(Format(false), Format(true), System.Array.Empty<string>());
    }

    private static ModConfig CreateDefaults()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }
}
