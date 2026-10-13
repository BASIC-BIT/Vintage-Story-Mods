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
        "map.visibility" or "characters.sheets" or "characters.access" or "characters.slots" or
        "roleplay.dice" or "roleplay.scenemarkers" or "roleplay.spectators" or "roleplay.nicknamechanges" or "roleplay.nicknamerules" or
        "server.history" or "server.retention" or "server.staffnotes" or "server.personalnotes" or "server.notelimits" or "server.stats";

    public static void Draw(Context context, ImageSurface surface, ElementBounds bounds, string pageId, SetupWizardDraft draft)
    {
        if (!Supports(pageId) || draft == null || surface.Width <= 0 || surface.Height <= 0) return;
        bool appearance = pageId.StartsWith("appearance.", StringComparison.Ordinal);
        double height = appearance ? 60 : Height;
        double scale = Math.Min(surface.Width / Width, surface.Height / height);
        context.Save();
        try
        {
            context.Rectangle(0, 0, surface.Width, surface.Height);
            context.Clip();
            context.Translate((surface.Width - Width * scale) / 2, (surface.Height - height * scale) / 2);
            context.Scale(scale, scale);
            if (appearance) DrawAppearance(context, pageId, draft);
            else
            {
                Box(context, 8, 8, 294, 204);
                if (pageId.StartsWith("map.", StringComparison.Ordinal)) DrawMap(context, draft);
                else if (pageId.StartsWith("characters.", StringComparison.Ordinal)) DrawCharacter(context, pageId, draft);
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
            double timeout = Number(draft, "TypingIndicatorTimeoutSeconds");
            Text(context, "Changes to composing after " + Format(timeout > 0 ? timeout : 5) + " seconds", 35, 45, 12);
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
            if (mode == "RpText") Text(context, "Minimum " + Value(draft, "SpeechBubbleMinimumDisplayMilliseconds", "3500") + " milliseconds", 69, 45, 12);
            return;
        }
        if (pageId == "appearance.nametagcolors")
        {
            Box(context, 48, 3, 214, 36, draft.Get("NametagBackgroundColor"), draft.Get("NametagBorderColor"));
            Text(context, "Pip", 140, 11, 17, true);
            Text(context, On(draft, "AllowPlayersToChangeNametagColors") ? "Color edits available with permission" : "Color editing commands off", 47, 45, 12);
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

    private static void DrawMap(Context context, SetupWizardDraft draft)
    {
        Heading(context, "Map");
        bool managed = On(draft, "ManageMapPlayerVisibility");
        bool hidden = On(draft, "MapHideOtherPlayers");
        double range = Number(draft, "MapPlayerRenderDistance");
        context.Save();
        context.Rectangle(18, 49, 274, 135);
        context.Clip();
        context.SetSourceRGBA(0.12, 0.18, 0.16, 1);
        context.Paint();
        context.SetSourceRGBA(0.35, 0.42, 0.33, 0.5);
        context.LineWidth = 1;
        for (int x = 18; x < 292; x += 24) { context.MoveTo(x, 49); context.LineTo(x, 184); }
        for (int y = 49; y < 184; y += 24) { context.MoveTo(18, y); context.LineTo(292, y); }
        context.Stroke();
        if (managed && !hidden && range >= 0)
        {
            context.SetSourceRGBA(0.85, 0.73, 0.45, 0.7);
            context.Arc(155, 114, Math.Max(1, range * 0.6), 0, Math.PI * 2);
            context.Stroke();
        }
        Marker(context, 155, 114, "You", true);
        if (managed && !hidden)
        {
            if (range < 0 || range >= 30) Marker(context, 169, 103, "30 blocks", false, 187, 77);
            if (range < 0 || range >= 80) Marker(context, 117, 143, "80 blocks", false, 72, 156);
            if (range < 0 || range >= 140) Marker(context, 239, 114, "140 blocks", false, 218, 130);
        }
        context.Restore();
        Text(context, !managed ? "World map settings apply" : hidden ? "Other players hidden" :
            range < 0 ? "Other players visible at any distance" : "Other players within " + Format(range) + " blocks", 18, 192, 13);
    }

    private static void DrawCharacter(Context context, string pageId, SetupWizardDraft draft)
    {
        Heading(context, "Character sheet");
        if (!On(draft, "EnableCharacterSheets", true)) { Text(context, "Character sheets off", 37, 98, 18); return; }
        if (pageId.Contains("slot", StringComparison.Ordinal) || pageId.Contains("profile", StringComparison.Ordinal))
        {
            bool slots = On(draft, "EnableRpCharacterSlots");
            Row(context, 58, "Slots", slots ? Value(draft, "MaxRpCharacterSlots", "3") : "1");
        }
        else Row(context, 58, "Name", "Pip");
        Row(context, 91, "Occupation", "Cartographer");
        Row(context, 124, "About", "I know these roads.");
        if (pageId == "characters.access")
        {
            Text(context, "Viewing range: " + Value(draft, "CharacterSheetLookRange", "12") + " blocks", 18, 168, 13);
            Text(context, On(draft, "CharacterSheetLookRequiresLineOfSight") ? "Clear view required" : "Viewing through obstacles allowed", 18, 191, 12);
        }
        else Text(context, pageId == "characters.slots" ? "Each character keeps their own belongings" :
            On(draft, "CharacterSheetAutoOpenOnCharacterScreen") ? "The C menu opens your sheet by default" : "Open your sheet with /charsheet", 18, 181, 12);
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
            Text(context, "Private rolls go only to the roller", 18, 169, 13);
            Text(context, On(draft, "EnableDiceRollSounds") ? "Dice sounds on" : "Dice sounds off", 18, 193, 13);
        }
        else if (pageId.Contains("spectator", StringComparison.Ordinal))
        {
            Heading(context, "Spectator chat");
            string name = On(draft, "UseNicknameInSpectatorOOC") ? "Pip" : "PipPlayer";
            Row(context, 58, "OOC", name + ": One moment.");
            Row(context, 99, "Speech", On(draft, "ProtectSpectatorRoleplayChat") ? "Blocked while invisible" : "Hello, traveler!");
            Row(context, 140, "Placed text", On(draft, "AllowSpectatorPlacedEnvironmentalMessages") ? "The gate creaks." : "Off for spectators");
        }
        else if (pageId.Contains("nickname", StringComparison.Ordinal))
        {
            Heading(context, "Nickname");
            Row(context, 58, "Nickname", "Pip");
            if (pageId == "roleplay.nicknamerules")
            {
                Row(context, 99, "Length", Value(draft, "MinNicknameLength", "3") + " to " + Value(draft, "MaxNicknameLength", "100") + " characters");
                Text(context, On(draft, "DisableNicknames") ? "Nickname commands off; saved names kept" : "Nickname commands enabled", 18, 164, 12);
            }
            else
            {
                Row(context, 99, "Name edit", On(draft, "ProximityChatAllowPlayersToChangeNicknames") && !On(draft, "DisableNicknames") ? "Allowed" : "Off");
                Row(context, 140, "Color edit", On(draft, "ProximityChatAllowPlayersToChangeNicknameColors") && !On(draft, "DisableNicknames") ? "Allowed" : "Off");
                Text(context, "Color permission: " + Value(draft, "ChangeNicknameColorPermission", "chat"), 18, 190, 12);
            }
        }
        else
        {
            Heading(context, "Scene description");
            if (On(draft, "EnableSceneMarkers"))
            {
                Box(context, 24, 58, 262, 91);
                Text(context, "The old gate", 41, 71, 18, true);
                Text(context, "Moss covers the worn stones.", 41, 103, 14);
            }
            else Text(context, "Scene markers off", 66, 88, 18);
            Text(context, "Descriptions remain where markers are placed", 18, 177, 12);
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
            Text(context, "Clear permission: " + Value(draft, "PlayerStatClearPermission", "commandplayer"), 18, 190, 12);
        }
        else if (pageId.Contains("note", StringComparison.Ordinal)) DrawNotes(context, pageId, draft);
        else
        {
            Heading(context, "Chat history");
            if (!On(draft, "EnableChatHistory")) { Text(context, "Chat history off", 68, 96, 18); return; }
            Row(context, 58, "Pip", "Hello, traveler!");
            Row(context, 91, "Mara", "One moment.");
            if (On(draft, "ChatHistoryCaptureNonBasicChat")) Row(context, 124, "Other chat", "Meet you by the gate.");
            if (pageId.Contains("retention", StringComparison.Ordinal) || pageId.Contains("limit", StringComparison.Ordinal))
            {
                double days = Number(draft, "ChatHistoryRetentionDays");
                double count = Number(draft, "ChatHistoryMaxEntries");
                Text(context, days == 0 ? "No age limit" : "Keep up to " + Format(days) + " days", 18, 172, 13);
                Text(context, count == 0 ? "No entry limit" : "Keep up to " + Format(count) + " entries", 18, 195, 13);
            }
            else Text(context, "Read permission: " + Value(draft, "ChatHistoryPermission", "commandplayer"), 18, 192, 13);
        }
    }

    private static void DrawNotes(Context context, string pageId, SetupWizardDraft draft)
    {
        bool personal = pageId.Contains("personal", StringComparison.Ordinal);
        bool sizes = pageId.Contains("size", StringComparison.Ordinal) || pageId.Contains("limit", StringComparison.Ordinal);
        Heading(context, sizes ? "Note limits" : personal ? "Personal notes" : "Staff notes");
        bool enabled = personal ? On(draft, "EnablePlayerNotes") : On(draft, "EnableAdminNotes") &&
            (On(draft, "EnableStructuredAdminNotes") || On(draft, "EnableAdminNoteLedger"));
        if (!sizes && !enabled) { Text(context, personal ? "Personal notes off" : "Staff notes off", 66, 97, 18); return; }
        if (sizes || personal || On(draft, "EnableStructuredAdminNotes")) Row(context, 58, "Note", "Met Mara at the north gate.");
        if (sizes || personal || On(draft, "EnableAdminNoteLedger")) Row(context, 108, personal ? "Notebook" : "Ledger", "Checked the western trail.");
        if (sizes)
        {
            Text(context, "Note limit: " + Value(draft, "MaxNoteLength", "2000") + " characters", 18, 172, 13);
            Text(context, "Ledger limit: " + Value(draft, "MaxFreeformNoteLength", "20000") + " characters", 18, 195, 13);
        }
        else Text(context, personal ? "Only the author can read personal notes" :
            "Staff permission: " + Value(draft, "AdminNotesPermission", "commandplayer"), 18, 192, 12);
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

    private static void Marker(Context context, double x, double y, string label, bool self, double labelX = -1, double labelY = -1)
    {
        context.SetSourceRGBA(self ? 0.96 : 0.68, self ? 0.84 : 0.88, self ? 0.52 : 0.85, 1);
        context.Arc(x, y, self ? 5 : 4, 0, Math.PI * 2);
        context.Fill();
        Text(context, label, labelX < 0 ? x + 7 : labelX, labelY < 0 ? y + 7 : labelY, 12, self);
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
    private static string Format(double value) => value.ToString("G4", CultureInfo.InvariantCulture);

    private static bool SetColor(Context context, string value)
    {
        if (value == null || value.Length is not (7 or 9) || value[0] != '#' ||
            !uint.TryParse(value.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint color)) return false;
        if (value.Length == 7) color = color << 8 | 255;
        context.SetSourceRGBA((color >> 24) / 255d, (color >> 16 & 255) / 255d, (color >> 8 & 255) / 255d, (color & 255) / 255d);
        return true;
    }
}
