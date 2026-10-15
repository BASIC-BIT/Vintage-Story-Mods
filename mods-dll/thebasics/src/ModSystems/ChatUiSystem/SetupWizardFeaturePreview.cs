using System;
using System.Globalization;
using Cairo;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Illustrative samples of draft choices, without reading or changing world state.</summary>
public static class SetupWizardFeaturePreview
{
    private const double Width = 310;
    private const double Height = 220;

    public static bool Supports(string pageId) => pageId is
        "appearance.bubbles" or "appearance.typing" or "appearance.names" or "appearance.nametagcolors" or
        "map.visibility" or "characters.sheets" or "characters.slots" or
        "roleplay.dice" or "roleplay.spectators" or
        "server.history" or "server.staffnotes" or "server.personalnotes" or "server.stats";

    public static bool IsSettingsOnly(string pageId) => pageId is "characters.access" or "roleplay.scenemarkers" or
        "roleplay.nicknamechanges" or "roleplay.nicknamerules" or "server.retention" or "server.notelimits";

    public static void Draw(Context context, ImageSurface surface, ElementBounds bounds, string pageId, SetupWizardDraft draft, ICoreClientAPI api = null)
    {
        if (!Supports(pageId) || draft == null || surface.Width <= 0 || surface.Height <= 0) return;
        bool appearance = pageId.StartsWith("appearance.", StringComparison.Ordinal);
        double height = appearance ? 60 : pageId == "map.visibility" ? 270 : Height;
        double scale = Math.Min(surface.Width / Width, surface.Height / height);
        context.Save();
        try
        {
            context.Rectangle(0, 0, surface.Width, surface.Height);
            context.Clip();
            context.Translate((surface.Width - Width * scale) / 2, (surface.Height - height * scale) / 2);
            context.Scale(scale, scale);
            if (appearance) DrawAppearance(context, pageId, draft);
            else if (pageId == "map.visibility") SetupWizardMapPreview.Draw(context, draft, api?.Gui.Icons);
            else
            {
                Box(context, 8, 8, 294, 204);
                if (pageId.StartsWith("characters.", StringComparison.Ordinal)) DrawCharacter(context, pageId, draft);
                else if (pageId.StartsWith("roleplay.", StringComparison.Ordinal)) DrawRoleplay(context, pageId, draft);
                else DrawRecords(context, pageId, draft);
            }
        }
        finally { context.Restore(); }
    }

    private static void DrawAppearance(Context context, string pageId, SetupWizardDraft draft)
    {
        if (pageId.Contains("typing", StringComparison.Ordinal))
        {
            if (!On(draft, "EnableTypingIndicator") || Number(draft, "TypingIndicatorMaxRange") <= 0)
            { Text(context, "Typing indicator hidden", 62, 22); return; }
            int mode = draft.Get("TypingIndicatorDisplayMode") switch { "Text" or "1" => 1, "Both" or "2" => 2, _ => 0 };
            double x = mode == 0 ? 137 : mode == 1 ? 114 : 86;
            if (mode != 1)
                for (int index = 0; index < 3; index++) { context.SetSourceRGBA(0.95, 0.89, 0.73, 1); context.Arc(x + index * 12, 28, 3, 0, Math.PI * 2); context.Fill(); }
            if (mode != 0) Text(context, Value(draft, "TypingIndicatorTextOverride", "Typing..."), mode == 1 ? x : x + 44, 19);
            return;
        }
        if (pageId.Contains("bubble", StringComparison.Ordinal))
        {
            string mode = Value(draft, "OverheadChatBubbleMode", "RpText");
            if (mode == "Off") { Text(context, "Speech bubbles off", 79, 22); return; }
            if (mode == "RpText" && On(draft, "DisableRPChat"))
            { Text(context, "Formatted bubbles need roleplay", 29, 22, 14); return; }
            Box(context, 22, 3, 266, 36);
            bool quoted = mode == "RpText" && ProximityChatPresentationModes.UsesSpeechQuotes(draft.Get("ProximityChatPresentationMode"));
            Text(context, quoted ? "\"Hello, traveler!\"" : "Hello, traveler!", 70, 13, 16);
            return;
        }
        if (pageId == "appearance.nametagcolors")
        {
            Box(context, 48, 3, 214, 36, draft.Get("NametagBackgroundColor"), draft.Get("NametagBorderColor"));
            Text(context, "Pip", 140, 11, 17, true);
            return;
        }
        if (Number(draft, "NametagRenderRange") <= 0)
        { Text(context, "Nametag range is 0 blocks", 54, 22, 14); return; }
        if (On(draft, "HideNametagUnlessTargeting")) { Text(context, "Name appears when targeted", 44, 22); return; }
        string nickname = On(draft, "ShowNicknameInNametag") ? "Pip" : "";
        string account = On(draft, "ShowPlayerNameInNametag") ? "PipPlayer" : "";
        if (nickname.Length == 0 && account.Length == 0) { Text(context, "Both names hidden", 80, 22); return; }
        Box(context, 48, 5, 214, 47, draft.Get("NametagBackgroundColor"), draft.Get("NametagBorderColor"));
        if (nickname.Length > 0) Text(context, nickname, 140, account.Length > 0 ? 9 : 20, 17, true);
        if (account.Length > 0) Text(context, account, 117, nickname.Length > 0 ? 31 : 20, 14);
    }

