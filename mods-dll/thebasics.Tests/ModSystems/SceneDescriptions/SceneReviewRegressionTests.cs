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
