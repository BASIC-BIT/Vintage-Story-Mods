using System;
using System.Globalization;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class DiceBubbleTexture
{
    internal static bool TryGetSides(string kind, out int? sides)
    {
        sides = null;
        if (kind == "dice") return true;
        if (kind == null || !kind.StartsWith("dice", StringComparison.Ordinal)) return false;
        if (!int.TryParse(kind.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            || value < 1 || value > 1_000_000) return false;
        sides = value;
        return true;
    }

    internal static LoadedTexture Create(ICoreClientAPI api, string summary, int? sides, CairoFont font)
    {
        using var surface = CreateSurface(summary, sides, font);
        var texture = new LoadedTexture(api);
        api.Gui.LoadOrUpdateCairoTexture(surface, linearMag: false, ref texture);
        return texture;
    }

    // One row: range marker, wireframe die, result. Arithmetic stays in chat.
    internal static ImageSurface CreateSurface(string summary, int? sides, CairoFont font)
    {
        var marker = summary.StartsWith("(W) ", StringComparison.Ordinal) || summary.StartsWith("(Y) ", StringComparison.Ordinal)
            ? summary[..3] : "";
        var result = marker.Length == 0 ? summary : summary[4..];
        var scale = GuiElement.scaled(1);
        var pad = 9 * scale;
        var badge = 46 * scale;
        var gap = 9 * scale;
        var text = font.GetTextExtents(result);
        var markerText = font.GetTextExtents(marker);
        var prefixWidth = marker.Length == 0 ? 0 : markerText.Width + gap;
        var height = (int)Math.Ceiling(Math.Max(badge, text.Height) + 2 * pad);
        var width = (int)Math.Ceiling(2 * pad + prefixWidth + badge + gap + text.Width + 2);
        var surface = new ImageSurface(Format.Argb32, width, height);
        using var ctx = new Context(surface);
        ctx.Operator = Operator.Source;
        ctx.SetSourceRGBA(0, 0, 0, 0);
        ctx.Paint();
        ctx.Operator = Operator.Over;
        GuiElement.RoundRectangle(ctx, 1, 1, width - 2, height - 2, 10 * scale);
        ctx.SetSourceRGBA(0.15, 0.12, 0.19, 0.94);
        ctx.FillPreserve();
        ctx.LineWidth = 2 * scale;
        ctx.SetSourceRGBA(0.79, 0.65, 0.91, 1);
        ctx.Stroke();

        DrawDie(ctx, pad + prefixWidth, (height - badge) / 2, badge, sides);
        font.SetupContext(ctx);
        ctx.SetSourceRGBA(1, 1, 1, 1);
        if (marker.Length > 0)
        {
            ctx.MoveTo(pad - markerText.XBearing, (height - markerText.Height) / 2 - markerText.YBearing);
            ctx.ShowText(marker);
        }
        ctx.MoveTo(pad + prefixWidth + badge + gap - text.XBearing, (height - text.Height) / 2 - text.YBearing);
        ctx.ShowText(result);
        surface.Flush();
        return surface;
    }

    private static void DrawDie(Context ctx, double x, double y, double size, int? sides)
    {
        ctx.Save();
        ctx.Translate(x, y);
        ctx.Scale(size, size);
        ctx.LineWidth = 0.035;
        ctx.LineJoin = LineJoin.Round;
        ctx.SetSourceRGBA(0.89, 0.81, 0.96, 1);
        // Icosahedral silhouette and triangular facets surround a clear central face.
        ctx.MoveTo(.5, .02);
        ctx.LineTo(.94, .25);
        ctx.LineTo(.94, .75);
        ctx.LineTo(.5, .98);
        ctx.LineTo(.06, .75);
        ctx.LineTo(.06, .25);
        ctx.ClosePath();
        ctx.Stroke();
        ctx.MoveTo(.5, .18);
        ctx.LineTo(.8, .74);
        ctx.LineTo(.2, .74);
        ctx.ClosePath();
        ctx.MoveTo(.5, .02); ctx.LineTo(.5, .18);
        ctx.MoveTo(.94, .25); ctx.LineTo(.5, .18);
        ctx.MoveTo(.94, .25); ctx.LineTo(.8, .74);
        ctx.MoveTo(.94, .75); ctx.LineTo(.8, .74);
        ctx.MoveTo(.5, .98); ctx.LineTo(.8, .74);
        ctx.MoveTo(.5, .98); ctx.LineTo(.2, .74);
        ctx.MoveTo(.06, .75); ctx.LineTo(.2, .74);
        ctx.MoveTo(.06, .25); ctx.LineTo(.2, .74);
        ctx.MoveTo(.06, .25); ctx.LineTo(.5, .18);
        ctx.Stroke();
        if (sides is int count)
        {
            var label = count.ToString(CultureInfo.InvariantCulture);
            ctx.SelectFontFace(GuiStyle.StandardFontName, FontSlant.Normal, FontWeight.Bold);
            ctx.SetFontSize(.33);
            var extents = ctx.TextExtents(label);
            if (extents.Width > .39) ctx.SetFontSize(.33 * .39 / extents.Width);
            extents = ctx.TextExtents(label);
            ctx.MoveTo(.5 - extents.Width / 2 - extents.XBearing, .55 - extents.Height / 2 - extents.YBearing);
            ctx.ShowText(label);
        }
        ctx.Restore();
    }
}
