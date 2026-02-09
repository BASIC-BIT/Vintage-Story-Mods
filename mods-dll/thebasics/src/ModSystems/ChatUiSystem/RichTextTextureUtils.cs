using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class RichTextTextureUtils
{
    /// <summary>
    /// Renders VTML into a Cairo texture, including <icon> tags.
    /// Returns null if rendering fails.
    /// </summary>
    public static LoadedTexture GenRichTextTexture(ICoreClientAPI capi, string vtml, CairoFont baseFont, int maxTextWidthPx, TextBackground background, int extraBottomMarginPx = 0)
    {
        if (capi == null || string.IsNullOrWhiteSpace(vtml))
        {
            return null;
        }

        try
        {
            var comps = VtmlUtil.Richtextify(capi, vtml, baseFont);

            // Measure with the maximum width.
            var guiScale = Math.Max(1, RuntimeEnv.GUIScale);
            var measureBounds = ElementBounds.FixedSize(maxTextWidthPx / (double)guiScale, 600 / (double)guiScale);
            measureBounds.ParentBounds = ElementBounds.Empty;
            var measure = new GuiElementRichtext(capi, comps, measureBounds);
            measure.BeforeCalcBounds();

            // GuiElementRichtext reports sizes in GUI units, not raw pixels.
            // Convert back to pixels for our Cairo surface sizing.
            var textWidthPx = (int)Math.Min(maxTextWidthPx, Math.Ceiling(measure.MaxLineWidth * guiScale));
            var textHeightPx = (int)Math.Ceiling(measure.TotalHeight * guiScale);
            textWidthPx = Math.Max(1, textWidthPx);
            textHeightPx = Math.Max(1, textHeightPx);

            // Reflow/align using the final width for correct centering.
            var finalBounds = ElementBounds.FixedSize(textWidthPx / (double)guiScale, textHeightPx / (double)guiScale);
            finalBounds.ParentBounds = ElementBounds.Empty;
            var rich = new GuiElementRichtext(capi, comps, finalBounds);
            rich.BeforeCalcBounds();

            var surfaceWidth = textWidthPx + 2 * background.HorPadding;
            var bubbleHeight = textHeightPx + 2 * background.VerPadding;
            var surfaceHeight = bubbleHeight + Math.Max(0, extraBottomMarginPx);

            using var surface = new ImageSurface(Format.Argb32, surfaceWidth, surfaceHeight);
            using var ctx = new Context(surface);

            // Background (do not include transparent bottom margin)
            GuiElement.RoundRectangle(ctx, 0, 0, surfaceWidth, bubbleHeight, background.Radius);
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

            // Render richtext at padded offset
            var offsetBounds = ElementBounds.Fixed(
                background.HorPadding / (double)guiScale,
                background.VerPadding / (double)guiScale,
                textWidthPx / (double)guiScale,
                textHeightPx / (double)guiScale
            );
            offsetBounds.ParentBounds = ElementBounds.Empty;
            rich.ComposeFor(offsetBounds, ctx, surface);

            var tex = new LoadedTexture(capi);
            capi.Gui.LoadOrUpdateCairoTexture(surface, linearMag: false, ref tex);
            return tex;
        }
        catch
        {
            return null;
        }
    }
}
