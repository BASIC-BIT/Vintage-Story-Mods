using SkiaSharp;

namespace TheBasics.GuiPreview;

public sealed record ImageDifference(bool Matches, int ChangedPixels, bool SameDimensions);

/// <summary>Compares decoded pixels; never creates or replaces an approved baseline.</summary>
public static class ImageComparison
{
    public static ImageDifference Compare(string expectedPath, string actualPath, string diffPath, byte channelTolerance = 0)
    {
        var destination = Path.GetFullPath(diffPath);
        if (string.Equals(destination, Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase)
            || string.Equals(destination, Path.GetFullPath(actualPath), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The diff must not overwrite either input image.", nameof(diffPath));
        using var expected = Decode(expectedPath);
        using var actual = Decode(actualPath);
        var sameDimensions = expected.Width == actual.Width && expected.Height == actual.Height;
        using var diff = new SKBitmap(Math.Max(expected.Width, actual.Width), Math.Max(expected.Height, actual.Height));
        var changed = 0;
        for (var y = 0; y < diff.Height; y++)
            for (var x = 0; x < diff.Width; x++)
            {
                var present = x < expected.Width && x < actual.Width && y < expected.Height && y < actual.Height;
                var left = x < expected.Width && y < expected.Height ? expected.GetPixel(x, y) : SKColors.Transparent;
                var right = x < actual.Width && y < actual.Height ? actual.GetPixel(x, y) : SKColors.Transparent;
                var differs = !present || Math.Abs(left.Red - right.Red) > channelTolerance
                    || Math.Abs(left.Green - right.Green) > channelTolerance || Math.Abs(left.Blue - right.Blue) > channelTolerance
                    || Math.Abs(left.Alpha - right.Alpha) > channelTolerance;
                if (differs) { changed++; diff.SetPixel(x, y, SKColors.Magenta); }
                else
                {
                    var gray = (byte)((right.Red + right.Green + right.Blue) / 6);
                    diff.SetPixel(x, y, new SKColor(gray, gray, gray, 255));
                }
            }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        using var encoded = diff.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(destination);
        encoded.SaveTo(stream);
        return new ImageDifference(sameDimensions && changed == 0, changed, sameDimensions);
    }

    private static SKBitmap Decode(string path)
    {
        using var input = File.OpenRead(path);
        using var codec = SKCodec.Create(input) ?? throw new InvalidDataException("Invalid image: " + path);
        if (codec.Info.Width is <= 0 or > 4096 || codec.Info.Height is <= 0 or > 4096)
            throw new InvalidDataException("Image dimensions must be 1..4096 pixels per axis.");
        return SKBitmap.Decode(codec, new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul))
            ?? throw new InvalidDataException("Could not decode image: " + path);
    }
}
