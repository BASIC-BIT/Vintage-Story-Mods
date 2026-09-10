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

    // Fixed interaction bounds include the complete idle-bob envelope without making targeting wobble.
    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        // Terrain traversal must not stop early on boxes extending beyond this voxel.
        // Client picking supplies its own nearest-symbol hit after terrain; other sight rays ignore symbols.
        if (api?.Side == EnumAppSide.Client) return [];
        var data = (blockAccessor.GetBlockEntity(pos) as SceneDescriptionBlockEntity)?.Data ?? new SceneDescriptionData();
        var radius = data.SelectionHalfExtent;
        var centerY = 0.65f + data.HeightOffset;
        return [new Cuboidf(0.5f - radius, centerY - radius, 0.5f - radius, 0.5f + radius, centerY + radius, 0.5f + radius)];
    }

    public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos) => [];

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        _interactions =
        [
            new WorldInteraction { ActionLangCode = "thebasics:scene-read", MouseButton = EnumMouseButton.Right },
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
        if (!SceneDescriptionSystem.SceneMarkersEnabled(api))
        {
            // Both sides run this, so the client refuses its own prediction and can say why.
            failureCode = "__ignore__";
            (api as ICoreClientAPI)?.TriggerIngameError(this, "scene-markers-disabled", Lang.Get("thebasics:scene-markers-disabled"));
            return false;
        }

        var placed = base.TryPlaceBlock(world, byPlayer, itemStack, blockSelection, ref failureCode);
        if (placed && world.Side == EnumAppSide.Server && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is SceneDescriptionBlockEntity blockEntity)
        {
            blockEntity.InitializeFromItem(itemStack, byPlayer);
        }

        return placed;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSelection)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is SceneDescriptionBlockEntity blockEntity)
        {
            if (byPlayer?.Entity?.Controls?.ShiftKey == true)
            {
                if (world.Side == EnumAppSide.Server) blockEntity.OpenEditor(byPlayer);
            }
            else if (api is ICoreClientAPI client)
            {
                var stack = CreateStackFromPlacedBlock(world, blockSelection.Position);
                stack.Attributes.SetString("title", blockEntity.Data.Title);
                stack.Attributes.SetString("text", VtmlUtils.EscapeVtml(blockEntity.Data.Body));
                new GuiDialogReadonlyBook(stack, client).TryOpen();
            }

            return true;
        }

        return false;
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
        if (string.IsNullOrWhiteSpace(data.Body))
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-empty-item-help"));
            return;
        }

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
