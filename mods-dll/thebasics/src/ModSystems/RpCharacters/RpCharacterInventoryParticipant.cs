using System;
using System.Linq;
using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.Server;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterInventoryParticipant : IRpCharacterSwitchParticipant
{
    public const string ParticipantCode = "thebasics:inventory";

    private static readonly string[] ScopedInventoryClasses =
    {
        "hotbar",
        "backpack",
        "character"
    };

    public string Code => ParticipantCode;

    public int Order => 200;

    public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
    {
        return RpCharacterOperationResult.Ok(string.Empty);
    }

    public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        var player = context.Player;
        var manager = player.InventoryManager;
        var snapshot = new RpCharacterInventorySnapshot
        {
            Available = true,
            ActiveHotbarSlotNumber = manager?.ActiveHotbarSlotNumber ?? 0
        };

        if (manager != null)
        {
            foreach (var className in ScopedInventoryClasses)
            {
                if (manager.GetOwnInventory(className) is not InventoryBase inventory)
                {
                    continue;
                }

                var tree = new TreeAttribute();
                inventory.ToTreeAttributes(tree);
                snapshot.Inventories.Add(new RpCharacterInventoryData
                {
                    ClassName = className,
                    TreeData = tree.ToBytes()
                });
            }
        }

        record.Inventory = snapshot;
    }

    public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
        if (!HasRestorableSnapshot(record))
        {
            return;
        }

        var player = context.Player;
        var manager = player.InventoryManager;
        if (manager == null)
        {
            return;
        }

        var snapshot = record.Inventory ?? new RpCharacterInventorySnapshot();
        snapshot.Inventories ??= new System.Collections.Generic.List<RpCharacterInventoryData>();

        foreach (var className in ScopedInventoryClasses)
        {
            if (manager.GetOwnInventory(className) is not InventoryBase inventory)
            {
                continue;
            }

            var data = snapshot.Inventories.FirstOrDefault(item => item.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
            var tree = RpCharacterSnapshotUtilities.FromBytes(data?.TreeData) ?? CreateEmptyInventoryTree(inventory);
            inventory.FromTreeAttributes(tree);
            inventory.AfterBlocksLoaded(player.Entity.World);
            MarkInventoryDirty(inventory);
        }

        if (snapshot.ActiveHotbarSlotNumber >= 0)
        {
            manager.ActiveHotbarSlotNumber = snapshot.ActiveHotbarSlotNumber;
            if (player.WorldData is ServerWorldPlayerData worldData)
            {
                worldData.SelectedHotbarSlot = snapshot.ActiveHotbarSlotNumber;
            }
        }

        player.BroadcastPlayerData(sendInventory: true);
        manager.BroadcastHotbarSlot();
    }

    internal static bool HasRestorableSnapshot(RpCharacterRecord record)
    {
        return record != null &&
               record.Inventory != null &&
               (record.Inventory.Available || record.Inventory.Inventories?.Count > 0);
    }

    private static TreeAttribute CreateEmptyInventoryTree(InventoryBase inventory)
    {
        var tree = new TreeAttribute();
        inventory.ToTreeAttributes(tree);
        tree["slots"] = new TreeAttribute();
        return tree;
    }

    private static void MarkInventoryDirty(InventoryBase inventory)
    {
        for (var slotId = 0; slotId < inventory.Count; slotId++)
        {
            var slot = inventory[slotId];
            if (slot == null)
            {
                continue;
            }

            inventory.MarkSlotDirty(slotId);
            inventory.OnItemSlotModified(slot);
        }
    }
}
