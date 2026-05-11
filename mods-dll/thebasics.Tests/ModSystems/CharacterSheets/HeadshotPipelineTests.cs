using System.IO;
using FluentAssertions;
using SkiaSharp;
using thebasics.ModSystems.CharacterSheets;

namespace thebasics.Tests.ModSystems.CharacterSheets;

public class HeadshotPipelineTests
{
    private static HeadshotPipeline.NormalizeOptions DefaultOptions(int targetDim = 64, int maxOutputBytes = 256 * 1024, int maxDecoded = 4096)
    {
        return new HeadshotPipeline.NormalizeOptions(targetDim, maxOutputBytes, maxDecoded);
    }

    private static byte[] EncodePng(int width, int height, SKColor? fill = null)
    {
        using var bmp = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(fill ?? SKColors.Magenta);
        }

        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static byte[] EncodeJpeg(int width, int height)
    {
        using var bmp = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.Cyan);
        }

        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }

    [Fact]
    public void Normalize_ValidPng_ProducesPngOfTargetDimension()
    {
        var input = EncodePng(800, 600);
        var result = HeadshotPipeline.Normalize(input, DefaultOptions());
        result.Ok.Should().BeTrue();
        result.Width.Should().Be(64);
        result.Height.Should().Be(64);
        HeadshotPipeline.IsPng(result.PngBytes).Should().BeTrue();
        result.Hash.Should().HaveLength(64);
    }

    [Fact]
    public void Normalize_ValidJpeg_AlsoAccepted()
    {
        var input = EncodeJpeg(300, 300);
        var result = HeadshotPipeline.Normalize(input, DefaultOptions());
        result.Ok.Should().BeTrue();
        HeadshotPipeline.IsPng(result.PngBytes).Should().BeTrue();
    }

    [Fact]
    public void Normalize_NonPngOrJpeg_Rejected()
    {
        var input = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }; // GIF magic
        var result = HeadshotPipeline.Normalize(input, DefaultOptions());
        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be("unsupported-format");
    }

    [Fact]
    public void Normalize_EmptyBytes_Rejected()
    {
        var result = HeadshotPipeline.Normalize(System.Array.Empty<byte>(), DefaultOptions());
        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be("empty");
    }

    [Fact]
    public void Normalize_RejectsImageExceedingDecodedDimensionGuard()
    {
        var input = EncodePng(2000, 2000);
        var options = DefaultOptions(maxDecoded: 1024);
        var result = HeadshotPipeline.Normalize(input, options);
        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be("dimensions-exceeded");
    }

    [Fact]
    public void Normalize_CenterCropsRectangularInputToSquare()
    {
        var input = EncodePng(800, 200);
        var result = HeadshotPipeline.Normalize(input, DefaultOptions());
        result.Ok.Should().BeTrue();
        result.Width.Should().Be(result.Height);
    }

    [Fact]
    public void Normalize_OutputSizeRespectsCapWhenDownscaling()
    {
        var input = EncodePng(2000, 2000);
        var options = DefaultOptions(targetDim: 128);
        var result = HeadshotPipeline.Normalize(input, options);
        result.Ok.Should().BeTrue();
        result.Width.Should().Be(128);
        result.Height.Should().Be(128);
    }

    [Fact]
    public void Normalize_DoesNotUpscale_KeepsOriginalDimensionWhenSmaller()
    {
        var input = EncodePng(32, 32);
        var options = DefaultOptions(targetDim: 256);
        var result = HeadshotPipeline.Normalize(input, options);
        result.Ok.Should().BeTrue();
        result.Width.Should().Be(32);
        result.Height.Should().Be(32);
    }

    [Fact]
    public void Normalize_HashIsDeterministicForSameInput()
    {
        var input = EncodePng(64, 64);
        var a = HeadshotPipeline.Normalize(input, DefaultOptions());
        var b = HeadshotPipeline.Normalize(input, DefaultOptions());
        a.Ok.Should().BeTrue();
        b.Ok.Should().BeTrue();
        a.Hash.Should().Be(b.Hash);
    }

    [Fact]
    public void Normalize_RejectsCorruptInput()
    {
        // PNG magic bytes followed by garbage.
        var input = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0xFF, 0x00, 0xFF };
        var result = HeadshotPipeline.Normalize(input, DefaultOptions());
        result.Ok.Should().BeFalse();
    }

    [Fact]
    public void IsPng_RecognizesValidMagic()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 };
        HeadshotPipeline.IsPng(bytes).Should().BeTrue();
    }

    [Fact]
    public void IsJpeg_RecognizesValidMagic()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 };
        HeadshotPipeline.IsJpeg(bytes).Should().BeTrue();
    }

    [Fact]
    public void IsPng_RejectsShortInput()
    {
        HeadshotPipeline.IsPng(new byte[] { 0x89, 0x50 }).Should().BeFalse();
        HeadshotPipeline.IsPng(null).Should().BeFalse();
    }

    [Fact]
    public void BuildMetadata_ReturnsNullWhenResultFailed()
    {
        var failed = new HeadshotPipeline.NormalizeResult(false, "oops", null, null, 0, 0);
        HeadshotPipeline.BuildMetadata(failed, 0).Should().BeNull();
    }

    [Fact]
    public void BuildMetadata_PopulatesFieldsFromResult()
    {
        var input = EncodePng(64, 64);
        var result = HeadshotPipeline.Normalize(input, DefaultOptions());
        var metadata = HeadshotPipeline.BuildMetadata(result, 1234567890);
        metadata.Should().NotBeNull();
        metadata!.Hash.Should().Be(result.Hash);
        metadata.Width.Should().Be(result.Width);
        metadata.Height.Should().Be(result.Height);
        metadata.UpdatedAtUnixMs.Should().Be(1234567890);
        metadata.ByteLength.Should().Be(result.PngBytes.Length);
    }

    [Fact]
    public void Normalize_RejectsOutputTooLarge()
    {
        // Generate a noisy PNG that doesn't compress well. 256x256 of random pixels won't fit in a tiny budget.
        var rand = new System.Random(42);
        using var bmp = new SKBitmap(256, 256);
        for (var y = 0; y < bmp.Height; y++)
        {
            for (var x = 0; x < bmp.Width; x++)
            {
                bmp.SetPixel(x, y, new SKColor((byte)rand.Next(256), (byte)rand.Next(256), (byte)rand.Next(256), 255));
            }
        }

        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        var input = data.ToArray();

        var options = DefaultOptions(targetDim: 256, maxOutputBytes: 4 * 1024); // 4 KB cap is far too tight for noise.
        var result = HeadshotPipeline.Normalize(input, options);
        result.Ok.Should().BeFalse();
        result.ErrorCode.Should().Be("output-too-large");
    }
}
