using System;
using System.Globalization;
using Cairo;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Local overhead artwork and timed samples. Reads draft values, never world state.</summary>
public static class SetupWizardAppearancePreview
{
    private const string Words = "Hello, traveler!";
    private const double Width = 310, Height = 270;
    // The live dialog places its detached, interactive actor inside these logical bounds.
    public const double ActorX = 112, ActorY = 111, ActorWidth = 86, ActorHeight = 110;

    internal readonly record struct NameSample(double Distance, bool Targeted, bool LineOfSight);

    public static bool Supports(string pageId) => pageId is
        "appearance.typing" or "appearance.bubbles" or "appearance.names" or "appearance.nametagcolors";

    public static double DurationSeconds(string pageId, SetupWizardDraft draft) => draft == null ? 0 : pageId switch
    {
        "appearance.typing" => TypingTimeout(draft) + 3,
        "appearance.bubbles" => BubbleDuration(draft) + 1,
        "appearance.names" => 12,
        "appearance.nametagcolors" => 4,
        _ => 0
    };

    public static ChatTypingIndicatorState TypingState(SetupWizardDraft draft, double time)
    {
        if (draft == null || !On(draft, "EnableTypingIndicator", true) || Number(draft, "TypingIndicatorMaxRange", 30) <= 0)
            return ChatTypingIndicatorState.None;
        if (Time(time) >= TypingTimeout(draft) + 2) return ChatTypingIndicatorState.None;
        return Time(time) <= TypingTimeout(draft) ? ChatTypingIndicatorState.Typing : ChatTypingIndicatorState.ChatOpenComposing;
    }

    public static string TypingLabel(SetupWizardDraft draft, double time)
    {
        var state = TypingState(draft, time);
        if (state == ChatTypingIndicatorState.None) return string.Empty;
        var mode = draft.Get("TypingIndicatorDisplayMode") switch
        { "Text" or "1" => TypingIndicatorDisplayMode.Text, "Icon" or "0" => TypingIndicatorDisplayMode.Icon, _ => TypingIndicatorDisplayMode.Both };
        var text = draft.Get("TypingIndicatorTextOverride");
        if (string.IsNullOrWhiteSpace(text)) text = Lang.Get("thebasics:typingindicator-typing-text");
        return TypingIndicatorRenderer.BuildLabel(state, mode, text);
    }

    public static double BubbleDuration(SetupWizardDraft draft)
    {
        if (draft == null || BubbleMode(draft) == OverheadChatBubbleModes.Off) return 0;
        var text = VtmlUtils.StripVtmlTags(BubbleText(draft));
        int minimum = CustomBubble(draft) ? (int)Number(draft, "SpeechBubbleMinimumDisplayMilliseconds", 3500) : 0;
        return SpeechBubbleVtmlPatches.EffectiveDurationMilliseconds(text.Length, minimum) / 1000d;
    }

    public static bool BubbleVisible(SetupWizardDraft draft, double time) =>
        draft != null && BubbleMode(draft) != OverheadChatBubbleModes.Off && Time(time) <= BubbleDuration(draft);

    public static string Name(SetupWizardDraft draft)
    {
        if (draft == null) return string.Empty;
        bool nickname = On(draft, "ShowNicknameInNametag", true), account = On(draft, "ShowPlayerNameInNametag", true);
        return nickname ? account ? "Pip (PipPlayer)" : "Pip" : account ? "PipPlayer" : string.Empty;
    }

    public static bool NameplateVisible(SetupWizardDraft draft, double distance, bool targeted, bool lineOfSight) =>
        Name(draft).Length > 0 && double.IsFinite(distance) && distance >= 0 &&
        distance < Math.Max(0, Number(draft, "NametagRenderRange", 30)) &&
        (!On(draft, "HideNametagUnlessTargeting") || targeted) &&
        (!On(draft, "NametagRequiresLineOfSight", true) || lineOfSight);

    internal static NameSample NameScene(SetupWizardDraft draft, double time)
    {
        double range = Math.Max(0, Number(draft, "NametagRenderRange", 30));
        double near = range > 0 ? Math.Min(8, range / 2) : 8;
        return (int)(Time(time) % 12 / 3) switch
        { 1 => new(near, false, true), 2 => new(range + 5, true, true), 3 => new(near, true, false), _ => new(near, true, true) };
    }

