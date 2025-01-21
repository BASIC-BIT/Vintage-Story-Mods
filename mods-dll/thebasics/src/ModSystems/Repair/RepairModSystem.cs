using thebasics.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Repair
{
    public class RepairModSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            API.ChatCommands.GetOrCreate("setdurability")
                .WithDescription("Sets the durability of the item held in your hand")
                .RequiresPrivilege(Privilege.root)
                .RequiresPlayer()
                .HandleWith(SetDurabilityCommand);
        }

        private TextCommandResult SetDurabilityCommand(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer) args.Caller.Player;
            var durability = (int) args.Parsers[0].GetValue();
            var item = GetHeldItem(player);

            if (item == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot set the durability of your currently held item.",
                };
            }

            SetItemDurability(item, durability);

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Item durability set to {durability}.",
            };
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