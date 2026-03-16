using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

internal static class RichTextTextureUtils
{
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
        if (capi == null || string.IsNullOrWhiteSpace(vtml))
        {
            return null;
        }

        try
        {
            var guiScale = Math.Max(1, RuntimeEnv.GUIScale);

            // Pass 1: measure at max width to get actual text dimensions.
            var measureComps = VtmlUtil.Richtextify(capi, vtml, baseFont);
            var measureBounds = ElementBounds.FixedSize(maxTextWidthPx / (double)guiScale, 600 / (double)guiScale);
            measureBounds.ParentBounds = ElementBounds.Empty;
            var measure = new GuiElementRichtext(capi, measureComps, measureBounds);
            measure.BeforeCalcBounds();

            var textWidthPx = (int)Math.Min(maxTextWidthPx, Math.Ceiling(measure.MaxLineWidth * guiScale));
            var textHeightPx = (int)Math.Ceiling(measure.TotalHeight * guiScale);
            textWidthPx = Math.Max(1, textWidthPx);
            textHeightPx = Math.Max(1, textHeightPx);

            // Pass 2: fresh components laid out at the measured tight width.
            // Left alignment avoids the VS centering bug with mixed-font inline components.
            var renderComps = VtmlUtil.Richtextify(capi, vtml, baseFont);
            var finalBounds = ElementBounds.FixedSize(textWidthPx / (double)guiScale, textHeightPx / (double)guiScale);
            finalBounds.ParentBounds = ElementBounds.Empty;
            var rich = new GuiElementRichtext(capi, renderComps, finalBounds);
            rich.BeforeCalcBounds();

            var surfaceWidth = textWidthPx + 2 * background.HorPadding;
            var bubbleHeight = textHeightPx + 2 * background.VerPadding;
            var surfaceHeight = bubbleHeight + Math.Max(0, extraBottomMarginPx);

            using var surface = new ImageSurface(Format.Argb32, surfaceWidth, surfaceHeight);
            using var ctx = new Context(surface);

            // Background (do not include transparent bottom margin).
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

            // Render text at a centered offset within the bubble.
            // Horizontal: center the text block within the surface width.
            // Vertical: center the text block within the bubble height (excluding bottom margin).
            // We use Left alignment in the font to avoid a VS centering bug at inline
            // tag boundaries, but manually center the rendered block within the surface.
            var hPad = (surfaceWidth - textWidthPx) / 2.0;
            var vPad = (bubbleHeight - textHeightPx) / 2.0;
            var offsetBounds = ElementBounds.Fixed(
                hPad / guiScale,
                vPad / guiScale,
                textWidthPx / (double)guiScale,
                textHeightPx / (double)guiScale
            );
            offsetBounds.ParentBounds = ElementBounds.Empty;
            rich.ComposeFor(offsetBounds, ctx, surface);

            // Explicitly clear the bottom margin region so no rendering artifacts
            // (from ComposeFor overflow or Cairo anti-aliasing) leak into the
            // transparent spacing that separates the bubble from the nametag.
            if (extraBottomMarginPx > 0)
            {
                ctx.Save();
                ctx.Operator = Operator.Clear;
                ctx.Rectangle(0, bubbleHeight, surfaceWidth, extraBottomMarginPx);
                ctx.Fill();
                ctx.Restore();
            }

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
