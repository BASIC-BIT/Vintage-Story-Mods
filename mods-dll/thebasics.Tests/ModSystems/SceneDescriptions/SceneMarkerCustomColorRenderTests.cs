using Cairo;
using NSubstitute;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneMarkerCustomColorRenderTests
{
    [Theory]
    [InlineData("")]
    [InlineData("#123")]
    [InlineData("#12345678")]
    [InlineData("blue")]
    public void MissingOrInvalidCustomColorKeepsTheExactPresetPalette(string custom)
    {
        var data = new SceneDescriptionData { Color = SceneMarkerColor.Blue, CustomColorHex = custom };
        Assert.Equal(SceneMarkerVisuals.Palette(SceneMarkerColor.Blue), SceneMarkerVisuals.ResolveColor(data));
        Assert.Equal("#8CBAE8", SceneMarkerVisuals.ColorHex(data));
    }

    [Fact]
    public void DifferentCustomColorsHaveDifferentTexturesAndHexCaseReusesThem()
    {
        var api = Substitute.For<ICoreClientAPI>();
        using var renderer = new SceneMarkerIconRenderer(api);
        var purple = new SceneDescriptionData { SymbolIconName = "wpHome", Color = SceneMarkerColor.Blue, CustomColorHex = "#A153CF" };
        var green = new SceneDescriptionData { SymbolIconName = "wpHome", Color = SceneMarkerColor.Blue, CustomColorHex = "#2E9E61" };
        var uploads = 0;
        LoadedTexture Upload() { uploads++; return new LoadedTexture(api); }
        renderer.BeginIconFrame([purple, green]);
        var first = renderer.GetIcon(purple, Upload);
        Assert.NotSame(first, renderer.GetIcon(green, Upload));
        purple.CustomColorHex = "#a153cf";
        purple.Color = SceneMarkerColor.Red;
        Assert.Same(first, renderer.GetIcon(purple, Upload));
        Assert.Equal(2, uploads);
    }

    [Fact]
    public void CustomColorWorkingSetIsRetainedThenUnusedTexturesAreDisposed()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var renderer = new SceneMarkerIconRenderer(api);
        var styles = Enumerable.Range(0, SceneMarkerIconRenderer.MaxCachedIcons + 1)
            .Select(i => new SceneDescriptionData { SymbolIconName = "wpHome", CustomColorHex = $"#{i:X6}" }).ToArray();
        var textures = new List<LoadedTexture>();
        LoadedTexture Upload() { var value = new LoadedTexture(api); textures.Add(value); return value; }
        for (var frame = 0; frame < 3; frame++)
        {
            renderer.BeginIconFrame(styles);
            foreach (var style in styles) renderer.GetIcon(style, Upload);
        }
        Assert.Equal(styles.Length, textures.Count);
        Assert.DoesNotContain(textures, texture => texture.Disposed);
        renderer.BeginIconFrame([]);
        Assert.Single(textures.Where(texture => texture.Disposed));
        renderer.Dispose();
        Assert.All(textures, texture => Assert.True(texture.Disposed));
    }

    [NativeCairoFact]
    public void CustomShapeAndCatalogIconUseTheSameExactRgb()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var icons = new IconUtil(api);
        api.Gui.Icons.Returns(icons);
        icons.CustomIcons["solid"] = (ctx, x, y, width, height, rgba) =>
        {
            ctx.SetSourceRGBA(rgba);
            ctx.Rectangle(x, y, width, height);
            ctx.Fill();
        };
        var data = new SceneDescriptionData { Color = SceneMarkerColor.Red, CustomColorHex = "#2673C9" };
        var rgb = SceneMarkerVisuals.ResolveColor(data);
        using var shape = new ImageSurface(Format.Argb32, 32, 32);
        using var shapeContext = new Context(shape);
        SceneMarkerVisuals.Draw(shapeContext, null!, 0, 0, 32, rgb, SceneMarkerSymbol.Dot);
        using var catalog = new ImageSurface(Format.Argb32, 32, 32);
        using var catalogContext = new Context(catalog);
        Assert.True(SceneTitleIcons.Draw(api, catalogContext, catalog, "solid", rgb));
        foreach (var surface in new[] { shape, catalog })
        {
            surface.Flush();
            var offset = 16 * surface.Stride + 16 * 4;
            Assert.Equal(new byte[] { 0xC9, 0x73, 0x26, 255 }, surface.Data.Skip(offset).Take(4).ToArray());
        }
        Assert.Equal("#2673C9", SceneMarkerVisuals.ColorHex(data));
    }

    [NativeCairoFact]
    public void SvgReceivesTheSameCustomTintAsCatalogIcons()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var asset = Substitute.For<IAsset>();
        api.Assets.TryGet(Arg.Is<AssetLocation>(location => location.ToString() == "game:textures/custom.svg")).Returns(asset);
        using var surface = new ImageSurface(Format.Argb32, 16, 16);
        using var context = new Context(surface);
        var rgb = SceneMarkerVisuals.ResolveColor(new SceneDescriptionData { CustomColorHex = "#2673C9" });
        Assert.True(SceneTitleIcons.Draw(api, context, surface, "svg:game:textures/custom.svg", rgb));
        api.Gui.Received(1).DrawSvg(asset, surface, 0, 0, 16, 16, ColorUtil.ToRgba(255, 0x26, 0x73, 0xC9));
    }
}
