using System;
using System.Text;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SceneDescriptions;

public sealed class SceneDescriptionBlock : BlockSign
{
    private WorldInteraction[] _interactions;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        _interactions =
        [
            new WorldInteraction
            {
                ActionLangCode = "thebasics:scene-description-edit-help",
                HotKeyCode = "shift",
                MouseButton = EnumMouseButton.Right,
            },
        ];
    }

    public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemStack, BlockSelection blockSelection, ref string failureCode)
    {
        var placed = base.TryPlaceBlock(world, byPlayer, itemStack, blockSelection, ref failureCode);
        if (placed && world.Side == EnumAppSide.Server && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is SceneDescriptionBlockEntity blockEntity)
        {
            blockEntity.InitializeFromItem(itemStack, byPlayer);
        }

        return placed;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSelection)
    {
        if (byPlayer?.Entity?.Controls?.ShiftKey == true && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is SceneDescriptionBlockEntity blockEntity)
        {
            // The held-item handler consumes padlocks. Never open/unlock the marker as a fallback.
            if (byPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Collectible is ItemPadlock)
            {
                return false;
            }

            if (world.Side == EnumAppSide.Server)
            {
                InteractWithMarker(byPlayer, blockEntity);
            }

            return true;
        }

        return false;
    }

    private static void InteractWithMarker(IPlayer player, SceneDescriptionBlockEntity marker)
    {
        if (!marker.Data.IsLocked)
        {
            marker.OpenEditor(player);
        }
        else if (player.InventoryManager.ActiveHotbarSlot?.Empty != true || !marker.TryUnlock(player))
        {
            DenyLocked(player);
        }
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySelection, bool firstEvent, ref EnumHandHandling handling)
    {
        if (byEntity?.Controls?.ShiftKey != true || string.IsNullOrWhiteSpace(slot?.Itemstack?.Attributes?.GetString(SceneDescriptionData.BodyAttribute)))
        {
            base.OnHeldInteractStart(slot, byEntity, blockSelection, entitySelection, firstEvent, ref handling);
            return;
        }

        handling = EnumHandHandling.PreventDefault;
        if (api is ICoreClientAPI capi)
        {
            var readableStack = slot.Itemstack.Clone();
            readableStack.Attributes.SetString("title", GetHeldItemName(readableStack));
            readableStack.Attributes.SetString("text", VtmlUtils.EscapeVtml(readableStack.Attributes.GetString(SceneDescriptionData.BodyAttribute, string.Empty)));
            new GuiDialogReadonlyBook(readableStack, capi).TryOpen();
        }
    }

    public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is SceneDescriptionBlockEntity marker &&
            marker.Data.IsLocked && !marker.CanBreak(byPlayer))
        {
            return [];
        }

        return [CreateStackFromPlacedBlock(world, pos)];
    }

    public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is SceneDescriptionBlockEntity marker && !marker.CanBreak(player))
        {
            return Math.Max(remainingResistance, 1);
        }

        return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is SceneDescriptionBlockEntity marker &&
            (marker.Data.IsLocked || byPlayer != null) && !marker.CanBreak(byPlayer))
        {
            DenyLocked(byPlayer);
            marker.MarkDirty(redrawOnClient: true);
            return;
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    public override void OnBlockExploded(IWorldAccessor world, BlockPos pos, BlockPos explosionCenter, EnumBlastType blastType, string ignitedByPlayerUid)
    {
        // Explosions must not become an indirect way to steal/destroy a locked marker.
        if (world.BlockAccessor.GetBlockEntity(pos) is SceneDescriptionBlockEntity { Data.IsLocked: true })
        {
            return;
        }

        base.OnBlockExploded(world, pos, explosionCenter, blastType, ignitedByPlayerUid);
    }

    private static void DenyLocked(IPlayer player) =>
        (player as IServerPlayer)?.SendIngameError("scene-description-locked", Lang.Get("thebasics:scene-description-locked-help"));

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        return CreateStackFromPlacedBlock(world, pos);
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        var title = itemStack?.Attributes?.GetString(SceneDescriptionData.TitleAttribute, string.Empty)?.Trim();
        return string.IsNullOrWhiteSpace(title) ? base.GetHeldItemName(itemStack) : VtmlUtils.StripVtmlTags(title, api?.Logger);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder description, IWorldAccessor world, bool withDebugInfo)
    {
        base.GetHeldItemInfo(inSlot, description, world, withDebugInfo);
        var data = SceneDescriptionData.ReadFrom(inSlot?.Itemstack?.Attributes);
        if (data.IsLocked)
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-locked-item-help"));
        }
        if (string.IsNullOrWhiteSpace(data.Body))
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-empty-item-help"));
            return;
        }

        description.AppendLine(data.Kind == SceneDescriptionKind.OocNotice
            ? Lang.Get("thebasics:scene-description-kind-ooc")
            : Lang.Get("thebasics:scene-description-kind-environmental"));
        if (!string.IsNullOrWhiteSpace(data.AuthorName))
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-authored-by", data.AuthorName));
        }

        description.AppendLine(VtmlUtils.EscapeVtml(Preview(data.Body)));
        description.AppendLine(Lang.Get("thebasics:scene-description-read-item-help"));
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return _interactions;
    }

    private ItemStack CreateStackFromPlacedBlock(IWorldAccessor world, BlockPos pos)
    {
        var canonicalBlock = world.GetBlock(CodeWithParts("ground", "north")) ?? this;
        var stack = new ItemStack(canonicalBlock);
        if (world.BlockAccessor.GetBlockEntity(pos) is SceneDescriptionBlockEntity blockEntity)
        {
            blockEntity.Data.WriteTo(stack.Attributes);
        }

        return stack;
    }

    private static string Preview(string body)
    {
        body = body.Replace('\n', ' ').Trim();
        return body.Length <= 180 ? body : body[..177] + "...";
    }
}
