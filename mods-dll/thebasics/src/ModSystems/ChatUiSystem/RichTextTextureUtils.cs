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

            // Pass 2: tighten width using actual laid-out line width at the measured width.
            // Some mixed VTML runs overestimate width in pass 1 and leave extra right-side gap.
            var tightMeasureComps = VtmlUtil.Richtextify(capi, vtml, baseFont);
            var tightMeasureBounds = ElementBounds.FixedSize(textWidthPx / (double)guiScale, 600 / (double)guiScale);
            tightMeasureBounds.ParentBounds = ElementBounds.Empty;
            var tightMeasure = new GuiElementRichtext(capi, tightMeasureComps, tightMeasureBounds);
            tightMeasure.BeforeCalcBounds();

            var tightenedWidthPx = (int)Math.Min(maxTextWidthPx, Math.Ceiling(tightMeasure.MaxLineWidth * guiScale));
            tightenedWidthPx = Math.Max(1, tightenedWidthPx);

            // Re-layout once if we shrank width materially (can affect wrapping/height).
            if (tightenedWidthPx + 1 < textWidthPx)
            {
                textWidthPx = tightenedWidthPx;

                var reflowComps = VtmlUtil.Richtextify(capi, vtml, baseFont);
                var reflowBounds = ElementBounds.FixedSize(textWidthPx / (double)guiScale, 600 / (double)guiScale);
                reflowBounds.ParentBounds = ElementBounds.Empty;
                var reflow = new GuiElementRichtext(capi, reflowComps, reflowBounds);
                reflow.BeforeCalcBounds();

                textHeightPx = (int)Math.Ceiling(reflow.TotalHeight * guiScale);
            }
            else
            {
                textHeightPx = (int)Math.Ceiling(tightMeasure.TotalHeight * guiScale);
            }

            textHeightPx = Math.Max(1, textHeightPx);

            // Pass 3: fresh components laid out at the final tight width/height.
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

            // Render at padded offset. Vanilla's bubble renderer centers the
            // entire texture above the player, giving a centered appearance.
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
