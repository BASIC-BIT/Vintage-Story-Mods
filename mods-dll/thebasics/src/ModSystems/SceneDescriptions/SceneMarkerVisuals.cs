using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using thebasics.ModSystems.ChatUiSystem;

namespace thebasics.ModSystems.SceneDescriptions;

internal static class SceneMarkerVisuals
{
    internal static ImageSurface DescriptionSurface(ICoreClientAPI api, SceneDescriptionData source)
    {
        var background = new TextBackground { FillColor = new[] { 0.08, 0.10, 0.14, 1.0 }, Padding = 8,
            Radius = 4, BorderWidth = 1, BorderColor = new[] { 0.65, 0.69, 0.75, 1.0 } };
        var font = new CairoFont(24, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble) { Orientation = EnumTextOrientation.Left };
        var data = source.Clone().Normalize();
        var vtml = SceneDescriptionFormatter.ToFloatingVtml(data);
        if (string.IsNullOrWhiteSpace(data.Title) || data.TitleIconName.Length == 0)
            return RichTextTextureUtils.GenRichTextSurface(api, vtml, font, 360, background);

        // Reserve an icon column so wrapping cannot move the icon above the title.
        var transparent = new TextBackground { FillColor = new double[4], Padding = 0, BorderWidth = 0, Radius = 0 };
        using var text = RichTextTextureUtils.GenRichTextSurface(api, vtml, font, 308, transparent, centerLines: false);
        if (text == null) return null;
        var guiScale = Math.Max(0.1f, RuntimeEnv.GUIScale);
        var padding = (int)Math.Ceiling(10 * guiScale);
        var iconSize = (int)Math.Ceiling(40 * guiScale);
        using var icon = new ImageSurface(Format.Argb32, iconSize, iconSize);
        using (var iconContext = new Context(icon))
        {
            if (!SceneTitleIcons.Draw(api, iconContext, icon, data.TitleIconName))
                return RichTextTextureUtils.GenRichTextSurface(api, vtml, font, 360, background);
        }
        var gap = (int)Math.Ceiling(10 * guiScale);
        var textOffset = (int)Math.Ceiling(5 * guiScale);
        var surface = new ImageSurface(Format.Argb32, padding * 2 + iconSize + gap + text.Width,
            padding * 2 + Math.Max(iconSize, text.Height + textOffset));
        using var ctx = new Context(surface);
        GuiElement.RoundRectangle(ctx, 1, 1, surface.Width - 2, surface.Height - 2, 4 * guiScale);
        ctx.SetSourceRGBA(background.FillColor);
        ctx.FillPreserve();
        ctx.LineWidth = 1;
        ctx.SetSourceRGBA(background.BorderColor);
        ctx.Stroke();

        ctx.SetSourceSurface(icon, padding, padding);
        ctx.Paint();
        ctx.SetSourceSurface(text, padding + iconSize + gap, padding + textOffset);
        ctx.Paint();
        return surface;
    }

    internal static Shape LoadShape(ICoreClientAPI api, SceneMarkerSymbol symbol)
    {
        if (symbol is SceneMarkerSymbol.Dot or SceneMarkerSymbol.Ring or SceneMarkerSymbol.Diamond) return null;
        var name = symbol switch
        {
            SceneMarkerSymbol.Question => "question",
            SceneMarkerSymbol.Information => "information",
            _ => "exclamation",
        };
        return api.Assets.Get(new AssetLocation($"thebasics:shapes/block/scene-symbol-{name}.json")).ToObject<Shape>();
    }

    internal static (double R, double G, double B) Palette(SceneMarkerColor color) => color switch
    {
        SceneMarkerColor.Parchment => (0.96, 0.91, 0.78),
        SceneMarkerColor.Blue => (0.55, 0.73, 0.91),
        SceneMarkerColor.Green => (0.60, 0.83, 0.62),
        _ => (1, 0.82, 0.30),
    };

    // Shared artwork for the billboard and editor preview.
    internal const float IndicatorOpacity = 0.68f;

    internal static void Draw(Context ctx, Shape shape, double x, double y, double size, SceneMarkerColor color = SceneMarkerColor.Gold,
        SceneMarkerSymbol symbol = SceneMarkerSymbol.Exclamation, float opacity = 1)
    {
        var unit = size / 16;
        var rgb = Palette(color);
        ctx.Save();
        if (opacity < 1) ctx.PushGroup();
        ctx.NewPath();
        if (symbol == SceneMarkerSymbol.Dot)
        {
            ctx.Arc(x + size / 2, y + size / 2, size * 0.16, 0, Math.PI * 2);
        }
        else if (symbol == SceneMarkerSymbol.Ring)
        {
            ctx.Arc(x + size / 2, y + size / 2, size * 0.34, 0, Math.PI * 2);
            ctx.NewSubPath();
            ctx.ArcNegative(x + size / 2, y + size / 2, size * 0.25, Math.PI * 2, 0);
        }
        else if (symbol == SceneMarkerSymbol.Diamond)
        {
            ctx.MoveTo(x + size / 2, y + size * 0.12);
            ctx.LineTo(x + size * 0.84, y + size / 2);
            ctx.LineTo(x + size / 2, y + size * 0.88);
            ctx.LineTo(x + size * 0.16, y + size / 2);
            ctx.ClosePath();
        }
        else if (shape != null)
        {
            foreach (var element in shape.Elements)
                ctx.Rectangle(x + element.From[0] * unit, y + (16 - element.To[1]) * unit,
                    (element.To[0] - element.From[0]) * unit, (element.To[1] - element.From[1]) * unit);
        }
        ctx.Clip();
        // Opaque interiors keep Cairo's premultiplied pixels compatible with the world's straight-alpha blend.
        ctx.SetSourceRGBA(rgb.R, rgb.G, rgb.B, 1);
        ctx.Paint();
        if (opacity < 1)
        {
            ctx.PopGroupToSource();
            ctx.PaintWithAlpha(opacity);
        }
        ctx.Restore();
    }
}
