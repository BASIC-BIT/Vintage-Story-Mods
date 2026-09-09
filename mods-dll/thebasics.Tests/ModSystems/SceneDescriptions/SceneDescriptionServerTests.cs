using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace thebasics.Tests.ModSystems.SceneDescriptions;

public class SceneDescriptionServerTests
{
    [Fact]
    public void AcceptedAppearanceIsRememberedOnlyForFreshMarkers()
    {
        var (marker, player, _) = CreateMarker();
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        { Title = "Old title", Body = "Old body", Color = (int)SceneMarkerColor.Blue, HeightOffset = 3, IndicatorScale = 2, TextDistance = 5, IdleBobbing = false, ShowBodyInBubble = true, BubbleScale = 1.5f, TitleIcon = 6, TitleIconName = "wpHome",
            Display = (int)SceneDescriptionDisplay.AlwaysNearby, Symbol = (int)SceneMarkerSymbol.Diamond, Effect = (int)SceneMarkerEffect.Hologram }));
        marker.InitializeFromItem(new ItemStack(new SceneDescriptionBlock()), player);
        marker.Data.Color.Should().Be(SceneMarkerColor.Blue);
        marker.Data.HeightOffset.Should().Be(3);
        marker.Data.IndicatorScale.Should().Be(2);
        marker.Data.IdleBobbing.Should().BeFalse();
        marker.Data.ShowBodyInBubble.Should().BeTrue();
        marker.Data.BubbleScale.Should().Be(1.5f);
        marker.Data.TitleIconName.Should().Be("wpHome");
        marker.Data.TitleIcon.Should().Be(6);
        marker.Data.Symbol.Should().Be(SceneMarkerSymbol.Diamond);
        marker.Data.Effect.Should().Be(SceneMarkerEffect.Plain);
        marker.Data.TextDistance.Should().Be(5);
        marker.Data.Display.Should().Be(SceneDescriptionDisplay.AlwaysNearby);
        marker.Data.Title.Should().BeEmpty();
        marker.Data.Body.Should().BeEmpty();
        marker.Data.AuthorUid.Should().Be(player.PlayerUID);
        var pickedUp = new ItemStack(new SceneDescriptionBlock());
        new SceneDescriptionData { Title = "Existing", Color = SceneMarkerColor.Green, HeightOffset = 1 }.WriteTo(pickedUp.Attributes);
        marker.InitializeFromItem(pickedUp, player);
        marker.Data.Title.Should().Be("Existing");
        marker.Data.Color.Should().Be(SceneMarkerColor.Green);
        marker.Data.HeightOffset.Should().Be(1);
        marker.InitializeFromItem(new ItemStack(new SceneDescriptionBlock()), new FakeServerPlayer("other"));
        marker.Data.Color.Should().Be(SceneMarkerColor.Gold);
        marker.Data.TextDistance.Should().Be(8);
    }

    [Fact]
    public void DeniedSaveDoesNotChangeRememberedAppearance()
    {
        var (marker, player, _) = CreateMarker(claim: false);
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket { Color = (int)SceneMarkerColor.Blue }));
        marker.InitializeFromItem(new ItemStack(new SceneDescriptionBlock()), player);
        marker.Data.Color.Should().Be(SceneMarkerColor.Gold);
    }

    [Fact]
    public void ModStartup_RegistersSceneMarkersWithoutPadlockHooks()
    {
        var system = new SceneDescriptionSystem();
        try
        {
            system.Start(Substitute.For<ICoreAPI>());
        }
        finally
        {
            system.Dispose();
        }
    }

    private static (SceneDescriptionBlockEntity Marker, FakeServerPlayer Player, IWorldAccessor World) CreateMarker(bool claim = true)
    {
        LangTestHelper.EnsureEnglish();
        var api = Substitute.For<ICoreAPI>();
        var world = Substitute.For<IWorldAccessor>();
        api.Side.Returns(EnumAppSide.Server);
        api.World.Returns(world);
        world.Side.Returns(EnumAppSide.Server);
        var player = new FakeServerPlayer("creator") { Entity = new EntityPlayer() };
        player.Entity.Pos.SetPos(0.5, 0.5, 0.5);
        var marker = new SceneDescriptionBlockEntity { Api = api, Pos = new BlockPos(0, 0, 0, 0), Block = new SceneDescriptionBlock() };
        world.BlockAccessor.GetBlockEntity(marker.Pos).Returns(marker);
        world.Claims.TryAccess(Arg.Any<IPlayer>(), marker.Pos, EnumBlockAccessFlags.BuildOrBreak).Returns(claim);
        marker.Data.AuthorUid = "creator";
        marker.Data.AuthorName = "Original";
        marker.Data.Body = "Original text";
        return (marker, player, world);
    }

    [Fact]
    public void SaveAndLockPacketLocksWithoutAConsumable()
    {
        var (marker, player, world) = CreateMarker();
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        { Body = "Saved and locked", LockAfterSave = true }));
        marker.Data.Body.Should().Be("Saved and locked");
        marker.Data.IsLocked.Should().BeTrue();
        marker.TryUnlock(player).Should().BeTrue();
        marker.TryUnlock(player).Should().BeFalse();
        world.DidNotReceive().SpawnItemEntity(Arg.Any<ItemStack>(), Arg.Any<Vec3d>(), Arg.Any<Vec3d>());
    }

    [Theory]
    [InlineData("other", true, false)]
    [InlineData("creator", false, false)]
    [InlineData("creator", true, true)]
    public void LockHandler_RejectsWrongOwnerDeniedClaimAndOutOfReach(string uid, bool claim, bool farAway)
    {
        var (marker, player, _) = CreateMarker(claim);
        player.PlayerUID = uid;
        if (farAway) player.Entity.Pos.SetPos(50, 50, 50);
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        { Body = "Denied save", LockAfterSave = true }));
        marker.Data.Body.Should().Be("Original text");
        marker.Data.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void UnlockHandler_RejectsOtherPlayerWithoutReturningPadlock()
    {
        var (marker, player, world) = CreateMarker();
        marker.Data.LockItemCode = "game:padlock-copper";
        player.PlayerUID = "other";
        marker.TryUnlock(player).Should().BeFalse();
        world.DidNotReceive().GetItem(Arg.Any<AssetLocation>());
        marker.Data.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void LockedMarker_ExplosionDoesNotDropOrRemoveIt()
    {
        var (marker, _, world) = CreateMarker();
        marker.Data.LockItemCode = "game:padlock-copper";
        marker.Block.OnBlockExploded(world, marker.Pos, marker.Pos, EnumBlastType.RockBlast, "other");
        world.BlockAccessor.DidNotReceive().SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>());
        world.DidNotReceive().SpawnItemEntity(Arg.Any<ItemStack>(), Arg.Any<Vec3d>(), Arg.Any<Vec3d>());
    }

    [Fact]
    public void AuthorizedEditPacket_PreservesCreatorWhenAnotherClaimMemberEdits()
    {
        var (marker, player, _) = CreateMarker();
        player.PlayerUID = "other";
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket { Body = "Edited" }));
        marker.Data.Body.Should().Be("Edited");
        marker.Data.AuthorUid.Should().Be("creator");
        marker.Data.AuthorName.Should().Be("Original");
    }

    [Fact]
    public void EditorOpenedBeforeLock_CannotSaveAfterLock()
    {
        var (marker, player, _) = CreateMarker();
        marker.Data.LockItemCode = "game:padlock-copper";
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket { Body = "Bypass" }));
        marker.Data.Body.Should().Be("Original text");
        marker.Data.IsLocked.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, SceneMarkerSymbol.Information)]
    [InlineData(true, true, SceneMarkerSymbol.Exclamation)]
    [InlineData(false, false, SceneMarkerSymbol.Exclamation)]
    public void AppearancePacketUsesExistingEditPermissions(bool locked, bool claim, SceneMarkerSymbol expected)
    {
        var (marker, player, _) = CreateMarker(claim);
        marker.Data.LockItemCode = locked ? "game:padlock-copper" : "";
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket
        {
            Body = "New appearance", Appearance = (int)SceneMarkerAppearance.Hybrid,
            Symbol = (int)SceneMarkerSymbol.Information, IconDistance = 80, UnlimitedIconDistance = true,
            Display = (int)SceneDescriptionDisplay.OnInteraction,
        }));
        marker.Data.Symbol.Should().Be(expected);
        marker.Data.Appearance.Should().Be(SceneMarkerAppearance.Billboard);
        marker.Data.AuthorUid.Should().Be("creator");
        marker.Data.IsLocked.Should().Be(locked);
        marker.Data.Display.Should().Be(!locked && claim ? SceneDescriptionDisplay.OnInteraction : SceneDescriptionDisplay.WhenTargeted);
        if (!locked && claim)
        {
            marker.Data.Symbol.Should().Be(SceneMarkerSymbol.Information);
            marker.Data.IconDistance.Should().Be(80);
            marker.Data.UnlimitedIconDistance.Should().BeTrue();
        }
    }

    [Fact]
    public void EditPacket_StillRejectsClaimDenial()
    {
        var (marker, player, _) = CreateMarker(claim: false);
        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket { Body = "Bypass" }));
        marker.Data.Body.Should().Be("Original text");
    }

    [Fact]
    public void BreakHandler_RejectsNonCreatorEvenWithClaimAccess()
    {
        var (marker, player, world) = CreateMarker();
        marker.Data.LockItemCode = "game:padlock-copper";
        player.PlayerUID = "other";
        marker.Block.OnBlockBroken(world, marker.Pos, player);
        world.BlockAccessor.DidNotReceive().SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>());
        world.DidNotReceive().SpawnItemEntity(Arg.Any<ItemStack>(), Arg.Any<Vec3d>(), Arg.Any<Vec3d>());
        marker.Data.IsLocked.Should().BeTrue();
    }

    [Theory]
    [InlineData("creator", false, true)]
    [InlineData("other", false, false)]
    [InlineData("admin", true, true)]
    public void PickupPolicy_UsesServerPrivilegeAndImmutableCreator(string uid, bool admin, bool allowed)
    {
        var (marker, player, _) = CreateMarker();
        marker.Data.LockItemCode = "game:padlock-copper";
        player.PlayerUID = uid;
        player.PrivilegeCheck = privilege => admin && privilege == Privilege.controlserver;
        marker.CanBreak(player).Should().Be(allowed);
    }
}
