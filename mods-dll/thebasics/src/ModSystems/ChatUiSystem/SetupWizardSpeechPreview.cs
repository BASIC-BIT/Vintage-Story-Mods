using System;
using System.Globalization;
using System.Linq;
using Cairo;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>One sample listener, using the same delivery and text rules as nearby chat.</summary>
public static class SetupWizardSpeechPreview
{
    private const string Words = "Meet me by the gate.";
    private const double PixelsPerBlock = 2.8;
    private static readonly ModConfig Defaults = CreateDefaults();
    private static readonly ProximityChatMode[] Modes = [ProximityChatMode.Whisper, ProximityChatMode.Normal, ProximityChatMode.Yell];

    public static bool HasScene(string pageId) => pageId is "chat.ranges" or "chat.obfuscation" or "chat.signlanguage" or "chat.occlusion";
    public static bool HasMessage(string pageId) => HasScene(pageId) || pageId == "chat.language";

    public static SetupWizardActorSample[] Actors(string pageId, SetupWizardDraft draft, double time, double width, double height)
    {
        if (!HasScene(pageId) || draft == null || !double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0) return [];
        var positions = PlaceActors(Listener(time), height);
        var animationTime = double.IsFinite(time) ? Math.Max(0, time) % 60 : 0;
        return
        [
            new("pip", "Pip", "idle", animationTime, positions.Speaker.CenterX - 15, positions.Speaker.MapY - 48, 30, 48),
            new("mara", "Mara", "idle", animationTime, positions.Listener.CenterX - 15, positions.Listener.MapY - 48, 30, 48)
        ];
    }

    internal readonly record struct ListenerSample(int Phase, int X, int Z)
    {
        public int GridDistance => Math.Abs(X) + Math.Abs(Z);
        public double StraightDistance => Math.Sqrt(X * X + Z * Z);
        public bool BehindWall => Phase == 2;
    }

    internal readonly record struct ActorPlacement(double MapX, double MapY, double CenterX);

    internal static (ActorPlacement Speaker, ActorPlacement Listener) PlaceActors(ListenerSample listener, double height)
    {
        const double originX = 84;
        var originY = (height - 25) / 2;
        var listenerX = originX + listener.X * PixelsPerBlock;
        var listenerY = originY + listener.Z * PixelsPerBlock;
        var speakerCenter = originX;
        var listenerCenter = listenerX;
        if (Math.Abs(listenerY - originY) < 48 && Math.Abs(listenerX - originX) < 40)
        {
            var offset = (40 - Math.Abs(listenerX - originX)) / 2;
            var direction = listenerX < originX ? -1 : 1;
            speakerCenter -= offset * direction;
            listenerCenter += offset * direction;
        }
        return (new(originX, originY, speakerCenter), new(listenerX, listenerY, listenerCenter));
    }

    internal static ListenerSample Listener(double time)
    {
        int phase = double.IsFinite(time) ? (int)(Math.Max(0, time) % 12 / 4) : 0;
        return phase switch { 1 => new(1, 16, 12), 2 => new(2, 8, 0), _ => new(0, 3, 0) };
    }

    internal static string DistanceLabel(string pageId, ListenerSample listener) => pageId == "chat.obfuscation"
        ? listener.StraightDistance.ToString("0.#", CultureInfo.InvariantCulture) + " blocks in a straight line"
        : listener.GridDistance + " blocks away";

    internal static bool Receives(string pageId, SetupWizardDraft draft, ProximityChatMode mode, ListenerSample listener)
    {
        if (draft == null) return false;
        if (pageId == "chat.signlanguage")
            return Languages(draft) && listener.GridDistance < SignRange(draft) &&
                (!listener.BehindWall || !On(draft, "RequireLineOfSightForSignLanguage", true));
        int range = Range(draft, mode);
        if (ModConfig.IsUnlimitedRange(range)) return true;
        var penalty = pageId == "chat.occlusion" && listener.BehindWall ? Math.Max(0, Integer(draft, "SpeechOcclusionWallPenaltyBlocks", 0)) : 0;
        return listener.GridDistance + penalty < range &&
            !(pageId == "chat.occlusion" && listener.BehindWall && On(draft, "RequireClearSoundPathForSpeech." + mode, false));
    }

    internal static double ObscuredFraction(SetupWizardDraft draft, ProximityChatMode mode, ListenerSample listener, bool audible = true)
    {
        int range = Range(draft, mode);
        if (!On(draft, "EnableDistanceObfuscationSystem", true) || ModConfig.IsUnlimitedRange(range)) return 0;
        int onset = Integer(draft, "ProximityChatModeObfuscationRanges." + mode, Defaults.GetModeObfuscationRange(mode));
        double distance = listener.StraightDistance + (audible && listener.BehindWall ? Math.Max(0, Integer(draft, "SpeechOcclusionWallPenaltyBlocks", 0)) : 0);
        if (distance < onset) return 0;
        return range <= onset ? 1 : Math.Clamp((distance - onset) / (range - onset), 0, 1);
    }

