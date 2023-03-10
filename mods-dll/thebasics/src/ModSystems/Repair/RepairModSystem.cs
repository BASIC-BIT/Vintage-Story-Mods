using thebasics.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Repair
{
    public class RepairModSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            API.RegisterSingleNumberCommand(
                "setdurability",
                "Sets the durability of the item held in your hand",
                SetDurabilityCommand, 
                "root");
        }

        private void SetDurabilityCommand(IServerPlayer player, int groupId, int durability)
        {
            var item = GetHeldItem(player);

            SetItemDurability(item, durability);
        }

        private Item GetHeldItem(IServerPlayer player)
        {
            var activeSlot = player.InventoryManager.ActiveHotbarSlot;

            if (activeSlot.Empty)
            {
                return null;
            }

            var itemStack = activeSlot.Itemstack;

            if (itemStack.Class == EnumItemClass.Block)
            {
                return null;
            }

            return itemStack.Item;
        }

        private void SetItemDurability(Item item, int durability)
        {
            item.Durability = durability;
        }
    }
}