using System;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ChatUiSystem;

/// <summary>
/// Builds the composite nametag texture: an MMO-style framed headshot on the left, separated
/// from the (existing chat-tech) text bubble on the right with a small gap, vertically centered.
/// When no headshot bytes are available, returns just the text bubble.
/// </summary>
internal static class NametagComposer
{
    private const int FrameBorderWidthPx = 3;
    private const double FrameCornerRadius = 6.0;
    /// <summary>Px gap between the framed headshot and the text bubble. Keeps the two visually distinct.</summary>
    private const int InterElementGapPx = 16;

    // Subtle vertical brown gradient, lighter in the middle to mimic MMO portrait frames.
    private static readonly Color FrameGradientTop = new(0.42, 0.30, 0.18, 1.0);
    private static readonly Color FrameGradientMid = new(0.55, 0.40, 0.24, 1.0);
    private static readonly Color FrameGradientBottom = new(0.32, 0.22, 0.13, 1.0);

    public sealed class Options
    {
        public string Vtml { get; init; }
        public CairoFont BaseFont { get; init; }
        public int MaxTextWidthPx { get; init; } = 400;
        public TextBackground TextBackground { get; init; }
        public BitmapExternal HeadshotBitmap { get; init; }
        public int HeadshotRenderSizePx { get; init; }
    }

    public static LoadedTexture Compose(ICoreClientAPI capi, Options options)
    {
        if (capi == null || options == null || string.IsNullOrWhiteSpace(options.Vtml))
        {
            return null;
        }

        ImageSurface textSurface = null;
        ImageSurface frameSurface = null;
        try
        {
            textSurface = RichTextTextureUtils.GenRichTextSurface(
                capi,
                options.Vtml,
                options.BaseFont,
                options.MaxTextWidthPx,
                options.TextBackground);
            if (textSurface == null)
            {
                return null;
            }

            var hasHeadshot = options.HeadshotBitmap != null && options.HeadshotRenderSizePx > 0;
            if (hasHeadshot)
            {
                // Decode failures here are recoverable — fall back to a text-only nametag rather than
                // failing the whole render and showing nothing.
                try
                {
                    frameSurface = BuildFramedHeadshotSurface(options.HeadshotBitmap, options.HeadshotRenderSizePx);
                }
                catch
                {
                    frameSurface = null;
                }
            }

            var textWidth = textSurface.Width;
            var textHeight = textSurface.Height;
            var frameWidth = frameSurface?.Width ?? 0;
            var frameHeight = frameSurface?.Height ?? 0;

            var compositeWidth = frameWidth + (frameSurface != null ? InterElementGapPx : 0) + textWidth;
            var compositeHeight = Math.Max(textHeight, frameHeight);

            using var composite = new ImageSurface(Format.Argb32, compositeWidth, compositeHeight);
            using var ctx = new Context(composite);

            if (frameSurface != null)
            {
                var frameY = (compositeHeight - frameHeight) / 2;
                ctx.SetSourceSurface(frameSurface, 0, frameY);
                ctx.Rectangle(0, frameY, frameWidth, frameHeight);
                ctx.Fill();
            }

            var textX = frameSurface != null ? frameWidth + InterElementGapPx : 0;
            var textY = (compositeHeight - textHeight) / 2;
            ctx.SetSourceSurface(textSurface, textX, textY);
            ctx.Rectangle(textX, textY, textWidth, textHeight);
            ctx.Fill();

            var tex = new LoadedTexture(capi);
            capi.Gui.LoadOrUpdateCairoTexture(composite, linearMag: false, ref tex);
            return tex;
        }
        catch
        {
            return null;
        }
        finally
        {
            textSurface?.Dispose();
            frameSurface?.Dispose();
        }
    }

    /// <summary>
    /// Renders the headshot bitmap onto a square surface with a subtle vertical brown gradient
    /// border. The frame and inner image share the same rounded-corner radius so they read as a
    /// single MMO-style portrait card.
    /// </summary>
    private static ImageSurface BuildFramedHeadshotSurface(BitmapExternal bitmap, int renderSizePx)
    {
        var border = FrameBorderWidthPx;
        var totalSize = renderSizePx + 2 * border;
        var surface = new ImageSurface(Format.Argb32, totalSize, totalSize);
        try
        {
            using var ctx = new Context(surface);

            using var gradient = new LinearGradient(0, 0, 0, totalSize);
            gradient.AddColorStop(0, FrameGradientTop);
            gradient.AddColorStop(0.5, FrameGradientMid);
            gradient.AddColorStop(1, FrameGradientBottom);
            ctx.SetSource(gradient);
            GuiElement.RoundRectangle(ctx, 0, 0, totalSize, totalSize, FrameCornerRadius);
            ctx.Fill();

            // Inset image, clipped to a smaller rounded rect so the image's hard corners don't
            // poke past the frame.
            using var imgSurface = GuiElement.getImageSurfaceFromAsset(bitmap, renderSizePx, renderSizePx);
            ctx.Save();
            GuiElement.RoundRectangle(ctx, border, border, renderSizePx, renderSizePx, Math.Max(0, FrameCornerRadius - border / 2.0));
            ctx.Clip();
            ctx.SetSourceSurface(imgSurface, border, border);
            ctx.Rectangle(border, border, renderSizePx, renderSizePx);
            ctx.Fill();
            ctx.Restore();

            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }
}