    public static string Message(string pageId, SetupWizardDraft draft, double time)
    {
        if (!HasMessage(pageId) || draft == null) return string.Empty;
        if (pageId == "chat.language")
        {
            if (draft.Languages.Count == 0) return string.Empty;
            var config = new ModConfig();
            config.InitializeDefaultsIfNeeded();
            ConfigAdminSaveWorkflow.ApplyValues(config, draft.Values);
            var selected = draft.Languages.FirstOrDefault(language => !language.Default) ?? draft.Languages[0];
            var preview = SetupWizardLanguagePreview.Build(selected, config: config);
            return preview.Understood + "<br>" + preview.Unfamiliar;
        }

        var listener = Listener(time);
        if (pageId == "chat.signlanguage")
            return Receives(pageId, draft, ProximityChatMode.Normal, listener)
                ? Speech(draft, ProximityChatMode.Normal, "Pip", LanguageSystem.SignLanguage, false,
                    ObscuredFraction(draft, ProximityChatMode.Normal, listener, audible: false))
                : string.Empty;
        return string.Join("<br>", Modes.Where(mode => Receives(pageId, draft, mode, listener))
            .Select(mode => Speech(draft, mode, "Pip", null, false, ObscuredFraction(draft, mode, listener))));
    }

    private static string Speech(SetupWizardDraft draft, ProximityChatMode mode, string name, Language language, bool unknown, double obscured)
    {
        language ??= ConfiguredLanguage(draft);
        bool languages = Languages(draft) && language != null;
        string Process(string text)
        {
            if (languages && unknown) text = ChatHelper.Italic(LanguageScrambler.ScrambleMessage(text, language));
            int index = 0;
            return ChatHelper.ObfuscateOutsideMarkup(text, obscured, () => ((index++ * 37 + 17) % 101) / 101d);
        }
        string presentation = ProximityChatPresentationModes.Normalize(draft.Get("ProximityChatPresentationMode"));
        if (On(draft, "DisableRPChat", false)) name += "Player";
        if (presentation == ProximityChatPresentationModes.Prose)
            return ChatHelper.FormatProseMessage(name + " waves. \"" + Words + "\"", language, Defaults, languages, Process,
                formatQuotedText: text => ChatVisualPreferenceResolver.FormatLanguageText(text, language, null));

        string message = Process(Words);
        if (ProximityChatPresentationModes.UsesSpeechQuotes(presentation))
            message = ChatHelper.WrapSpeechQuotes(message, language, Defaults, languages);
        if (languages)
        {
            if (language == LanguageSystem.SignLanguage) message = ChatHelper.Italic(message);
            message = ChatVisualPreferenceResolver.FormatLanguageText(message, language, null);
        }
        if (presentation is ProximityChatPresentationModes.SimpleSpeech or ProximityChatPresentationModes.PlainProximity)
            return name + ": " + message;
        string verb = language == LanguageSystem.SignLanguage && languages ? "signs" : mode switch
        { ProximityChatMode.Whisper => "whispers", ProximityChatMode.Yell => "yells", _ => "says" };
        return name + " " + verb + " " + message;
    }

    private static Language ConfiguredLanguage(SetupWizardDraft draft)
    {
        foreach (var entry in draft.Languages.Where(entry => entry != null).OrderByDescending(entry => entry.Default))
        {
            var errors = LanguageConfigAdmin.ValidateEntries([entry with { Default = true }], out var languages);
            if (errors.Count == 0) return languages[0];
        }
        return null;
    }

