using System;
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
                .WithArgs(API.ChatCommands.Parsers.Int("durability"))
                .RequiresPlayer()
                .HandleWith(SetDurabilityCommand);
        }

        private TextCommandResult SetDurabilityCommand(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer) args.Caller.Player;

            // Parsed by .WithArgs(Int("durability"))
            var durability = (int)args[0];
            var activeSlot = player.InventoryManager?.ActiveHotbarSlot;

            if (activeSlot == null || activeSlot.Empty || activeSlot.Itemstack == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Nothing in active hands.",
                };
            }

            var stack = activeSlot.Itemstack;
            if (stack.Class == EnumItemClass.Block)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot set durability on blocks.",
                };
            }

            var maxDurability = stack.Collectible?.GetMaxDurability(stack) ?? 0;
            if (maxDurability <= 0)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Held item does not have durability.",
                };
            }

            // Clamp to a sensible range.
            durability = Math.Max(0, Math.Min(durability, maxDurability));

            stack.Attributes.SetInt("durability", durability);
            activeSlot.MarkDirty();

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Item durability set to {durability}.",
            };
        }
    }
}
