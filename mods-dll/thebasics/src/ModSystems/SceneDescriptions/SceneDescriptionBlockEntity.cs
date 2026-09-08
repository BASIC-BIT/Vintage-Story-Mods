using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SceneDescriptions;

public sealed class SceneDescriptionBlockEntity : BlockEntity
{
    private const int OpenEditorPacketId = 1001;
    private const int SaveEditorPacketId = 1002;
    private const double MaxEditDistance = 8;

    private SceneDescriptionDialog _dialog;

    public SceneDescriptionData Data { get; private set; } = new();

    public void InitializeFromItem(ItemStack itemStack, IPlayer player)
    {
        Data = SceneDescriptionData.ReadFrom(itemStack?.Attributes);
        Data.EstablishCreator(player?.PlayerUID, player?.PlayerName);

        Data.Normalize();
        MarkDirty(redrawOnClient: true);
    }

    public void OpenEditor(IPlayer player)
    {
        if (Api is not ICoreServerAPI serverApi || player is not IServerPlayer serverPlayer)
        {
            return;
        }

        if (!CanEdit(player) || !IsWithinEditDistance(player))
        {
            serverPlayer.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }

        serverApi.Network.SendBlockEntityPacket(serverPlayer, Pos, OpenEditorPacketId, SerializerUtil.Serialize(ToPacket(Data)));
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetId, byte[] data)
    {
        if (packetId != SaveEditorPacketId || Api.Side != EnumAppSide.Server)
        {
            return;
        }

        if (!CanEdit(player))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }

        if (!IsWithinEditDistance(player))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-too-far", Lang.Get("thebasics:scene-description-too-far"));
            return;
        }

        SceneDescriptionEditPacket packet;
        try
        {
            packet = SerializerUtil.Deserialize<SceneDescriptionEditPacket>(data);
        }
        catch (Exception ex)
        {
            Api.Logger.Warning("[THEBASICS] Rejected malformed scene description edit at {0}: {1}", Pos, ex.Message);
            return;
        }

        Data.ApplyText(FromPacket(packet));
        MarkDirty(redrawOnClient: true);
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        Api.World.Logger.Audit("{0} edited a scene marker at {1}.", player.PlayerName, Pos);
    }

    public override void OnReceivedServerPacket(int packetId, byte[] data)
    {
        if (packetId != OpenEditorPacketId || Api is not ICoreClientAPI clientApi)
        {
            return;
        }

        SceneDescriptionEditPacket packet;
        try
        {
            packet = SerializerUtil.Deserialize<SceneDescriptionEditPacket>(data);
        }
        catch (Exception ex)
        {
            Api.Logger.Warning("[THEBASICS] Could not open scene description editor at {0}: {1}", Pos, ex.Message);
            return;
        }

        _dialog?.TryClose();
        _dialog = new SceneDescriptionDialog(clientApi, FromPacket(packet), saved =>
        {
            clientApi.Network.SendBlockEntityPacket(Pos, SaveEditorPacketId, SerializerUtil.Serialize(ToPacket(saved)));
            _dialog = null;
        }, () => _dialog = null);
        _dialog.TryOpen();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        Data.WriteTo(tree);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Data = SceneDescriptionData.ReadFrom(tree);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder description)
    {
        base.GetBlockInfo(forPlayer, description);
        description.AppendLine(Lang.Get(Data.IsLocked
            ? "thebasics:scene-description-locked-help"
            : "thebasics:scene-description-lock-help"));
        if (string.IsNullOrWhiteSpace(Data.Body))
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-empty-block-help"));
        }
    }

    public override void OnBlockRemoved()
    {
        CloseDialog();
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        CloseDialog();
        base.OnBlockUnloaded();
    }

    private bool CanEdit(IPlayer player)
    {
        return Data.CanEdit(player?.PlayerUID, HasClaimAccess(player));
    }

    internal bool CanBreak(IPlayer player)
    {
        return Data.CanBreak(player?.PlayerUID, IsAdmin(player), HasClaimAccess(player));
    }

    internal bool TryLock(IPlayer player, ItemSlot slot)
    {
        if (Api.Side != EnumAppSide.Server || !IsWithinEditDistance(player) ||
            !Data.CanLock(player?.PlayerUID, HasClaimAccess(player)) ||
            slot?.Itemstack?.Collectible is not ItemPadlock || slot.Itemstack.StackSize < 1)
        {
            return false;
        }

        Data.LockItemCode = slot.Itemstack.Collectible.Code.ToString();
        slot.TakeOut(1);
        slot.MarkDirty();
        PersistLockChange(player, "locked");
        return true;
    }

    internal bool TryUnlock(IPlayer player)
    {
        if (Api.Side != EnumAppSide.Server || !IsWithinEditDistance(player) ||
            !Data.CanUnlock(player?.PlayerUID, IsAdmin(player), HasClaimAccess(player)))
        {
            return false;
        }

        var padlock = Api.World.GetItem(new AssetLocation(Data.LockItemCode));
        // Fail closed if a saved lock's item is unavailable, rather than lose or duplicate it.
        if (padlock is not ItemPadlock)
        {
            return false;
        }

        Data.LockItemCode = string.Empty;
        PersistLockChange(player, "unlocked");
        var stack = new ItemStack(padlock);
        if (!player.InventoryManager.TryGiveItemstack(stack))
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        return true;
    }

    private void PersistLockChange(IPlayer player, string action)
    {
        MarkDirty(redrawOnClient: true);
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        Api.World.Logger.Audit("{0} {1} a scene marker at {2}.", player.PlayerName, action, Pos);
    }

    private bool HasClaimAccess(IPlayer player) =>
        player != null && Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.BuildOrBreak);

    private static bool IsAdmin(IPlayer player) => player?.HasPrivilege(Privilege.controlserver) == true;

    private bool IsWithinEditDistance(IPlayer player)
    {
        return player?.Entity?.Pos?.XYZ != null && player.Entity.Pos.XYZ.SquareDistanceTo(Pos.ToVec3d().Add(0.5, 0.5, 0.5)) <= MaxEditDistance * MaxEditDistance;
    }

    private void CloseDialog()
    {
        _dialog?.TryClose();
        _dialog = null;
    }

    private static SceneDescriptionEditPacket ToPacket(SceneDescriptionData data)
    {
        data = (data ?? new SceneDescriptionData()).Clone().Normalize();
        return new SceneDescriptionEditPacket
        {
            Title = data.Title,
            Body = data.Body,
            Kind = (int)data.Kind,
        };
    }

    private static SceneDescriptionData FromPacket(SceneDescriptionEditPacket packet)
    {
        return new SceneDescriptionData
        {
            Title = packet?.Title ?? string.Empty,
            Body = packet?.Body ?? string.Empty,
            Kind = (SceneDescriptionKind)(packet?.Kind ?? 0),
        }.Normalize();
    }
}
