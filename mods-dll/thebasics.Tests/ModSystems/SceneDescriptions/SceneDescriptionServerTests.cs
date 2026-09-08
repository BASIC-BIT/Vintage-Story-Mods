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
    public void ModStartup_InstallsAndRemovesPadlockHookAgainstGameAssembly()
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
    public void LockUnlock_ConsumesOnePadlockAndReturnsItOnlyOnce()
    {
        var (marker, player, world) = CreateMarker();
        var padlock = new ItemPadlock { Code = new AssetLocation("game:padlock-copper") };
        var slot = new DummySlot(new ItemStack(padlock, 2));
        player.InventoryManager = Substitute.For<IPlayerInventoryManager>();
        player.InventoryManager.TryGiveItemstack(Arg.Any<ItemStack>()).Returns(true);
        world.GetItem(Arg.Any<AssetLocation>()).Returns(padlock);

        marker.TryLock(player, slot).Should().BeTrue();
        slot.StackSize.Should().Be(1);
        marker.TryLock(player, slot).Should().BeFalse();
        slot.StackSize.Should().Be(1);
        marker.TryUnlock(player).Should().BeTrue();
        marker.TryUnlock(player).Should().BeFalse();
        player.InventoryManager.Received(1).TryGiveItemstack(Arg.Is<ItemStack>(stack => stack.Item == padlock && stack.StackSize == 1));
        marker.Data.IsLocked.Should().BeFalse();
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
        var slot = new DummySlot(new ItemStack(new ItemPadlock { Code = new AssetLocation("game:padlock-copper") }));
        marker.TryLock(player, slot).Should().BeFalse();
        slot.StackSize.Should().Be(1);
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
