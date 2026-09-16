using TheBasics.GuiPreview;
using Vintagestory.API.MathTools;
using Xunit;

namespace thebasics.Tests.GuiPreview;

public class SoftwareCanvasTests
{
    [Fact]
    public void StandardOverOpaqueAndTintUseShaderChannels()
    {
        var c = new SoftwareCanvas(1, 1);
        c.Clear(0, 0, 255, 255);
        int id = c.UploadTexture([0, 0, 255, 128], 1, 1, 4);
        c.DrawTexture(id, 0, 0, 1, 1, tint: new Vec4f(0.5f, 1, 1, 1));
        Assert.Equal(new byte[] { 64, 0, 127, 191 }, c.GetRgbaPixels());
    }

    [Fact]
    public void TransparentNearFragmentWritesDepthButDisabledDepthDoesNot()
    {
        var c = new SoftwareCanvas(1, 1);
        int translucent = c.UploadTexture([0, 0, 255, 128], 1, 1, 4);
        int blue = c.UploadTexture([255, 0, 0, 255], 1, 1, 4);
        c.DrawTexture(translucent, 0, 0, 1, 1, 100);
        c.DrawTexture(blue, 0, 0, 1, 1, 50);
        Assert.Equal(new byte[] { 128, 0, 0, 64 }, c.GetRgbaPixels());
        c.Clear(); c.DepthTestEnabled = false;
        c.DrawTexture(translucent, 0, 0, 1, 1, 100);
        c.DepthTestEnabled = true;
        c.DrawTexture(blue, 0, 0, 1, 1, 50);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, c.GetRgbaPixels());
    }

    [Fact]
    public void FocusOutlineLeavesInteriorUntouched()
    {
        var c = new SoftwareCanvas(3, 3);
        c.DrawRectangle(0, 0, 50, 2, 2, unchecked((int)0xffff0000));
        var rgba = c.GetRgbaPixels();
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, rgba[16..20]);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rgba[..4]);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, rgba[32..36]);
    }

    [Fact]
    public void SameSizeUploadRetainsFilterAndPngRetainsRawRgba()
    {
        var c = new SoftwareCanvas(4, 1);
        byte[] source = [0, 0, 255, 128, 255, 0, 0, 128];
        int id = c.UploadTexture(source, 2, 1, 8);
        c.UploadTexture(source, 2, 1, 8, true, id);
        c.DrawTexture(id, 0, 0, 4, 1);
        Assert.Equal(new byte[] { 128, 0, 0, 64 }, c.GetRgbaPixels()[..4]);
        string path = Path.Combine(Path.GetTempPath(), $"gui-compositor-{Guid.NewGuid():N}.png");
        try
        {
            c.SavePng(path);
            using var codec = SkiaSharp.SKCodec.Create(path);
            using var bitmap = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(4, 1, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul));
            Assert.Equal(SkiaSharp.SKCodecResult.Success, codec.GetPixels(bitmap.Info, bitmap.GetPixels()));
            byte[] actual = new byte[16];
            System.Runtime.InteropServices.Marshal.Copy(bitmap.GetPixels(), actual, 0, 16);
            Assert.Equal(c.GetRgbaPixels(), actual);
        }
        finally { File.Delete(path); }
    }
    [Theory]
    [InlineData(false, 128, 64)]
    [InlineData(true, 255, 128)]
    public void BlendUsesGlFactorsForBothColorAndAlpha(bool premul, byte red, byte alpha)
    {
        var c = new SoftwareCanvas(1, 1);
        int id = c.UploadTexture([0, 0, 255, 128], 1, 1, 4);
        c.DrawTexture(id, 0, 0, 1, 1, premultipliedAlpha: premul);
        Assert.Equal(new byte[] { red, 0, 0, alpha }, c.GetRgbaPixels());
    }

    [Fact]
    public void UploadCopiesStrideAndReplacementDeletionAreObservable()
    {
        var c = new SoftwareCanvas(1, 2);
        byte[] data = [0, 0, 255, 255, 123, 123, 123, 123, 0, 255, 0, 255];
        int id = c.UploadTexture(data, 1, 2, 8);
        Array.Fill<byte>(data, 0);
        c.DrawTexture(id, 0, 0, 1, 2);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 0, 255, 0, 255 }, c.GetRgbaPixels());
        Assert.Equal(id, c.UploadTexture([255, 0, 0, 255], 1, 1, 4, textureId: id));
        c.DrawTexture(id, 0, 0, 1, 2);
        Assert.Equal(new byte[] { 0, 0, 255, 255, 0, 0, 255, 255 }, c.GetRgbaPixels());
        c.DeleteTexture(id);
        Assert.Throws<InvalidOperationException>(() => c.DrawTexture(id, 0, 0, 1, 1));
    }

    [Fact]
    public void MagnificationNearestVersusLinearAndMinificationLinear()
    {
        byte[] colors = [0, 0, 255, 255, 255, 0, 0, 255];
        var c = new SoftwareCanvas(4, 1);
        int nearest = c.UploadTexture(colors, 2, 1, 8);
        c.DrawTexture(nearest, 0, 0, 4, 1);
        Assert.Equal(new byte[] { 255, 0, 0, 255, 255, 0, 0, 255, 0, 0, 255, 255, 0, 0, 255, 255 }, c.GetRgbaPixels());
        int linear = c.UploadTexture(colors, 2, 1, 8, true);
        c.DrawTexture(linear, 0, 0, 4, 1);
        // Default GL_REPEAT: the samples outside the first/last texel center wrap.
        Assert.Equal(new byte[] { 191, 0, 64, 255, 191, 0, 64, 255, 64, 0, 191, 255, 64, 0, 191, 255 }, c.GetRgbaPixels());
        c.Clear();
        c.DrawTexture(nearest, 0, 0, 1, 1);
        Assert.Equal(new byte[] { 128, 0, 128, 255 }, c.GetRgbaPixels()[..4]);
    }

    [Fact]
    public void DepthEqualPassesNearerWinsAndDiscardDoesNotWriteDepth()
    {
        var c = new SoftwareCanvas(1, 1);
        int red = c.UploadTexture([0, 0, 255, 255], 1, 1, 4);
        int blue = c.UploadTexture([255, 0, 0, 255], 1, 1, 4);
        int empty = c.UploadTexture([0, 255, 0, 0], 1, 1, 4);
        c.DrawTexture(red, 0, 0, 1, 1, 20);
        c.DrawTexture(blue, 0, 0, 1, 1, 10);
        Assert.Equal((byte)255, c.GetRgbaPixels()[0]);
        c.DrawTexture(empty, 0, 0, 1, 1, 40);
        c.DrawTexture(blue, 0, 0, 1, 1, 20);
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, c.GetRgbaPixels());
    }

    [Fact]
    public void TranslationRestoresZAndRawScissorUsesBottomOrigin()
    {
        var c = new SoftwareCanvas(2, 2);
        int red = c.UploadTexture([0, 0, 255, 255], 1, 1, 4);
        c.SetScissor(1, 0, 1, 1);
        c.ScissorEnabled = true;
        c.PushMatrix(); c.Translate(1, 1, 20);
        c.DrawTexture(red, 0, 0, 1, 1, 0);
        c.PopMatrix();
        c.ScissorEnabled = false;
        c.DrawTexture(red, 0, 0, 1, 1, 0, new Vec4f(0, 1, 1, 1));
        Assert.Equal(new byte[] { 0, 0, 0, 255, 0, 0, 0, 0, 0, 0, 0, 0, 255, 0, 0, 255 }, c.GetRgbaPixels());
    }
}
