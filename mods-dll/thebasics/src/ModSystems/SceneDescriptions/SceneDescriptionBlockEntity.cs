using System;
using System.Linq;
using System.Text;
using thebasics.Extensions;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace thebasics.ModSystems.SceneDescriptions;

public sealed class SceneDescriptionBlockEntity : BlockEntity
{
    private const int OpenEditorPacketId = 1001;
    private const int SaveEditorPacketId = 1002;
    private const int UnlockPacketId = 1003;
    internal const int MarkReadPacketId = 1004;
    internal const int MarkUnreadPacketId = 1005;
    private const int ClearReadPacketId = 1006;
    private const double MaxEditDistance = 8;
    private const string AppearancePreferencesKey = "thebasics-scene-appearance";

    private SceneDescriptionDialog _dialog;
    // A client-side placement prediction creates this entity with default data before the server's
    // copy (with the placer's appearance preferences) arrives, so drawing is deferred until then.
    private bool _synced;

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (_synced) RegisterAppearance();
    }

    private void RegisterAppearance()
    {
        if (Api is ICoreClientAPI client) client.ModLoader.GetModSystem<SceneDescriptionSystem>().Register(this);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator) => true;

    public SceneDescriptionData Data { get; private set; } = new();

    public void InitializeFromItem(ItemStack itemStack, IPlayer player)
    {
        Data = SceneDescriptionData.ReadFrom(itemStack?.Attributes);
        // Only pristine crafted markers inherit defaults. Picked-up markers carry scene metadata.
        if (itemStack?.Attributes?.HasAttribute(SceneDescriptionData.TitleAttribute) != true)
        {
            Data = new SceneDescriptionData();
            if (player is IServerPlayer serverPlayer)
            {
                try
                {
                    var stored = serverPlayer.GetModData<SceneDescriptionEditPacket>(AppearancePreferencesKey);
                    if (stored != null) Data = FromPacket(stored).AppearanceDefaults();
                }
                catch (Exception ex) { Api.Logger.Warning("[THEBASICS] Could not read scene appearance preferences: {0}", ex.Message); }
            }
        }
        Data.EstablishCreator(player?.PlayerUID, player?.PlayerName);

        // Every placement is a fresh marker as far as read marks are concerned.
        Data.Stamp();
        Data.Normalize();
        MarkDirty(redrawOnClient: true);
    }

    public void OpenEditor(IPlayer player)
    {
        if (Api is not ICoreServerAPI serverApi || player is not IServerPlayer serverPlayer)
        {
            return;
        }

        // The dialog only ever opens from this packet, so refusing here is also the client gate.
        if (!SceneDescriptionSystem.SceneMarkersEnabled(Api))
        {
            serverPlayer.SendIngameError("scene-markers-disabled", Lang.Get("thebasics:scene-markers-disabled"));
            return;
        }

        if (!HasClaimAccess(player) || !IsWithinEditDistance(player))
        {
            serverPlayer.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }

        var packet = ToPacket(Data);
        packet.CanManageLock = Data.IsLocked
            ? Data.CanUnlock(player.PlayerUID, IsAdmin(player), true)
            : Data.CanLock(player.PlayerUID, true);
        serverApi.Network.SendBlockEntityPacket(serverPlayer, Pos, OpenEditorPacketId, SerializerUtil.Serialize(packet));
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetId, byte[] data)
    {
        if (Api.Side == EnumAppSide.Server && !SceneDescriptionSystem.SceneMarkersEnabled(Api))
        {
            (player as IServerPlayer)?.SendIngameError("scene-markers-disabled", Lang.Get("thebasics:scene-markers-disabled"));
            return;
        }

        if (Api.Side != EnumAppSide.Server) return;

        switch (packetId)
        {
            case UnlockPacketId:
                HandleUnlock(player);
                return;
            case MarkReadPacketId:
            case MarkUnreadPacketId:
                HandleReadMark(player, packetId);
                return;
            case ClearReadPacketId:
                HandleClearRead(player);
                return;
            case SaveEditorPacketId:
                HandleSave(player, data);
                return;
        }
    }

    private void HandleUnlock(IPlayer player)
    {
        if (TryUnlock(player)) OpenEditor(player);
        else (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
    }

    // Marking a marker read is a personal preference, so it needs no edit permission.
    private void HandleReadMark(IPlayer player, int packetId)
    {
        if (player is not IServerPlayer readingPlayer) return;
        // Markers placed before read marks existed carry no stamp; give them one on first use.
        if (packetId == MarkReadPacketId && Data.ReadStamp == 0)
        {
            Data.Stamp();
            MarkDirty(redrawOnClient: true);
            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }

        SetReadMark(readingPlayer, packetId == MarkReadPacketId ? Data.ReadStamp : 0);
    }

    private void HandleClearRead(IPlayer player)
    {
        if (!CanEdit(player) || !IsWithinEditDistance(player))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }

        Data.Stamp();
        MarkDirty(redrawOnClient: true);
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        Api.World.Logger.Audit("{0} cleared the read marks on a scene marker at {1}.", player.PlayerName, Pos);
    }

    private void HandleSave(IPlayer player, byte[] data)
    {
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

        if (packet?.LockAfterSave == true && !Data.CanLock(player.PlayerUID, HasClaimAccess(player)))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }
        Data.ApplyText(FromPacket(packet));
        // Edited text is new content, so existing read marks no longer apply.
        Data.Stamp();
        if (player is IServerPlayer savingPlayer)
        {
            var defaults = ToPacket(Data.AppearanceDefaults());
            savingPlayer.SetModData(AppearancePreferencesKey, defaults);
        }
        if (packet?.LockAfterSave == true) Data.LockItemCode = "ui";
        MarkDirty(redrawOnClient: true);
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        Api.World.Logger.Audit("{0} edited a scene marker at {1}.", player.PlayerName, Pos);
        if (packet?.LockAfterSave == true) OpenEditor(player);
    }

    public override void OnReceivedServerPacket(int packetId, byte[] data)
    {
        if (packetId is MarkReadPacketId or MarkUnreadPacketId)
        {
            var stamp = data is { Length: 8 } ? BitConverter.ToInt64(data) : 0;
            SceneReadMarks.SetClientMark(Pos, stamp);
            if (Api is ICoreClientAPI client)
                foreach (var reader in client.Gui.OpenedGuis.OfType<SceneReadonlyBookDialog>().ToArray())
                    if (reader.Pos.Equals(Pos)) reader.Refresh(stamp);
            return;
        }

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

        _dialog?.TryCloseWithoutPrompt();
        _dialog = new SceneDescriptionDialog(clientApi, FromPacket(packet), packet.CanManageLock, (saved, lockAfterSave) =>
        {
            var savedPacket = ToPacket(saved);
            savedPacket.LockAfterSave = lockAfterSave;
            clientApi.Network.SendBlockEntityPacket(Pos, SaveEditorPacketId, SerializerUtil.Serialize(savedPacket));
            _dialog = null;
        }, () => clientApi.Network.SendBlockEntityPacket(Pos, UnlockPacketId),
            () => clientApi.Network.SendBlockEntityPacket(Pos, ClearReadPacketId), () => _dialog = null);
        _dialog.TryOpen();
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        Data.WriteTo(tree, includeReadStamp: true);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Data = SceneDescriptionData.ReadFrom(tree);
        _synced = true;
        if (Api is ICoreClientAPI)
        {
            RegisterAppearance();
            MarkDirty(redrawOnClient: true);
        }
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder description)
    {
        base.GetBlockInfo(forPlayer, description);
        // The name line above this is already the title (see SceneDescriptionBlock.GetHeldItemName).
        if (!string.IsNullOrWhiteSpace(Data.Body)) description.AppendLine(SceneDescriptionFormatter.InspectorPreview(Data.Body));
        if (string.IsNullOrWhiteSpace(Data.Body))
        {
            description.AppendLine(Lang.Get("thebasics:scene-description-empty-block-help"));
        }
    }

    public override void OnBlockRemoved()
    {
        UnregisterAppearance();
        CloseDialog();
        base.OnBlockRemoved();
    }

    public override void OnBlockUnloaded()
    {
        UnregisterAppearance();
        CloseDialog();
        base.OnBlockUnloaded();
    }

    private void UnregisterAppearance()
    {
        if (Api is ICoreClientAPI client)
            client.ModLoader.GetModSystem<SceneDescriptionSystem>().Unregister(this);
    }

    private void SetReadMark(IServerPlayer player, long stamp)
    {
        var marks = player.GetSceneReadMarks();
        SceneReadMarks.Set(marks.Marks, SceneReadMarks.Key(Pos), stamp);
        player.SetSceneReadMarks(marks);
        (Api as ICoreServerAPI)?.Network.SendBlockEntityPacket(player, Pos,
            stamp == 0 ? MarkUnreadPacketId : MarkReadPacketId, BitConverter.GetBytes(stamp));
    }

    private bool CanEdit(IPlayer player)
    {
        return Data.CanEdit(player?.PlayerUID, HasClaimAccess(player));
    }

    internal bool CanBreak(IPlayer player)
    {
        return Data.CanBreak(player?.PlayerUID, IsAdmin(player), HasClaimAccess(player));
    }

    internal bool TryUnlock(IPlayer player)
    {
        if (Api.Side != EnumAppSide.Server || !IsWithinEditDistance(player) ||
            !Data.CanUnlock(player?.PlayerUID, IsAdmin(player), HasClaimAccess(player)))
        {
            return false;
        }

        Data.LockItemCode = string.Empty;
        PersistLockChange(player, "unlocked");
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
        _dialog?.TryCloseWithoutPrompt();
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
            Display = (int)data.Display,
            Appearance = (int)data.Appearance,
            Symbol = (int)data.Symbol,
            SymbolIconName = data.SymbolIconName,
            IconDistance = data.IconDistance,
            UnlimitedIconDistance = data.UnlimitedIconDistance,
            TextDistance = data.TextDistance,
            UnlimitedTextDistance = data.UnlimitedTextDistance,
            HeightOffset = data.HeightOffset,
            IndicatorScale = data.IndicatorScale,
            Color = (int)data.Color,
            Effect = (int)data.Effect,
            IdleBobbing = data.IdleBobbing,
            ShowBodyInBubble = data.ShowBodyInBubble,
            BubbleScale = data.BubbleScale,
            TitleIcon = data.TitleIcon,
            TitleIconName = data.TitleIconName,
            IsLocked = data.IsLocked,
        };
    }

    private static SceneDescriptionData FromPacket(SceneDescriptionEditPacket packet)
    {
        packet ??= new SceneDescriptionEditPacket();
        return new SceneDescriptionData
        {
            Title = packet.Title,
            Body = packet.Body,
            Kind = (SceneDescriptionKind)packet.Kind,
            Display = (SceneDescriptionDisplay)packet.Display,
            Appearance = (SceneMarkerAppearance)packet.Appearance,
            Symbol = (SceneMarkerSymbol)packet.Symbol,
            SymbolIconName = packet.SymbolIconName,
            IconDistance = packet.IconDistance,
            UnlimitedIconDistance = packet.UnlimitedIconDistance,
            TextDistance = packet.TextDistance,
            UnlimitedTextDistance = packet.UnlimitedTextDistance,
            HeightOffset = packet.HeightOffset,
            IndicatorScale = packet.IndicatorScale,
            Color = (SceneMarkerColor)packet.Color,
            Effect = (SceneMarkerEffect)packet.Effect,
            IdleBobbing = packet.IdleBobbing,
            ShowBodyInBubble = packet.ShowBodyInBubble,
            BubbleScale = packet.BubbleScale,
            TitleIcon = packet.TitleIcon,
            TitleIconName = packet.TitleIconName,
            LockItemCode = packet.IsLocked ? "ui" : string.Empty,
        }.Normalize();
    }
}
