using SkiaSharp;
using TheBasics.GuiPreview;
using Xunit;

namespace thebasics.Tests.GuiPreview;

public class ImageComparisonTests
{
    [Fact]
    public void Changed_pixel_fails_and_produces_a_reviewable_diff()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gui-diff-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            using var expected = new SKBitmap(2, 1);
            expected.Erase(SKColors.White);
            using var actual = new SKBitmap(2, 1);
            actual.Erase(SKColors.White);
            actual.SetPixel(1, 0, SKColors.Black);
            Write(expected, Path.Combine(directory, "expected.png"));
            Write(actual, Path.Combine(directory, "actual.png"));
            var result = ImageComparison.Compare(Path.Combine(directory, "expected.png"), Path.Combine(directory, "actual.png"), Path.Combine(directory, "diff.png"));
            Assert.False(result.Matches);
            Assert.Equal(1, result.ChangedPixels);
            Assert.True(File.Exists(Path.Combine(directory, "diff.png")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static void Write(SKBitmap bitmap, string path)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var output = File.Create(path);
        data.SaveTo(output);
    }
}
