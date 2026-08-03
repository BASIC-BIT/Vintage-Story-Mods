using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace Ropeway;

/// <summary>
/// Server authority for every mutation in the mod: candidate scans, linking, unlinking, rope accounting
/// and the cabin lifecycle. Client requests are hints, never authority.
/// </summary>
public sealed class RopewayLinkService
{
    public const string HaulRopeCode = "haulrope";
    public const string CabinEntityCode = "cabin";

    private readonly ICoreServerAPI sapi;
    private readonly RopewayModSystem modSystem;

    public RopewayLinkService(ICoreServerAPI sapi, RopewayModSystem modSystem)
    {
        this.sapi = sapi;
        this.modSystem = modSystem;
    }

    private Item HaulRope => sapi.World.GetItem(new AssetLocation("ropeway", HaulRopeCode));

    // -------------------------------------------------------------- network entry points

    public void OnLinkRequest(IServerPlayer fromPlayer, TowerLinkRequest packet)
    {
        // The client list is a hint and can be stale or forged - TryLink re-runs every rule.
        if (packet?.FromTower != null && packet.ToTower != null) TryLink(fromPlayer, packet.FromTower, packet.ToTower);
    }

    public void OnUnlinkRequest(IServerPlayer fromPlayer, TowerUnlinkRequest packet)
    {
        if (packet?.FromTower != null && packet.ToTower != null) TryUnlink(fromPlayer, packet.FromTower, packet.ToTower);
    }

    public void OnRenameRequest(IServerPlayer fromPlayer, TowerRenameRequest packet)
    {
        var be = TowerAt(packet?.Tower);
        if (be == null) return;

        if (!MayEdit(fromPlayer, packet.Tower)) return;

        // BEPylonBase.SanitiseName is the trust boundary; Rename runs it and syncs only on a real change.
        // Re-sending the list refreshes the open picker with whatever the sanitiser actually kept - straight
        // through SendCandidateList, because the guards in SendCandidates would toast at a rename.
        if (be.Rename(packet.Name)) SendCandidateList(fromPlayer, be, packet.Tower);
    }

