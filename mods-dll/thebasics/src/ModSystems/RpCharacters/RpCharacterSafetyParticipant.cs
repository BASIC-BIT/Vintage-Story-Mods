using System;
using System.Collections.Generic;
using System.Globalization;
using thebasics.ModSystems.RpCharacters.Models;
using Vintagestory.API.Common;

namespace thebasics.ModSystems.RpCharacters;

public class RpCharacterSafetyParticipant : IRpCharacterSwitchParticipant
{
    private static readonly HashSet<string> AllowedOpenInventoryClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hotbar",
        "backpack",
        "character",
        "craftinggrid",
        "mouse",
        "ground",
        "creative"
    };

    private readonly System.Func<string, object[], string> _localize;

    public RpCharacterSafetyParticipant(System.Func<string, object[], string> localize = null)
    {
        _localize = localize ?? EnglishText;
    }

    public string Code => "thebasics:safety";

    public int Order => -1000;

    public RpCharacterOperationResult Validate(RpCharacterSwitchContext context)
    {
        var player = context.Player;
        var entity = player?.Entity;
        if (player == null || entity == null)
        {
            return Error("rpchar-error-valid-online-player");
        }

        if (!entity.Alive)
        {
            return Error("rpchar-error-switch-dead");
        }

        if (!entity.TryStopHandAction(forceStop: true, EnumItemUseCancelReason.ReleasedMouse))
        {
            return Error("rpchar-error-switch-hand-use");
        }

        if (entity.MountedOn != null && !entity.TryUnmount())
        {
            return Error("rpchar-error-switch-mounted");
        }

        if (player.InventoryManager?.MouseItemSlot?.Empty == false)
        {
            return Error("rpchar-error-switch-cursor");
        }

        if (HasExternalOpenInventory(player))
        {
            return Error("rpchar-error-switch-open-container");
        }

        if (CraftingGridHasInput(player))
        {
            return Error("rpchar-error-switch-crafting-grid");
        }

        return RpCharacterOperationResult.Ok(string.Empty);
    }

    public void Capture(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
    }

    public void Restore(RpCharacterSwitchContext context, RpCharacterRecord record)
    {
    }

    private static bool HasExternalOpenInventory(IPlayer player)
    {
        if (player.InventoryManager?.OpenedInventories == null)
        {
            return false;
        }

        foreach (var inventory in player.InventoryManager.OpenedInventories)
        {
            if (inventory == null)
            {
                continue;
            }

            var className = inventory.ClassName ?? string.Empty;
            if (!AllowedOpenInventoryClasses.Contains(className))
            {
                return true;
            }

            if (!string.Equals(inventory.InventoryID, className + "-" + player.PlayerUID, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CraftingGridHasInput(IPlayer player)
    {
        var craftingGrid = player.InventoryManager?.GetOwnInventory("craftinggrid");
        if (craftingGrid == null)
        {
            return false;
        }

        var inputSlotCount = Math.Max(0, craftingGrid.Count - 1);
        for (var slotId = 0; slotId < inputSlotCount; slotId++)
        {
            if (craftingGrid[slotId]?.Empty == false)
            {
                return true;
            }
        }

        return false;
    }

    private RpCharacterOperationResult Error(string key, params object[] args)
    {
        return RpCharacterOperationResult.Error(_localize(key, args ?? Array.Empty<object>()));
    }

    private static string EnglishText(string key, object[] args)
    {
        var template = key switch
        {
            "rpchar-error-valid-online-player" => "A valid online player is required.",
            "rpchar-error-switch-dead" => "Cannot switch RP characters while dead.",
            "rpchar-error-switch-hand-use" => "Cannot switch RP characters while the active hand action is still running.",
            "rpchar-error-switch-mounted" => "Cannot switch RP characters while mounted because unmounting failed.",
            "rpchar-error-switch-cursor" => "Cannot switch RP characters while carrying an item on the cursor.",
            "rpchar-error-switch-open-container" => "Cannot switch RP characters while an external container is open.",
            "rpchar-error-switch-crafting-grid" => "Cannot switch RP characters while the crafting grid contains input items.",
            _ => key
        };

        return args.Length == 0 ? template : string.Format(CultureInfo.InvariantCulture, template, args);
    }
}
