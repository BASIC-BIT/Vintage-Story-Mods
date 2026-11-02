using System.Text.Json;
using TheBasics.GuiPreview;
using Vintagestory.API.Client;

namespace thebasics.Tests.GuiPreview;

[Collection("Standalone GUI")]
public class SoftwareSeraphPreviewTests
{
    [VisualTheory]
    [InlineData(1d)]
    [InlineData(1.25)]
    public void RealSkinClothingAndNativePosesRenderRepeatablyWithoutGameEffects(double scale)
    {
        using var host = Host(scale);
        using var actor = host.CreateGuide(host.Api, "Pip");
        var bounds = Bounds(host);
        actor.SetAnimation("idle", 0.75);
        var idle = Frame(host, actor, bounds);
        Assert.True(VisiblePixels(idle) > 500);
        Assert.True(VisiblePixels(idle) < 320 * 300);
        actor.SetAnimation("wave", 0.75);
        var wave = Frame(host, actor, bounds);
        Assert.False(idle.SequenceEqual(wave));
        actor.SetAnimation("wave", 0.75);
        Assert.Equal(wave, Frame(host, actor, bounds));
        Assert.Contains(host.AssetFiles, path => path.EndsWith("setup-guide.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(host.AssetFiles, path => path.Contains("seraph", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".json"));
        Assert.Contains(host.AssetFiles, path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(host.Canvas.Commands, command => JsonSerializer.SerializeToElement(command).GetProperty("op").GetString() == "seraph");
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
        var output = Environment.GetEnvironmentVariable("THEBASICS_GUI_OUTPUT");
        if (!string.IsNullOrEmpty(output))
        {
            Directory.CreateDirectory(output);
            host.Canvas.SavePng(Path.Combine(output, $"seraph-wave-{scale}.png"));
        }
    }

    [VisualTheory]
    [InlineData(1d)]
    public void PipAndMaraUseDifferentActualAppearanceFixtures(double scale)
    {
        using var host = Host(scale);
        using var pip = host.CreateGuide(host.Api, "Pip");
        using var mara = host.CreateGuide(host.Api, "Mara");
        pip.SetAnimation("idle", 0.75);
        mara.SetAnimation("idle", 0.75);
        Assert.False(Frame(host, pip, Bounds(host)).SequenceEqual(Frame(host, mara, Bounds(host))));
        Assert.Contains(host.AssetFiles, path => path.EndsWith("setup-guide-mara.json", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void LiveAnimationDragAndMomentumContinueAfterPinnedCapture(double scale)
    {
        using var host = Host(scale);
        using var actor = host.CreateGuide(host.Api, "Pip");
        var bounds = Bounds(host);
        actor.SetAnimation("wave", 0.5);
        var pinned = Frame(host, actor, bounds, 0.2f);
        Assert.Equal(pinned, Frame(host, actor, bounds, 0.2f));
        actor.PlayAnimation("wave");
        Assert.Equal(pinned, Frame(host, actor, bounds));
        var start = Frame(host, actor, bounds, 0.1f);
        Assert.False(start.SequenceEqual(Frame(host, actor, bounds, 0.25f)));
        host.SetClock(100);
        actor.BeginDrag(100);
        host.SetClock(120);
        actor.DragTo(160);
        var dragged = Frame(host, actor, bounds);
        actor.EndDrag();
        Assert.False(dragged.SequenceEqual(Frame(host, actor, bounds, 0.05f)));
        actor.EndDrag(allowMomentum: false);
        var stopped = Frame(host, actor, bounds);
        Assert.Equal(stopped, Frame(host, actor, bounds));
        Assert.Empty(host.RecordedEffects);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void LivePlaybackCanContinuePastTheCaptureTimeLimit(double scale)
    {
        using var host = Host(scale);
        using var actor = host.CreateGuide(host.Api, "Pip");
        actor.SetAnimation("idle", 59.9);
        actor.PlayAnimation("idle");
        Frame(host, actor, Bounds(host), 0.25f);
        var draw = host.Canvas.Commands.Select(command => JsonSerializer.SerializeToElement(command))
            .Last(command => command.GetProperty("op").GetString() == "seraph");
        Assert.True(draw.GetProperty("time").GetDouble() > 60);
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void TransparentFadesAndDisposalDoNotLeakOpaqueRectanglesOrTextures(double scale)
    {
        using var host = Host(scale);
        using var actor = host.CreateGuide(host.Api, "Pip");
        var bounds = Bounds(host);
        actor.SetAnimation("idle", 0.75);
        host.Canvas.Clear();
        actor.Render(0, bounds, 0.5f);
        var faded = host.Canvas.GetRgbaPixels();
        Assert.InRange(faded.Where((_, index) => index % 4 == 3).Max(), 1, 128);
        Assert.True(VisiblePixels(faded) > 500);
        for (int pixel = 0; pixel < faded.Length; pixel += 4)
            for (int channel = 0; channel < 3; channel++) Assert.True(faded[pixel + channel] <= faded[pixel + 3]);
        host.Canvas.Clear();
        actor.Render(0, bounds, 0);
        Assert.Equal(0, VisiblePixels(host.Canvas.GetRgbaPixels()));
        actor.Dispose();
        Assert.False(actor.IsReady);
        actor.Render(0, bounds);
        Assert.Equal(0, VisiblePixels(host.Canvas.GetRgbaPixels()));
        Assert.Contains(host.Canvas.Commands, command => JsonSerializer.SerializeToElement(command).GetProperty("op").GetString() == "delete");
        Assert.Empty(host.UnsupportedCalls);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void SleepingSeraphUsesTheBroadsidePoseAndFillsItsHorizontalFrame(double scale)
    {
        using var host = Host(scale);
        var model = SeraphModel.Load(host.Api, "Pip");
        var pixels = SeraphRasterizer.Render(model, model.Pose("sleep", 2), 108, 35, MathF.PI, fitPosed: true);
        var visible = Enumerable.Range(0, 108 * 35).Where(index => pixels[index * 4 + 3] > 0).ToArray();
        Assert.NotEmpty(visible);
        Assert.True(visible.Max(index => index % 108) - visible.Min(index => index % 108) > 80);
        Assert.InRange(visible.Max(index => index / 108) - visible.Min(index => index / 108), 10, 32);
    }

    [VisualTheory]
    [InlineData(1d)]
    public void InvalidAnimationAndCaptureTimeFailInsteadOfDrawingAFallback(double scale)
    {
        using var host = Host(scale);
        using var actor = host.CreateGuide(host.Api, "Pip");
        Assert.Throws<ArgumentOutOfRangeException>(() => actor.SetAnimation("idle", double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => actor.SetAnimation("idle", 61));
        Assert.ThrowsAny<ArgumentException>(() => actor.SetAnimation("missing-pose", 0));
        Assert.ThrowsAny<ArgumentException>(() => host.CreateGuide(host.Api, "Unknown"));
    }

    private static PreviewHost Host(double scale) => new(Environment.GetEnvironmentVariable("VINTAGE_STORY")!,
        Environment.GetEnvironmentVariable("THEBASICS_GUI_ASSETS")!, 320, 300, scale);
    private static ElementBounds Bounds(PreviewHost host)
    {
        var bounds = ElementBounds.Fixed(30, 20, 160, 220);
        bounds.ParentBounds = host.Api.Gui.WindowBounds;
        bounds.CalcWorldBounds();
        return bounds;
    }
    private static byte[] Frame(PreviewHost host, thebasics.ModSystems.ChatUiSystem.ISetupGuidePreview actor, ElementBounds bounds, float dt = 0)
    {
        host.Canvas.Clear();
        actor.Render(dt, bounds);
        return host.Canvas.GetRgbaPixels();
    }
    private static int VisiblePixels(byte[] rgba) => rgba.Where((value, index) => index % 4 == 3 && value > 0).Count();
}
