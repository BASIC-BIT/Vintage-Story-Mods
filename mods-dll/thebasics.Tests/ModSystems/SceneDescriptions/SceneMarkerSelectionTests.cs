using FluentAssertions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.MathTools;
using Vintagestory.API.Client;
using NSubstitute;
using Newtonsoft.Json.Linq;
using thebasics.Utilities;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneMarkerSelectionTests
{
    [Fact]
    public void PickingExtentTracksIndicatorSizeWithoutMovingItsCenter()
    {
        var ray = new Ray(new Vec3d(0.9, 3, -3), new Vec3d(0, 0, 8));
        var center = new Vec3d(0, 3, 0);
        var small = new SceneDescriptionData { IndicatorScale = 0.25f };
        var large = new SceneDescriptionData { IndicatorScale = 3 };
        SceneMarkerSelection.IntersectBox(ray, center, 8, small.SelectionHalfExtent).Should().BeNull();
        SceneMarkerSelection.IntersectBox(ray, center, 8, large.SelectionHalfExtent).Should().NotBeNull();
        SceneMarkerSelection.IntersectBox(ray, center, 1, large.SelectionHalfExtent).Should().BeNull();
    }

    [Fact]
    public void ShippedMarkerMetadataPassesBothServerSightFilters()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln"))) directory = directory.Parent;
        directory.Should().NotBeNull();
        var json = JObject.Parse(File.ReadAllText(Path.Combine(directory!.FullName,
            "mods-dll/thebasics/assets/thebasics/blocktypes/scene-marker.json")));
        var marker = new SceneDescriptionBlock
        {
            BlockId = 42,
            Code = new AssetLocation("thebasics:scene-marker-ground"),
            RenderPass = Enum.Parse<EnumChunkRenderPass>(json.Value<string>("renderpass")!, true)
        };
        var policy = SightBlockPolicy.Resolve([marker], [], []);
        policy.GeneralFilter(new BlockPos(0), marker).Should().BeFalse();
        policy.StrictFilter(new BlockPos(0), marker).Should().BeFalse();
    }

    [Fact]
    public void SelectionHooksInstallAndDisposeAgainstTheGameApi()
    {
        using var selection = new SceneMarkerSelection(Substitute.For<ICoreClientAPI>());
    }

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    public void SupplementIsLimitedToPlayerPicking(bool picking, bool collision, bool sameWorld, bool expected)
    {
        SceneMarkerSelection.ShouldSupplement(picking, collision, sameWorld).Should().Be(expected);
    }

    [Fact]
    public void RaisedSymbolCanBeHitWithAnUnnormalizedRay()
    {
        var ray = new Ray(new Vec3d(0.5, 3.65, -3), new Vec3d(0, 0, 8));
        var hit = SceneMarkerSelection.IntersectBox(ray, new Vec3d(0.5, 3.65, 0.5), 8);
        hit.Should().NotBeNull();
        hit.Z.Should().BeApproximately(0.02, 0.00001);
    }

    [Fact]
    public void HitTestRespectsCloserTerrainRangeAndRayDirection()
    {
        var center = new Vec3d(0.5, 3.65, 0.5);
        var origin = new Vec3d(0.5, 3.65, -3);
        SceneMarkerSelection.IntersectBox(new Ray(origin, new Vec3d(0, 0, 8)), center, 2).Should().BeNull();
        SceneMarkerSelection.IntersectBox(new Ray(origin, new Vec3d(0, 0, -8)), center, 8).Should().BeNull();
        SceneMarkerSelection.IntersectBox(new Ray(origin, new Vec3d()), center, 8).Should().BeNull();
        SceneMarkerSelection.IntersectBox(new Ray(new Vec3d(5, 3.65, -3), new Vec3d(0, 0, 8)), center, 8).Should().BeNull();
    }
}