    /// <summary>
    /// The rider's stop key. The gate is the mount and nothing else: you can only ever aim the cabin you are
    /// sitting in, which makes reach and claim checks meaningless here - boarding already passed them.
    /// </summary>
    public void OnStopRequest(IServerPlayer fromPlayer, RiderStopRequest packet)
    {
        if (packet == null || fromPlayer?.Entity?.MountedOn?.Entity is not EntityRopewayCabin cabin) return;
        if (cabin.EntityId != packet.CabinEntityId) return;

        var line = RopewayLine.GetOrBuild(modSystem, cabin.LineKey);
        var outcome = cabin.RequestStop(line, fromPlayer.PlayerUID, out var tower);
        if (outcome != CabinCall.Called)
        {
            // One message for every refusal there is - a truncated line, a chain that just re-canonicalised,
            // a two-tower line the cabin is standing on the far end of. Silence is what made the controls
            // look absent in the first place, so an unhelpful answer still beats none. The two power
            // refusals get their own, because they are the only ones the player can do something about.
            SendPowerRefusal(fromPlayer, outcome, "ropeway-no-stop", "ropeway:err-no-stop");
            return;
        }

        var anchor = SpanMath.AnchorOf(tower);
        fromPlayer.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            Lang.Get("ropeway:stop-requested", DisplayName(TowerAt(tower), anchor.X - cabin.Pos.X, anchor.Z - cabin.Pos.Z)),
            EnumChatType.Notification);
    }

    // -------------------------------------------------------------- interaction

    /// <summary>
    /// Empty hand (or anything that is not the cabin item): call the cabin to this tower, otherwise open the
    /// picker. Every complete tower on the line is a station, not only the two ends - on a four-tower line
    /// the end-only rule made the two middle towers scenery and most of the rope pointless.
    /// </summary>
    public void OnTowerInteract(IServerPlayer player, BlockPos pos)
    {
        var be = TowerAt(pos);
        if (be == null || !be.StructureComplete) return;

        var line = RopewayLine.GetOrBuild(modSystem, pos);
        var cabin = FindCabin(line);

        if (cabin != null && line != null)
        {
            if (cabin.HasPassenger)
            {
                player.SendIngameError("ropeway-cabin-busy", Lang.Get("ropeway:err-cabin-busy"));
                return;
            }

            var outcome = cabin.CallTo(line, pos, player.PlayerUID);
            switch (outcome)
            {
                case CabinCall.Called:
                    // Bearing from the cabin to the tower, for an unnamed one: it says which way the thing
                    // the player is waiting for is coming from.
                    var anchor = SpanMath.AnchorOf(pos);
                    player.SendMessage(
                        GlobalConstants.InfoLogChatGroup,
                        Lang.Get("ropeway:cabin-called", DisplayName(be, anchor.X - cabin.Pos.X, anchor.Z - cabin.Pos.Z)),
                        EnumChatType.Notification);
                    return;

                case CabinCall.AlreadyHere:
                    player.SendIngameError("ropeway-cabin-here", Lang.Get("ropeway:err-cabin-here"));
                    return;

                case CabinCall.NoStore:
                case CabinCall.StoreUnreachable:
                case CabinCall.NoPower:
                case CabinCall.TooDear:
                    SendPowerRefusal(player, outcome, null, null);
                    return;
            }

            // Unreachable. On a truncated line that is why, and saying so beats opening a picker that is
            // about to refuse the same click for the same reason.
            if (line.Truncated)
            {
                player.SendIngameError("ropeway-line-truncated", Lang.Get("ropeway:err-line-truncated"));
                return;
            }
        }

        SendCandidates(player, pos);
    }

    /// <summary>
    /// The one place a power refusal turns into words, shared by the ground call and the rider's stop key
    /// so the two cannot drift. "No tension weight" and "not wound enough yet" are different problems with
    /// different fixes - one is a block you have not built, the other is a wait - and reporting either as
    /// the generic refusal is what makes a required-power design feel broken rather than merely unpowered.
    /// <paramref name="fallbackCode"/> null means the caller has its own handling for everything else.
    /// </summary>
    private static void SendPowerRefusal(IServerPlayer player, CabinCall outcome, string fallbackCode, string fallbackLangKey)
    {
        var refusal = EntityRopewayCabin.Refusal(outcome);

        if (refusal.Code != null) player.SendIngameError(refusal.Code, Lang.Get(refusal.Ground));
        else if (fallbackCode != null) player.SendIngameError(fallbackCode, Lang.Get(fallbackLangKey));
    }

    public void SendCandidates(IServerPlayer player, BlockPos from)
    {
        var be = TowerAt(from);
        if (be == null || !be.StructureComplete) return;

        // Same reason TryLink refuses below: with part of the line unloaded, every row would fail on click,
        // and an empty picker reading "No linkable towers in range" would blame the surroundings instead.
        // A full tower is no longer refused here - its existing spans are exactly what the picker now shows,
        // and unlinking one is the only way out of that state short of breaking the block.
        if (RopewayLine.GetOrBuild(modSystem, from)?.Truncated == true)
        {
            player.SendIngameError("ropeway-line-truncated", Lang.Get("ropeway:err-line-truncated-link"));
            return;
        }

        SendCandidateList(player, be, from);
    }

    private void SendCandidateList(IServerPlayer player, BEPylonBase be, BlockPos from)
    {
        var response = new TowerCandidatesResponse
        {
            FromTower = from.Copy(),
            FromName = be.TowerName,
            RopeInInventory = CountRope(player),
            Candidates = new List<TowerCandidate>()
        };

        // Existing spans first: they are the answer to "what am I already connected to", and the player
        // reads down from the top.
        var anchor = SpanMath.AnchorOf(from);
        foreach (var peer in be.Spans)
        {
            var span = anchor.DistanceTo(SpanMath.AnchorOf(peer));
            response.Candidates.Add(new TowerCandidate
            {
                Pos = peer.Copy(),
                Distance = (int)Math.Round(span),
                RopeCost = SpanMath.RopeRefund(span, be.RopePerBlock),
                Name = TowerAt(peer)?.TowerName,
                Linked = true
            });
        }

        foreach (var candidate in ScanCandidates(be, from))
        {
            response.Candidates.Add(candidate);
        }

        sapi.Network.GetChannel(RopewayModSystem.ChannelName).SendPacket(response, player);
    }

    /// <summary>
    /// Every row the picker shows must succeed on click, so this applies the same rules TryLink does.
    /// Cheap filters first, then the clearance sweep on at most maxCandidates towers.
    /// </summary>
    private List<TowerCandidate> ScanCandidates(BEPylonBase be, BlockPos from)
    {
        var result = new List<TowerCandidate>();

        // TryLink refuses on a full tower, so offering one a link row would break the "every row succeeds"
        // contract the moment the picker started opening on full towers.
        if (be.Spans.Count >= BEPylonBase.MaxSpansPerTower) return result;

        var anchorFrom = SpanMath.AnchorOf(from);
        var lineFrom = RopewayLine.GetOrBuild(modSystem, from);
        var lengthFrom = lineFrom?.TotalLength ?? 0;

        // TryLink is the authority and re-checks this against the merged line's own weight; here it only
        // has to keep a row off the picker that the click would refuse.
        var capacity = StoreCapacity(lineFrom) ?? RopewayPower.DefaultCapacity;

        var near = new List<KeyValuePair<double, BlockPos>>();
        foreach (var entry in modSystem.LoadedTowers)
        {
            var pos = entry.Key;
            var other = entry.Value;
            if (pos.Equals(from) || pos.dimension != from.dimension) continue;
            if (other == null || !other.StructureComplete) continue;
            if (other.Spans.Count >= BEPylonBase.MaxSpansPerTower) continue;
            if (be.HasSpanTo(pos)) continue;
            if (lineFrom != null && Contains(lineFrom, pos)) continue;

            double span = anchorFrom.DistanceTo(SpanMath.AnchorOf(pos));
            if (span > be.MaxSpan) continue;

            near.Add(new KeyValuePair<double, BlockPos>(span, pos));
        }

        near.Sort((a, b) => a.Key.CompareTo(b.Key));

        var limit = Math.Min(near.Count, be.MaxCandidates);
        for (var i = 0; i < limit; i++)
        {
            var span = near[i].Key;
            var pos = near[i].Value;

            var lineTo = RopewayLine.GetOrBuild(modSystem, pos);
            if (lineTo?.Truncated == true) continue;
            if (lengthFrom + (lineTo?.TotalLength ?? 0) + span > be.MaxLineLength) continue;
            if (EntityRopewayCabin.WorstTripCost(RopewayLine.Preview(lineFrom, from, lineTo, pos)) > capacity) continue;
            if (!SpanMath.IsSpanClear(sapi.World, anchorFrom, SpanMath.AnchorOf(pos), out _)) continue;

            result.Add(new TowerCandidate
            {
                Pos = pos.Copy(),
                Distance = (int)Math.Round(span),
                RopeCost = SpanMath.RopeCost(span, be.RopePerBlock),
                Name = TowerAt(pos)?.TowerName
            });
        }

        return result;
    }

    // -------------------------------------------------------------- linking

    /// <summary>Reads first, mutates last. A single-pass "deduct as you go" eats rope on a failed build.</summary>
    public bool TryLink(IServerPlayer player, BlockPos from, BlockPos to)
    {
        var beFrom = TowerAt(from);
        var beTo = TowerAt(to);

        if (beFrom == null || beTo == null)
        {
            player.SendIngameError("ropeway-tower-gone", Lang.Get("ropeway:err-tower-gone"));
            return false;
        }

        if (from.Equals(to))
        {
            player.SendIngameError("ropeway-self-link", Lang.Get("ropeway:err-self-link"));
            return false;
        }

        if (!beFrom.StructureComplete || !beTo.StructureComplete)
        {
            player.SendIngameError("ropeway-tower-incomplete", Lang.Get("ropeway:err-tower-incomplete"));
            return false;
        }

        if (!MayEdit(player, from, to)) return false;

        if (beFrom.HasSpanTo(to) || beTo.HasSpanTo(from))
        {
            player.SendIngameError("ropeway-already-linked", Lang.Get("ropeway:err-already-linked"));
            return false;
        }

        if (beFrom.Spans.Count >= BEPylonBase.MaxSpansPerTower || beTo.Spans.Count >= BEPylonBase.MaxSpansPerTower)
        {
            player.SendIngameError("ropeway-tower-full", Lang.Get("ropeway:err-tower-full"));
            return false;
        }

        var anchorFrom = SpanMath.AnchorOf(from);
        var anchorTo = SpanMath.AnchorOf(to);
        double span = anchorFrom.DistanceTo(anchorTo);

        if (from.dimension != to.dimension || span > beFrom.MaxSpan)
        {
            player.SendIngameError("ropeway-span-too-long", Lang.Get("ropeway:err-span-too-long", (int)beFrom.MaxSpan));
            return false;
        }

        var lineFrom = RopewayLine.GetOrBuild(modSystem, from);
        var lineTo = RopewayLine.GetOrBuild(modSystem, to);

        // Contains cannot see towers past an unloaded end, so on a truncated line "is the target already on
        // this line?" simply has no answer - and answering it wrongly builds a cycle WalkChain is written on
        // the assumption cannot exist. TotalLength under-reports for the same reason, so the maxLineLength
        // ceiling is unenforceable too. If it cannot be proven, it is not allowed.
        if (lineFrom?.Truncated == true || lineTo?.Truncated == true)
        {
            player.SendIngameError("ropeway-line-truncated", Lang.Get("ropeway:err-line-truncated-link"));
            return false;
        }

        if (lineFrom != null && Contains(lineFrom, to))
        {
            player.SendIngameError("ropeway-already-linked", Lang.Get("ropeway:err-already-linked"));
            return false;
        }

        if ((lineFrom?.TotalLength ?? 0) + (lineTo?.TotalLength ?? 0) + span > beFrom.MaxLineLength)
        {
            player.SendIngameError("ropeway-line-too-long", Lang.Get("ropeway:err-line-too-long", (int)beFrom.MaxLineLength));
            return false;
        }

        // The store's capacity is a flat number while a quote is length + 2 x climb, so a line can sit well
        // inside maxLineLength and still carry a leg no FULL weight could ever pay for - permanently
        // unrunnable in that direction, with nothing at runtime able to fix it and the refusal telling the
        // player to wait for wind that will never be enough. Link time is the last moment they can still
        // act, and this sits above the mutation line, so it costs them nothing but the click.
        var capacity = StoreCapacity(lineFrom) ?? StoreCapacity(lineTo) ?? RopewayPower.DefaultCapacity;
        var worst = EntityRopewayCabin.WorstTripCost(RopewayLine.Preview(lineFrom, from, lineTo, to));
        if (worst > capacity)
        {
            player.SendIngameError(
                "ropeway-line-too-steep",
                Lang.Get("ropeway:err-line-too-steep", (int)Math.Ceiling(worst), (int)Math.Round(capacity)));
            return false;
        }

        if (!SpanMath.IsSpanClear(sapi.World, anchorFrom, anchorTo, out _))
        {
            player.SendIngameError("ropeway-span-blocked", Lang.Get("ropeway:err-span-blocked"));
            return false;
        }

        // Same rule as TryUnlink, and for the same reason: the merge below re-bases the cabin, which parks
        // it at an end of the new chain - an arbitrary teleport of whoever is sitting in it. Growing a line
        // is not urgent enough to move a rider for; wait until they get out.
        if (IsLineOccupied(from) || IsLineOccupied(to))
        {
            player.SendIngameError("ropeway-line-in-use", Lang.Get("ropeway:err-line-in-use"));
            return false;
        }

        var cost = SpanMath.RopeCost(span, beFrom.RopePerBlock);
        var have = CountRope(player);
        if (player.WorldData.CurrentGameMode != EnumGameMode.Creative && have < cost)
        {
            player.SendIngameError("ropeway-not-enough-rope", Lang.Get("ropeway:err-not-enough-rope", cost, have));
            return false;
        }

        // ---- nothing above this line mutates ----

        if (!TryConsumeRope(player, cost))
        {
            player.SendIngameError("ropeway-not-enough-rope", Lang.Get("ropeway:err-not-enough-rope", cost, CountRope(player)));
            return false;
        }

        beFrom.AddSpan(to);
        beTo.AddSpan(from);

        modSystem.InvalidateLine(from);
        modSystem.InvalidateLine(to);
        modSystem.LineCache.Remove(from);
        modSystem.LineCache.Remove(to);

        // The chain just grew or two chains merged, which moves what Travelled points at.
        var merged = RopewayLine.GetOrBuild(modSystem, from);
        if (merged != null) FindCabin(merged)?.RebaseTo(merged);

        // The only other confirmation is walking up to a pylon head and reading the block-info panel, so
        // without this a link is indistinguishable from a bug that ate the rope.
        player.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            Lang.Get("ropeway:span-linked", DisplayName(beTo, to.X - from.X, to.Z - from.Z), (int)Math.Round(span), cost),
            EnumChatType.Notification);

        return true;
    }

    /// <summary>
    /// Drops one span between two named towers, the picker's counterpart to <see cref="TryLink"/>. Same
    /// refund and same cabin re-base as <see cref="UnlinkAll"/>, but for a single peer: reading UnlinkAll's
    /// loop would fire its "no survivor, drop the cabin" branch on the first peer of a two-span tower.
    /// </summary>
    public bool TryUnlink(IServerPlayer player, BlockPos from, BlockPos to)
    {
        var beFrom = TowerAt(from);
        var beTo = TowerAt(to);

        if (beFrom == null || beTo == null)
        {
            player.SendIngameError("ropeway-tower-gone", Lang.Get("ropeway:err-tower-gone"));
            return false;
        }

        if (!beFrom.HasSpanTo(to) && !beTo.HasSpanTo(from))
        {
            player.SendIngameError("ropeway-not-linked", Lang.Get("ropeway:err-not-linked"));
            return false;
        }

        if (!MayEdit(player, from, to)) return false;

        // Same rule as breaking a pylon head: cutting the line under a seated rider moves them an arbitrary
        // distance, which is a fall-damage vector and a free long-range teleport.
        if (IsLineOccupied(from))
        {
            player.SendIngameError("ropeway-line-in-use", Lang.Get("ropeway:err-line-in-use"));
            return false;
        }

        // ---- nothing above this line mutates ----

        var span = SpanMath.AnchorOf(from).DistanceTo(SpanMath.AnchorOf(to));
        var name = DisplayName(beTo, to.X - from.X, to.Z - from.Z);
        var cabin = FindCabin(RopewayLine.GetOrBuild(modSystem, from));

        beFrom.RemoveSpan(to);
        beTo.RemoveSpan(from);

        // Both towers were on one line, so invalidating from either end drops the whole old chain.
        modSystem.InvalidateLine(from);
        modSystem.LineCache.Remove(from);
        modSystem.InvalidateLine(to);
        modSystem.LineCache.Remove(to);

        // The cabin's line just split. IsLineOccupied above means nobody is aboard, so a re-base is free.
        if (cabin != null)
        {
            var survivor = PickSurvivor(
                new[] { RopewayLine.GetOrBuild(modSystem, from), RopewayLine.GetOrBuild(modSystem, to) },
                cabin.LineKey);

            if (survivor != null) cabin.RebaseTo(survivor);
            else cabin.DropAndDie(player);
        }

        var refund = SpanMath.RopeRefund(span, beFrom.RopePerBlock);
        if (player.WorldData.CurrentGameMode != EnumGameMode.Creative) GiveRope(player, refund);

        player.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            Lang.Get("ropeway:span-cut", name, refund),
            EnumChatType.Notification);

        // The picker stays open on the tower that was clicked, so refresh what it is showing.
        SendCandidateList(player, beFrom, from);
        return true;
    }

    /// <summary>
    /// What to call a tower in a message. Its player-set name, or the compass bearing to it from wherever
    /// the message is about - never a raw coordinate triple and never an "unnamed" placeholder.
    /// </summary>
    private static string DisplayName(BEPylonBase be, double dx, double dz)
    {
        return be?.TowerName ?? Lang.Get(SpanMath.CompassKey(dx, dz));
    }

    /// <summary>Drops every span on a tower, refunding floor(span) to the breaker and unlinking the peers.</summary>
    public void UnlinkAll(BlockPos pos, IServerPlayer refundTo)
    {
        var be = TowerAt(pos);
        if (be == null || be.Spans.Count == 0) return;

        var cabin = FindCabin(RopewayLine.GetOrBuild(modSystem, pos));
        var peers = be.Spans.ToArray();
        var anchor = SpanMath.AnchorOf(pos);
        var refund = 0;

        // The weight bound to THIS tower is about to lose its anchor, and an orphaned weight is a line with
        // no store at all - every StoreOn returns null and the only recovery was breaking and replacing the
        // block. Re-bind it to a surviving peer: that is the same line minus one tower, so it cannot
        // silently re-home the weight to somebody else's ropeway the way re-deriving by proximity would.
        // Lowest position when a mid-line break leaves two halves, so which half inherits the store is the
        // same after a reload as before it. A tower with no spans at all early-returned above: there is
        // nothing left to bind to, and the block-info panel already says the weight is orphaned. The weight
        // stands within towerRadius of the footing, so its chunk is loaded whenever the tower's is.
        BETensionWeight.StoreAt(modSystem, pos)?.Bind(RopewayLine.Lowest(peers));

        foreach (var peer in peers)
        {
            refund += SpanMath.RopeRefund(anchor.DistanceTo(SpanMath.AnchorOf(peer)), be.RopePerBlock);
            TowerAt(peer)?.RemoveSpan(pos);
            modSystem.InvalidateLine(peer);
            modSystem.LineCache.Remove(peer);
        }

        be.Spans.Clear();

        // No MarkDirty on `be`: both callers are removal paths (OnBlockBroken, OnBlockRemoved), and on the
        // latter ServerWorldMap.RemoveBlockEntity has already detached it from its chunk. The surviving
        // peers get their redraw from RemoveSpan above, which is the half of the cable that is still there.
        modSystem.InvalidateLine(pos);
        modSystem.LineCache.Remove(pos);

        // The cabin's line either shrank or ceased to exist. A survivor re-bases; with no survivor the
        // cabin would hang in mid-air forever, uncollectable, taking its item with it.
        if (cabin != null)
        {
            var survivors = new List<RopewayLine>();
            foreach (var peer in peers) survivors.Add(RopewayLine.GetOrBuild(modSystem, peer));

            // Re-basing parks the cabin at an end tower, which for a mid-line break is a teleport of most of
            // the line. Nobody rides that: unseat first, exactly as DropAndDie does, and whoever was aboard
            // stays where the cabin was rather than being carried to where it is going. (The hand-break path
            // refuses outright while occupied; this is the explosion / SetBlock(0) path.)
            cabin.UnseatAll();

            var survivor = PickSurvivor(survivors, cabin.LineKey);
            if (survivor != null) cabin.RebaseTo(survivor);
            else cabin.DropAndDie(refundTo);
        }

        if (refundTo != null && refundTo.WorldData.CurrentGameMode != EnumGameMode.Creative) GiveRope(refundTo, refund);
    }

    // -------------------------------------------------------------- cabin

    public bool TryPlaceCabin(IServerPlayer player, BlockPos pos, ItemSlot slot)
    {
        var be = TowerAt(pos);
        if (be == null || !be.StructureComplete) return false;

        var line = RopewayLine.GetOrBuild(modSystem, pos);
        if (line == null || !be.IsEndpoint)
        {
            player.SendIngameError("ropeway-not-endpoint", Lang.Get("ropeway:err-not-endpoint"));
            return false;
        }

        if (FindCabin(line) != null)
        {
            player.SendIngameError("ropeway-cabin-exists", Lang.Get("ropeway:err-cabin-exists"));
            return false;
        }

        var type = sapi.World.GetEntityType(new AssetLocation("ropeway", CabinEntityCode));
        if (type == null)
        {
            sapi.Logger.Error("Ropeway: no such entity type ropeway:{0}", CabinEntityCode);
            return false;
        }

        if (sapi.World.ClassRegistry.CreateEntity(type) is not EntityRopewayCabin cabin) return false;

        var atStart = pos.Equals(line.Towers[0]);
        var anchor = atStart ? line.Anchors[0] : line.Anchors[line.Anchors.Length - 1];

        cabin.LineKey = line.Towers[0].Copy();
        cabin.Travelled = atStart ? 0 : line.TotalLength;
        cabin.Outbound = atStart;
        cabin.Pos.SetPosWithDimension(new Vec3d(anchor.X, anchor.Y - cabin.HangDropDefault, anchor.Z));
        cabin.Attributes.SetString("origin", "playerplaced");

        sapi.World.SpawnEntity(cabin);

        if (player.WorldData.CurrentGameMode != EnumGameMode.Creative && slot?.Itemstack != null)
        {
            slot.TakeOut(1);
            slot.MarkDirty();
        }

        return true;
    }

    /// <summary>The cabin belonging to a line, if it is loaded.</summary>
    public EntityRopewayCabin FindCabin(RopewayLine line)
    {
        return EntityRopewayCabin.FindOn(sapi.World, line);
    }

    public bool IsLineOccupied(BlockPos anyTower)
    {
        return FindCabin(RopewayLine.GetOrBuild(modSystem, anyTower))?.HasPassenger == true;
    }

    // -------------------------------------------------------------- rope accounting

    public int CountRope(IServerPlayer player)
    {
        var item = HaulRope;
        if (item == null || player?.Entity == null) return 0;

        var total = 0;
        player.Entity.WalkInventory(slot =>
        {
            if (slot is ItemSlotCreative || slot.Inventory is not InventoryBasePlayer) return true;
            if (slot.Itemstack?.Collectible == item) total += slot.StackSize;
            return true;
        });

        return total;
    }

    /// <summary>Collect, verify the total, only then take out. Never partially drains a short inventory.</summary>
    public bool TryConsumeRope(IServerPlayer player, int quantity)
    {
        if (quantity <= 0) return true;
        if (player.WorldData.CurrentGameMode == EnumGameMode.Creative) return true;

        var item = HaulRope;
        if (item == null) return false;

        var slots = new List<ItemSlot>();
        var sizes = new List<int>();
        player.Entity.WalkInventory(slot =>
        {
            if (slot is ItemSlotCreative || slot.Inventory is not InventoryBasePlayer) return true;
            if (slot.Itemstack?.Collectible == item)
            {
                slots.Add(slot);
                sizes.Add(slot.StackSize);
            }

            return true;
        });

        var plan = SpanMath.PlanConsumption(sizes, quantity);
        if (plan == null) return false;

        for (var i = 0; i < slots.Count; i++)
        {
            if (plan[i] <= 0) continue;
            slots[i].TakeOut(plan[i]);
            // TakeOut on the partial path calls neither OnItemSlotModified nor MarkDirty.
            slots[i].MarkDirty();
        }

        return true;
    }

    /// <summary>Inventory, then the world. Never silently voided.</summary>
    public void GiveRope(IServerPlayer player, int quantity)
    {
        var item = HaulRope;
        if (item == null || quantity <= 0 || player?.Entity == null) return;

        var perStack = Math.Max(1, item.MaxStackSize);
        while (quantity > 0)
        {
            var size = Math.Min(perStack, quantity);
            quantity -= size;

            var stack = new ItemStack(item, size);
            if (!player.InventoryManager.TryGiveItemstack(stack, slotNotifyEffect: true))
            {
                sapi.World.SpawnItemEntity(stack, player.Entity.Pos.XYZ);
            }
        }
    }

    // -------------------------------------------------------------- helpers

    /// <summary>
    /// The trust gate for every tower-mutating packet - link, unlink and rename. All three arrive as
    /// client packets carrying an arbitrary BlockPos, so without a distance term any client can rename a
    /// tower, or cut a span and be paid rope for it, on any loaded tower in the world from anywhere in it.
    /// <paramref name="clicked"/> is the tower the click was on and is the one the player's reach has to
    /// cover; <paramref name="peer"/> is the far end of a span, legitimately up to maxSpan away, so it gets
    /// the claim check and not the distance one. Generous on range - PickingRange is the block reach and
    /// this is measured to a block centre, so a couple of blocks of slack keeps a legitimate click from
    /// being refused over rounding and the player's eye height.
    /// <para>
    /// CentreOf, not AnchorOf: the click landed on the footing at the player's feet, while the anchor is
    /// the sheave four blocks above it. Measuring to the anchor charges every click four blocks of reach
    /// it never used, which on a tower dug into a slope is the difference between working and not.
    /// </para>
    /// </summary>
    private bool MayEdit(IServerPlayer player, BlockPos clicked, BlockPos peer = null)
    {
        if (player?.Entity == null || clicked == null) return false;

        var reach = player.WorldData.PickingRange + 3;
        if (player.Entity.Pos.SquareDistanceTo(SpanMath.CentreOf(clicked)) > reach * reach)
        {
            player.SendIngameError("ropeway-too-far", Lang.Get("ropeway:err-too-far"));
            return false;
        }

        // Same gate as placing or breaking the block: a span and a name are both visible to everyone.
        if (!sapi.World.Claims.TryAccess(player, clicked, EnumBlockAccessFlags.BuildOrBreak) ||
            (peer != null && !sapi.World.Claims.TryAccess(player, peer, EnumBlockAccessFlags.BuildOrBreak)))
        {
            player.SendIngameError("ropeway-no-permission", Lang.Get("ropeway:err-no-permission"));
            return false;
        }

        return true;
    }

    /// <summary>
    /// The capacity of the weight already serving a line, or null when it has none. A line usually has no
    /// weight yet while it is being built, which is why every caller falls back to the blocktype's default:
    /// a link gate that only bound lines that already had a store would gate nothing at all.
    /// </summary>
    private double? StoreCapacity(RopewayLine line)
    {
        return BETensionWeight.StoreOn(modSystem, line)?.Capacity;
    }

    private BEPylonBase TowerAt(BlockPos pos)
    {
        if (pos == null) return null;
        if (modSystem.LoadedTowers.TryGetValue(pos, out var be) && be != null) return be;
        return sapi.World.BlockAccessor.GetBlockEntity(pos) as BEPylonBase;
    }

    /// <summary>
    /// Which half of a broken line the cabin is actually on. Breaking a mid-line tower splits the line in
    /// two, and taking whichever half resolves first sends the cabin to the far end of the wrong one. The
    /// cabin's <see cref="EntityRopewayCabin.LineKey"/> is a tower it was on, so the half that still contains
    /// it is its half. Falls back to the first surviving half for the case that key was the broken tower
    /// itself - only reachable when it was an end tower, where there is exactly one half anyway. Pure.
    /// </summary>
    public static RopewayLine PickSurvivor(IReadOnlyList<RopewayLine> survivors, BlockPos lineKey)
    {
        RopewayLine first = null;
        if (survivors == null) return null;

        for (var i = 0; i < survivors.Count; i++)
        {
            var line = survivors[i];
            if (line == null) continue;
            if (Contains(line, lineKey)) return line;
            first ??= line;
        }

        return first;
    }

    private static bool Contains(RopewayLine line, BlockPos pos)
    {
        return line?.IndexOf(pos) >= 0;
    }
}
