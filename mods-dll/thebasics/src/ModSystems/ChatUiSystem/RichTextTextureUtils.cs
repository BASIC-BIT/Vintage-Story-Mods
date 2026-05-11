using System;
using System.Collections.Generic;
using Cairo;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class RichTextTextureUtils
{
    private const int LayoutSlackPx = 2;
    private const int MeasureHeightPx = 600;

    /// <summary>
    /// Renders VTML into a Cairo texture, including &lt;icon&gt; tags.
    /// Returns null if rendering fails.
    /// </summary>
    /// <remarks>
    /// The font's Orientation must be <see cref="EnumTextOrientation.Left"/>.
    /// VS's GuiElementRichtext has a bug where Center alignment miscalculates
    /// x-offsets at inline tag boundaries (bold/color/font transitions), causing
    /// the first styled component on a line to overlap subsequent text.
    /// We render left-aligned and rely on vanilla's bubble renderer to center
    /// the resulting texture above the player.
    /// </remarks>
    public static LoadedTexture GenRichTextTexture(ICoreClientAPI capi, string vtml, CairoFont baseFont, int maxTextWidthPx, TextBackground background, int extraBottomMarginPx = 0)
    {
        var surface = GenRichTextSurface(capi, vtml, baseFont, maxTextWidthPx, background, extraBottomMarginPx);
        if (surface == null)
        {
            return null;
        }

        try
        {
            var tex = new LoadedTexture(capi);
            capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: false, ref tex);
            return tex;
        }
        finally
        {
            surface.Dispose();
        }
    }

    /// <summary>
    /// Same renderer as <see cref="GenRichTextTexture"/> but returns the raw Cairo surface so
    /// callers can composite the bubble alongside other Cairo content (e.g. a framed headshot
    /// in a nametag). Caller owns the returned surface and must dispose it.
    /// </summary>
    public static ImageSurface GenRichTextSurface(ICoreClientAPI capi, string vtml, CairoFont baseFont, int maxTextWidthPx, TextBackground background, int extraBottomMarginPx = 0)
    {
        if (capi == null || string.IsNullOrWhiteSpace(vtml))
        {
            return null;
        }

        try
        {
            vtml = VtmlUtils.NormalizeVtmlForRendering(vtml);

            var guiScale = RuntimeEnv.GUIScale > 0 ? RuntimeEnv.GUIScale : 1;
            var maxTextWidthAtScalePx = GetScaledLengthPx(maxTextWidthPx, guiScale);
            var measureHeightAtScalePx = GetScaledLengthPx(MeasureHeightPx, guiScale);

            // Pass 1: measure at max width to get actual text dimensions.
            var measureComps = VtmlUtil.Richtextify(capi, vtml, baseFont);
            var measureBounds = ElementBounds.FixedSize(maxTextWidthAtScalePx / (double)guiScale, measureHeightAtScalePx / (double)guiScale);
            measureBounds.ParentBounds = ElementBounds.Empty;
            var measure = new GuiElementRichtext(capi, measureComps, measureBounds);
            measure.BeforeCalcBounds();

            var wrapped = UsesMultipleVisualLines(measureComps);
            var textWidthPx = wrapped
                ? maxTextWidthAtScalePx
                : GetUnwrappedTextureWidthPx(measure.MaxLineWidth, maxTextWidthAtScalePx);
            var textHeightPx = (int)Math.Ceiling(measure.TotalHeight);
            textWidthPx = Math.Max(1, textWidthPx);
            textHeightPx = Math.Max(1, textHeightPx);

            // Pass 2: fresh components laid out at a safe width.
            // Left alignment avoids the VS centering bug with mixed-font inline components.
            // If pass 1 wrapped, keep the original max width during layout so pass 2
            // cannot reflow into more lines than were measured. We shrink the texture
            // after layout using the actual widest visual line.
            var renderComps = VtmlUtil.Richtextify(capi, vtml, baseFont);
            var layoutWidthPx = wrapped ? maxTextWidthAtScalePx : textWidthPx;
            var finalBounds = ElementBounds.FixedSize(layoutWidthPx / (double)guiScale, textHeightPx / (double)guiScale);
            finalBounds.ParentBounds = ElementBounds.Empty;
            var rich = new GuiElementRichtext(capi, renderComps, finalBounds);
            rich.BeforeCalcBounds();
            textHeightPx = Math.Max(1, (int)Math.Ceiling(rich.TotalHeight));
            if (wrapped)
            {
                textWidthPx = GetWrappedTextureWidthPx(renderComps, maxTextWidthAtScalePx);
            }
            CenterVisualLines(renderComps, textWidthPx);

            var hPad = GetScaledLengthPx(background.HorPadding, guiScale);
            var verPad = GetScaledLengthPx(background.VerPadding, guiScale);
            // Inset the bubble path so its stroke renders fully inside the surface, plus one
            // extra transparent pixel to absorb Cairo's AA bleed (which extends ~0.5px past the
            // geometric stroke). Without that AA margin the surface's outermost column ends up
            // with ~50% alpha border color, which flickers under GL sampling at varying subpixel
            // positions.
            var borderInset = background.BorderWidth > 0 ? (int)Math.Ceiling(background.BorderWidth / 2.0) + 1 : 0;
            var bubbleWidth = textWidthPx + 2 * hPad;
            var bubbleHeight = textHeightPx + 2 * verPad;
            var surfaceWidth = bubbleWidth + 2 * borderInset;
            var surfaceHeight = bubbleHeight + 2 * borderInset + Math.Max(0, extraBottomMarginPx);

            var surface = new ImageSurface(Format.Argb32, surfaceWidth, surfaceHeight);
            using var ctx = new Context(surface);

            GuiElement.RoundRectangle(ctx, borderInset, borderInset, bubbleWidth, bubbleHeight, background.Radius);
            ctx.SetSourceRGBA(background.FillColor);
            if (background.BorderWidth > 0)
            {
                ctx.FillPreserve();
                ctx.LineWidth = background.BorderWidth;
                ctx.SetSourceRGBA(background.BorderColor);
                ctx.Stroke();
            }
            else
            {
                ctx.Fill();
            }

            // Text inside the bubble, centered vertically within the bubble's interior.
            var vPad = (bubbleHeight - textHeightPx) / 2.0;
            var offsetBounds = ElementBounds.Fixed(
                (borderInset + hPad) / guiScale,
                (borderInset + vPad) / guiScale,
                textWidthPx / (double)guiScale,
                textHeightPx / (double)guiScale
            );
            offsetBounds.ParentBounds = ElementBounds.Empty;
            rich.ComposeFor(offsetBounds, ctx, surface);

            // Clear the bottom transparent margin (chat-bubble spacing) so no stray Cairo AA
            // bleeds into the gap separating the bubble from the nametag below it.
            if (extraBottomMarginPx > 0)
            {
                ctx.Save();
                ctx.Operator = Operator.Clear;
                ctx.Rectangle(0, borderInset + bubbleHeight, surfaceWidth, extraBottomMarginPx + borderInset);
                ctx.Fill();
                ctx.Restore();
            }

            return surface;
        }
        catch
        {
            return null;
        }
    }

    private static bool UsesMultipleVisualLines(RichTextComponentBase[] components)
    {
        if (components == null)
        {
            return false;
        }

        var hasFirstLineY = false;
        double firstLineY = 0;

        for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            var boundsPerLine = components[componentIndex]?.BoundsPerLine;
            if (boundsPerLine == null)
            {
                continue;
            }

            for (var boundsIndex = 0; boundsIndex < boundsPerLine.Length; boundsIndex++)
            {
                var bounds = boundsPerLine[boundsIndex];
                if (!hasFirstLineY)
                {
                    hasFirstLineY = true;
                    firstLineY = bounds.Y;
                    continue;
                }

                if (Math.Abs(bounds.Y - firstLineY) > 0.5)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int GetUnwrappedTextureWidthPx(double measuredWidthPx, int maxTextWidthPx)
    {
        // VS lineizes with width >= available, so a second pass at exactly the
        // measured width can orphan a trailing quote/punctuation on a new line.
        return (int)Math.Min(maxTextWidthPx, Math.Ceiling(measuredWidthPx) + LayoutSlackPx);
    }

    private static int GetScaledLengthPx(int unscaledLengthPx, double guiScale)
    {
        return Math.Max(1, (int)Math.Ceiling(unscaledLengthPx * guiScale));
    }

    private static int GetWrappedTextureWidthPx(RichTextComponentBase[] components, int maxTextWidthPx)
    {
        var maxLineWidth = 0.0;
        var lines = GetVisualLines(components);
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            maxLineWidth = Math.Max(maxLineWidth, line.MaxX - line.MinX);
        }

        if (maxLineWidth <= 0)
        {
            return maxTextWidthPx;
        }

        return Math.Max(1, (int)Math.Min(maxTextWidthPx, Math.Ceiling(maxLineWidth) + LayoutSlackPx));
    }

    private static void CenterVisualLines(RichTextComponentBase[] components, double textWidthPx)
    {
        if (components == null || textWidthPx <= 0)
        {
            return;
        }

        var lines = GetVisualLines(components);
        for (var lineIndex = 0; lineIndex < lines.Count; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineWidth = line.MaxX - line.MinX;
            if (lineWidth <= 0 || lineWidth >= textWidthPx)
            {
                continue;
            }

            var targetX = (textWidthPx - lineWidth) / 2.0;
            var deltaX = targetX - line.MinX;
            if (Math.Abs(deltaX) <= 0.5)
            {
                continue;
            }

            ShiftVisualLine(components, line.Y, deltaX);
        }
    }

    private static List<VisualLine> GetVisualLines(RichTextComponentBase[] components)
    {
        var lines = new List<VisualLine>();
        for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            var boundsPerLine = components[componentIndex]?.BoundsPerLine;
            if (boundsPerLine == null)
            {
                continue;
            }

            for (var boundsIndex = 0; boundsIndex < boundsPerLine.Length; boundsIndex++)
            {
                var bounds = boundsPerLine[boundsIndex];
                if (bounds.Width <= 0)
                {
                    continue;
                }

                var line = FindVisualLine(lines, bounds.Y);
                if (line == null)
                {
                    lines.Add(new VisualLine(bounds.Y, bounds.X, bounds.X + bounds.Width));
                    continue;
                }

                line.MinX = Math.Min(line.MinX, bounds.X);
                line.MaxX = Math.Max(line.MaxX, bounds.X + bounds.Width);
            }
        }

        return lines;
    }

    private static VisualLine FindVisualLine(List<VisualLine> lines, double y)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (Math.Abs(lines[i].Y - y) <= 0.5)
            {
                return lines[i];
            }
        }

        return null;
    }

    private static void ShiftVisualLine(RichTextComponentBase[] components, double y, double deltaX)
    {
        for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
        {
            var boundsPerLine = components[componentIndex]?.BoundsPerLine;
            if (boundsPerLine == null)
            {
                continue;
            }

            for (var boundsIndex = 0; boundsIndex < boundsPerLine.Length; boundsIndex++)
            {
                var bounds = boundsPerLine[boundsIndex];
                if (Math.Abs(bounds.Y - y) <= 0.5)
                {
                    bounds.X += deltaX;
                }
            }
        }
    }

    private sealed class VisualLine
    {
        public double Y { get; }
        public double MinX { get; set; }
        public double MaxX { get; set; }

        public VisualLine(double y, double minX, double maxX)
        {
            Y = y;
            MinX = minX;
            MaxX = maxX;
        }
    }
}
