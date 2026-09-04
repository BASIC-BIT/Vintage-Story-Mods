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

        // Split out of the catch-all below because it is the one refusal with a cause the rider can do
        // something about, and the one they will otherwise blame on the key: a parked cabin on a line where
        // nothing is turning. A rider whose cabin is merely stalled mid-span never reaches this.
        if (outcome == CabinCall.NoDrive)
        {
            fromPlayer.SendIngameError("ropeway-no-drive", Lang.Get("ropeway:err-no-drive"));
            return;
        }

        if (outcome != CabinCall.Called)
        {
            // One message for every refusal there is - a truncated line, a chain that just re-canonicalised,
            // a two-tower line the cabin is standing on the far end of. Silence is what made the controls
            // look absent in the first place, so an unhelpful answer still beats none.
            fromPlayer.SendIngameError("ropeway-no-stop", Lang.Get("ropeway:err-no-stop"));
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

                case CabinCall.NoDrive:
                    // The call is refused rather than banked, so this message is the whole of what the player
                    // gets - it has to name the cause, because "nothing happened" is indistinguishable from a
                    // broken tower. The footing's own panel says the same thing in more words.
                    player.SendIngameError("ropeway-no-drive", Lang.Get("ropeway:err-no-drive"));
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
        // contract the moment the picker started opening on full towers. A SHAFT footing is full at one span,
        // not two - phase 1 has no intermediate floors and TryLink says so.
        if (be.Spans.Count >= (be.IsShaft ? 1 : BEPylonBase.MaxSpansPerTower)) return result;

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
            if (other.Spans.Count >= BEPylonBase.MaxSpansPerTower) continue;
            if (be.HasSpanTo(pos)) continue;
            if (lineFrom != null && Contains(lineFrom, pos)) continue;

            // The picker's contract is that every row it shows succeeds on click, so it applies TryLink's shaft
            // rules rather than looser ones - a shaft station is offered only the other end of its own shaft,
            // facing the same way, with neither footing already carrying a span, and a ropeway tower is never
            // offered a shaft station at all. See TryLink for what the one-span clause closes.
            if (be.IsShaft != other.IsShaft) continue;
            if (be.IsShaft && other.Spans.Count > 0) continue;
            if (be.IsShaft && !SpanMath.ShaftLinkFits(
                    from, be.IsShaftHead, be.PassageFacing, pos, other.IsShaftHead, other.PassageFacing))
            {
                continue;
            }

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
            var peer = TowerAt(pos);
            if (!SpanMath.IsSpanClear(sapi.World, anchorFrom, SpanMath.AnchorOf(pos), out _, ShaftAxis(be, peer)))
            {
                continue;
            }

            result.Add(new TowerCandidate
            {
                Pos = pos.Copy(),
                Distance = (int)Math.Round(span),
                RopeCost = SpanMath.RopeCost(span, be.RopePerBlock),
                Name = peer?.TowerName
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

        // VERTICALITY IS STRUCTURAL, and this is where it is enforced. Nothing in the mod asks whether a span
        // is vertical; a shaft line is a line whose footings are shaft stations, and the only span two shaft
        // stations may carry is straight up their own column with the sheave on top. That refusal is what
        // makes every "on a shaft..." branch downstream safe to write: the mixed line those branches would be
        // wrong about - a vertical stub bolted onto a hill line, a ropeway leg hung off a hoistway - is not
        // buildable, so there is no per-span geometric fact being hoisted to a whole line anywhere.
        // See SpanMath.ShaftLinkFits for what each clause closes.
        if (beFrom.IsShaft || beTo.IsShaft)
        {
            if (!beFrom.IsShaft || !beTo.IsShaft
                || !SpanMath.ShaftLinkFits(
                    from, beFrom.IsShaftHead, beFrom.PassageFacing, to, beTo.IsShaftHead, beTo.PassageFacing))
            {
                player.SendIngameError("ropeway-shaft-column", Lang.Get("ropeway:err-shaft-column"));
                return false;
            }

            // ONE SPAN PER SHAFT FOOTING, and it lives here rather than in ShaftLinkFits because it is a
            // question about the footing's EXISTING spans, not about this span's geometry. ShaftLinkFits is a
            // per-SPAN predicate and MaxSpansPerTower is 2, so its "one head" clause does not make "one head
            // per line" true: `foot@0 -> head@10` then `head@10 -> foot@5` is a FOLD - Cumulative [0, 10, 15],
            // DirectionAt flipping to (0,-1,0) at t = 10, and ShaftRenderer's counterweight mirror (which
            // takes Anchors[0] and Anchors[^1], now both FEET) drawing the mass at world Y = -0.5. And
            // `foot@0 -> head@10` plus `foot@0 -> head@20` puts TWO sheaves on one line: two ShaftRenderers
            // each drawing the whole rope, two drives pooled, and ShaftFacing taken from whichever head the
            // GetOrBuild walk reaches last. Phase 1 explicitly has no intermediate floors, so the honest rule
            // is the one that exactly implements that. Whatever lands intermediate floors takes this clause
            // out and owns the fold and the second sheave instead.
            if (beFrom.Spans.Count > 0 || beTo.Spans.Count > 0)
            {
                player.SendIngameError("ropeway-shaft-one-span", Lang.Get("ropeway:err-shaft-one-span"));
                return false;
            }
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

        // No steepness gate. A climb is load, not a wall: a line too steep for the drive you have runs
        // slowly, or not until you build a bigger mill, and both of those are answers the player can act on
        // from inside the game. Refusing the link was only ever needed because a flat store could not pay
        // for a steep trip at any speed.
        if (!SpanMath.IsSpanClear(sapi.World, anchorFrom, anchorTo, out _, ShaftAxis(beFrom, beTo)))
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

        // Either end of the new span may have just become a corner, so both are asked.
        WarnOnCorner(player, beFrom, from, to);
        WarnOnCorner(player, beTo, to, from);

        // NOT on a shaft, and this is the one guard the pitch warning needs. PitchTan is +infinity for a
        // vertical span - deliberately, and documented there - so WarnOnPitch would tell a player that their
        // brand-new lift climbs at 90 degrees against a ceiling of 11, on the very machine that was given no
        // crossarm precisely so it would not eat one. The warning names a defect a shaft cannot have.
        if (!beFrom.IsShaft) WarnOnPitch(player, anchorFrom, anchorTo);

        return true;
    }

    /// <summary>
    /// The corridor frame for a span between two footings: the SHAFT HEAD's own passage facing, or null on
    /// every ropeway span. At link time there is no line yet, so this cannot come from "the line's upper
    /// tower" - it comes from whichever of the two footings wears the sheave, which is the same tower
    /// <c>RopewayLine.ShaftFacing</c> reads once a line exists.
    /// </summary>
    private static BlockFacing ShaftAxis(BEPylonBase a, BEPylonBase b)
    {
        if (a is { IsShaftHead: true }) return a.PassageFacing;
        return b is { IsShaftHead: true } ? b.PassageFacing : null;
    }

    /// <summary>
    /// One chat line when the span just strung is steeper than the tower can pass the cabin through. WARN,
    /// NEVER REFUSE, for the same reason <see cref="WarnOnCorner"/> does: the route is legal, buildable and
    /// works, and a ropeway that refused a climb would be refusing the thing it exists for. What the player
    /// sees is the cabin's roof passing through the crossarm as it leaves the lower tower, and its floor
    /// through the footing plinth as it comes down into one - both inside the four blocks at each end that
    /// <see cref="SpanMath.TrimForTowers"/> hands to the tower's own structure, and neither of them a
    /// collision, because a mounted rider has none.
    /// <para>
    /// It says the number rather than "too steep" because the number is the only actionable part: the cure is
    /// a shallower span, and a player standing between two towers can see whether that is available. There is
    /// no facing to turn and no block to break. See <see cref="SpanMath.PassablePitchTan"/> for why the tower
    /// cannot simply be given the headroom instead.
    /// </para>
    /// </summary>
    private static void WarnOnPitch(IServerPlayer player, Vec3d anchorFrom, Vec3d anchorTo)
    {
        var tan = SpanMath.PitchTan(anchorFrom, anchorTo);
        if (tan <= SpanMath.PassablePitchTan) return;

        player.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            Lang.Get("ropeway:span-too-steep",
                (int)Math.Round(Math.Atan(tan) * (180 / Math.PI)),
                (int)Math.Round(Math.Atan(SpanMath.PassablePitchTan) * (180 / Math.PI))),
            EnumChatType.Notification);
    }

    /// <summary>
    /// One chat line when a tower has just become a corner the cabin cannot pass cleanly. WARN, NEVER REFUSE:
    /// the route is legal, buildable and works, players build ugly corners on purpose, and refusing one for a
    /// cosmetic reason turns a shrug into a wall. It runs AFTER the mutation because a corner only exists
    /// once the second span does.
    /// <para>
    /// The bisector is read out of <see cref="RopewayLine.DirectionAt"/> at the vertex rather than derived
    /// here, so the direction the message talks about is by construction the direction the cabin actually
    /// takes through the tower. A three-tower mini-line is enough for that and needs no world access.
    /// </para>
    /// <para>
    /// TWO messages because there are two different answers. Under 90 degrees of turn a cardinal facing
    /// usually exists that carries the corner, and naming it is something the player can act on in one block
    /// break. At and past 90 the best of four cardinals is still outside
    /// <see cref="SpanMath.CornerTolerance"/>, so there is nothing to turn the footing to and saying "face it
    /// east" would be advice that does not work. This is the closure `KNOWN-ISSUES` and `TURNING-SPEC` §7 both
    /// name as the cheap one: psi is what owns the tolerance, nothing else in the mod ever mentions psi, and
    /// no curve, radius or cabin length changes it.
    /// </para>
    /// </summary>
    private void WarnOnCorner(IServerPlayer player, BEPylonBase tower, BlockPos pos, BlockPos peer)
    {
        if (tower == null || tower.Spans.Count != 2) return;

        var line = RopewayLine.FromTowers(new List<BlockPos> { tower.Spans[0], pos, tower.Spans[1] });
        if (line == null) return;

        var into = line.Anchors[1].Clone().Sub(line.Anchors[0]);
        var outOf = line.Anchors[2].Clone().Sub(line.Anchors[1]);
        var turn = Math.Abs(GameMath.AngleRadDistance(
            (float)Math.Atan2(into.X, into.Z), (float)Math.Atan2(outOf.X, outOf.Z))) * GameMath.RAD2DEG;

        var dir = line.DirectionAt(line.Cumulative[1]);
        var bisector = Math.Atan2(dir.X, dir.Z);
        var tolerance = SpanMath.CornerTolerance(turn);
        if (SpanMath.AxisError(bisector, tower.PassageFacing) <= tolerance) return;

        var best = BlockFacing.HORIZONTALS[0];
        foreach (var facing in BlockFacing.HORIZONTALS)
        {
            if (SpanMath.AxisError(bisector, facing) < SpanMath.AxisError(bisector, best)) best = facing;
        }

        var name = DisplayName(tower, pos.X - peer.X, pos.Z - peer.Z);
        var degrees = (int)Math.Round(turn);
        player.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            SpanMath.AxisError(bisector, best) <= tolerance
                ? Lang.Get("ropeway:corner-facing", name, degrees, Lang.Get("game:facing-" + best.Code))
                : Lang.Get("ropeway:corner-too-sharp", name, degrees),
            EnumChatType.Notification);
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
    internal static string DisplayName(BEPylonBase be, double dx, double dz)
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
            //
            // UnseatAll clears the riders past CanUnmount's nothing-under-us refusal itself - that clearance
            // was here first and moved to the chokepoint, because DropAndDie is the other caller and clearing
            // only this one left the other one covered by an argument about call order. See UnseatAll.
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

        // TRUNCATION IS ASKED FIRST, and it has to be, because the tensioner answer underneath it is not
        // trustworthy while part of the line is dark. HasTensioner needs StructureComplete, which is fifteen
        // GetBlockRaw reads, and BlockAccessorRelaxed hands back AIR for an unloaded chunk - so the far
        // station of a 320-block line reads "not a tensioner" from the near end at the shipped 256-block view
        // distance. Answering that with "build a tension station" tells a player who has one to go and build
        // a second. The honest message already existed for the link path; this is the same sentence at the
        // one other place the truncation can lie.
        if (line.Truncated)
        {
            player.SendIngameError("ropeway-line-truncated", Lang.Get("ropeway:err-line-truncated-link"));
            return false;
        }

        // THE ONE BUILD REQUIREMENT the tensioner carries, and it is enforced here rather than at departure
        // on purpose: a line with no tensioner is an unfinished line, not a line having a bad day, so it is
        // told to the player while they are building - once, at the moment they hang the cabin - instead of
        // becoming a runtime state with a refusal, a toast and a wait attached. Break the station afterwards
        // and the cabin keeps running; the tower panel says the tensioner is missing.
        if (!BEPylonBase.HasTensioner(modSystem, line))
        {
            player.SendIngameError("ropeway-no-tensioner", Lang.Get("ropeway:err-no-tensioner"));
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
