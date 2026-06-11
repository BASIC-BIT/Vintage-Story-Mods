using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.Utilities;

public static class TemporalGearUtil
{
    public static bool IsPlayerHoldingTemporalGear(IServerPlayer player)
    {
        return player?.Entity != null &&
               (ItemSlotContainsTemporalGear(player.Entity.LeftHandItemSlot) ||
                ItemSlotContainsTemporalGear(player.Entity.RightHandItemSlot));
    }

    public static bool TryConsumeTemporalGear(IServerPlayer player)
    {
        if (player?.Entity == null)
        {
            return false;
        }

        var leftHand = ItemSlotContainsTemporalGear(player.Entity.LeftHandItemSlot);
        var rightHand = ItemSlotContainsTemporalGear(player.Entity.RightHandItemSlot);

        if (!leftHand && !rightHand)
        {
            return false;
        }

        var itemSlot = leftHand ? player.Entity.LeftHandItemSlot : player.Entity.RightHandItemSlot;

        itemSlot.TakeOut(1);
        itemSlot.MarkDirty();
        player.Entity.MarkShapeModified();
        player.BroadcastPlayerData(true);

        return true;
    }

    public static bool TryReturnTemporalGear(ICoreServerAPI api, IServerPlayer player)
    {
        if (api == null || player?.Entity == null)
        {
            return false;
        }

        var temporalGearItem = api.World.GetItem(new AssetLocation("game:gear-temporal"));
        if (temporalGearItem == null)
        {
            api.Logger.Error("Could not find temporal gear item to return to player - this is probably a mod bug!");
            return false;
        }

        var temporalGearStack = new ItemStack(temporalGearItem, 1);

        if (player.InventoryManager.TryGiveItemstack(temporalGearStack, slotNotifyEffect: true))
        {
            return true;
        }

        if (TryPutInHandSlot(player.Entity.LeftHandItemSlot, temporalGearStack, player) ||
            TryPutInHandSlot(player.Entity.RightHandItemSlot, temporalGearStack, player))
        {
            return true;
        }

        api.World.SpawnItemEntity(temporalGearStack, player.Entity.Pos.XYZ);
        player.SendMessage(GlobalConstants.CurrentChatGroup,
            Lang.Get("thebasics:tpa-notify-gear-dropped"),
            EnumChatType.Notification);

        return true;
    }

    private static bool ItemSlotContainsTemporalGear(ItemSlot itemSlot)
    {
        return itemSlot?.Itemstack?.Item is ItemTemporalGear;
    }

    private static bool TryPutInHandSlot(ItemSlot slot, ItemStack itemStack, IServerPlayer player)
    {
        if (slot == null || !slot.Empty)
        {
            return false;
        }

        slot.Itemstack = itemStack;
        slot.MarkDirty();
        player.SendMessage(GlobalConstants.CurrentChatGroup,
            Lang.Get("thebasics:tpa-notify-gear-returned-hand"),
            EnumChatType.Notification);

        return true;
    }
}