    private static void DrawCharacter(Context context, string pageId, SetupWizardDraft draft)
    {
        Heading(context, "Character sheet");
        if (!On(draft, "EnableCharacterSheets", true)) { Text(context, "Character sheets off", 37, 98, 18); return; }
        if (pageId == "characters.slots")
        {
            bool slots = On(draft, "EnableRpCharacterSlots");
            Row(context, 58, "Slots", slots ? Value(draft, "MaxRpCharacterSlots", "3") : "1");
        }
        else Row(context, 58, "Name", "Pip");
        Row(context, 91, "Occupation", "Cartographer");
        Row(context, 124, "About", "I know these roads.");
    }

    private static void DrawRoleplay(Context context, string pageId, SetupWizardDraft draft)
    {
        if (pageId.Contains("dice", StringComparison.Ordinal))
        {
            Heading(context, "Dice roll");
            if (!On(draft, "EnableDiceRolling")) { Text(context, "Dice rolling off", 65, 96, 18); return; }
            Die(context, 74, 66, 3);
            Die(context, 148, 66, 4);
            Text(context, "Pip rolls 2d6: 7", 72, 134, 18, true);
        }
        else if (pageId.Contains("spectator", StringComparison.Ordinal))
        {
            Heading(context, "Spectator chat");
            string name = On(draft, "UseNicknameInSpectatorOOC") ? "Pip" : "PipPlayer";
            Row(context, 58, "OOC", name + ": One moment.");
            Row(context, 99, "Speech", On(draft, "ProtectSpectatorRoleplayChat") ? "Blocked while invisible" : "Hello, traveler!");
            Row(context, 140, "Placed text", On(draft, "AllowSpectatorPlacedEnvironmentalMessages") ? "The gate creaks." : "Off for spectators");
        }
    }

    private static void DrawRecords(Context context, string pageId, SetupWizardDraft draft)
    {
        if (pageId.Contains("stat", StringComparison.Ordinal))
        {
            Heading(context, "Player statistics");
            if (!On(draft, "PlayerStatSystem")) { Text(context, "Player statistics off", 45, 97, 18); return; }
            Row(context, 58, "Deaths", "2");
            Row(context, 91, "NPC kills", "12");
            Row(context, 124, "Blocks broken", "48");
        }
        else if (pageId.Contains("note", StringComparison.Ordinal)) DrawNotes(context, pageId, draft);
        else
        {
            Heading(context, "Chat history");
            if (!On(draft, "EnableChatHistory")) { Text(context, "Chat history off", 68, 96, 18); return; }
            Row(context, 58, "Pip", "Hello, traveler!");
            Row(context, 91, "Mara", "One moment.");
            if (On(draft, "ChatHistoryCaptureNonBasicChat")) Row(context, 124, "Other chat", "Meet you by the gate.");
        }
    }

