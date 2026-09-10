using FluentAssertions;
using NSubstitute;
using thebasics.Extensions;
using thebasics.ModSystems.SceneDescriptions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
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
        {
            Title = "Old title",
            Body = "Old body",
            Color = (int)SceneMarkerColor.Blue,
            HeightOffset = 3,
            IndicatorScale = 2,
            TextDistance = 5,
            IdleBobbing = false,
            ShowBodyInBubble = true,
            BubbleScale = 1.5f,
            TitleIcon = 6,
            TitleIconName = "wpHome",
            Display = (int)SceneDescriptionDisplay.AlwaysNearby,
            Symbol = (int)SceneMarkerSymbol.Diamond,
            SymbolIconName = "wpStar1",
            Effect = (int)SceneMarkerEffect.Hologram
        }));
        marker.InitializeFromItem(new ItemStack(new SceneDescriptionBlock()), player);
        marker.Data.Color.Should().Be(SceneMarkerColor.Blue);
        marker.Data.HeightOffset.Should().Be(3);
        marker.Data.IndicatorScale.Should().Be(2);
        marker.Data.IdleBobbing.Should().BeFalse();
        marker.Data.ShowBodyInBubble.Should().BeTrue();
        marker.Data.BubbleScale.Should().Be(1.5f);
        marker.Data.TitleIconName.Should().Be("wpHome");
        marker.Data.TitleIcon.Should().Be(0);
        marker.Data.Symbol.Should().Be(SceneMarkerSymbol.Diamond);
        marker.Data.SymbolIconName.Should().Be("wpStar1");
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
            Body = "New appearance",
            Appearance = (int)SceneMarkerAppearance.Hybrid,
            Symbol = (int)SceneMarkerSymbol.Information,
            IconDistance = 80,
            UnlimitedIconDistance = true,
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
    public void HeldItemTooltip_DropsTheVanillaMaterialLine()
    {
        LangTestHelper.EnsureEnglish();
        var block = new SceneDescriptionBlock();
        var material = Lang.Get("Material: ") + Lang.Get("blockmaterial-" + EnumBlockMaterial.Stone);
        block.WithoutMaterialLine("Scene marker\r\n" + material + "\r\nWeight: 1\r\n", null, null)
            .Should().Be("Scene marker\r\nWeight: 1\r\n");
        block.WithoutMaterialLine("Scene marker\r\n", null, null).Should().Be("Scene marker\r\n");
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

    [Fact]
    public void BreakHandler_RefusesTheCreatorAndAdminsWhileLocked()
    {
        var (marker, player, world) = CreateMarker();
        marker.Data.LockItemCode = "game:padlock-copper";
        player.PrivilegeCheck = _ => true;
        marker.Block.OnBlockBroken(world, marker.Pos, player);
        world.BlockAccessor.DidNotReceive().SetBlock(Arg.Any<int>(), Arg.Any<BlockPos>());
        marker.Block.GetDrops(world, marker.Pos, player).Should().BeEmpty();
        marker.Data.IsLocked.Should().BeTrue();
    }

    [Theory]
    [InlineData("creator", false, true, false)]
    [InlineData("other", false, true, false)]
    [InlineData("admin", true, true, false)]
    [InlineData("creator", false, false, true)]
    [InlineData("other", false, false, true)]
    public void PickupPolicy_LocksOutEveryoneAndOtherwiseUsesClaimAccess(string uid, bool admin, bool locked, bool allowed)
    {
        var (marker, player, _) = CreateMarker();
        marker.Data.LockItemCode = locked ? "game:padlock-copper" : string.Empty;
        player.PlayerUID = uid;
        player.PrivilegeCheck = privilege => admin && privilege == Privilege.controlserver;
        SceneDescriptionBlock.BreakRefused(marker, player).Should().Be(!allowed);
    }
    [Fact]
    public void MarkReadPacket_RecordsTheCurrentStampWithoutEditPermission()
    {
        var (marker, player, _) = CreateMarker(claim: false);
        marker.Data.Stamp();
        var stamp = marker.Data.ReadStamp;
        marker.OnReceivedClientPacket(player, 1004, null);
        var key = SceneReadMarks.Key(marker.Pos);
        SceneReadMarks.IsRead(player.GetSceneReadMarks().Marks, key, stamp).Should().BeTrue();

        marker.OnReceivedClientPacket(player, 1005, null);
        SceneReadMarks.IsRead(player.GetSceneReadMarks().Marks, key, stamp).Should().BeFalse();
    }

    [Fact]
    public void SavingOrClearingIssuesAFreshStampThatInvalidatesExistingMarks()
    {
        var (marker, player, _) = CreateMarker();
        marker.Data.Stamp();
        var key = SceneReadMarks.Key(marker.Pos);
        marker.OnReceivedClientPacket(player, 1004, null);
        var marks = player.GetSceneReadMarks().Marks;

        marker.OnReceivedClientPacket(player, 1002, SerializerUtil.Serialize(new SceneDescriptionEditPacket { Body = "Rewritten" }));
        SceneReadMarks.IsRead(marks, key, marker.Data.ReadStamp).Should().BeFalse();

        marker.OnReceivedClientPacket(player, 1004, null);
        marks = player.GetSceneReadMarks().Marks;
        SceneReadMarks.IsRead(marks, key, marker.Data.ReadStamp).Should().BeTrue();
        marker.OnReceivedClientPacket(player, 1006, null);
        SceneReadMarks.IsRead(marks, key, marker.Data.ReadStamp).Should().BeFalse();
    }

    [Fact]
    public void ClearReadPacket_RequiresEditPermission()
    {
        var (marker, player, _) = CreateMarker(claim: false);
        marker.Data.Stamp();
        var stamp = marker.Data.ReadStamp;
        marker.OnReceivedClientPacket(player, 1006, null);
        marker.Data.ReadStamp.Should().Be(stamp);
    }

    [Fact]
    public void PlacingAMarkerStampsItSoPickupAndReplaceClearsReadMarks()
    {
        var (marker, player, _) = CreateMarker();
        var picked = new ItemStack(new SceneDescriptionBlock());
        new SceneDescriptionData { Title = "Existing", ReadStamp = 42 }.WriteTo(picked.Attributes);
        marker.InitializeFromItem(picked, player);
        marker.Data.ReadStamp.Should().NotBe(42).And.NotBe(0);
    }
}
