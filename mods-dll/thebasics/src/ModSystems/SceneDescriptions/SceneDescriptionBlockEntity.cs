using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

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
        if (string.IsNullOrWhiteSpace(Data.AuthorUid) && player != null)
        {
            Data.AuthorUid = player.PlayerUID ?? string.Empty;
            Data.AuthorName = player.PlayerName ?? string.Empty;
        }

        Data.Normalize();
        MarkDirty(redrawOnClient: true);
    }

    public void OpenEditor(IPlayer player)
    {
        if (Api is not ICoreServerAPI serverApi || player is not IServerPlayer serverPlayer)
        {
            return;
        }

        if (!CanEdit(player))
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

        var next = FromPacket(packet);
        next.AuthorUid = player.PlayerUID;
        next.AuthorName = player.PlayerName;
        Data = next.Normalize();
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
        return player != null && Api.World.Claims.TryAccess(player, Pos, EnumBlockAccessFlags.BuildOrBreak);
    }

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