    public static string Message(string pageId, SetupWizardDraft draft, double time)
    {
        if (!Supports(pageId) || draft == null) return string.Empty;
        if (pageId == "appearance.typing") return TypingState(draft, time) switch
        {
            ChatTypingIndicatorState.None => Time(time) >= TypingTimeout(draft) + 2 ? "Chat closed" : "Typing indicators off",
            ChatTypingIndicatorState.Typing => "Typing...", _ => "Thinking..."
        };
        if (pageId == "appearance.bubbles") return BubbleMode(draft) == OverheadChatBubbleModes.Off ? "Speech bubbles off" :
            BubbleVisible(draft, time) ? "Message sent" : "Message expired";
        if (pageId == "appearance.nametagcolors") return Name(draft);
        var scene = NameScene(draft, time);
        return Format(scene.Distance) + " blocks, " + (!scene.LineOfSight ? "behind a wall" : scene.Targeted ? "targeted" : "looking away");
    }

    public static void Draw(Context context, ImageSurface surface, ElementBounds bounds, string pageId, SetupWizardDraft draft,
        double time, ICoreClientAPI api = null)
    {
        if (!Supports(pageId) || draft == null || surface.Width <= 0 || surface.Height <= 0) return;
        double scale = Math.Min(surface.Width / Width, surface.Height / Height);
        context.Save();
        try
        {
            context.Rectangle(0, 0, surface.Width, surface.Height); context.Clip();
            context.Translate((surface.Width - Width * scale) / 2, (surface.Height - Height * scale) / 2);
            context.Scale(scale, scale);
            Ground(context);
            if (pageId == "appearance.typing")
            {
                using var indicator = TypingSurface(api, draft, time);
                Paint(context, indicator, 22, 66, 266, 55);
                Caption(context, TypingState(draft, time) == ChatTypingIndicatorState.None ? Message(pageId, draft, time) :
                    Format(Time(time)) + " s since last keypress", 247);
            }
            else if (pageId == "appearance.bubbles")
            {
                if (BubbleVisible(draft, time))
                {
                    using var bubble = BubbleSurface(api, draft);
                    Paint(context, bubble, 12, 51, 286, 72);
                }
                Caption(context, BubbleMode(draft) == OverheadChatBubbleModes.Off ? "Off" :
                    Format(Math.Max(0, BubbleDuration(draft) - Time(time))) + " s", 247);
            }
            else
            {
                bool colors = pageId == "appearance.nametagcolors";
                var scene = colors ? new NameSample(8, true, true) : NameScene(draft, time);
                if (colors || NameplateVisible(draft, scene.Distance, scene.Targeted, scene.LineOfSight))
                {
                    using var plate = NameplateSurface(api, draft);
                    Paint(context, plate, 12, 66, 286, 55);
                }
                if (!colors)
                {
                    Target(context, scene.Targeted ? 155 : 227, 153);
                    if (!scene.LineOfSight) Wall(context);
                    Caption(context, Format(scene.Distance) + " blocks", 247);
                }
            }
        }
        finally { context.Restore(); }
    }

    private static bool CustomBubble(SetupWizardDraft draft) =>
        BubbleMode(draft) == OverheadChatBubbleModes.RpText && !On(draft, "DisableRPChat");
    private static string BubbleMode(SetupWizardDraft draft) => OverheadChatBubbleModes.Normalize(draft.Get("OverheadChatBubbleMode"));
    private static string BubbleText(SetupWizardDraft draft)
    {
        if (!CustomBubble(draft)) return Words;
        string text = ProximityChatPresentationModes.UsesSpeechQuotes(draft.Get("ProximityChatPresentationMode")) ? "\"" + Words + "\"" : Words;
        // shortcut: one styled span avoids fractional-scale inline overlap; expand after the shared renderer is fixed.
        return "<i>" + text + "</i>";
    }

    internal static ImageSurface BubbleSurface(ICoreClientAPI api, SetupWizardDraft draft)
    {
        var background = SpeechBubbleVtmlPatches.GetBubbleBackground(null);
        if (!CustomBubble(draft)) background.Padding = 3;
        return RichTextTextureUtils.GenRichTextSurface(api, BubbleText(draft),
            SpeechBubbleVtmlPatches.CreateBubbleFont(null), 350, background);
    }

    internal static ImageSurface NameplateSurface(ICoreClientAPI api, SetupWizardDraft draft) =>
        RichTextTextureUtils.GenRichTextSurface(api, Name(draft), EntityBehaviorNameTagPatches.CreateNametagBaseFont(), 400,
            EntityBehaviorNameTagPatches.CreateNametagBackground(
                Color(draft.Get("NametagBackgroundColor"), GuiStyle.DialogLightBgColor),
                Color(draft.Get("NametagBorderColor"), GuiStyle.DialogBorderColor)));

