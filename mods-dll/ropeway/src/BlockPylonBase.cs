using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace Ropeway;

/// <summary>
/// The tower's controller block: the footing you place on the ground, first, before any of the tower
/// exists. Owns the interaction state machine and the break-time refund. Validation is server side; the
/// highlight overlay and the toast are client side, because HighlightIncompleteParts and
/// TriggerIngameError are both client-only.
/// <para>
/// EVERY verb lives here - call, picker, guide, hang the cabin - and none on the sheave four blocks up.
/// The footing is at the player's feet, it is the block the ghost overlay radiates from, and it is the
/// only one of the tower's cells that is a single well-known block rather than "whatever log you used".
/// </para>
/// </summary>
public class BlockPylonBase : Block
{
    // Reserved highlight slots. Vanilla's multiblock overlay owns slot 23, so these stay clear of it.
    // Unused in v0.1 - the pre-placement ghost and the live span preview are not in this lane's scope.
    public const int GhostHighlightSlot = 1200;
    public const int PreviewHighlightSlot = 1201;

    public const string CabinItemCode = "ropeway:cabin";

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        var be = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEPylonBase;
        if (be == null) return base.OnBlockInteractStart(world, byPlayer, blockSel);

        // Sneak + right-click is the guide, on both sides, before anything else can consume the click.
        if (byPlayer.Entity?.Controls?.ShiftKey == true)
        {
            if (world.Side == EnumAppSide.Client)
            {
                world.Api.ModLoader.GetModSystem<RopewayModSystem>()?.GuideDialog?.Show();
            }

            return true;
        }

        if (world.Side == EnumAppSide.Server)
        {
            var player = byPlayer as IServerPlayer;
            if (player == null) return true;

            if (!be.Validate()) return true;

            var service = world.Api.ModLoader.GetModSystem<RopewayModSystem>()?.LinkService;
            if (service == null) return true;

            // Ctrl + right-click is always the picker. A plain right-click on an end station calls the cabin
            // home and stops there, which left naming and unlinking unreachable on exactly the tower a
            // player most wants to name. Ctrl rather than sneak, which already opens the guide.
            if (byPlayer.Entity?.Controls?.CtrlKey == true)
            {
                service.SendCandidates(player, blockSel.Position);
                return true;
            }

            var slot = byPlayer.InventoryManager?.ActiveHotbarSlot;
            if (slot?.Itemstack?.Collectible?.Code?.ToString() == CabinItemCode)
            {
                service.TryPlaceCabin(player, blockSel.Position, slot);
            }
            else
            {
                service.OnTowerInteract(player, blockSel.Position);
            }

            return true;
        }

        // Client: guidance only.
        if (!be.StructureComplete)
        {
            var missing = Math.Max(0, be.IncompleteCount());
            (world.Api as ICoreClientAPI)?.TriggerIngameError(this, "tower-incomplete", Lang.Get("ropeway:tower-incomplete", missing));
            be.ShowIncompleteParts(byPlayer);
        }
        else
        {
            be.ClearHighlights(byPlayer);
        }

        return true;
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        var modSystem = world.Api.ModLoader.GetModSystem<RopewayModSystem>();
        var be = world.BlockAccessor.GetBlockEntity(pos) as BEPylonBase;

        if (world.Side == EnumAppSide.Server && be != null && modSystem?.LinkService != null)
        {
            // Refuse while someone is riding: shrinking the line under a seated player teleports them an
            // arbitrary distance, which is a fall-damage vector and a free long-range teleport.
            if (modSystem.LinkService.IsLineOccupied(pos))
            {
                (byPlayer as IServerPlayer)?.SendIngameError("ropeway-line-in-use", Lang.Get("ropeway:err-line-in-use"));
                world.BlockAccessor.MarkBlockDirty(pos);
                return;
            }

            modSystem.LinkService.UnlinkAll(pos, byPlayer as IServerPlayer);
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        // Empty-hand right-click is the least discoverable verb in Vintage Story, so this ships with the block.
        var be = world.BlockAccessor.GetBlockEntity(selection.Position) as BEPylonBase;

        WorldInteraction[] own;
        if (be == null || !be.StructureComplete)
        {
            own = new[]
            {
                new WorldInteraction { ActionLangCode = "ropeway:blockhelp-missing", MouseButton = EnumMouseButton.Right }
            };
        }
        else if (be.Spans.Count > 0)
        {
            // Any tower on a line is a station: the plain click calls the cabin to it, so the picker is the
            // ctrl one on all of them. Only a tower with no spans yet has nothing to call and keeps the
            // picker on the plain click, which is how a line gets built in the first place.
            own = new[]
            {
                new WorldInteraction { ActionLangCode = "ropeway:blockhelp-call", MouseButton = EnumMouseButton.Right },
                new WorldInteraction { ActionLangCode = "ropeway:blockhelp-pick", MouseButton = EnumMouseButton.Right, HotKeyCode = "ctrl" }
            };

            // The cabin still hangs at an end tower only.
            if (be.IsEndpoint)
            {
                own = own.Append(new WorldInteraction { ActionLangCode = "ropeway:blockhelp-place-cabin", MouseButton = EnumMouseButton.Right });
            }
        }
        else
        {
            own = new[]
            {
                new WorldInteraction { ActionLangCode = "ropeway:blockhelp-pick", MouseButton = EnumMouseButton.Right }
            };
        }

        var guide = new WorldInteraction
        {
            ActionLangCode = "ropeway:blockhelp-guide",
            MouseButton = EnumMouseButton.Right,
            HotKeyCode = "shift"
        };

        return own.Append(guide).Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
    }
}
