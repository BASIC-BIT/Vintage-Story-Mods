using System;
using System.Linq;
using System.Collections.Generic;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Transformers;
using thebasics.Utilities;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class SetupWizardChatTextEditor
{
    internal static readonly string[] OtherWordingKeys = ["ProximityChatModeBabbleVerb", "ProseNicknameToken", "AttributeFreeformMessagesToPlayerName", "RPTextTogglePermission"];
    internal static readonly string[] Pairs = ["Bold", "Italic", "Emote", "Environmental", "PlacedEnvironmental", "OOC", "GlobalOOC", "Quote", "SignLanguageQuote"];
    private static readonly string[] PairLabels = ["Bold", "Italics", "Emotes", "Environmental text", "Placed environmental text", "Local OOC", "Global OOC", "Quotes", "Signing quotes"];
    internal static bool Supports(string page) => page is "chat.wording" or "chat.shortcuts";

    internal static double Add(GuiComposer composer, ICoreClientAPI api, SetupWizardDraft draft, string page, double width, Action<string, string> changed)
    {
        var refresh = new List<Action>();
        void Label(string text, double x, double y, double w, double h = 22) => composer.AddInteractiveElement(
            new SetupWizardViewportRichtext(api, VtmlUtils.EscapeVtml(text), CairoFont.WhiteSmallText().WithFontSize(13), ElementBounds.Fixed(x, y, w, h)));
        void Input(string key, double x, double y, double w)
        {
            var input = new SetupWizardViewportTextInput(api, ElementBounds.Fixed(x, y, w, 28), value => { changed(key, value); foreach (var update in refresh) update(); }, CairoFont.TextInput());
            input.SetMaxLength(Math.Max(2048, draft.Get(key)?.Length ?? 0));
            input.SetValue(draft.Get(key), false);
            composer.AddInteractiveElement(input, key);
        }
        double y = 0;
        if (page == "chat.wording")
        {
            var column = (width - 80) / 2;
            foreach (var mode in new[] { "Whisper", "Normal", "Yell" })
            {
                Label(mode == "Normal" ? "Talk" : mode, 0, y, width); y += 24;
                var prefixes = new[] { "ProximityChatModeVerbs.", "ProximityChatModeQuestionVerbs.", "ProximityChatModePunctuation." };
                var labels = new[] { "Speech verbs", "Question verbs", "End mark" };
                for (int i = 0; i < 3; i++)
                {
                    var cellWidth = i == 2 ? 64 : column;
                    Label(labels[i], i * (column + 8), y, cellWidth);
                    Input(prefixes[i] + mode, i * (column + 8), y + 23, cellWidth);
                }
                y += 65;
            }
            return y;
        }
        var half = (width - 12) / 2;
        for (int i = 0; i < Pairs.Length; i++)
        {
            var pair = Pairs[i];
            Label(PairLabels[i], 0, y, width); y += 23;
            Label("Start", 0, y, half); Label("End", half + 12, y, half);
            Input("ChatDelimiters." + pair + ".Start", 0, y + 22, half);
            Input("ChatDelimiters." + pair + ".End", half + 12, y + 22, half);
            foreach (var output in new[] { ("typed", y + 56), ("result", y + 98) })
            {
                string Text() => VtmlUtils.EscapeVtml(Excerpt(output.Item1 == "typed" ? "Type: " + Typed(draft, pair) : "Example: " + Result(draft, pair)));
                var example = new SetupWizardViewportRichtext(api, Text(), CairoFont.WhiteSmallText().WithFontSize(13), ElementBounds.Fixed(0, output.Item2, width, 40));
                composer.AddInteractiveElement(example, "shortcut-" + pair + "-" + output.Item1);
                refresh.Add(() => example.SetNewText(Text(), CairoFont.WhiteSmallText().WithFontSize(13), null));
            }
            y += 147;
        }
        return y;
    }

    private static string Excerpt(string value) => value.Length > 65 ? value[..62] + "..." : value;

    internal static string Typed(SetupWizardDraft draft, string pair) => draft.Get("ChatDelimiters." + pair + ".Start") + Words(pair) + draft.Get("ChatDelimiters." + pair + ".End");
    private static string Words(string pair) => pair is "Emote" ? "waves." : pair is "Environmental" or "PlacedEnvironmental" ? "The gate creaks." : "Meet me by the gate.";
    internal static string Result(SetupWizardDraft draft, string pair)
    {
        var config = new ModConfig(); config.InitializeDefaultsIfNeeded();
        foreach (var part in new[] { "Start", "End" })
        {
            var key = "ChatDelimiters." + pair + "." + part;
            ConfigAdminSettingRegistry.TryGet(key, out var definition);
            if (!definition.TrySetValue(config, draft.Get(key), out var error)) return "Invalid " + part.ToLowerInvariant() + ": " + error;
        }
        var competing = pair is "Bold" or "Italic" ? new[] { "Bold", "Italic" } :
            pair is "Quote" or "SignLanguageQuote" ? Array.Empty<string>() : new[] { "Emote", "Environmental", "PlacedEnvironmental", "OOC", "GlobalOOC" };
        if (competing.Any(other => other != pair && draft.Get("ChatDelimiters." + other + ".Start") == draft.Get("ChatDelimiters." + pair + ".Start")))
            return "Shared opening shortcut; interpretation depends on context.";
        if (pair is "Bold" or "Italic")
        {
            var errors = ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values.Where(value =>
                value.Key.StartsWith("ChatDelimiters.Bold.", StringComparison.Ordinal) ||
                value.Key.StartsWith("ChatDelimiters.Italic.", StringComparison.Ordinal)));
            if (errors.Count > 0) return errors[0];
            // AccentTransformer is pure text processing; the isolated system never starts or sends chat.
            var context = new MessageContext { Message = Typed(draft, pair) };
            var result = new AccentTransformer(new RPProximityChatSystem { Config = config }).Transform(context).Message;
            return result.Replace("<strong>", "").Replace("</strong>", "").Replace("<i>", "").Replace("</i>", "");
        }
        string Name(string key) => draft.Get(key) == "1" ? "Pip" : "PipPlayer";
        return pair switch
        {
            "Emote" => "Pip waves.",
            "Environmental" or "PlacedEnvironmental" => (draft.Get("AttributeFreeformMessagesToPlayerName") == "1" ? "[PipPlayer] " : "") + "The gate creaks.",
            "OOC" => "(OOC) " + Name("UseNicknameInOOC") + ": " + Words(pair),
            "GlobalOOC" => draft.Get("EnableGlobalOOC") == "0" || draft.Get("DisableRPChat") == "1" ? "Global OOC is off." : "(GOOC) " + Name("UseNicknameInGlobalOOC") + ": " + Words(pair),
            "Quote" or "SignLanguageQuote" => Typed(draft, pair),
            _ => Words(pair)
        };
    }

    internal static string Preview(string page, SetupWizardDraft draft)
    {
        if (page == "chat.shortcuts") return "<strong>" + VtmlUtils.EscapeVtml(Excerpt(Result(draft, "Bold"))) + "</strong><br><i>" + VtmlUtils.EscapeVtml(Excerpt(Result(draft, "Italic"))) + "</i><br>" + VtmlUtils.EscapeVtml(Excerpt(Result(draft, "Emote")));
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var errors = ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values);
        if (errors.Count > 0) return VtmlUtils.EscapeVtml(Excerpt(errors[0]));
        var lines = new[] { ProximityChatMode.Whisper, ProximityChatMode.Normal, ProximityChatMode.Yell }.Select(mode =>
        {
            // Sample the first configured choice so a stationary preview never changes at random.
            config.ProximityChatModeVerbs[mode] = [config.GetModeVerbs(mode)[0]];
            if (config.TryGetModeQuestionVerbs(mode, out var questions)) config.ProximityChatModeQuestionVerbs[mode] = [questions[0]];
            var words = "Hello" + config.GetModePunctuation(mode);
            var verb = ChatHelper.GetProximityChatVerb(null, mode, config, words);
            var question = ChatHelper.GetProximityChatVerb(null, mode, config, "Ready?");
            return VtmlUtils.EscapeVtml(Excerpt("Pip " + verb + " " + ChatHelper.WrapSpeechQuotes(words, null, config, false))) + "<br>" +
                VtmlUtils.EscapeVtml(Excerpt("Pip " + question + " " + ChatHelper.WrapSpeechQuotes("Ready?", null, config, false)));
        });
        var speech = string.Join("<br>", lines);
        config.ProseNicknameToken = VtmlUtils.EscapeVtml(Excerpt(config.ProseNicknameToken));
        config.EmoteColor = "#dddddd";
        var typed = config.ProseNicknameToken + " waves.";
        var prose = ChatHelper.FormatProseMessage(typed, null, config, false, nicknameReplacement: "Pip");
        return speech + "<br>" + VtmlUtils.EscapeVtml(Excerpt("Pip " + config.ProximityChatModeBabbleVerb + " ...")) +
            "<br>Type: " + VtmlUtils.EscapeVtml(Excerpt(draft.Get("ProseNicknameToken") + " waves.")) +
            "<br>" + (config.AttributeFreeformMessagesToPlayerName ? "[PipPlayer] " : "") + prose;

    }
}
