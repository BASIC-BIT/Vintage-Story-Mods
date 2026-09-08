using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneMarkerAppearanceTests
{
    [Fact]
    public void BillboardIsSelectableWithoutAnInvisiblePhysicalObstacle()
    {
        var accessor = Substitute.For<IBlockAccessor>();
        var pos = new BlockPos(0, 0, 0, 0);
        var marker = new SceneDescriptionBlockEntity();
        marker.Data.Appearance = SceneMarkerAppearance.Billboard;
        accessor.GetBlockEntity(pos).Returns(marker);
        var block = new SceneDescriptionBlock();
        block.GetSelectionBoxes(accessor, pos).Should().ContainSingle();
        block.GetCollisionBoxes(accessor, pos).Should().BeEmpty();
    }

    [Theory]
    [InlineData(SceneMarkerAppearance.Stone)]
    [InlineData(SceneMarkerAppearance.Model)]
    [InlineData(SceneMarkerAppearance.Billboard)]
    [InlineData(SceneMarkerAppearance.Hybrid)]
    public void AppearanceSurvivesStorageAndClone(SceneMarkerAppearance appearance)
    {
        var data = new SceneDescriptionData { Appearance = appearance, Symbol = SceneMarkerSymbol.Question,
            IconDistance = 72, UnlimitedIconDistance = true, AuthorUid = "creator", LockItemCode = "game:padlock-copper" };
        var tree = new TreeAttribute();
        data.Normalize();
        data.WriteTo(tree);
        SceneDescriptionData.ReadFrom(tree).Clone().Should().BeEquivalentTo(data);
    }

    [Fact]
    public void OldMarkersMigrateToBillboardAppearance()
    {
        var tree = new TreeAttribute();
        tree.SetString("sceneBody", "Old description");
        var data = SceneDescriptionData.ReadFrom(tree);
        data.Appearance.Should().Be(SceneMarkerAppearance.Billboard);
        data.IconDistance.Should().Be(24);
        data.UnlimitedIconDistance.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(2048, 1024)]
    [InlineData(float.NaN, 24)]
    [InlineData(float.PositiveInfinity, 24)]
    public void InvalidAppearanceInputIsNormalized(float distance, float expected)
    {
        var data = new SceneDescriptionData { Appearance = (SceneMarkerAppearance)100,
            Symbol = (SceneMarkerSymbol)(-1), IconDistance = distance }.Normalize();
        data.Appearance.Should().Be(SceneMarkerAppearance.Billboard);
        data.Symbol.Should().Be(SceneMarkerSymbol.Exclamation);
        data.IconDistance.Should().Be(expected);
    }

    [Theory]
    [InlineData(25, 0)]
    [InlineData(24, 0)]
    [InlineData(21, 0.5)]
    [InlineData(18, 1)]
    [InlineData(0, 1)]
    public void BillboardFadesAcrossOuterQuarter(double distance, float expected)
    {
        new SceneDescriptionData { Appearance = SceneMarkerAppearance.Billboard }
            .GetIconOpacity(distance).Should().Be(expected);
    }

    [Theory]
    [InlineData(SceneMarkerAppearance.Stone, 1)]
    [InlineData(SceneMarkerAppearance.Model, 1)]
    [InlineData(SceneMarkerAppearance.Billboard, 1)]
    [InlineData(SceneMarkerAppearance.Hybrid, 1)]
    public void RetiredModesAlsoDisplayAsUnlimitedBillboards(SceneMarkerAppearance appearance, float expected)
    {
        new SceneDescriptionData { Appearance = appearance, UnlimitedIconDistance = true }
            .GetIconOpacity(100000).Should().Be(expected);
    }
}
