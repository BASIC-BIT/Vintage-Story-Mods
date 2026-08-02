using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ropeway;

public sealed class RopewayModSystem : ModSystem
{
    public const string ChannelName = "ropeway";

    /// <summary>
    /// Every loaded tower, keyed by its FOOTING position - the one canonical position, the same one
    /// <see cref="LineCache"/>, <see cref="RopewayLine.Towers"/>, every persisted span and the cabin's
    /// LineKey use. BEPylonBase.Initialize adds, OnBlockUnloaded removes.
    /// </summary>
    public readonly Dictionary<BlockPos, BEPylonBase> LoadedTowers = new();

    /// <summary>Derived line geometry, keyed by every member tower. Never persisted; InvalidateLine drops it.</summary>
    public readonly Dictionary<BlockPos, RopewayLine> LineCache = new();

    public RopewayLinkService LinkService { get; private set; }

    public PylonPickerDialog Dialog { get; private set; }

    public RopewayGuideDialog GuideDialog { get; private set; }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        api.RegisterBlockClass("BlockPylonBase", typeof(BlockPylonBase));
        api.RegisterBlockClass("BlockPylonHead", typeof(BlockPylonHead));

        // MIGRATION, deliberate: the block entity class name changed from "PylonHead" with the controller.
        // A pre-footing world has its towers' block entities saved under the old name, and ServerChunk.cs:531
        // logs and DISCARDS a block entity whose class will not instantiate - so every legacy tower loads as
        // inert decoration with no spans, no route state and nothing to walk a line through. That is the
        // whole migration: it fails safe by construction, no upgrader, no half-converted towers. Reusing the
        // old name would instead resurrect those towers four blocks below their own geometry.
        api.RegisterBlockEntityClass("PylonBase", typeof(BEPylonBase));
        api.RegisterEntity("EntityRopewayCabin", typeof(EntityRopewayCabin));

        // Both sides, or EntityAgent.Initialize cannot re-resolve WatchedAttributes["mountedOn"] after a relog
        // and the rider is ejected. Precedent: VSSurvivalMod Core.cs:10.
        api.RegisterMountable("ropewaycabin", EntityRideableSeat.GetMountable);

        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<TowerCandidatesResponse>()
            .RegisterMessageType<TowerLinkRequest>()
            .RegisterMessageType<TowerUnlinkRequest>()
            .RegisterMessageType<TowerRenameRequest>()
            .RegisterMessageType<TowerCandidate>();
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        LinkService = new RopewayLinkService(api, this);

        api.Network.GetChannel(ChannelName)
            .SetMessageHandler<TowerLinkRequest>(LinkService.OnLinkRequest)
            .SetMessageHandler<TowerUnlinkRequest>(LinkService.OnUnlinkRequest)
            .SetMessageHandler<TowerRenameRequest>(LinkService.OnRenameRequest);

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
