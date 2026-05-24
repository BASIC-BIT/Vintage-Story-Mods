using System;
using System.Globalization;
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
                .WithArgs(new WordArgParser("durability", true))
                .RequiresPlayer()
                .HandleWith(SetDurabilityCommand);
        }

        private TextCommandResult SetDurabilityCommand(TextCommandCallingArgs args)
        {
            var player = (IServerPlayer)args.Caller.Player;

            var durabilityInput = (string)args[0];
            if (!TryGetRepairableStack(player, out var activeSlot, out var stack, out var maxDurability, out var errorKey))
            {
                return Error(errorKey);
            }

            if (!TryParseDurabilityInput(durabilityInput, maxDurability, out var durability, out var error, out var showPercentHint))
            {
                return Error(error == DurabilityInputError.Negative
                    ? "thebasics:repair-error-negative-durability"
                    : "thebasics:repair-error-invalid-durability");
            }

            stack.Attributes.SetInt("durability", durability);
            activeSlot.MarkDirty();

            var message = Lang.Get("thebasics:repair-success-set", durability, maxDurability);
            if (showPercentHint)
            {
                message += " " + Lang.Get("thebasics:repair-hint-use-full-percent");
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = message,
            };
        }

        private static bool TryGetRepairableStack(
            IServerPlayer player,
            out ItemSlot activeSlot,
            out ItemStack stack,
            out int maxDurability,
            out string errorKey)
        {
            activeSlot = player.InventoryManager?.ActiveHotbarSlot;
            stack = activeSlot?.Itemstack;
            maxDurability = GetMaxDurability(stack);

            if (SlotHasNoItem(activeSlot, stack))
            {
                errorKey = "thebasics:repair-error-nothing-in-hand";
                return false;
            }

            if (stack.Class == EnumItemClass.Block)
            {
                errorKey = "thebasics:repair-error-is-block";
                return false;
            }

            if (maxDurability <= 0)
            {
                errorKey = "thebasics:repair-error-no-durability";
                return false;
            }

            errorKey = null;
            return true;
        }

        private static bool SlotHasNoItem(ItemSlot activeSlot, ItemStack stack)
        {
            return activeSlot == null || activeSlot.Empty || stack == null;
        }

        private static int GetMaxDurability(ItemStack stack)
        {
            return stack?.Collectible?.GetMaxDurability(stack) ?? 0;
        }

        private static TextCommandResult Error(string langKey)
        {
            return new TextCommandResult
            {
                Status = EnumCommandStatus.Error,
                StatusMessage = Lang.Get(langKey),
            };
        }

        public static bool TryParseDurabilityInput(
            string input,
            int maxDurability,
            out int durability,
            out DurabilityInputError error,
            out bool showPercentHint)
        {
            durability = 0;
            error = DurabilityInputError.None;
            showPercentHint = false;

            if (string.IsNullOrWhiteSpace(input) || maxDurability <= 0)
            {
                error = DurabilityInputError.Invalid;
                return false;
            }

            var trimmed = input.Trim();
            if (trimmed.EndsWith("%", StringComparison.Ordinal))
            {
                return TryParsePercentDurability(trimmed, maxDurability, out durability, out error);
            }

            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var absoluteDurability))
            {
                error = DurabilityInputError.Invalid;
                return false;
            }

            if (absoluteDurability < 0)
            {
                error = DurabilityInputError.Negative;
                return false;
            }

            durability = Math.Min(absoluteDurability, maxDurability);
            showPercentHint = absoluteDurability == 100 && maxDurability > 100;
            return true;
        }

        private static bool TryParsePercentDurability(
            string trimmed,
            int maxDurability,
            out int durability,
            out DurabilityInputError error)
        {
            durability = 0;
            error = DurabilityInputError.None;

            var percentText = trimmed[..^1];
            if (!double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) ||
                !double.IsFinite(percent))
            {
                error = DurabilityInputError.Invalid;
                return false;
            }

            if (percent < 0)
            {
                error = DurabilityInputError.Negative;
                return false;
            }

            var requested = (int)Math.Round(maxDurability * percent / 100d, MidpointRounding.AwayFromZero);
            durability = Math.Max(0, Math.Min(requested, maxDurability));
            return true;
        }

        public enum DurabilityInputError
        {
            None,
            Invalid,
            Negative
        }
    }
}
