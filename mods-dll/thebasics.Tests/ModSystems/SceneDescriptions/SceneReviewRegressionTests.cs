using System.Reflection;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection(AnalyticsServiceTestCollection.Name)]
public class SceneReviewRegressionTests
{
    [Fact]
    public void VisibleIconWorkingSetSurvivesCacheLimitAcrossFrames()
    {
        var api = Substitute.For<ICoreClientAPI>();
        using var renderer = new SceneMarkerIconRenderer(api);
        var styles = Enumerable.Range(0, SceneMarkerIconRenderer.MaxCachedIcons + 1)
            .Select(i => new SceneDescriptionData { SymbolIconName = "style-" + i }).ToArray();
        var uploads = 0;
        LoadedTexture Upload() { uploads++; return new LoadedTexture(api); }
        for (var frame = 0; frame < 4; frame++)
        {
            renderer.BeginIconFrame(styles);
            foreach (var style in styles) renderer.GetIcon(style, Upload);
        }
        uploads.Should().Be(styles.Length);
        renderer.BeginIconFrame([]);
        var field = typeof(SceneMarkerIconRenderer).GetField("_icons", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((System.Collections.IDictionary)field.GetValue(renderer)!).Count.Should().Be(SceneMarkerIconRenderer.MaxCachedIcons);
    }
    [Fact]
    public void CullingDimensionsSurviveTextureEvictionWithoutRemeasuring()
    {
        using var renderer = new SceneMarkerIconRenderer(Substitute.For<ICoreClientAPI>());
        var markers = Enumerable.Range(0, 40).Select(_ => new SceneDescriptionBlockEntity()).ToArray();
        var measures = 0;
        (int, int) Measure() { measures++; return (360, 120); }
        for (var frame = 0; frame < 3; frame++)
            foreach (var marker in markers) renderer.DescriptionSize(marker, Measure).Should().Be((360, 120));
        measures.Should().Be(40);
        markers[0].Data.Title = "Changed";
        renderer.DescriptionSize(markers[0], Measure);
        measures.Should().Be(41);
        renderer.Unregister(markers[0]);
        renderer.DescriptionSize(markers[0], Measure);
        measures.Should().Be(42);
    }

    [Fact]
    public void VisibleDescriptionWorkingSetSurvivesCacheLimit()
    {
        var api = Substitute.For<ICoreClientAPI>();
        using var renderer = new SceneMarkerIconRenderer(api);
        var markers = Enumerable.Range(0, SceneMarkerIconRenderer.MaxCachedDescriptions + 1)
            .Select(_ => new SceneDescriptionBlockEntity()).ToArray();
        var shownField = typeof(SceneMarkerIconRenderer).GetField("_shownDescriptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var shown = (HashSet<SceneDescriptionBlockEntity>)shownField.GetValue(renderer)!;
        shown.UnionWith(markers);
        foreach (var marker in markers) renderer.CacheDescription(marker, new LoadedTexture(api));
        var field = typeof(SceneMarkerIconRenderer).GetField("_descriptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cache = (System.Collections.IDictionary)field.GetValue(renderer)!;
        cache.Count.Should().Be(markers.Length);
        foreach (var marker in markers) cache.Contains(marker).Should().BeTrue();
        foreach (var marker in markers) renderer.Unregister(marker);
        cache.Count.Should().Be(0);
    }
    [Fact]
    public void CustomIconCacheKeyIncludesTheFallbackSymbol()
    {
        var first = new SceneDescriptionData { SymbolIconName = "missing", Symbol = SceneMarkerSymbol.Diamond };
        var second = first.Clone();
        second.Symbol = SceneMarkerSymbol.Information;
        first.SymbolIconKey.Should().NotBe(second.SymbolIconKey);
    }
    [Fact]
    public void DescriptionCacheRemainsBoundedAcrossDistinctMarkers()
    {
        var api = Substitute.For<ICoreClientAPI>();
        using var renderer = new SceneMarkerIconRenderer(api);
        var markers = Enumerable.Range(0, SceneMarkerIconRenderer.MaxCachedDescriptions + 3)
            .Select(_ => new SceneDescriptionBlockEntity()).ToArray();
        foreach (var marker in markers) renderer.CacheDescription(marker, new LoadedTexture(api));
        var field = typeof(SceneMarkerIconRenderer).GetField("_descriptions", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cache = (System.Collections.IDictionary)field.GetValue(renderer)!;
        cache.Count.Should().Be(SceneMarkerIconRenderer.MaxCachedDescriptions);
        cache.Contains(markers[0]).Should().BeFalse();
        cache.Contains(markers[^1]).Should().BeTrue();
    }
    [Theory]
    [InlineData("&lt; &gt; &nbsp; &amp;")]
    [InlineData("<strong>literal</strong> && &lt;icon&gt;")]
    public void LiteralEntitiesSurviveTheGameParser(string authored)
    {
        var escaped = SceneDescriptionFormatter.EscapeLiteral(authored);
        thebasics.Utilities.VtmlUtils.StripVtmlTags(escaped, Substitute.For<ILogger>()).Should().Be(authored);
        var rendered = SceneDescriptionFormatter.ToFloatingVtml(new SceneDescriptionData { Title = authored });
        thebasics.Utilities.VtmlUtils.StripVtmlTags(rendered, Substitute.For<ILogger>()).Should().Be(authored);
    }

    [Fact]
    public void RuntimeSettingIgnoresPendingConfigAndRebuildsAlreadyLoadedMeshes()
    {
        var api = Substitute.For<ICoreClientAPI>();
        var system = new SceneDescriptionSystem();
        api.ModLoader.GetModSystem<SceneDescriptionSystem>().Returns(system);
        var marker = new SceneDescriptionBlockEntity { Api = api, Pos = new BlockPos(0, 0, 0, 0) };
        system.Register(marker);
        system.SetRuntimeEnabled(false);
        ((ICoreAPI)api).World.BlockAccessor.Received(1).MarkBlockDirty(marker.Pos, (IPlayer)null!);
        marker.Api = api;
        marker.OnTesselation(null, null).Should().BeFalse();
        var configField = typeof(thebasics.ModSystems.ChatUiSystem.ChatUiSystem)
            .GetField("_config", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = configField.GetValue(null);
        try
        {
            configField.SetValue(null, new ModConfig { EnableSceneMarkers = true });
            SceneDescriptionSystem.SceneMarkersEnabled(api).Should().BeFalse();
            system.SetRuntimeEnabled(false);
            ((ICoreAPI)api).World.BlockAccessor.Received(1).MarkBlockDirty(marker.Pos, (IPlayer)null!);
            system.SetRuntimeEnabled(true);
            ((ICoreAPI)api).World.BlockAccessor.Received(2).MarkBlockDirty(marker.Pos, (IPlayer)null!);
            marker.OnTesselation(null, null).Should().BeTrue();
        }
        finally { configField.SetValue(null, previous); }
    }
    [Fact]
    public void AuthoredMarkupIsEscapedRatherThanDeletedFromItemName()
    {
        var block = new SceneDescriptionBlock();
        var stack = new ItemStack(block);
        stack.Attributes.SetString(SceneDescriptionData.TitleAttribute, "<strong>notice</strong>");
        block.GetHeldItemName(stack).Should().Be("&lt;strong&gt;notice&lt;/strong&gt;");
    }

    [Fact]
    public void BuriedSymbolRetainsPhysicalClientTarget()
    {
        var block = new SceneDescriptionBlock();
        var apiField = typeof(CollectibleObject).GetField("api", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var api = Substitute.For<ICoreClientAPI>();
        api.Side.Returns(EnumAppSide.Client);
        apiField.SetValue(block, api);
        var physical = new Cuboidf(0, 0, 0, 1, 0.1f, 1);
        block.SelectionBoxes = [physical];
        var accessor = Substitute.For<IBlockAccessor>();
        var pos = new BlockPos(0, 0, 0, 0);
        var marker = new SceneDescriptionBlockEntity();
        marker.Data.HeightOffset = -2;

        accessor.GetBlockEntity(pos).Returns(marker);
        block.GetSelectionBoxes(accessor, pos).Should().Contain(physical);
    }

    [Fact]
    public void IconCacheEvictsOldKeysAndRetainsOnlyItsLimit()
    {
        var api = Substitute.For<ICoreClientAPI>();
        using var renderer = new SceneMarkerIconRenderer(api);
        var textures = Enumerable.Range(0, SceneMarkerIconRenderer.MaxCachedIcons + 3)
            .Select(_ => new LoadedTexture(api)).ToArray();
        for (var i = 0; i < textures.Length; i++)
            renderer.CacheIcon(("untrusted-icon-" + i, SceneMarkerColor.Gold), textures[i]);
        var field = typeof(SceneMarkerIconRenderer).GetField("_icons", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var cache = (System.Collections.IDictionary)field.GetValue(renderer)!;
        cache.Count.Should().Be(SceneMarkerIconRenderer.MaxCachedIcons);
        cache.Contains(("untrusted-icon-0", SceneMarkerColor.Gold)).Should().BeFalse();
        cache.Contains(("untrusted-icon-130", SceneMarkerColor.Gold)).Should().BeTrue();
    }
}
