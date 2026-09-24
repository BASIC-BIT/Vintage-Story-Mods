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
    internal const int RequestEditorPacketId = 1007;
    internal const int AddEntryPacketId = 1008;
    internal const int RemoveEntryPacketId = 1009;
    private const double MaxEditDistance = 8;
    private const string AppearancePreferencesKey = "thebasics-scene-appearance";

    private SceneDescriptionDialog _dialog;
    private SceneEntryChooserDialog _chooser;
    private SceneDescriptionData _summaryData;
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

    // Keep the physical plate in the terrain mesh independently of the floating overlays.
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator) => false;

    // Client-only damage presentation, retained while cracks heal after looking away.
    internal bool BreakingSymbol { get; set; }

    public SceneDescriptionEntries Entries { get; private set; } = new();
    public SceneDescriptionData Data => Entries.Primary.Data;

    // The first entry supplies the shared icon style. A multi-entry marker shows a neutral
    // targeted caption instead of implying that one author's text represents every entry.
    internal SceneDescriptionData DisplayData
    {
        get
        {
            if (Entries.Entries.Count == 1) return Data;
            if (_summaryData != null) return _summaryData;
            _summaryData = Data.Clone();
            _summaryData.Title = Lang.Get("thebasics:scene-entry-count", Entries.Entries.Count);
            _summaryData.Body = string.Empty;
            _summaryData.Kind = SceneDescriptionKind.Environmental;
            _summaryData.ShowBodyInBubble = false;
            _summaryData.Display = SceneDescriptionDisplay.WhenTargeted;
            return _summaryData;
        }
    }

    internal bool AllReadOnClient => Entries.Entries.All(entry => SceneReadMarks.IsRead(Pos, entry.Id, entry.Data.ReadStamp));

    public void InitializeFromItem(ItemStack itemStack, IPlayer player)
    {
        Entries = SceneDescriptionEntries.ReadFrom(itemStack?.Attributes);
        // Only pristine crafted markers inherit defaults. Picked-up markers carry scene metadata.
        if (itemStack?.Attributes?.HasAttribute(SceneDescriptionData.TitleAttribute) != true &&
            itemStack?.Attributes?.GetTreeAttribute("sceneEntries") == null)
        {
            Entries = new SceneDescriptionEntries();
            if (player is IServerPlayer serverPlayer)
            {
                try
                {
                    var stored = serverPlayer.GetModData<SceneDescriptionEditPacket>(AppearancePreferencesKey);
                    if (stored != null) Data.ApplyText(FromPacket(stored).AppearanceDefaults());
                }
                catch (Exception ex) { Api.Logger.Warning("[THEBASICS] Could not read scene appearance preferences: {0}", ex.Message); }
            }
        }
        foreach (var entry in Entries.Entries)
        {
            entry.Data.EstablishCreator(player?.PlayerUID, player?.PlayerName);
            // Every placement is fresh as far as read marks are concerned.
            entry.Data.Stamp();
            entry.Data.Normalize();
        }
        _summaryData = null;
        MarkDirty(redrawOnClient: true);
    }

    public void OpenEditor(IPlayer player, string entryId = null)
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

        var entry = ResolveEntry(entryId);
        if (entry == null)
        {
            if (!string.IsNullOrEmpty(entryId))
                serverPlayer.SendIngameError("scene-entry-removed", Lang.Get("thebasics:scene-entry-removed"));
            return;
        }
        var editorData = entry.Data.Clone();
        if (entry != Entries.Primary) editorData.ApplyAppearance(Data);
        var packet = ToPacket(editorData);
        packet.EntryId = entry.Id;
        packet.CanManageLock = entry.Data.IsLocked
            ? entry.Data.CanUnlock(player.PlayerUID, IsAdmin(player), true)
            : entry.Data.CanLock(player.PlayerUID, true);
        serverApi.Network.SendBlockEntityPacket(serverPlayer, Pos, OpenEditorPacketId, SerializerUtil.Serialize(packet));
    }

    internal SceneDescriptionEntry TryAddEntry(IPlayer player, bool persist = true)
    {
        if (Api.Side != EnumAppSide.Server || !HasClaimAccess(player) || !IsWithinEditDistance(player) || !SceneAnalytics.Written(Entries)) return null;
        var entry = Entries.Add(Data.AppearanceDefaults());
        if (entry == null) return null;
        entry.Data.EstablishCreator(player.PlayerUID, player.PlayerName);
        entry.Data.Stamp();
        if (persist) PersistEntries(player, "added a description to");
        return entry;
    }

    internal bool TryRemoveEntry(IPlayer player, string entryId)
    {
        var entry = Entries.Find(entryId);
        if (Api.Side != EnumAppSide.Server || entry == null || !entry.Data.CanEdit(player?.PlayerUID, HasClaimAccess(player)) ||
            !IsWithinEditDistance(player) || !Entries.Remove(entryId)) return false;
        _summaryData = null;
        PersistEntries(player, "removed a description from");
        return true;
    }

    internal void ShowChooser(ICoreClientAPI client, bool editing)
    {
        if (client == null) return;
        if (editing && !CanOpenEditChooser(client.World.Player)) return;
        _chooser?.TryClose();
        var choices = Entries.Entries.Select(entry => new SceneEntryChoice(entry.Id, entry.Data.Title, entry.Data.AuthorName,
            SceneReadMarks.IsRead(Pos, entry.Id, entry.Data.ReadStamp), entry.Data.IsLocked,
            editing && Entries.Entries.Count > 1 && !entry.Data.IsLocked)).ToArray();
        _chooser = new SceneEntryChooserDialog(client, choices, editing, editing && SceneAnalytics.Written(Entries) && choices.Length < SceneDescriptionEntries.MaxEntries,
            id =>
            {
                if (editing) client.Network.SendBlockEntityPacket(Pos, RequestEditorPacketId, SerializerUtil.Serialize(new SceneEntryActionPacket { EntryId = id }));
                else OpenReader(client, id, backToChooser: true);
            },
            () => client.Network.SendBlockEntityPacket(Pos, AddEntryPacketId),
            id => client.Network.SendBlockEntityPacket(Pos, RemoveEntryPacketId, SerializerUtil.Serialize(new SceneEntryActionPacket { EntryId = id })),
            () => _chooser = null);
        _chooser.TryOpen();
    }

    internal void OpenReader(ICoreClientAPI client, string entryId = null, bool backToChooser = false)
    {
        if (client == null) return;
        var entry = ResolveEntry(entryId);
        if (entry == null)
        {
            if (!string.IsNullOrEmpty(entryId))
                client.TriggerIngameError(this, "scene-entry-removed", Lang.Get("thebasics:scene-entry-removed"));
            return;
        }
        var stack = new ItemStack(Block);
        stack.Attributes.SetString("title", entry.Data.Title);
        stack.Attributes.SetString("text", SceneDescriptionFormatter.EscapeLiteral(entry.Data.Body));
        if (new SceneReadonlyBookDialog(stack, client, Pos.Copy(), entry.Id, entry.Data.ReadStamp, entry.Data.TitleIconName,
                backToChooser ? () => ShowChooser(client, editing: false) : null).TryOpen())
            client.ModLoader.GetModSystem<SceneDescriptionSystem>()?.Analytics.ReaderOpened(Pos, entry.Id);
    }

    private SceneDescriptionEntry ResolveEntry(string entryId) =>
        string.IsNullOrEmpty(entryId) ? (Entries.Entries.Count == 1 ? Entries.Primary : null) : Entries.Find(entryId);

    private void PersistEntries(IPlayer player, string action)
    {
        MarkDirty(redrawOnClient: true);
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        Api.World.Logger.Audit("{0} {1} a scene marker at {2}.", player.PlayerName, action, Pos);
    }

    public override void OnReceivedClientPacket(IPlayer player, int packetId, byte[] data)
    {
        if (Api.Side == EnumAppSide.Server && !SceneDescriptionSystem.SceneMarkersEnabled(Api)
            && packetId != MarkReadPacketId && packetId != MarkUnreadPacketId)
        {
            (player as IServerPlayer)?.SendIngameError("scene-markers-disabled", Lang.Get("thebasics:scene-markers-disabled"));
            return;
        }

        if (Api.Side != EnumAppSide.Server) return;

        switch (packetId)
        {
            case UnlockPacketId:
                HandleUnlock(player, data);
                return;
            case MarkReadPacketId:
            case MarkUnreadPacketId:
                HandleReadMark(player, packetId, data);
                return;
            case ClearReadPacketId:
                HandleClearRead(player, data);
                return;
            case SaveEditorPacketId:
                HandleSave(player, data);
                return;
            case RequestEditorPacketId:
                OpenEditor(player, ReadActionId(data));
                return;
            case AddEntryPacketId:
                HandleAddEntry(player);
                return;
            case RemoveEntryPacketId:
                HandleRemoveEntry(player, data);
                return;
        }
    }

    private void HandleUnlock(IPlayer player, byte[] data)
    {
        var entryId = ReadActionId(data);
        if (TryUnlock(player, entryId)) OpenEditor(player, entryId);
        else (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
    }

    private void HandleAddEntry(IPlayer player)
    {
        if (player is IServerPlayer serverPlayer && Api is ICoreServerAPI serverApi &&
            HasClaimAccess(player) && IsWithinEditDistance(player) && SceneAnalytics.Written(Entries) &&
            Entries.Entries.Count < SceneDescriptionEntries.MaxEntries)
        {
            var packet = ToPacket(Data.AppearanceDefaults());
            packet.CreateNew = true;
            packet.CanManageLock = true;
            serverApi.Network.SendBlockEntityPacket(serverPlayer, Pos, OpenEditorPacketId, SerializerUtil.Serialize(packet));
            return;
        }
        (player as IServerPlayer)?.SendIngameError("scene-entry-full", Lang.Get(
            Entries.Entries.Count >= SceneDescriptionEntries.MaxEntries ? "thebasics:scene-entry-full" : "thebasics:scene-description-no-access"));
    }

    private void HandleRemoveEntry(IPlayer player, byte[] data)
    {
        var entryId = ReadActionId(data);
        if (TryRemoveEntry(player, entryId)) return;
        (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get(
            Entries.Entries.Count == 1 ? "thebasics:scene-entry-last" : "thebasics:scene-description-no-access"));
    }

    private static string ReadActionId(byte[] data)
    {
        if (data == null || data.Length == 0) return null;
        try { return SerializerUtil.Deserialize<SceneEntryActionPacket>(data)?.EntryId; }
        catch { return null; }
    }

    // Marking a marker read is a personal preference, so it needs no edit permission.
    private void HandleReadMark(IPlayer player, int packetId, byte[] data)
    {
        if (player is not IServerPlayer readingPlayer) return;
        SceneReadMarkPacket packet;
        try
        {
            if (packetId == MarkUnreadPacketId && data is not { Length: > 0 }) packet = new SceneReadMarkPacket();
            // Old clients sent the displayed stamp as a raw Int64. A protobuf packet for the
            // unstamped "legacy" entry is also eight bytes, so length alone cannot identify it.
            else if (data is { Length: 8 } && Entries.Entries.Count == 1 &&
                     BitConverter.ToInt64(data) == Data.ReadStamp)
                packet = new SceneReadMarkPacket { Stamp = Data.ReadStamp };
            else packet = SerializerUtil.Deserialize<SceneReadMarkPacket>(data);
        }
        catch { return; }
        var entry = ResolveEntry(packet?.EntryId);
        if (entry == null) return;
        // The reader is a snapshot. Never mark replacement content the player has not opened.
        if (packetId == MarkReadPacketId && packet.Stamp != entry.Data.ReadStamp) return;
        // Markers placed before read marks existed carry no stamp; give them one on first use.
        if (packetId == MarkReadPacketId && entry.Data.ReadStamp == 0)
        {
            entry.Data.Stamp();
            MarkDirty(redrawOnClient: true);
            Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        }

        var wasRead = SceneReadMarks.IsRead(readingPlayer.GetSceneReadMarks().Marks, Pos, entry.Id, entry.Data.ReadStamp);
        var markRead = packetId == MarkReadPacketId;
        SetReadMark(readingPlayer, entry, markRead ? entry.Data.ReadStamp : 0);
        if (SceneDescriptionSystem.SceneMarkersEnabled(Api) && wasRead != markRead && player.Entity?.Pos?.Dimension == Pos.dimension && IsWithinEditDistance(player))
            SceneAnalytics.Track(entry.Data, markRead ? "marked_read" : "marked_unread");
    }

    private void HandleClearRead(IPlayer player, byte[] data)
    {
        var entry = ResolveEntry(ReadActionId(data));
        if (entry == null || !entry.Data.CanEdit(player?.PlayerUID, HasClaimAccess(player)) || !IsWithinEditDistance(player))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }

        entry.Data.Stamp();
        MarkDirty(redrawOnClient: true);
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        Api.World.Logger.Audit("{0} cleared the read marks on a scene description at {1}.", player.PlayerName, Pos);
    }

    private void HandleSave(IPlayer player, byte[] data)
    {
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

        if (packet == null) return;
        var creating = packet.CreateNew;
        var entry = creating ? null : ResolveEntry(packet.EntryId);
        if (!CanSave(player, creating, entry))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }
        if (!IsWithinEditDistance(player))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-too-far", Lang.Get("thebasics:scene-description-too-far"));
            return;
        }
        if (creating && !SceneAnalytics.Written(FromPacket(packet)))
        {
            (player as IServerPlayer)?.SendIngameError("scene-entry-empty", Lang.Get("thebasics:scene-entry-empty-save"));
            return;
        }
        if (packet.LockAfterSave && !creating && !entry.Data.CanLock(player.PlayerUID, true))
        {
            (player as IServerPlayer)?.SendIngameError("scene-description-no-access", Lang.Get("thebasics:scene-description-no-access"));
            return;
        }
        if (creating)
        {
            entry = TryAddEntry(player, persist: false);
            if (entry == null) return;
        }
        var previousData = creating ? new SceneDescriptionData() : entry.Data.Clone();
        entry.Data.ApplyText(FromPacket(packet), includeAppearance: entry == Entries.Primary);
        if (entry == Entries.Primary) Entries.SyncAppearance();
        // Edited text is new content, so existing read marks no longer apply.
        entry.Data.Stamp();
        _summaryData = null;
        if (player is IServerPlayer savingPlayer)
        {
            var defaults = ToPacket(entry.Data.AppearanceDefaults());
            savingPlayer.SetModData(AppearancePreferencesKey, defaults);
        }
        if (packet.LockAfterSave) entry.Data.LockItemCode = "ui";
        MarkDirty(redrawOnClient: true);
        Api.World.BlockAccessor.GetChunkAtBlockPos(Pos)?.MarkModified();
        Api.World.Logger.Audit("{0} edited a scene description at {1}.", player.PlayerName, Pos);
        SceneAnalytics.Track(entry.Data, "saved", "scene_save_kind", SceneAnalytics.SaveKind(previousData, entry.Data));
    }

    private bool CanSave(IPlayer player, bool creating, SceneDescriptionEntry entry) =>
        HasClaimAccess(player) && (creating
            ? SceneAnalytics.Written(Entries) && Entries.Entries.Count < SceneDescriptionEntries.MaxEntries
            : entry != null && entry.Data.CanEdit(player?.PlayerUID, true));

    public override void OnReceivedServerPacket(int packetId, byte[] data)
    {
        if (packetId is MarkReadPacketId or MarkUnreadPacketId)
        {
            SceneReadMarkPacket readPacket;
            try
            {
                readPacket = SerializerUtil.Deserialize<SceneReadMarkPacket>(data);
            }
            catch { return; }
            var entry = ResolveEntry(readPacket?.EntryId);
            if (entry == null) return;
            SceneReadMarks.SetClientMark(Pos, entry.Id, readPacket.Stamp);
            if (Api is ICoreClientAPI client)
                foreach (var reader in client.Gui.OpenedGuis.OfType<SceneReadonlyBookDialog>().ToArray())
                    if (reader.Pos.Equals(Pos) && reader.EntryId == entry.Id) reader.Refresh(readPacket.Stamp);
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
        var entryId = packet.EntryId;
        var options = EditorOptions(packet);
        _dialog = new SceneDescriptionDialog(clientApi, FromPacket(packet), options, (saved, lockAfterSave) =>
        {
            var savedPacket = ToPacket(saved);
            savedPacket.EntryId = entryId;
            savedPacket.CreateNew = packet.CreateNew;
            savedPacket.LockAfterSave = lockAfterSave;
            clientApi.Network.SendBlockEntityPacket(Pos, SaveEditorPacketId, SerializerUtil.Serialize(savedPacket));
            _dialog = null;
        }, packet.CreateNew ? null : () => clientApi.Network.SendBlockEntityPacket(Pos, UnlockPacketId, SerializerUtil.Serialize(new SceneEntryActionPacket { EntryId = entryId })),
            packet.CreateNew ? null : () => clientApi.Network.SendBlockEntityPacket(Pos, ClearReadPacketId, SerializerUtil.Serialize(new SceneEntryActionPacket { EntryId = entryId })),
            () => _dialog = null,
            () => clientApi.Network.SendBlockEntityPacket(Pos, AddEntryPacketId));
        _dialog.TryOpen();
    }

    internal SceneDescriptionDialogOptions EditorOptions(SceneDescriptionEditPacket packet) => new(packet.CanManageLock,
        CanAdd: !packet.CreateNew && SceneAnalytics.Written(Entries) && Entries.Entries.Count < SceneDescriptionEntries.MaxEntries,
        CanEditAppearance: !packet.CreateNew && packet.EntryId == Entries.Primary.Id,
        IsNewEntry: packet.CreateNew);

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        Entries.WriteTo(tree, includeReadStamps: true);
        // Older builds can still display the first entry, though they cannot preserve a collection.
        Data.WriteTo(tree, includeReadStamp: true);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        Entries = SceneDescriptionEntries.ReadFrom(tree);
        _summaryData = null;
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
        if (Entries.Entries.Count > 1)
        {
            description.AppendLine(Lang.Get("thebasics:scene-entry-count", Entries.Entries.Count));
            return;
        }
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

    private void SetReadMark(IServerPlayer player, SceneDescriptionEntry entry, long stamp)
    {
        var marks = player.GetSceneReadMarks();
        SceneReadMarks.Set(marks.Marks, Pos, entry.Id, stamp);
        player.SetSceneReadMarks(marks);
        (Api as ICoreServerAPI)?.Network.SendBlockEntityPacket(player, Pos,
            stamp == 0 ? MarkUnreadPacketId : MarkReadPacketId,
            SerializerUtil.Serialize(new SceneReadMarkPacket { EntryId = entry.Id, Stamp = stamp }));
    }

    internal bool CanBreak(IPlayer player)
    {
        var hasClaimAccess = HasClaimAccess(player);
        return Entries.Entries.All(entry => entry.Data.CanBreak(player?.PlayerUID, IsAdmin(player), hasClaimAccess));
    }

    internal bool CanOpenEditChooser(IPlayer player) => HasClaimAccess(player) && IsWithinEditDistance(player);

    internal bool TryUnlock(IPlayer player, string entryId = null)
    {
        var entry = ResolveEntry(entryId);
        if (Api.Side != EnumAppSide.Server || !IsWithinEditDistance(player) ||
            entry == null || !entry.Data.CanUnlock(player?.PlayerUID, IsAdmin(player), HasClaimAccess(player)))
        {
            return false;
        }

        entry.Data.LockItemCode = string.Empty;
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
        return player?.Entity?.Pos?.XYZ != null && player.Entity.Pos.Dimension == Pos.dimension && player.Entity.Pos.XYZ.SquareDistanceTo(Pos.ToVec3d().Add(0.5, 0.5, 0.5)) <= MaxEditDistance * MaxEditDistance;
    }

    private void CloseDialog()
    {
        _dialog?.TryCloseWithoutPrompt();
        _dialog = null;
        _chooser?.TryClose();
        _chooser = null;
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
