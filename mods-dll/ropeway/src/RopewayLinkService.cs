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

    // -------------------------------------------------------------- interaction

    /// <summary>Empty hand (or anything that is not the cabin item): call the cabin home, otherwise open the picker.</summary>
    public void OnTowerInteract(IServerPlayer player, BlockPos pos)
    {
        var be = TowerAt(pos);
        if (be == null || !be.StructureComplete) return;

        var line = RopewayLine.GetOrBuild(modSystem, pos);
        var cabin = FindCabin(line);

        if (cabin != null && be.IsEndpoint && line != null)
        {
            if (cabin.HasPassenger)
            {
                player.SendIngameError("ropeway-cabin-busy", Lang.Get("ropeway:err-cabin-busy"));
                return;
            }

            if (cabin.CallTo(line, pos)) return;

            // Otherwise the empty-hand right-click does nothing at all: no cabin, no picker, no error.
            if (line.Truncated)
            {
                player.SendIngameError("ropeway-line-truncated", Lang.Get("ropeway:err-line-truncated"));
                return;
            }
        }

        SendCandidates(player, pos);
    }

    public void SendCandidates(IServerPlayer player, BlockPos from)
    {
        var be = TowerAt(from);
        if (be == null || !be.StructureComplete) return;

        // Otherwise a full tower opens an empty picker reading "No linkable towers in range", which is a
        // completely different situation from the one the player is actually in.
        if (be.Spans.Count >= BEPylonHead.MaxSpansPerTower)
        {
            player.SendIngameError("ropeway-tower-full", Lang.Get("ropeway:err-tower-full"));
            return;
        }

        // Same reason TryLink refuses below: with part of the line unloaded, every row would fail on click,
        // and an empty picker reading "No linkable towers in range" would blame the surroundings instead.
        if (RopewayLine.GetOrBuild(modSystem, from)?.Truncated == true)
        {
            player.SendIngameError("ropeway-line-truncated", Lang.Get("ropeway:err-line-truncated-link"));
            return;
        }

        var response = new TowerCandidatesResponse
        {
            FromTower = from.Copy(),
            RopeInInventory = CountRope(player),
            Candidates = new List<TowerCandidate>()
        };

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
    private List<TowerCandidate> ScanCandidates(BEPylonHead be, BlockPos from)
    {
        var result = new List<TowerCandidate>();
        var anchorFrom = SpanMath.AnchorOf(from);
        var lineFrom = RopewayLine.GetOrBuild(modSystem, from);
        var lengthFrom = lineFrom?.TotalLength ?? 0;

        var near = new List<KeyValuePair<double, BlockPos>>();
        foreach (var entry in modSystem.LoadedTowers)
        {
            var pos = entry.Key;
            var other = entry.Value;
            if (pos.Equals(from) || pos.dimension != from.dimension) continue;
            if (other == null || !other.StructureComplete) continue;
            if (other.Spans.Count >= BEPylonHead.MaxSpansPerTower) continue;
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
            if (!SpanMath.IsSpanClear(sapi.World, anchorFrom, SpanMath.AnchorOf(pos), out _)) continue;

            result.Add(new TowerCandidate
            {
                Pos = pos.Copy(),
                Distance = (int)Math.Round(span),
                RopeCost = SpanMath.RopeCost(span, be.RopePerBlock)
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

        if (!sapi.World.Claims.TryAccess(player, from, EnumBlockAccessFlags.BuildOrBreak) ||
            !sapi.World.Claims.TryAccess(player, to, EnumBlockAccessFlags.BuildOrBreak))
        {
            player.SendIngameError("ropeway-no-permission", Lang.Get("ropeway:err-no-permission"));
            return false;
        }

        if (beFrom.HasSpanTo(to) || beTo.HasSpanTo(from))
        {
            player.SendIngameError("ropeway-already-linked", Lang.Get("ropeway:err-already-linked"));
            return false;
        }

        if (beFrom.Spans.Count >= BEPylonHead.MaxSpansPerTower || beTo.Spans.Count >= BEPylonHead.MaxSpansPerTower)
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

        if (!SpanMath.IsSpanClear(sapi.World, anchorFrom, anchorTo, out _))
        {
            player.SendIngameError("ropeway-span-blocked", Lang.Get("ropeway:err-span-blocked"));
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
            Lang.Get("ropeway:span-linked", (int)Math.Round(span), cost),
            EnumChatType.Notification);

        return true;
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
    // ponytail: O(loaded entities) scan, only ever on a click or a block break. Index by line if a profile ever shows it.
    public EntityRopewayCabin FindCabin(RopewayLine line)
    {
        if (line == null) return null;

        foreach (var entity in sapi.World.LoadedEntities.Values)
        {
            if (entity is EntityRopewayCabin cabin && cabin.LineKey != null && Contains(line, cabin.LineKey)) return cabin;
        }

        return null;
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

    private BEPylonHead TowerAt(BlockPos pos)
    {
        if (pos == null) return null;
        if (modSystem.LoadedTowers.TryGetValue(pos, out var be) && be != null) return be;
        return sapi.World.BlockAccessor.GetBlockEntity(pos) as BEPylonHead;
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
        if (line?.Towers == null || pos == null) return false;
        for (var i = 0; i < line.Towers.Length; i++)
        {
            if (pos.Equals(line.Towers[i])) return true;
        }

        return false;
    }
}
