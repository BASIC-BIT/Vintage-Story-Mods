using System;
using System.Text;
using thebasics.Utilities;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SceneDescriptions;

public sealed class SceneDescriptionBlock : BlockSign, ICustomSelectionBoxRender
{
    private WorldInteraction[] _interactions;
    private long _lastBreakWarningMs;

    // Fixed interaction bounds include the complete idle-bob envelope without making targeting wobble.
    // The box is what the player aims at, so the outline and the break cracks are drawn on it too,
    // rather than on the ground plate or the wall plaque the block shape actually occupies.
    internal static Cuboidf SymbolBox(SceneDescriptionData data)
    {
        var radius = data.SelectionHalfExtent;
        var centerY = 0.65f + data.HeightOffset;
        return new Cuboidf(0.5f - radius, centerY - radius, 0.5f - radius, 0.5f + radius, centerY + radius, 0.5f + radius);
    }

    private SceneDescriptionData DataAt(IBlockAccessor blockAccessor, BlockPos pos) =>
        (blockAccessor?.GetBlockEntity(pos) as SceneDescriptionBlockEntity)?.Data ?? new SceneDescriptionData();

    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos)
    {
        // Terrain traversal must not stop early on boxes extending beyond this voxel.
        // Client picking supplies its own nearest-symbol hit after terrain; other sight rays ignore symbols.
        if (api?.Side == EnumAppSide.Client) return [];
        return [SymbolBox(DataAt(blockAccessor, pos))];
    }

    // Client outline. Vanilla would ask GetSelectionBoxes, which is empty here to keep the raycaster honest,
    // so draw the symbol box ourselves instead of leaving the targeted marker with no outline at all.
    public void RenderSelectionBoxes(BlockSelection blockSel, RenderBoxDelegate renderBoxHandler)
    {
        var capi = api as ICoreClientAPI;
        if (capi == null || blockSel?.Position == null) return;
        renderBoxHandler(SymbolBox(DataAt(capi.World.BlockAccessor, blockSel.Position)),
            1.6f * ClientSettings.Wireframethickness, GetSelectionColor(capi, blockSel.Position));
    }

    // Break cracks default to the tesselated block shape, i.e. the plate or the wall plaque. Redirect them
    // onto the same centred cube the player is aiming at. blockModelData is left alone: the decal shader
    // reads its UVs per vertex for the opaque-texel test, and it always has more vertices than this cube.
    public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
    {
        if (blockModelData == null || blockModelData.VerticesCount < 24) return;
        var data = DataAt(world?.BlockAccessor, pos);
        var radius = data.SelectionHalfExtent;
        decalModelData = CubeMeshUtil.GetCubeOnlyScaleXyz(radius, radius, new Vec3f(0.5f, 0.65f + data.HeightOffset, 0.5f));
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
                new SceneReadonlyBookDialog(stack, client, blockSelection.Position.Copy(), blockEntity.Data.ReadStamp, blockEntity.Data.TitleIconName).TryOpen();
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
        if (world.BlockAccessor.GetBlockEntity(pos) is SceneDescriptionBlockEntity marker && BreakRefused(marker, byPlayer))
        {
            return [];
        }

        return [CreateStackFromPlacedBlock(world, pos)];
    }

    // A lock is absolute: the creator and admins unlock in the editor first, they do not get a break exemption.
    internal static bool BreakRefused(SceneDescriptionBlockEntity marker, IPlayer player) =>
        marker.Data.IsLocked || (player != null && !marker.CanBreak(player));

    public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        if (api.World.BlockAccessor.GetBlockEntity(blockSel.Position) is SceneDescriptionBlockEntity marker && BreakRefused(marker, player))
        {
            WarnBreakRefused(marker);
            return Math.Max(remainingResistance, 1);
        }

        return base.OnGettingBroken(player, blockSel, itemslot, remainingResistance, dt, counter);
    }

    // The client keeps the resistance above zero, so the server never sees the attempt and never gets to
    // explain it. Say it here instead, throttled because holding the mouse re-enters this every tick.
    private void WarnBreakRefused(SceneDescriptionBlockEntity marker)
    {
        if (api is not ICoreClientAPI capi) return;
        var now = capi.World.ElapsedMilliseconds;
        if (now - _lastBreakWarningMs < 1500) return;
        _lastBreakWarningMs = now;
        var locked = marker.Data.IsLocked;
        capi.TriggerIngameError(this, locked ? "scene-description-locked" : "scene-description-no-access",
            Lang.Get(locked ? "thebasics:scene-description-locked-help" : "thebasics:scene-description-no-access"));
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is SceneDescriptionBlockEntity marker && BreakRefused(marker, byPlayer))
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
        var vanilla = new StringBuilder();
        base.GetHeldItemInfo(inSlot, vanilla, world, withDebugInfo);
        description.Append(WithoutMaterialLine(vanilla.ToString(), world, inSlot?.Itemstack));
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

    // Block.GetHeldItemInfo always prints "Material: Stone" and offers no way to opt out, and a marker is a
    // note rather than a building material. Rebuild the exact line vanilla emitted and drop it.
    internal string WithoutMaterialLine(string info, IWorldAccessor world, ItemStack stack)
    {
        var material = Lang.Get("Material: ") + Lang.Get("blockmaterial-" + GetBlockMaterial(world?.BlockAccessor, null, stack));
        var start = info.IndexOf(material, StringComparison.Ordinal);
        if (start < 0) return info;
        var end = info.IndexOf('\n', start);
        return end < 0 ? info[..start] : info.Remove(start, end - start + 1);
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
