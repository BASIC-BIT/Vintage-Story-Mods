using System;
using System.Linq;
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

    // The terrain picker uses index 0 for the plate; the supplemental picker uses index 1 for the symbol.
    internal const int SymbolSelectionIndex = 1;

    // Include the complete idle-bob envelope without making targeting wobble.
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
        // Out-of-voxel symbol boxes must not interrupt client terrain traversal.
        // Plates remain targetable regardless of the floating indicator's distance or height.
        if (api?.Side == EnumAppSide.Client || !SceneDescriptionSystem.SceneMarkersEnabled(api))
            return SelectionBoxes ?? [];
        return [.. SelectionBoxes ?? [], SymbolBox(DataAt(blockAccessor, pos))];
    }

    internal Cuboidf SelectedBox(BlockSelection selection)
    {
        if (selection.SelectionBoxIndex == SymbolSelectionIndex && SceneDescriptionSystem.SceneMarkersEnabled(api))
            return SymbolBox(DataAt(api.World.BlockAccessor, selection.Position));
        return SelectionBoxes?[0];
    }

    public void RenderSelectionBoxes(BlockSelection blockSel, RenderBoxDelegate renderBoxHandler)
    {
        if (api is not ICoreClientAPI capi || blockSel?.Position == null) return;
        var box = SelectedBox(blockSel);
        if (box != null)
            renderBoxHandler(box, 1.6f * ClientSettings.Wireframethickness, GetSelectionColor(capi, blockSel.Position));
    }

    public override void GetDecal(IWorldAccessor world, BlockPos pos, ITexPositionSource decalTexSource, ref MeshData decalModelData, ref MeshData blockModelData)
    {
        var marker = world?.BlockAccessor.GetBlockEntity(pos) as SceneDescriptionBlockEntity;
        if (marker?.BreakingSymbol != true ||
            !SceneDescriptionSystem.SceneMarkersEnabled(api))
        {
            base.GetDecal(world, pos, decalTexSource, ref decalModelData, ref blockModelData);
            return;
        }
        // Leave the terrain model's UVs intact for the decal shader's opaque-texel test.
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

        var placed = blockSelection.Face == BlockFacing.DOWN
            ? TryPlaceCeiling(world, byPlayer, blockSelection, ref failureCode)
            : base.TryPlaceBlock(world, byPlayer, itemStack, blockSelection, ref failureCode);
        if (placed && world.Side == EnumAppSide.Server && world.BlockAccessor.GetBlockEntity(blockSelection.Position) is SceneDescriptionBlockEntity blockEntity)
        {
            blockEntity.InitializeFromItem(itemStack, byPlayer);
            var reused = itemStack?.Attributes?.HasAttribute(SceneDescriptionData.TitleAttribute) == true ||
                itemStack?.Attributes?.GetTreeAttribute("sceneEntries") != null;
            SceneAnalytics.Track(blockEntity.Entries, "placed", "scene_placement", reused ? "reused" : "fresh",
                blockEntity.Block?.Variant?["attachment"] switch { "wall" => "wall", "ground" => "ground", _ => null });
            if (reused && SceneAnalytics.Written(blockEntity.Entries)) SceneAnalytics.Track(blockEntity.Entries, "moved", "scene_placement", "reused",
                blockEntity.Block?.Variant?["attachment"] switch { "wall" => "wall", "ground" => "ground", _ => null });
        }

        return placed;
    }

    private bool TryPlaceCeiling(IWorldAccessor world, IPlayer player, BlockSelection selection, ref string failureCode)
    {
        var supportPos = selection.Position.UpCopy();
        var support = world.BlockAccessor.GetBlock(supportPos);
        if (!support.CanAttachBlockAt(world.BlockAccessor, this, supportPos, BlockFacing.DOWN) &&
            support.GetAttributes(world.BlockAccessor, supportPos)?.IsTrue("partialAttachable") != true)
        {
            failureCode = "requiresattachable";
            return false;
        }
        var side = SuggestedHVOrientation(player, selection)[0].Code;
        var ceiling = world.BlockAccessor.GetBlock(CodeWithParts("ceiling", side));
        if (!ceiling.CanPlaceBlock(world, player, selection, ref failureCode)) return false;
        world.BlockAccessor.SetBlock(ceiling.BlockId, selection.Position);
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSelection)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is SceneDescriptionBlockEntity blockEntity)
        {
            if (byPlayer?.Entity?.Controls?.ShiftKey == true)
            {
                if (!SceneDescriptionSystem.SceneMarkersEnabled(api))
                {
                    (api as ICoreClientAPI)?.TriggerIngameError(this, "scene-markers-disabled", Lang.Get("thebasics:scene-markers-disabled"));
                    return true;
                }
                if (blockEntity.Entries.Entries.Count == 1)
                {
                    if (world.Side == EnumAppSide.Server) blockEntity.OpenEditor(byPlayer);
                }
                else if (api is ICoreClientAPI editClient)
                {
                    if (blockEntity.CanOpenEditChooser(byPlayer)) blockEntity.ShowChooser(editClient, editing: true);
                    else editClient.TriggerIngameError(this, "scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
                }
            }
            else if (api is ICoreClientAPI client)
            {
                if (blockEntity.Entries.Entries.Count == 1) blockEntity.OpenReader(client);
                else blockEntity.ShowChooser(client, editing: false);
            }

            return true;
        }

        return false;
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSelection, EntitySelection entitySelection, bool firstEvent, ref EnumHandHandling handling)
    {
        var entries = SceneDescriptionEntries.ReadFrom(slot?.Itemstack?.Attributes);
        if (byEntity?.Controls?.ShiftKey != true || !entries.Entries.Any(entry => SceneAnalytics.Written(entry.Data)))
        {
            base.OnHeldInteractStart(slot, byEntity, blockSelection, entitySelection, firstEvent, ref handling);
            return;
        }

        handling = EnumHandHandling.PreventDefault;
        if (api is ICoreClientAPI capi)
        {
            if (entries.Entries.Count == 1) OpenHeldReader(capi, slot.Itemstack, entries.Primary);
            else
            {
                var choices = entries.Entries.Select(entry => new SceneEntryChoice(entry.Id, entry.Data.Title,
                    entry.Data.AuthorName, null, entry.Data.IsLocked)).ToArray();
                new SceneEntryChooserDialog(capi, choices, editing: false, canAdd: false,
                    id =>
                    {
                        var selected = entries.Find(id);
                        if (selected != null) OpenHeldReader(capi, slot.Itemstack, selected);
                    }).TryOpen();
            }
        }
    }

    private static void OpenHeldReader(ICoreClientAPI client, ItemStack source, SceneDescriptionEntry entry)
    {
        var readableStack = source.Clone();
        readableStack.Attributes.SetString("title", entry.Data.Title);
        readableStack.Attributes.SetString("text", SceneDescriptionFormatter.EscapeLiteral(entry.Data.Body));
        if (new GuiDialogReadonlyBook(readableStack, client).TryOpen())
            client.ModLoader.GetModSystem<SceneDescriptionSystem>()?.Analytics.ReaderOpened(null, entry.Id);
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
        marker.Entries.Entries.Any(entry => entry.Data.IsLocked) || (player != null && !marker.CanBreak(player));

    public override float OnGettingBroken(IPlayer player, BlockSelection blockSel, ItemSlot itemslot, float remainingResistance, float dt, int counter)
    {
        var marker = api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as SceneDescriptionBlockEntity;
        if (marker != null && BreakRefused(marker, player))
        {
            WarnBreakRefused(marker);
            return Math.Max(remainingResistance, 1);
        }

        if (api.Side == EnumAppSide.Client && marker != null)
            marker.BreakingSymbol = blockSel.SelectionBoxIndex == SymbolSelectionIndex;

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
        var locked = marker.Entries.Entries.Any(entry => entry.Data.IsLocked);
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

        var removed = (world.BlockAccessor.GetBlockEntity(pos) as SceneDescriptionBlockEntity)?.Entries;
        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        if (world.Side == EnumAppSide.Server && SceneDescriptionSystem.SceneMarkersEnabled(api) && byPlayer != null && removed != null && world.BlockAccessor.GetBlockEntity(pos) is not SceneDescriptionBlockEntity)
            SceneAnalytics.Track(removed, "removed");
    }

    public override void OnBlockExploded(IWorldAccessor world, BlockPos pos, BlockPos explosionCenter, EnumBlastType blastType, string ignitedByPlayerUid)
    {
        // Explosions must not become an indirect way to steal/destroy a locked marker.
        if (world.BlockAccessor.GetBlockEntity(pos) is SceneDescriptionBlockEntity marker &&
            marker.Entries.Entries.Any(entry => entry.Data.IsLocked))
        {
            return;
        }

        base.OnBlockExploded(world, pos, explosionCenter, blastType, ignitedByPlayerUid);
    }

    private static void DenyLocked(IPlayer player) =>
        (player as IServerPlayer)?.SendIngameError("scene-description-locked", Lang.Get("thebasics:scene-description-locked-help"));

    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
    {
        var stack = CreateStackFromPlacedBlock(world, pos);
        var copy = SceneDescriptionEntries.ReadFrom(stack.Attributes);
        foreach (var entry in copy.Entries) entry.Data.LockItemCode = string.Empty;
        copy.WriteTo(stack.Attributes);
        copy.Primary.Data.WriteTo(stack.Attributes);
        return stack;
    }

    public override string GetHeldItemName(ItemStack itemStack)
    {
        var entries = SceneDescriptionEntries.ReadFrom(itemStack?.Attributes);
        if (entries.Entries.Count > 1) return Lang.Get("thebasics:scene-entry-count", entries.Entries.Count);
        var title = itemStack?.Attributes?.GetString(SceneDescriptionData.TitleAttribute, string.Empty)?.Trim();
        return string.IsNullOrWhiteSpace(title) ? base.GetHeldItemName(itemStack) : SceneDescriptionFormatter.EscapeLiteral(title);
    }

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder description, IWorldAccessor world, bool withDebugInfo)
    {
        var vanilla = new StringBuilder();
        base.GetHeldItemInfo(inSlot, vanilla, world, withDebugInfo);
        description.Append(WithoutMaterialLine(vanilla.ToString(), world, inSlot?.Itemstack));
        var entries = SceneDescriptionEntries.ReadFrom(inSlot?.Itemstack?.Attributes);
        if (entries.Entries.Count > 1)
        {
            description.AppendLine(Lang.Get("thebasics:scene-entry-count", entries.Entries.Count));
            foreach (var entry in entries.Entries)
                description.AppendLine(SceneDescriptionFormatter.EscapeLiteral(string.IsNullOrWhiteSpace(entry.Data.Title)
                    ? Lang.Get("thebasics:scene-entry-untitled") : entry.Data.Title));
            description.AppendLine(Lang.Get("thebasics:scene-entry-read-item-help"));
            return;
        }
        var data = SceneDescriptionData.ReadFrom(inSlot?.Itemstack?.Attributes);
        if (!SceneAnalytics.Written(data))
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-empty-item-help"));
            return;
        }

        if (!string.IsNullOrWhiteSpace(data.AuthorName))
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-authored-by", data.AuthorName));
        }

        description.AppendLine(SceneDescriptionFormatter.EscapeLiteral(Preview(data.Body)));
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
            blockEntity.Entries.WriteTo(stack.Attributes);
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
