using System;
using thebasics.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.Repair
{
    public class RepairModSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            API.ChatCommands.GetOrCreate("setdurability")
                .WithDescription(Lang.Get("thebasics:repair-cmd-setdurability-desc"))
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
                    StatusMessage = Lang.Get("thebasics:repair-error-nothing-in-hand"),
                };
            }

            var stack = activeSlot.Itemstack;
            if (stack.Class == EnumItemClass.Block)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:repair-error-is-block"),
                };
            }

            var maxDurability = stack.Collectible?.GetMaxDurability(stack) ?? 0;
            if (maxDurability <= 0)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:repair-error-no-durability"),
                };
            }

            // Clamp to a sensible range.
            durability = Math.Max(0, Math.Min(durability, maxDurability));

            stack.Attributes.SetInt("durability", durability);
            activeSlot.MarkDirty();

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:repair-success-set", durability),
            };
        }
    }
}