    public static void Draw(Context context, ImageSurface surface, ElementBounds bounds, string pageId, SetupWizardDraft draft, double time)
    {
        if (!HasScene(pageId) || draft == null || surface.Width <= 0 || surface.Height <= 0) return;
        double width = 310, height = surface.Height / (surface.Width / width);
        var listener = Listener(time);
        var positions = PlaceActors(listener, height);
        double originX = positions.Speaker.MapX, originY = positions.Speaker.MapY;
        double listenerX = positions.Listener.MapX, listenerY = positions.Listener.MapY;
        bool received = pageId == "chat.signlanguage"
            ? Receives(pageId, draft, ProximityChatMode.Normal, listener)
            : Modes.Any(mode => Receives(pageId, draft, mode, listener));
        context.Save();
        try
        {
            context.Scale(surface.Width / width, surface.Width / width);
            context.Rectangle(0, 0, width, height);
            context.Clip();
            context.SetSourceRGB(0.19, 0.23, 0.19);
            context.Paint();
            context.SetSourceRGBA(0.7, 0.76, 0.62, 0.08);
            context.LineWidth = 0.6;
            for (double x = originX % 28; x < width; x += 28) { context.MoveTo(x, 0); context.LineTo(x, height - 25); }
            for (double y = originY % 28; y < height - 25; y += 28) { context.MoveTo(0, y); context.LineTo(width, y); }
            context.Stroke();
            context.Save();
            context.Rectangle(0, 0, width, height - 25);
            context.Clip();
            int range = pageId == "chat.signlanguage" ? SignRange(draft) : Range(draft, ProximityChatMode.Normal);
            if (range > 0)
            {
                double radius = Math.Min(512, range - 1) * PixelsPerBlock;
                context.SetSourceRGBA(0.79, 0.74, 0.48, 0.65);
                context.LineWidth = 1.1;
                context.MoveTo(originX, originY - radius); context.LineTo(originX + radius, originY);
                context.LineTo(originX, originY + radius); context.LineTo(originX - radius, originY);
                context.ClosePath(); context.Stroke();
            }
            if (pageId == "chat.obfuscation" && On(draft, "EnableDistanceObfuscationSystem", true) && range > 0)
            {
                int onset = Integer(draft, "ProximityChatModeObfuscationRanges.Normal", Defaults.GetModeObfuscationRange(ProximityChatMode.Normal));
                context.SetSourceRGBA(0.63, 0.83, 0.81, 0.75);
                context.SetDash([3, 4], 0);
                context.Arc(originX, originY, Math.Clamp(onset, 0, 512) * PixelsPerBlock, 0, Math.PI * 2);
                context.Stroke(); context.SetDash([], 0);
            }
            if (pageId == "chat.signlanguage" || pageId == "chat.occlusion" && listener.BehindWall)
            {
                context.SetSourceRGB(0.11, 0.13, 0.12);
                context.Rectangle(originX + 15, originY - 32, 6, 43); context.Fill();
                context.SetSourceRGB(0.49, 0.48, 0.41);
                context.Rectangle(originX + 13, originY - 33, 6, 42); context.Fill();
                context.SetSourceRGBA(0.18, 0.19, 0.16, 0.7);
                context.LineWidth = 0.7;
                for (int y = -5; y < 9; y += 5) { context.MoveTo(originX + 13, originY + y); context.LineTo(originX + 19, originY + y); }
                context.MoveTo(originX + 16, originY - 9); context.LineTo(originX + 16, originY - 5);
                context.MoveTo(originX + 16, originY); context.LineTo(originX + 16, originY + 5);
                context.Stroke();
            }
            context.SetSourceRGBA(received ? 0.61 : 0.86, received ? 0.85 : 0.49, received ? 0.79 : 0.39, 0.9);
            context.LineWidth = 1;
            context.SetDash([2, 3], 0);
            context.MoveTo(originX, originY); context.LineTo(listenerX, listenerY); context.Stroke(); context.SetDash([], 0);
            foreach (var actor in new[] { positions.Speaker, positions.Listener })
            {
                context.MoveTo(actor.CenterX, actor.MapY); context.LineTo(actor.MapX, actor.MapY); context.Stroke();
                context.Arc(actor.MapX, actor.MapY, 2, 0, Math.PI * 2); context.Fill();
            }
            context.SetSourceRGBA(0.96, 0.93, 0.83, 0.9);
            context.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Normal);
            context.SetFontSize(11);
            context.MoveTo(positions.Speaker.CenterX - context.TextExtents("Pip").Width / 2, originY + 15); context.ShowText("Pip");
            context.MoveTo(positions.Listener.CenterX - context.TextExtents("You").Width / 2, listenerY + 15); context.ShowText("You");
            context.Restore();
            context.SetSourceRGBA(0.08, 0.1, 0.08, 0.85);
            context.Rectangle(0, height - 25, width, 25); context.Fill();
            context.SetSourceRGBA(0.91, 0.88, 0.77, 1);
            context.SelectFontFace("sans-serif", FontSlant.Normal, FontWeight.Normal);
            context.SetFontSize(11);
            context.MoveTo(10, height - 8); context.ShowText(DistanceLabel(pageId, listener));
            context.MoveTo(216, height - 9); context.LineTo(244, height - 9); context.Stroke();
            context.MoveTo(250, height - 6); context.ShowText("10 blocks");
        }
        finally { context.Restore(); }
    }

    private static bool Languages(SetupWizardDraft draft) => On(draft, "EnableLanguageSystem", true) && !On(draft, "DisableRPChat", false);
    private static int SignRange(SetupWizardDraft draft)
    {
        int range = Integer(draft, "SignLanguageRange", Defaults.GetSignLanguageRange());
        return range < 0 ? Defaults.GetSignLanguageRange() : range;
    }
    private static int Range(SetupWizardDraft draft, ProximityChatMode mode) => Integer(draft, "ProximityChatModeDistances." + mode, Defaults.GetModeDistance(mode));
    private static int Integer(SetupWizardDraft draft, string key, int fallback) => int.TryParse(draft.Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
    private static bool On(SetupWizardDraft draft, string key, bool fallback) => ConfigAdminSettingDefinition.TryParseBool(draft.Get(key), out bool value) ? value : fallback;
    private static ModConfig CreateDefaults() { var config = new ModConfig(); config.InitializeDefaultsIfNeeded(); return config; }
}
