using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ropeway;

public sealed class RopewayModSystem : ModSystem
{
    public const string ChannelName = "ropeway";

    /// <summary>
    /// The rider's only control. A hotkey rather than a seat control on purpose: reading Controls.Forward
    /// the way EntityBehaviorRideable does needs <c>controllable: true</c> on a seat, and a controlling
    /// client stops interpolating the cabin's position and rides in a 30 Hz stutter. Vanilla's own
    /// multi-stop rideable, EntityElevator, has no rider controls at all - you call it from the outside -
    /// so there was nothing to copy.
    /// </summary>
    public const string StopHotkey = "ropewaystop";

    /// <summary>
    /// Every loaded tower, keyed by its FOOTING position - the one canonical position, the same one
    /// <see cref="LineCache"/>, <see cref="RopewayLine.Towers"/>, every persisted span and the cabin's
    /// LineKey use. BEPylonBase.Initialize adds, OnBlockUnloaded removes.
    /// </summary>
    public readonly Dictionary<BlockPos, BEPylonBase> LoadedTowers = new();

    // There were two more tables here, one of every loaded tension weight and one of every loaded drive
    // housing, keyed by their own positions because a block bound to a line by PROXIMITY has no tower to be
    // indexed under. Both are cells of a station now, so the footing above finds them at a known offset and
    // there is nothing to keep in step with chunk loads.

    /// <summary>Derived line geometry, keyed by every member tower. Never persisted; InvalidateLine drops it.</summary>
    public readonly Dictionary<BlockPos, RopewayLine> LineCache = new();

    public RopewayLinkService LinkService { get; private set; }

    public PylonPickerDialog Dialog { get; private set; }

    public RopewayGuideDialog GuideDialog { get; private set; }

    /// <summary>Client only. Nothing to dispose - its hotkey and tick listener die with the session.</summary>
    public RopewayRideCamera RideCamera { get; private set; }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        // One block class for all FIVE footings - pylonbase, drivestation, tensionstation, and the shaft's
        // shafthead and shaftfoot. They differ only in the multiblockStructure and the handful of attributes
        // their own Attributes carry, which BEPylonBase reads off its block. The shaft is a second machine
        // and it added no block class, no block entity class and no entity.
        api.RegisterBlockClass("BlockPylonBase", typeof(BlockPylonBase));
        api.RegisterBlockClass("BlockPylonHead", typeof(BlockPylonHead));
        api.RegisterBlockClass("BlockDriveHousing", typeof(BlockDriveHousing));

        // MIGRATION, deliberate: the block entity class name changed from "PylonHead" with the controller.
        // A pre-footing world has its towers' block entities saved under the old name, and ServerChunk.cs:531
        // logs and DISCARDS a block entity whose class will not instantiate - so every legacy tower loads as
        // inert decoration with no spans, no route state and nothing to walk a line through. That is the
        // whole migration: it fails safe by construction, no upgrader, no half-converted towers. Reusing the
        // old name would instead resurrect those towers four blocks below their own geometry.
        // "TensionWeight" is deliberately NOT registered any more, and the same migration applies to it: a
        // world built on the old scheme has weights saved under that name, ServerChunk discards every one of
        // them on load, and the blocks stay as decoration. Nothing is left holding a reference, because the
        // only thing that ever held one was the position table this deleted.
        api.RegisterBlockEntityClass("PylonBase", typeof(BEPylonBase));
        api.RegisterBlockEntityClass("Bullwheel", typeof(BEBullwheel));
        api.RegisterBlockEntityClass("DriveHousing", typeof(BEDriveHousing));
        api.RegisterEntity("EntityRopewayCabin", typeof(EntityRopewayCabin));

        // Both sides, or EntityAgent.Initialize cannot re-resolve WatchedAttributes["mountedOn"] after a relog
        // and the rider is ejected. Precedent: VSSurvivalMod Core.cs:10.
        api.RegisterMountable("ropewaycabin", EntityRideableSeat.GetMountable);

        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<TowerCandidatesResponse>()
            .RegisterMessageType<TowerLinkRequest>()
            .RegisterMessageType<TowerUnlinkRequest>()
            .RegisterMessageType<TowerRenameRequest>()
            .RegisterMessageType<RiderStopRequest>()
            .RegisterMessageType<TowerCandidate>();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        LinkService = new RopewayLinkService(api, this);

