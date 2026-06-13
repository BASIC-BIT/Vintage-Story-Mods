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

    public sealed class Options
    {
        public string Vtml { get; init; }
        public CairoFont BaseFont { get; init; }
        public int MaxTextWidthPx { get; init; } = 400;
        public TextBackground TextBackground { get; init; }
        public BitmapExternal HeadshotBitmap { get; init; }
        public int HeadshotRenderSizePx { get; init; }
        public double[] HeadshotBorderColor { get; init; }
    }

    public static LoadedTexture Compose(ICoreClientAPI capi, Options options)
    {
        if (!HasValidOptions(capi, options))
        {
            return null;
        }

        ImageSurface textSurface = null;
        ImageSurface frameSurface = null;
        try
        {
            textSurface = CreateTextSurface(capi, options);
            if (textSurface == null)
            {
                return null;
            }

            frameSurface = TryBuildFramedHeadshotSurface(options);
            return BuildCompositeTexture(capi, textSurface, frameSurface);
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

    private static bool HasValidOptions(ICoreClientAPI capi, Options options)
    {
        return capi != null && options != null && !string.IsNullOrWhiteSpace(options.Vtml);
    }

    private static ImageSurface CreateTextSurface(ICoreClientAPI capi, Options options)
    {
        return RichTextTextureUtils.GenRichTextSurface(
            capi,
            options.Vtml,
            options.BaseFont,
            options.MaxTextWidthPx,
            options.TextBackground);
    }

    private static ImageSurface TryBuildFramedHeadshotSurface(Options options)
    {
        if (options.HeadshotBitmap == null || options.HeadshotRenderSizePx <= 0)
        {
            return null;
        }

        try
        {
            return BuildFramedHeadshotSurface(options.HeadshotBitmap, options.HeadshotRenderSizePx, options.HeadshotBorderColor);
        }
        catch
        {
            return null;
        }
    }

    private static LoadedTexture BuildCompositeTexture(ICoreClientAPI capi, ImageSurface textSurface, ImageSurface frameSurface)
    {
        var textWidth = textSurface.Width;
        var textHeight = textSurface.Height;
        var frameWidth = frameSurface?.Width ?? 0;
        var frameHeight = frameSurface?.Height ?? 0;
        var compositeWidth = frameWidth + (frameSurface != null ? InterElementGapPx : 0) + textWidth;
        var compositeHeight = Math.Max(textHeight, frameHeight);

        using var composite = new ImageSurface(Format.Argb32, compositeWidth, compositeHeight);
        using var ctx = new Context(composite);
        ClearSurface(ctx);
        DrawComposite(ctx, textSurface, frameSurface, compositeHeight);

        var tex = new LoadedTexture(capi);
        capi.Gui.LoadOrUpdateCairoTexture(composite, linearMag: false, ref tex);
        return tex;
    }

    private static void DrawComposite(Context ctx, ImageSurface textSurface, ImageSurface frameSurface, int compositeHeight)
    {
        var frameWidth = frameSurface?.Width ?? 0;
        if (frameSurface != null)
        {
            var frameY = (compositeHeight - frameSurface.Height) / 2;
            ctx.SetSourceSurface(frameSurface, 0, frameY);
            ctx.Rectangle(0, frameY, frameSurface.Width, frameSurface.Height);
            ctx.Fill();
        }

        var textX = frameSurface != null ? frameWidth + InterElementGapPx : 0;
        var textY = (compositeHeight - textSurface.Height) / 2;
        ctx.SetSourceSurface(textSurface, textX, textY);
        ctx.Rectangle(textX, textY, textSurface.Width, textSurface.Height);
        ctx.Fill();
    }

    /// <summary>
    /// Renders the headshot bitmap onto a square surface with the same border color as the text
    /// bubble. The frame and inner image share the same rounded-corner radius so they read as one
    /// nameplate.
    /// </summary>
    private static ImageSurface BuildFramedHeadshotSurface(BitmapExternal bitmap, int renderSizePx, double[] borderColor)
    {
        var border = FrameBorderWidthPx;
        var totalSize = renderSizePx + 2 * border;
        var surface = new ImageSurface(Format.Argb32, totalSize, totalSize);
        try
        {
            using var ctx = new Context(surface);
            ClearSurface(ctx);

            ctx.SetSourceRGBA(borderColor ?? GuiStyle.DialogBorderColor);
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

    private static void ClearSurface(Context ctx)
    {
        ctx.Save();
        ctx.Operator = Operator.Clear;
        ctx.Paint();
        ctx.Restore();
    }
}
