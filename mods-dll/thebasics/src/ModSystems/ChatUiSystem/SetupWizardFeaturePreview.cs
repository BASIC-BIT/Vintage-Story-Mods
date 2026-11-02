using System;
using Cairo;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Client;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>Illustrative samples of draft choices, without reading or changing world state.</summary>
public static class SetupWizardFeaturePreview
{
    private const double Width = 310;
    private const double Height = 220;

    public static bool Supports(string pageId) => pageId is
        "map.visibility" or
        "roleplay.dice" or "roleplay.scenemarkers" or "roleplay.spectators" or
        "server.history" or "server.stats";

    public static bool IsSettingsOnly(string pageId) => pageId is "characters.access" or
        "roleplay.nicknamechanges" or "roleplay.nicknamerules" or "server.retention" or "server.notelimits" or "server.diagnostics" or "server.integrations";

    public static void Draw(Context context, ImageSurface surface, ElementBounds bounds, string pageId, SetupWizardDraft draft, ICoreClientAPI api = null)
    {
        if (!Supports(pageId) || draft == null || surface.Width <= 0 || surface.Height <= 0) return;
        double height = pageId == "map.visibility" ? 270 : Height;
        double scale = Math.Min(surface.Width / Width, surface.Height / height);
        context.Save();
        try
        {
            context.Rectangle(0, 0, surface.Width, surface.Height);
            context.Clip();
            context.Translate((surface.Width - Width * scale) / 2, (surface.Height - height * scale) / 2);
            context.Scale(scale, scale);
            if (pageId == "map.visibility") SetupWizardMapPreview.Draw(context, draft, api?.Gui.Icons);
            else
            {
                Box(context, 8, 8, 294, 204);
                if (pageId.StartsWith("roleplay.", StringComparison.Ordinal)) DrawRoleplay(context, pageId, draft, api);
                else DrawRecords(context, pageId, draft);
            }
        }
        finally { context.Restore(); }
    }

    private static void DrawRoleplay(Context context, string pageId, SetupWizardDraft draft, ICoreClientAPI api)
    {
        if (pageId.Contains("dice", StringComparison.Ordinal))
        {
            Heading(context, "Dice roll");
            if (!On(draft, "EnableDiceRolling")) { Text(context, "Dice rolling off", 65, 96, 18); return; }
            var roll = DiceEvaluator.EvaluateInput("d20", _ => 17);
            using var bubble = DiceBubbleTexture.CreateSurface(DicePresentation.Summary(roll, ProximityChatMode.Normal),
                roll.SimpleSides, new CairoFont(25, GuiStyle.StandardFontName, new[] { 1d, 1, 1, 1 }));
            PaintSurface(context, bubble, 40, 60, 230, 70);
            using var chat = RichTextTextureUtils.GenRichTextSurface(api,
                DicePresentation.Chat(roll, "Pip", ProximityChatMode.Normal, false), CairoFont.WhiteSmallText(), 270,
                new TextBackground { FillColor = new[] { 0.08, 0.07, 0.05, 0.96 }, Padding = 8, Radius = 2 }, centerLines: false);
            PaintSurface(context, chat, 18, 149, 274, 42);
        }
        else if (pageId == "roleplay.scenemarkers") DrawSceneMarker(context, draft, api);
        else if (pageId.Contains("spectator", StringComparison.Ordinal))
        {
            Heading(context, "Spectator chat");
            string name = On(draft, "UseNicknameInSpectatorOOC") ? "Pip" : "PipPlayer";
            Row(context, 58, "OOC", name + ": One moment.");
            Row(context, 99, "Speech", On(draft, "ProtectSpectatorRoleplayChat") ? "Blocked while invisible" : "Hello, traveler!");
            Row(context, 140, "Placed text", On(draft, "AllowSpectatorPlacedEnvironmentalMessages") ? "The gate creaks." : "Off for spectators");
        }
    }

    private static void DrawSceneMarker(Context context, SetupWizardDraft draft, ICoreClientAPI api)
    {
        context.SetSourceRGBA(0.24, 0.29, 0.20, 1);
        context.MoveTo(22, 176);
        context.LineTo(155, 147);
        context.LineTo(288, 176);
        context.LineTo(155, 209);
        context.ClosePath();
        context.Fill();
        context.SetSourceRGBA(0.15, 0.18, 0.12, 1);
        context.MoveTo(22, 176);
        context.LineTo(155, 209);
        context.LineTo(288, 176);
        context.LineTo(288, 185);
        context.LineTo(155, 218);
        context.LineTo(22, 185);
        context.ClosePath();
        context.Fill();
        context.Save();
        context.Translate(155, 185);
        context.Scale(1, 0.4);
        context.SetSourceRGBA(0.11, 0.11, 0.10, 1);
        context.Arc(0, 0, 26, 0, Math.PI * 2);
        context.Fill();
        context.SetSourceRGBA(0.35, 0.28, 0.20, 1);
        context.LineWidth = 7;
        context.MoveTo(-20, -9); context.LineTo(20, 9);
        context.MoveTo(-17, 13); context.LineTo(17, -13);
        context.Stroke();
        context.Restore();
        if (!On(draft, "EnableSceneMarkers", true) || api == null) return;

        var data = new SceneDescriptionData
        {
            Title = "Old campsite",
            Body = "Ashes are cold. Someone left in a hurry.",
            ShowBodyInBubble = true,
            Display = SceneDescriptionDisplay.AlwaysNearby,
            Symbol = SceneMarkerSymbol.Information
        };
        using var description = SceneMarkerVisuals.DescriptionSurface(api, data);
        PaintSurface(context, description, 12, 18, 286, 100);
        var shape = SceneMarkerVisuals.LoadShape(api, data.Symbol);
        SceneMarkerVisuals.Draw(context, shape, 133, 127, 44, data.Color, data.Symbol, SceneMarkerVisuals.IndicatorOpacity);
    }

    private static void PaintSurface(Context context, ImageSurface surface, double x, double y, double width, double height)
    {
        if (surface == null) return;
        var scale = Math.Min(width / surface.Width, height / surface.Height);
        context.Save();
        context.Translate(x + (width - surface.Width * scale) / 2, y + (height - surface.Height * scale) / 2);
        context.Scale(scale, scale);
        context.SetSourceSurface(surface, 0, 0);
        context.Paint();
        context.Restore();
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
        else
        {
            Heading(context, "Chat history");
            if (!On(draft, "EnableChatHistory")) { Text(context, "Chat history off", 68, 96, 18); return; }
            Row(context, 58, "Pip", "Hello, traveler!");
            Row(context, 91, "Mara", "One moment.");
            if (On(draft, "ChatHistoryCaptureNonBasicChat")) Row(context, 124, "Other chat", "Meet you by the gate.");
        }
    }

    private static void Heading(Context context, string title) => Text(context, title, 18, 23, 18, true);

    private static void Row(Context context, double y, string label, string value)
    {
        Box(context, 18, y, 274, 29);
        Text(context, label, 27, y + 7, 13, true);
        Text(context, value, 136, y + 7, 13);
    }

    private static void Box(Context context, double x, double y, double width, double height)
    {
        GuiElement.RoundRectangle(context, x, y, width, height, 3);
        context.SetSourceRGBA(0.15, 0.13, 0.10, 0.96);
        context.FillPreserve();
        context.SetSourceRGBA(0.63, 0.53, 0.37, 0.8);
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

    private static bool On(SetupWizardDraft draft, string key, bool fallback = false) =>
        ConfigAdminSettingDefinition.TryParseBool(draft.Get(key), out bool enabled) ? enabled : fallback;
}
