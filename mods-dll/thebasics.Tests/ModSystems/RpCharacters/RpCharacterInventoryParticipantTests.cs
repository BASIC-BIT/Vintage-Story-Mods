using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.RpCharacters;
using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace thebasics.Tests.ModSystems.RpCharacters;

public class RpCharacterInventoryParticipantTests
{
    [Fact]
    public void CaptureAndRestore_PreservesFullInventoriesAndOpaqueItemMetadata()
    {
        var inventories = new Dictionary<string, TestInventory>
        {
            ["hotbar"] = new TestInventory("hotbar", "player-1", CreateFullInventoryTree("hotbar", 12)),
            ["backpack"] = new TestInventory("backpack", "player-1", CreateFullInventoryTree("backpack", 16)),
            ["character"] = new TestInventory("character", "player-1", CreateFullInventoryTree("character", 8))
        };
        var manager = CreateManager(inventories);
        manager.ActiveHotbarSlotNumber.Returns(7);
        var player = CreatePlayer(manager);
        var participant = new RpCharacterInventoryParticipant();
        var record = new RpCharacterRecord();
        var context = CreateContext(player, record);

        participant.Capture(context, record);
        var serializedRecord = SerializerUtil.Deserialize<RpCharacterRecord>(SerializerUtil.Serialize(record));

        foreach (var inventory in inventories.Values)
        {
            inventory.Payload = CreateFullInventoryTree("overwritten", inventory.Count);
        }
        manager.ActiveHotbarSlotNumber = 1;

        participant.Restore(CreateContext(player, serializedRecord), serializedRecord);

        serializedRecord.Inventory.Inventories.Should().HaveCount(3);
        AssertInventory(inventories["hotbar"], "hotbar", 12);
        AssertInventory(inventories["backpack"], "backpack", 16);
        AssertInventory(inventories["character"], "character", 8);
        manager.ActiveHotbarSlotNumber.Should().Be(7);
        manager.Received(1).BroadcastHotbarSlot();
        player.Received(1).BroadcastPlayerData(true);
        inventories.Values.Should().OnlyContain(inventory =>
            inventory.AfterBlocksLoadedCalls == 1 &&
            ReferenceEquals(inventory.LastWorld, player.Entity.World));
    }

    [Fact]
    public void Restore_NewCharacterSnapshotClearsAllScopedInventorySlots()
    {
        var inventories = new Dictionary<string, TestInventory>
        {
            ["hotbar"] = new TestInventory("hotbar", "player-1", CreateFullInventoryTree("hotbar", 12)),
            ["backpack"] = new TestInventory("backpack", "player-1", CreateFullInventoryTree("backpack", 16)),
            ["character"] = new TestInventory("character", "player-1", CreateFullInventoryTree("character", 8))
        };
        var manager = CreateManager(inventories);
        var player = CreatePlayer(manager);
        var record = new RpCharacterRecord
        {
            SnapshotVersion = 2,
            Inventory = new RpCharacterInventorySnapshot { Available = true }
        };

        new RpCharacterInventoryParticipant().Restore(CreateContext(player, record), record);

        inventories.Values.Should().OnlyContain(inventory =>
            inventory.Payload.GetTreeAttribute("slots").Count == 0);
    }

    private static IPlayerInventoryManager CreateManager(IReadOnlyDictionary<string, TestInventory> inventories)
    {
        var manager = Substitute.For<IPlayerInventoryManager>();
        manager.GetOwnInventory(Arg.Any<string>()).Returns(call =>
            inventories.TryGetValue(call.Arg<string>(), out var inventory) ? inventory : null);
        return manager;
    }

    private static IServerPlayer CreatePlayer(IPlayerInventoryManager manager)
    {
        var entity = new EntityPlayer
        {
            World = Substitute.For<IWorldAccessor>()
        };
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns("player-1");
        player.InventoryManager.Returns(manager);
        player.Entity.Returns(entity);
        return player;
    }

    private static RpCharacterSwitchContext CreateContext(IServerPlayer player, RpCharacterRecord record)
    {
        return new RpCharacterSwitchContext(
            player,
            new ModConfig(),
            new RpCharacterRegistry(),
            record,
            record);
    }

    private static TreeAttribute CreateFullInventoryTree(string marker, int slotCount)
    {
        var tree = new TreeAttribute();
        tree.SetString("marker", marker);
        tree.SetBytes("opaque", [0, 1, 127, 255]);

        var slots = new TreeAttribute();
        for (var slotId = 0; slotId < slotCount; slotId++)
        {
            var stack = new TreeAttribute();
            stack.SetString("code", $"examplemod:item-{slotId}");
            stack.SetInt("stacksize", 64);
            var attributes = new TreeAttribute();
            attributes.SetString("owner", $"character-{marker}");
            attributes.SetBytes("customPayload", [(byte)slotId, 42, 99]);
            stack["attributes"] = attributes;
            slots[slotId.ToString()] = stack;
        }

        tree["slots"] = slots;
        return tree;
    }

    private static void AssertInventory(TestInventory inventory, string marker, int slotCount)
    {
        inventory.Payload.GetString("marker").Should().Be(marker);
        inventory.Payload.GetBytes("opaque").Should().Equal(0, 1, 127, 255);
        var slots = inventory.Payload.GetTreeAttribute("slots");
        slots.Count.Should().Be(slotCount);
        for (var slotId = 0; slotId < slotCount; slotId++)
        {
            var stack = slots.GetTreeAttribute(slotId.ToString());
            stack.GetString("code").Should().Be($"examplemod:item-{slotId}");
            stack.GetInt("stacksize").Should().Be(64);
            var attributes = stack.GetTreeAttribute("attributes");
            attributes.GetString("owner").Should().Be($"character-{marker}");
            attributes.GetBytes("customPayload").Should().Equal((byte)slotId, 42, 99);
        }
    }

    private sealed class TestInventory : InventoryBase
    {
        private readonly ItemSlot[] _slots;

        public TestInventory(string className, string playerUid, TreeAttribute payload)
            : base(className, playerUid, null)
        {
            _slots = new ItemSlot[payload.GetTreeAttribute("slots").Count];
            Payload = payload;
        }

        public TreeAttribute Payload { get; set; }

        public int AfterBlocksLoadedCalls { get; private set; }

        public IWorldAccessor? LastWorld { get; private set; }

        public override int Count => _slots.Length;

        public override ItemSlot this[int slotId]
        {
            get => _slots[slotId];
            set => _slots[slotId] = value;
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            Payload = (TreeAttribute)tree.Clone();
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            foreach (var entry in Payload)
            {
                tree[entry.Key] = entry.Value.Clone();
            }
        }

        public override void AfterBlocksLoaded(IWorldAccessor world)
        {
            AfterBlocksLoadedCalls++;
            LastWorld = world;
        }
    }
}
