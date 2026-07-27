using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.RpCharacters;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.RpCharacters;

public class RpCharacterSafetyParticipantTests
{
    [Fact]
    public void HasExternalOpenInventory_AllowsModdedPlayerOwnedInventory()
    {
        var player = CreatePlayer(new TestPlayerInventory("moddedbelt", "player-1"));

        var result = RpCharacterSafetyParticipant.HasExternalOpenInventory(player);

        result.Should().BeFalse();
    }

    [Fact]
    public void HasExternalOpenInventory_RejectsNonPlayerInventory()
    {
        var externalInventory = Substitute.For<IInventory>();
        externalInventory.ClassName.Returns("chest");
        externalInventory.InventoryID.Returns("chest-10/20/30");
        var player = CreatePlayer(externalInventory);

        var result = RpCharacterSafetyParticipant.HasExternalOpenInventory(player);

        result.Should().BeTrue();
    }

    [Fact]
    public void HasExternalOpenInventory_RejectsPlayerInventoryOwnedByAnotherPlayer()
    {
        var player = CreatePlayer(new TestPlayerInventory("moddedbelt", "player-2"));

        var result = RpCharacterSafetyParticipant.HasExternalOpenInventory(player);

        result.Should().BeTrue();
    }

    [Fact]
    public void Validate_RejectsDeadPlayerBeforeInventorySwitching()
    {
        var player = Substitute.For<IServerPlayer>();
        player.Entity.Returns(new EntityPlayer { Alive = false });
        var context = new RpCharacterSwitchContext(
            player,
            new(),
            new(),
            new(),
            new());

        var result = new RpCharacterSafetyParticipant().Validate(context);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Cannot switch RP characters while dead.");
    }

    private static IPlayer CreatePlayer(IInventory openedInventory)
    {
        var manager = Substitute.For<IPlayerInventoryManager>();
        manager.OpenedInventories.Returns([openedInventory]);

        var player = Substitute.For<IPlayer>();
        player.PlayerUID.Returns("player-1");
        player.InventoryManager.Returns(manager);
        return player;
    }

    private sealed class TestPlayerInventory : InventoryBasePlayer
    {
        private readonly ItemSlot[] _slots = [];

        public TestPlayerInventory(string className, string playerUid)
            : base(className, playerUid, null)
        {
        }

        public override int Count => _slots.Length;

        public override ItemSlot this[int slotId]
        {
            get => _slots[slotId];
            set => _slots[slotId] = value;
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
        }
    }
}
