using System.Reflection;
using Newtonsoft.Json.Linq;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Extensions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

[Collection(AnalyticsServiceTestCollection.Name)]
public class SceneMarkerPlateTests
{
    [Fact]
    public void EnabledMarkerKeepsItsTerrainMesh()
    {
        var (_, marker, _, _) = CreateMarker();
        marker.OnTesselation(null, null).Should().BeFalse("the normal plate mesh must be added to the terrain");
    }

    [Theory]
    [InlineData(0, 0f, 0.0625f)]
    [InlineData(1, 1.748f, 2.552f)]
    public void OutlineFollowsTheSelectedPart(int part, float bottom, float top)
    {
        var (block, marker, _, _) = CreateMarker();
        marker.Data.HeightOffset = 1.5f;
        marker.Data.IconDistance = 1;
        marker.Data.IndicatorScale = 1;
        var outlined = block.SelectedBox(new BlockSelection { Position = marker.Pos, SelectionBoxIndex = part });
        outlined.Should().NotBeNull();
        outlined!.Y1.Should().BeApproximately(bottom, 0.00001f);
        outlined.Y2.Should().BeApproximately(top, 0.00001f);
    }

    [Theory]
    [InlineData(EnumAppSide.Client)]
    [InlineData(EnumAppSide.Server)]
    public void PhysicalTargetDoesNotDependOnIconDistanceOrHeight(EnumAppSide side)
    {
        var (block, marker, api, world) = CreateMarker();
        api.Side.Returns(side);
        marker.Data.IconDistance = 1;
        marker.Data.HeightOffset = 5;
        marker.Data.GetIconOpacity(3).Should().Be(0);
        var boxes = block.GetSelectionBoxes(world.BlockAccessor, marker.Pos);
        boxes.Should().Contain(box => box.Y1 == 0 && box.Y2 == 0.0625f);
        block.GetCollisionBoxes(world.BlockAccessor, marker.Pos).Should().BeEmpty();
    }

    [Fact]
    public void BreakingPlateKeepsTheTerrainDecal()
    {
        var (block, marker, _, world) = CreateMarker();
        ((PlateTestPlayer)world.Player).CurrentBlockSelection = new BlockSelection { Position = marker.Pos, SelectionBoxIndex = 0 };
        var terrain = CubeMeshUtil.GetCube();
        var original = CubeMeshUtil.GetCube();
        var decal = original;
        block.GetDecal(world, marker.Pos, null, ref decal, ref terrain);
        decal.Should().BeSameAs(original);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CeilingPlacementUsesCeilingVariantAndChecksSupport(bool supported)
    {
        var (block, _, _, world) = CreateMarker();
        block.Code = new AssetLocation("thebasics:scene-marker-ground-north");
        var selection = new BlockSelection { Position = new BlockPos(0, 0, 0, 0), Face = BlockFacing.DOWN, HitPosition = new Vec3d(0.5, 1, 0.5) };
        var support = new Block { SideSolid = new SmallBoolArray(supported ? 63 : 0) };
        world.BlockAccessor.GetBlock(selection.Position.UpCopy()).Returns(support);
        world.BlockAccessor.GetBlock(selection.Position).Returns(new Block { Replaceable = 10000 });
        world.BlockAccessor.GetBlock(Arg.Any<AssetLocation>()).Returns(info => new Block
        {
            BlockId = ((AssetLocation)info[0]).Path.Contains("-ceiling-") ? 101 : 100
        });
        var player = new FakeServerPlayer("creator") { Entity = new EntityPlayer() };
        player.Entity.Pos.SetPos(0.5, -1, 2);
        world.Claims.TryAccess(player, selection.Position, EnumBlockAccessFlags.BuildOrBreak).Returns(true);
        var failure = "";
        block.TryPlaceBlock(world, player, new ItemStack(block), selection, ref failure).Should().Be(supported);
        if (supported) world.BlockAccessor.Received(1).SetBlock(101, selection.Position);
        else world.BlockAccessor.DidNotReceive().SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>());
    }