    internal static ImageSurface TypingSurface(ICoreClientAPI api, SetupWizardDraft draft, double time)
    {
        string label = TypingLabel(draft, time);
        var font = TypingIndicatorRenderer.CreateFont();
        int width = 180;
        if (!label.Contains('<'))
            width = Math.Max(width, (int)Math.Ceiling(font.GetTextExtents(label).Width / GuiScale) + 4);
        return RichTextTextureUtils.GenRichTextSurface(api, label, font, width, TypingIndicatorRenderer.CreateBackground());
    }

    private static void Paint(Context context, ImageSurface picture, double x, double y, double width, double height)
    {
        if (picture == null) return;
        var scale = Math.Min(1 / GuiScale, Math.Min(width / picture.Width, height / picture.Height));
        context.Save();
        context.Translate(x + (width - picture.Width * scale) / 2, y + (height - picture.Height * scale) / 2);
        context.Scale(scale, scale); context.SetSourceSurface(picture, 0, 0); context.Paint(); context.Restore();
    }

    private static void Ground(Context c)
    {
        c.Save(); c.Translate(155, 219); c.Scale(1, .3); c.SetSourceRGBA(.16, .20, .15, .75);
        c.Arc(0, 0, 97, 0, Math.PI * 2); c.Fill(); c.Restore();
        c.SetSourceRGBA(.62, .66, .43, .35); c.LineWidth = 1;
        for (int x = 66; x < 252; x += 28) { c.MoveTo(x, 221); c.LineTo(x + 10, 218); }
        c.Stroke();
    }

    private static void Target(Context c, double x, double y)
    {
        c.SetSourceRGBA(.9, .85, .65, .85); c.LineWidth = 1;
        for (int side = -1; side <= 1; side += 2)
        {
            c.MoveTo(x + side * 8, y); c.LineTo(x + side * 3, y);
            c.MoveTo(x, y + side * 8); c.LineTo(x, y + side * 3);
        }
        c.Stroke();
    }

    private static void Wall(Context c)
    {
        c.SetSourceRGBA(.72, .48, .35, .8); c.SetDash([3, 4], 0); c.LineWidth = 1;
        c.MoveTo(34, 199); c.LineTo(155, 153); c.Stroke(); c.SetDash([], 0);
        c.SetSourceRGBA(.36, .35, .29, 1); c.Rectangle(78, 149, 28, 68); c.Fill();
        c.SetSourceRGBA(.65, .61, .49, 1); c.Rectangle(75, 145, 27, 68); c.Fill();
        c.SetSourceRGBA(.33, .32, .27, .8); c.LineWidth = 1;
        for (int y = 159; y < 212; y += 14) { c.MoveTo(75, y); c.LineTo(102, y); c.MoveTo(y % 28 == 19 ? 83 : 94, y - 14); c.RelLineTo(0, 14); }
        c.Stroke();
    }

    private static void Caption(Context c, string text, double y)
    {
        c.SelectFontFace(GuiStyle.StandardFontName, FontSlant.Normal, FontWeight.Normal); c.SetFontSize(12);
        c.SetSourceRGBA(.9, .86, .74, 1); c.MoveTo((Width - c.TextExtents(text).Width) / 2, y); c.ShowText(text);
    }
    private static double[] Color(string text, double[] fallback)
    {
        text = text?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length is not (7 or 9) || text[0] != '#' ||
            !uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) return fallback;
        return Vintagestory.API.MathTools.ColorUtil.Hex2Doubles(text);
    }
    private static double TypingTimeout(SetupWizardDraft draft) { var seconds = Number(draft, "TypingIndicatorTimeoutSeconds", 5); return seconds <= 0 ? 5 : seconds; }
    private static bool On(SetupWizardDraft draft, string key, bool fallback = false) =>
        ConfigAdminSettingDefinition.TryParseBool(draft.Get(key), out var on) ? on : fallback;
    private static double Number(SetupWizardDraft draft, string key, double fallback) =>
        double.TryParse(draft.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? Math.Clamp(number, 0, 60000) : fallback;
    private static double Time(double time) => double.IsFinite(time) ? Math.Max(0, time) : 0;
    private static double GuiScale => RuntimeEnv.GUIScale > 0 ? RuntimeEnv.GUIScale : 1;
    private static string Format(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}
