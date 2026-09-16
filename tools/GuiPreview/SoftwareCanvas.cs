using System.Runtime.InteropServices;
using SkiaSharp;
using Vintagestory.API.MathTools;

namespace TheBasics.GuiPreview;

/// <summary>Bounded software implementation of the 1.22.6 GUI texture path. Pixels retain GL
/// framebuffer channel values, including standard blending's alpha-squared behavior.</summary>
public sealed class SoftwareCanvas
{
    private sealed record Texture(int Width, int Height, byte[] Pixels, bool LinearMagnification);
    private readonly Dictionary<int, Texture> textures = new();
    private readonly Stack<(double X, double Y, double Z)> matrices = new();
    private readonly byte[] pixels;
    private readonly double[] depth;
    private (double X, double Y, double Z) translation;
    private (int X, int Y, int Width, int Height) scissor;
    private int nextTextureId = 1;
    public int Width { get; }
    public int Height { get; }
    public bool ScissorEnabled { get; set; }
    public bool DepthTestEnabled { get; set; } = true;
    public bool DepthWriteEnabled { get; set; } = true;
    public double AlphaTest { get; set; }
    public List<object> Commands { get; } = new();

    public SoftwareCanvas(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width; Height = height;
        pixels = new byte[checked(width * height * 4)];
        depth = new double[checked(width * height)];
        Clear();
    }