        api.Network.GetChannel(ChannelName)
            .SetMessageHandler<TowerLinkRequest>(LinkService.OnLinkRequest)
            .SetMessageHandler<TowerUnlinkRequest>(LinkService.OnUnlinkRequest)
            .SetMessageHandler<TowerRenameRequest>(LinkService.OnRenameRequest)
            .SetMessageHandler<RiderStopRequest>(LinkService.OnStopRequest);

        // Disconnect while riding is handled by EntityRopewayCabin.DropGhostPassengers on the server tick,
        // which also covers a crashed client and a despawned rider. One mechanism, not two.
        api.Event.ServerRunPhase(EnumServerRunPhase.RunGame, () => VerifyStructureWildcards(api));
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        Dialog = new PylonPickerDialog(api, this);
        GuideDialog = new RopewayGuideDialog(api);

        api.Network.GetChannel(ChannelName)
            .SetMessageHandler<TowerCandidatesResponse>(Dialog.OnCandidates);

        // R is unbound in vanilla (nothing in ClientMain or any of the three content mods registers it), so
        // this costs no existing binding, and the player can move it in Settings > Controls like any other.
        api.Input.RegisterHotKey(StopHotkey, Lang.Get("ropeway:hotkey-stop"), GlKeys.R, HotkeyType.CharacterControls);
        api.Input.SetHotKeyHandler(StopHotkey, _ =>
        {
            // Not riding: hand the key straight back rather than swallowing it, so it stays free for
            // whatever else the player has bound to it.
            if (api.World?.Player?.Entity?.MountedOn?.Entity is not EntityRopewayCabin cabin) return false;

            api.Network.GetChannel(ChannelName).SendPacket(new RiderStopRequest { CabinEntityId = cabin.EntityId });
            return true;
        });

        // Constructed here, not in Start: the flags it holds must be empty for every session. Camera mode is
        // never persisted (PlayerCamera is rebuilt per session, Camera's ctor sets FirstPerson), so a relog
        // while the outside view is on leaks nothing - the fresh instance simply has nothing to restore.
        RideCamera = new RopewayRideCamera(api);
    }

    /// <summary>
    /// The already-cached line through <paramref name="anyTower"/>, or null. Does not build - invalidation
    /// is the only caller and building a line in order to drop it would repopulate the cache during
    /// teardown. Everything that wants a line calls <see cref="RopewayLine.GetOrBuild"/>.
    /// </summary>
    private RopewayLine GetCachedLine(BlockPos anyTower)
    {
        if (anyTower == null) return null;
        return LineCache.TryGetValue(anyTower, out var line) ? line : null;
    }

    /// <summary>Drops the cached line for every tower that was a member of it.</summary>
    public void InvalidateLine(BlockPos anyTower)
    {
        var line = GetCachedLine(anyTower);
        if (line?.Towers == null)
        {
            if (anyTower != null) LineCache.Remove(anyTower);
            return;
        }

        foreach (var tower in line.Towers) LineCache.Remove(tower);
    }

    /// <summary>
    /// MultiblockStructure.HighlightIncompleteParts indexes SearchBlocks(wildcard)[0] on every missing cell,
    /// so an unresolvable wildcard is a client-side IndexOutOfRangeException. Turn that into a startup log line.
    /// </summary>
    private void VerifyStructureWildcards(ICoreServerAPI api)
    {
        foreach (var block in api.World.Blocks)
        {
            if (block?.Code == null || block.Code.Domain != "ropeway") continue;

            var structure = block.Attributes?["multiblockStructure"]?.AsObject<MultiblockStructure>();
            if (structure?.BlockNumbers == null) continue;

            foreach (var wildcard in structure.BlockNumbers.Keys)
            {
                if (api.World.SearchBlocks(wildcard).Length > 0) continue;

                api.Logger.Error(
                    "Ropeway: multiblockStructure on {0} lists '{1}', which matches no loaded block. " +
                    "Build guidance cannot highlight those cells and the tower can never be completed.",
                    block.Code, wildcard);
            }
        }
    }

    public override void Dispose()
    {
        Dialog?.Dispose();
        GuideDialog?.Dispose();
        base.Dispose();
    }
}