    [Fact]
    public void CeilingAssetTurnsThePlateTowardTheRoom()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln"))) directory = directory.Parent;
        var assets = Path.Combine(directory!.FullName, "mods-dll/thebasics/assets/thebasics");
        var json = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes/scene-marker.json")));
        var shapeRef = json["shapebytype"]!["*-ceiling-*"]!;
        shapeRef.Value<string>("base").Should().Be("thebasics:block/scene-marker-ceiling");
        var shape = JObject.Parse(File.ReadAllText(Path.Combine(assets,
            "shapes/" + shapeRef.Value<string>("base")!.Split(':')[1] + ".json")));
        var plate = shape["elements"]![0]!;
        var inlay = shape["elements"]![1]!;
        var from = plate["from"]!.ToObject<float[]>()!;
        var to = plate["to"]!.ToObject<float[]>()!;
        var inlayFrom = inlay["from"]!.ToObject<float[]>()!;
        var inlayTo = inlay["to"]!.ToObject<float[]>()!;
        (to[0] - from[0]).Should().Be(10);
        (to[2] - from[2]).Should().Be(10);
        (inlayTo[0] - inlayFrom[0]).Should().Be(4);
        (inlayTo[2] - inlayFrom[2]).Should().Be(4);
        var bounds = new Cuboidf(from[0] / 16, from[1] / 16, from[2] / 16, to[0] / 16, to[1] / 16, to[2] / 16)
            .RotatedCopy(shapeRef.Value<float>("rotateX"), 0, 0, new Vec3d(0.5, 0.5, 0.5));
        bounds.Y2.Should().BeApproximately(1, 0.00001f, "the ceiling plate must touch its support above");
        bounds.Y1.Should().BeGreaterThan(0.9f);
        var box = json["selectionboxbytype"]!["*-ceiling-*"]!.ToObject<Cuboidf>()!;
        box.X1.Should().BeApproximately(bounds.X1, 0.00001f);
        box.X2.Should().BeApproximately(bounds.X2, 0.00001f);
        box.Y1.Should().BeApproximately(bounds.Y1, 0.00001f);
        box.Y2.Should().BeApproximately(bounds.Y2, 0.00001f);
        box.Z1.Should().BeApproximately(bounds.Z1, 0.00001f);
        box.Z2.Should().BeApproximately(bounds.Z2, 0.00001f);
    }

    [Fact]
    public void GroundPlateAndInlayAreHalfAsWideAndStayCentered()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln"))) directory = directory.Parent;
        var shape = JObject.Parse(File.ReadAllText(Path.Combine(directory!.FullName,
            "mods-dll/thebasics/assets/thebasics/shapes/block/scene-marker-ground.json")));
        var plate = shape["elements"]![0]!;
        var inlay = shape["elements"]![1]!;
        var plateFrom = plate["from"]!.ToObject<float[]>()!;
        var plateTo = plate["to"]!.ToObject<float[]>()!;
        var inlayFrom = inlay["from"]!.ToObject<float[]>()!;
        var inlayTo = inlay["to"]!.ToObject<float[]>()!;

        (plateTo[0] - plateFrom[0]).Should().Be(2.5f);
        (plateTo[2] - plateFrom[2]).Should().Be(10);
        (plateTo[1] - plateFrom[1]).Should().Be(1);
        (plateTo[0] + plateFrom[0]).Should().Be(16);
        (plateTo[2] + plateFrom[2]).Should().Be(16);
        (inlayTo[0] - inlayFrom[0]).Should().Be(1);
        (inlayTo[2] - inlayFrom[2]).Should().Be(4);
        (inlayTo[1] - inlayFrom[1]).Should().BeApproximately(0.35f, 0.00001f);
        (inlayTo[0] + inlayFrom[0]).Should().Be(16);
        (inlayTo[2] + inlayFrom[2]).Should().Be(16);
    }

    [Theory]
    [InlineData("ground", "down", 1)]
    [InlineData("ceiling", "down", 1)]
    [InlineData("wall", "north", 2)]
    public void InlayOmitsOnlyItsHiddenContactFace(string attachment, string hiddenFace, int contactAxis)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln"))) directory = directory.Parent;
        var shape = JObject.Parse(File.ReadAllText(Path.Combine(directory!.FullName,
            $"mods-dll/thebasics/assets/thebasics/shapes/block/scene-marker-{attachment}.json")));
        var plate = shape["elements"]![0]!;
        var inlay = shape["elements"]![1]!;
        var plateTo = plate["to"]!.ToObject<float[]>()!;
        var inlayFrom = inlay["from"]!.ToObject<float[]>()!;
        plateTo[contactAxis].Should().Be(inlayFrom[contactAxis]);

        var faces = (JObject)inlay["faces"]!;
        faces.Property(hiddenFace).Should().BeNull("the inlay's contact face is coplanar with the plate surface");
        faces.Properties().Should().HaveCount(5, "the other inlay faces remain visible");
        ((JObject)plate["faces"]!).Properties().Should().HaveCount(6);
    }

    [Theory]
    [InlineData("north")]
    [InlineData("east")]
    [InlineData("south")]
    [InlineData("west")]
    public void GroundSelectionFollowsTheRotatedPlate(string side)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Vintage-Story-Mods.sln"))) directory = directory.Parent;
        var assets = Path.Combine(directory!.FullName, "mods-dll/thebasics/assets/thebasics");
        var block = JObject.Parse(File.ReadAllText(Path.Combine(assets, "blocktypes/scene-marker.json")));
        var shape = JObject.Parse(File.ReadAllText(Path.Combine(assets, "shapes/block/scene-marker-ground.json")));
        var plate = shape["elements"]![0]!;
        var from = plate["from"]!.ToObject<float[]>()!;
        var to = plate["to"]!.ToObject<float[]>()!;
        var rotation = block["shapebytype"]![$"*-ground-{side}"]!.Value<float>("rotateY");
        var bounds = new Cuboidf(from[0] / 16, from[1] / 16, from[2] / 16, to[0] / 16, to[1] / 16, to[2] / 16)
            .RotatedCopy(0, rotation, 0, new Vec3d(0.5, 0.5, 0.5));
        var selection = block["selectionboxbytype"]![$"*-ground-{side}"]!.ToObject<Cuboidf>()!;

        selection.X1.Should().BeApproximately(bounds.X1, 0.00001f);
        selection.X2.Should().BeApproximately(bounds.X2, 0.00001f);
        selection.Z1.Should().BeApproximately(bounds.Z1, 0.00001f);
        selection.Z2.Should().BeApproximately(bounds.Z2, 0.00001f);
    }
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void DamageCracksStayOnLastDamagedPartAfterLookingAway(int part)
    {
        var (block, marker, _, world) = CreateMarker();
        var player = (PlateTestPlayer)world.Player;
        player.InventoryManager = Substitute.For<IPlayerInventoryManager>();
        var slot = new DummySlot();
        player.InventoryManager.ActiveHotbarSlot.Returns(slot);
        world.Claims.TryAccess(player, marker.Pos, EnumBlockAccessFlags.BuildOrBreak).Returns(true);
        var selection = new BlockSelection { Position = marker.Pos, SelectionBoxIndex = part };
        player.CurrentBlockSelection = selection;
        block.OnGettingBroken(player, selection, slot, 1, 0.1f, 1);
        player.CurrentBlockSelection = null!;
        var terrain = CubeMeshUtil.GetCube();
        var original = CubeMeshUtil.GetCube();
        var decal = original;
        block.GetDecal(world, marker.Pos, null, ref decal, ref terrain);
        if (part == 0) ReferenceEquals(decal, original).Should().BeTrue();
        else
        {
            ReferenceEquals(decal, original).Should().BeFalse();
            decal.xyz.Where((_, i) => i % 3 == 1).Min().Should().BeApproximately(0.248f, 0.00001f);
        }
    }
    private sealed class PlateTestPlayer : FakeServerPlayer, IClientPlayer
    {
        public float CameraPitch { get; set; }
        public float CameraRoll { get; set; }
        public float CameraYaw { get; set; }
        public EnumCameraMode CameraMode => EnumCameraMode.FirstPerson;
        public void ShowChatNotification(string message) { }
        public void TriggerFpAnimation(EnumHandInteract anim) { }
    }

    private static (SceneDescriptionBlock, SceneDescriptionBlockEntity, ICoreClientAPI, IClientWorldAccessor) CreateMarker()
    {
        var api = Substitute.For<ICoreClientAPI>();
        api.Side.Returns(EnumAppSide.Client);
        api.ObjectCache.Returns(new Dictionary<string, object>());
        var world = Substitute.For<IClientWorldAccessor>();
        api.World.Returns(world);
        ((ICoreAPI)api).World.Returns(world);
        world.ReturnsForAll<IClientPlayer>(new PlateTestPlayer { Entity = new EntityPlayer() });
        var system = new SceneDescriptionSystem();
        api.ModLoader.GetModSystem<SceneDescriptionSystem>().Returns(system);
        system.SetRuntimeEnabled(true);
        var block = new SceneDescriptionBlock { SelectionBoxes = [new Cuboidf(0.1875f, 0, 0.1875f, 0.8125f, 0.0625f, 0.8125f)] };
        typeof(CollectibleObject).GetField("api", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(block, api);
        var marker = new SceneDescriptionBlockEntity { Api = api, Block = block, Pos = new BlockPos(0, 0, 0, 0) };
        world.BlockAccessor.GetBlockEntity(marker.Pos).Returns(marker);
        return (block, marker, api, world);
    }
}