    public void Clear(byte r = 0, byte g = 0, byte b = 0, byte a = 0)
    {
        for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = r; pixels[i + 1] = g; pixels[i + 2] = b; pixels[i + 3] = a; }
        Array.Fill(depth, double.NegativeInfinity);
        Commands.Clear();
    }

    public int UploadTexture(byte[] bgra, int width, int height, int stride, bool linearMagnification = false, int textureId = 0)
    {
        ArgumentNullException.ThrowIfNull(bgra);
        if (width <= 0 || height <= 0 || stride < checked(width * 4) || bgra.Length < checked((height - 1) * stride + width * 4))
            throw new ArgumentException("Invalid BGRA dimensions or row stride.");
        if (textureId < 0) throw new ArgumentOutOfRangeException(nameof(textureId));
        if (textureId == 0) textureId = nextTextureId++;
        else nextTextureId = Math.Max(nextTextureId, checked(textureId + 1));
        if (textures.TryGetValue(textureId, out var old) && old.Width == width && old.Height == height)
            linearMagnification = old.LinearMagnification; // TexSubImage2D does not reset filtering.
        var rgba = new byte[checked(width * height * 4)];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        {
            int src = y * stride + x * 4, dst = (y * width + x) * 4;
            rgba[dst] = bgra[src + 2]; rgba[dst + 1] = bgra[src + 1]; rgba[dst + 2] = bgra[src]; rgba[dst + 3] = bgra[src + 3];
        }
        textures[textureId] = new(width, height, rgba, linearMagnification);
        Commands.Add(new { op = "upload", textureId, width, height, stride, linearMagnification });
        return textureId;
    }

    public void DeleteTexture(int id) { textures.Remove(id); Commands.Add(new { op = "delete", id }); }
    public void PushMatrix() { matrices.Push(translation); Commands.Add(new { op = "pushMatrix" }); }
    public void PopMatrix()
    {
        if (matrices.Count == 0) throw new InvalidOperationException("Unbalanced GUI matrix pop.");
        translation = matrices.Pop(); Commands.Add(new { op = "popMatrix" });
    }
    public void Translate(double x, double y, double z)
    {
        Validate(x, y, z); translation = (translation.X + x, translation.Y + y, translation.Z + z);
        Commands.Add(new { op = "translate", x, y, z });
    }
    public void SetScissor(int x, int bottomY, int width, int height)
    {
        if (width < 0 || height < 0) throw new ArgumentOutOfRangeException(nameof(width));
        scissor = (x, Height - bottomY - height, width, height);
        Commands.Add(new { op = "scissor", x, bottomY, width, height });
    }

    public void DrawTexture(int id, double x, double y, double width, double height, double z = 50, Vec4f? tint = null, bool premultipliedAlpha = false)
    {
        if (!textures.TryGetValue(id, out var texture)) throw new InvalidOperationException($"Unknown GUI texture {id}.");
        Validate(x, y, width, height, z);
        if (width < 0 || height < 0) throw new NotSupportedException("Mirrored GUI rectangles require an explicit supported transform.");
        double tx = x + translation.X, ty = y + translation.Y, tz = z + translation.Z;
        bool linear = texture.LinearMagnification || width < texture.Width || height < texture.Height;
        Commands.Add(new
        {
            op = "texture",
            id,
            x = tx,
            y = ty,
            width,
            height,
            z = tz,
            premultipliedAlpha,
            linear,
            sourceWidth = texture.Width,
            sourceHeight = texture.Height,
            tint = tint == null ? null : new[] { tint.R, tint.G, tint.B, tint.A },
            scissorEnabled = ScissorEnabled
        });
        if (width == 0 || height == 0) return;
        Raster(tx, ty, width, height, tz, (px, py, channel) =>
        {
            double u = (px + 0.5 - tx) / width * texture.Width - 0.5, v = (py + 0.5 - ty) / height * texture.Height - 0.5;
            double sample;
            if (!linear) sample = Texel(texture, (int)Math.Floor(u + 0.5), (int)Math.Floor(v + 0.5), channel);
            else
            {
                int ix = (int)Math.Floor(u), iy = (int)Math.Floor(v); double fx = u - ix, fy = v - iy;
                sample = (Texel(texture, ix, iy, channel) * (1 - fx) + Texel(texture, ix + 1, iy, channel) * fx) * (1 - fy)
                    + (Texel(texture, ix, iy + 1, channel) * (1 - fx) + Texel(texture, ix + 1, iy + 1, channel) * fx) * fy;
            }
            double multiplier = tint == null ? 1 : channel switch { 0 => tint.R, 1 => tint.G, 2 => tint.B, _ => tint.A };
            return Math.Clamp(sample / 255 * multiplier, 0, 1);
        }, premultipliedAlpha);
    }

    public void DrawRectangle(double x, double y, double z, double width, double height, int color)
    {
        Validate(x, y, z, width, height);
        if (width < 0 || height < 0) throw new NotSupportedException("Negative outline dimensions.");
        // GL_LINES, one physical pixel. Interior is untouched. Pixel-boundary tie-breaking
        // is a software convention pending real-driver calibration.
        double tx = x + translation.X, ty = y + translation.Y, tz = z + translation.Z;
        double[] rgba = [((color >> 16) & 255) / 255d, ((color >> 8) & 255) / 255d, (color & 255) / 255d, ((uint)color >> 24) / 255d];
        Commands.Add(new { op = "outline", x = tx, y = ty, z = tz, width, height, color });
        Raster(tx, ty, width + 1, height + 1, tz, (px, py, ch) => rgba[ch], false,
            (px, py) => px == (int)Math.Floor(tx) || px == (int)Math.Floor(tx + width) || py == (int)Math.Floor(ty) || py == (int)Math.Floor(ty + height));
    }

    private void Raster(double x, double y, double width, double height, double z, Func<int, int, int, double> sample, bool premul, Func<int, int, bool>? covered = null)
    {
        // OrthoMode: near=.4, far=20001, model-view translation=-19849.
        if (z < -152 || z > 19848.6) return;
        int left = (int)Math.Clamp(Math.Ceiling(x - 0.5), 0, Width), right = (int)Math.Clamp(Math.Ceiling(x + width - 0.5), 0, Width);
        int top = (int)Math.Clamp(Math.Ceiling(y - 0.5), 0, Height), bottom = (int)Math.Clamp(Math.Ceiling(y + height - 0.5), 0, Height);
        for (int py = top; py < bottom; py++) for (int px = left; px < right; px++)
        {
            if (covered != null && !covered(px, py)) continue;
            if (ScissorEnabled && (px < scissor.X || py < scissor.Y || px >= (long)scissor.X + scissor.Width || py >= (long)scissor.Y + scissor.Height)) continue;
            int pos = py * Width + px;
            if (DepthTestEnabled && z < depth[pos]) continue; // LEQUAL in projected depth.
            double alpha = sample(px, py, 3);
            if (alpha <= AlphaTest) continue;
            double factor = premul ? 1 : alpha;
            for (int ch = 0; ch < 4; ch++) pixels[pos * 4 + ch] = Byte(sample(px, py, ch) * factor + pixels[pos * 4 + ch] / 255d * (1 - alpha));
            if (DepthTestEnabled && DepthWriteEnabled) depth[pos] = z;
        }
    }
    private static double Texel(Texture texture, int x, int y, int channel)
    {
        x = (x % texture.Width + texture.Width) % texture.Width; y = (y % texture.Height + texture.Height) % texture.Height;
        return texture.Pixels[(y * texture.Width + x) * 4 + channel];
    }
    private static byte Byte(double value) => (byte)Math.Clamp(Math.Round(value * 255, MidpointRounding.AwayFromZero), 0, 255);
    private static void Validate(params double[] values)
    {
        if (values.Any(v => !double.IsFinite(v))) throw new ArgumentException("GUI coordinates must be finite.");
    }
    public byte[] GetRgbaPixels() => (byte[])pixels.Clone();
    public void SavePng(string path)
    {
        using var bitmap = new SKBitmap(new SKImageInfo(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path); encoded.SaveTo(stream);
    }
}