    private static void DrawNotes(Context context, string pageId, SetupWizardDraft draft)
    {
        bool personal = pageId.Contains("personal", StringComparison.Ordinal);
        Heading(context, personal ? "Personal notes" : "Staff notes");
        bool enabled = personal ? On(draft, "EnablePlayerNotes") : On(draft, "EnableAdminNotes") &&
            (On(draft, "EnableStructuredAdminNotes") || On(draft, "EnableAdminNoteLedger"));
        if (!enabled) { Text(context, personal ? "Personal notes off" : "Staff notes off", 66, 97, 18); return; }
        if (personal || On(draft, "EnableStructuredAdminNotes")) Row(context, 58, "Note", "Met Mara at the north gate.");
        if (personal || On(draft, "EnableAdminNoteLedger")) Row(context, 108, personal ? "Notebook" : "Ledger", "Checked the western trail.");
    }

    private static void Heading(Context context, string title) => Text(context, title, 18, 23, 18, true);

    private static void Row(Context context, double y, string label, string value)
    {
        Box(context, 18, y, 274, 29);
        Text(context, label, 27, y + 7, 13, true);
        Text(context, value, 136, y + 7, 13);
    }

    private static void Box(Context context, double x, double y, double width, double height, string background = null, string border = null)
    {
        GuiElement.RoundRectangle(context, x, y, width, height, 3);
        if (!SetColor(context, background)) context.SetSourceRGBA(0.15, 0.13, 0.10, 0.96);
        context.FillPreserve();
        if (!SetColor(context, border)) context.SetSourceRGBA(0.63, 0.53, 0.37, 0.8);
        context.LineWidth = 1;
        context.Stroke();
    }

    private static void Text(Context context, string value, double x, double y, double size = 16, bool bold = false)
    {
        context.Save();
        context.Rectangle(x, y, Math.Max(0, Width - x - 12), size * 1.5);
        context.Clip();
        context.SelectFontFace(GuiStyle.StandardFontName, FontSlant.Normal, bold ? FontWeight.Bold : FontWeight.Normal);
        context.SetFontSize(size);
        context.SetSourceRGBA(0.96, 0.92, 0.84, 1);
        context.MoveTo(x, y + context.FontExtents.Ascent);
        context.ShowText(value);
        context.Restore();
    }

    private static void Die(Context context, double x, double y, int value)
    {
        Box(context, x, y, 47, 47);
        for (int index = 0; index < value; index++)
        {
            double dx = value == 3 ? 11 + index * 12 : index % 2 == 0 ? 11 : 36;
            double dy = value == 3 ? 11 + index * 12 : index < 2 ? 11 : 36;
            context.SetSourceRGBA(0.95, 0.89, 0.72, 1);
            context.Arc(x + dx, y + dy, 3, 0, Math.PI * 2);
            context.Fill();
        }
    }

    private static bool On(SetupWizardDraft draft, string key, bool fallback = false) =>
        ConfigAdminSettingDefinition.TryParseBool(draft.Get(key), out bool enabled) ? enabled : fallback;
    private static double Number(SetupWizardDraft draft, string key) =>
        double.TryParse(draft.Get(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) ? value : 0;
    private static string Value(SetupWizardDraft draft, string key, string fallback) => string.IsNullOrEmpty(draft.Get(key)) ? fallback : draft.Get(key);

    private static bool SetColor(Context context, string value)
    {
        if (value == null || value.Length is not (7 or 9) || value[0] != '#' ||
            !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint color)) return false;
        if (value.Length == 7) color = color << 8 | 255;
        context.SetSourceRGBA((color >> 24) / 255d, (color >> 16 & 255) / 255d, (color >> 8 & 255) / 255d, (color & 255) / 255d);
        return true;
    }
}
