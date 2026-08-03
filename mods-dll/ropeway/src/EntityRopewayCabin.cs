using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace Ropeway;

/// <summary>What a call to a tower would do. Not a bool: "it is already here" and "it cannot get here" are
/// different things to tell the player, and reporting either as failure is what made calling look broken.</summary>
public enum CabinCall
{
    Called,
    AlreadyHere,
    Unreachable
}

/// <summary>
/// The rideable cabin. Server-authoritative motion along the line polyline; the client only
/// interpolates and plays sound. Deliberately has no physics behavior - one would let the controlling
/// client predict and steal authority from the server.
/// </summary>
public class EntityRopewayCabin : Entity, ISeatInstSupplier, IMountableListener
{
    public const double BoardingGraceSeconds = 3.0;
    public const double DefaultSpeed = 2.2;

    /// <summary>
    /// How far the cabin's origin hangs below the rope. Re-derived for the ground-controller tower rather
    /// than carried over, and it lands on the same 2.0 by arithmetic rather than by luck: with the sheave
    /// <see cref="SpanMath.SheaveHeight"/> = 4 cells above the footing, the roof (origin + 1.25) has to
    /// clear the crossarm's underside and the floor (origin - 1.25) has to clear the footing's top, which
    /// leaves 1.75 &lt; hangDrop &lt; 2.75 (plinth top at +0.5, crossarm underside at +4.0 - the crossarm's
    /// foot plate reaches the block boundary so it sits flat on the posts). 2.0 is NOT the midpoint of that
    /// window - 2.25 is - it deliberately sits low in it, giving 0.75 blocks of air under the floor against
    /// 0.25 over the roof, because clipping the crossarm reads far worse than a low floor. The mast tip
    /// lands exactly in the sheave throat either way. Changing this or SheaveHeight alone breaks the fit;
    /// TheCabinFitsThroughTheTower is what catches it.
    /// </summary>
    public const double DefaultHangDrop = 2.0;

    /// <summary>Close enough to a tower to count as standing at it, in metres along the line.</summary>
    public const double ArrivalTolerance = 0.5;

    /// <summary>Nobody called the cabin: it rides to the end of the line the way it always did.</summary>
    public const double NoDestination = -1;

    /// <summary>
    /// The tower at travelled == 0. Every other bit of route state is derived from the blocks.
    /// <para>
    /// WatchedAttributes for the same reason as <see cref="Destination"/>, and it is not optional here:
    /// <c>Entity.ToBytes</c> writes <see cref="Entity.Attributes"/> only <c>if (!forClient)</c> and
    /// <c>FromBytes</c> reads it only <c>if (!isSync)</c>, so a key kept there is ALWAYS null client side -
    /// and <see cref="FindOn"/> matches on it, for a block-info panel that only ever runs client side.
    /// </para>
    /// </summary>
    public BlockPos LineKey
    {
        get => BEPylonBase.ReadPos(WatchedAttributes, "lineKey");
        set
        {
            if (value != null) BEPylonBase.WritePos(WatchedAttributes, "lineKey", value);
        }
    }

    /// <summary>
    /// Metres from the line's canonical <c>Towers[0]</c>, which <see cref="RopewayLine.WalkChain"/> picks by
    /// position and not by which tower the walk started from. <see cref="LineKey"/> is kept equal to it so
    /// the two agree, but it is only a handle into the chain - a chain that re-canonicalises under a stale
    /// LineKey makes this number mean somewhere else entirely, which is what
    /// <see cref="RebaseTo"/> exists to repair. The one seam the cabin has on route state.
    /// </summary>
    public double Travelled;

    /// <summary>
    /// Which way along the line the cabin is currently running. Still a stored flag rather than a function
    /// of <see cref="Destination"/>: an ordinary ride has no destination at all - it runs to the end and
    /// turns around - so deriving direction from one would leave the plain ride with nothing to derive from.
    /// A call sets it once, from where the target is relative to where the cabin stands.
    /// </summary>
    public bool Outbound = true;

    private double speed = DefaultSpeed;
    private double hangDrop = DefaultHangDrop;
    private bool departed;
    private bool boarding;
    private double boardAccum;
    private int lastSegment = -1;

    /// <summary>
    /// Who asked for the current trip, for the one message they are owed if it is abandoned. Persisted
    /// alongside the destination, and kept out of WatchedAttributes because no client has a use for it.
    /// </summary>
    private string calledBy;

    public double HangDropDefault => hangDrop;

    public override double FrustumSphereRadius => base.FrustumSphereRadius * 2.5;

    public override bool ApplyGravity => false;

    public override bool IsCreature => true;

    public override bool IsInteractable => true;

    public override bool CanCollect(Entity byEntity) => false;

    // Else the cabin goes out of range for its own passengers. EntityBoat does exactly this.
    public override bool InRangeOf(Vec3d position, float horRangeSq, float vertRange)
    {
        return base.Pos.InRangeOf(position, horRangeSq + 64f, vertRange);
    }

    /// <summary>Drives client sound and the mount/unmount gate. Written only on change.</summary>
    public bool IsMoving
    {
        get => WatchedAttributes.GetBool("moving");
        set
        {
            if (WatchedAttributes.GetBool("moving") != value) WatchedAttributes.SetBool("moving", value);
        }
    }

    /// <summary>
    /// The tower a call is running to, as a distance from <c>Towers[0]</c> - the same scale as
    /// <see cref="Travelled"/> and meaningless under a different canonical chain, which is why every
    /// <see cref="Hold"/> drops it. <see cref="NoDestination"/> when nothing was called.
    /// <para>
    /// WatchedAttributes rather than <see cref="Entity.Attributes"/>, unlike Travelled: both are persisted
    /// (Entity.ToBytes writes WatchedAttributes on the save path too), but Attributes is written
    /// <c>if (!forClient)</c>, and the block-info panel that says "the cabin is on its way here" is client
    /// side and has nothing else to read. Written only on change, as <see cref="IsMoving"/> is.
    /// </para>
    /// </summary>
    public double Destination
    {
        get => WatchedAttributes.GetDouble("destination", NoDestination);
        set
        {
            if (WatchedAttributes.GetDouble("destination", NoDestination) != value) WatchedAttributes.SetDouble("destination", value);
        }
    }

    public bool HasDestination => Destination >= 0;

    public bool HasPassenger
    {
        get
        {
            var seats = GetBehavior<EntityBehaviorSeatable>()?.Seats;
            if (seats == null) return false;
            for (var i = 0; i < seats.Length; i++)
            {
                if (seats[i]?.Passenger != null) return true;
            }

            return false;
        }
    }

    public override void Initialize(EntityProperties properties, ICoreAPI api, long InChunkIndex3d)
    {
        speed = properties?.Attributes?["speed"].AsDouble(DefaultSpeed) ?? DefaultSpeed;
        hangDrop = properties?.Attributes?["hangDrop"].AsDouble(DefaultHangDrop) ?? DefaultHangDrop;
        base.Initialize(properties, api, InChunkIndex3d);
    }

    /// <summary>
    /// hitboxSize is a Vec2f and the base implementation hard-codes Y1 = 0, so JSON cannot describe a box
    /// around a model that hangs below its own Pos. The whole passenger compartment sits at Pos.Y-1.25 to
    /// Pos.Y, which left it unclickable - and boarding by clicking the body is exactly what
    /// interactMountAnySeat is for.
    /// <para>
    /// The horizontal extents are SQUARE on purpose. Entity.SelectionBox is world-axis-aligned and is never
    /// rotated by yaw - Entity.IntersectsRay ends in RayIntersectsWithCuboid(SelectionBox, Pos...) with no
    /// yaw term anywhere in the chain - while the cabin's world footprint does follow its yaw. An AABB that
    /// cannot rotate must CIRCUMSCRIBE the model at any yaw, not fit it at one: a box matching the model
    /// bounds (x +/-2.0 travel, z +/-1.4375 across) is correct only on an east-west line and is transposed
    /// on a north-south one, killing half a block of each cabin end. So both horizontal half-extents are
    /// the long axis. The surplus off the sides is harmless: EntityBehaviorSelectionBoxes.IntersectsRay
    /// tests the yaw-rotated seat boxes first and returns PreventDefault on a hit.
    /// </para>
    /// Overriding rather than assigning after Initialize because this is the one funnel every reset routes
    /// through: SyncedTreeAttribute.FromBytes invokes every registered modified listener with no path
    /// filter, so any full WatchedAttributes sync re-runs Entity.updateColSelBoxes and would put the JSON
    /// box back.
    /// </summary>
    public override void SetSelectionBox(float length, float height)
    {
        SelectionBox = new Cuboidf(-2.05f, -1.3f, -2.05f, 2.05f, 2.05f, 2.05f);
        OriginSelectionBox = SelectionBox.Clone();
    }

    public IMountableSeat CreateSeat(IMountable mountable, string seatId, SeatConfig config)
    {
        return new RopewayCabinSeat(mountable, seatId, config);
    }

    public void DidMount(EntityAgent entityAgent)
    {
        if (World?.Side != EnumAppSide.Server)
        {
            TellRiderAboutTheStopKey(entityAgent);
            return;
        }

        if (departed) return;

        boarding = true;
        boardAccum = 0;
    }

    /// <summary>
    /// The stop key is the cabin's only rider control, and a rider who does not know it is there rides to the
    /// end of the line every time - which is exactly how it read in play. Said on boarding, client side and
    /// to the local player only, because the key it has to name is the player's OWN binding and the server
    /// cannot see that.
    /// </summary>
    private void TellRiderAboutTheStopKey(EntityAgent entityAgent)
    {
        if (Api is not ICoreClientAPI capi || entityAgent == null || entityAgent != capi.World?.Player?.Entity) return;

        capi.ShowChatMessage(Lang.Get("ropeway:ride-hint", Binding(capi, RopewayModSystem.StopHotkey, "ropeway:hotkey-stop")));
        capi.ShowChatMessage(Lang.Get("ropeway:ridecam-hint", Binding(capi, RopewayRideCamera.Hotkey, "ropeway:hotkey-ridecam")));
    }

    /// <summary>
    /// The player's CURRENT binding for a hotkey, never the default - the whole point of naming a key in a
    /// hint is that it is the key they would actually press, and they can move ours in Settings > Controls
    /// like any other. Falls back to the hotkey's own name, which is what they will see in that list.
    /// </summary>
    public static string Binding(ICoreClientAPI capi, string hotkey, string nameLangCode)
    {
        return capi?.Input?.GetHotKeyByCode(hotkey)?.CurrentMapping?.ToString() ?? Lang.Get(nameLangCode);
    }

    public void DidUnmount(EntityAgent entityAgent)
    {
        if (World?.Side != EnumAppSide.Server || HasPassenger) return;

        boarding = false;
        boardAccum = 0;
    }

    /// <summary>
    /// Shown when a player looks at the cabin. The mount lines come from the seatable behavior; this is the
    /// one verb that is not a click, and interaction help is where a Vintage Story player looks for verbs.
    /// </summary>
    public override WorldInteraction[] GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player)
    {
        return base.GetInteractionHelp(world, es, player)
            .Append(new WorldInteraction
            {
                ActionLangCode = "ropeway:entityhelp-stop",
                MouseButton = EnumMouseButton.None,
                HotKeyCode = RopewayModSystem.StopHotkey
            })
            .Append(new WorldInteraction
            {
                ActionLangCode = "ropeway:entityhelp-ridecam",
                MouseButton = EnumMouseButton.None,
                HotKeyCode = RopewayRideCamera.Hotkey
            });
    }

    public override void OnGameTick(float dt)
    {
        if (World.Side == EnumAppSide.Server) ServerTick(dt);
        base.OnGameTick(dt);
    }

    private void ServerTick(float dt)
    {
        dt = Math.Min(0.5f, dt);

        var line = ResolveLine();
        if (line == null || line.TotalLength <= 0)
        {
            // Block entity and entity load order is not guaranteed, and a chunk under the line can unload.
            // Holding still is the whole recovery.
            IsMoving = false;
            departed = false;

            // Unless the anchor tower is loaded and simply has no spans left: then the line is gone for
            // good, not merely unloaded. Nothing else can ever remove this entity, so without the drop it
            // is immortal litter and the cabin item that paid for it is destroyed.
            if (LineKey != null && ModSystem?.LoadedTowers.ContainsKey(LineKey) == true) DropAndDie(null);
            return;
        }

        if (!line.Towers[0].Equals(LineKey))
        {
            // The chain re-canonicalised under us - it shrank, grew or flipped - so Travelled is measured
            // from a tower that is no longer index 0 and points somewhere else entirely. Re-basing parks the
            // cabin at a known tower, which is a teleport: fine for an empty cabin, not for a seated rider,
            // who waits instead for the chain to rebuild the way it was.
            Hold("ropeway:call-abandoned-line");
            if (!HasPassenger) RebaseTo(line);
            return;
        }

        DropGhostPassengers(line);

        if (Travelled < line.MinTravel || Travelled > line.MaxTravel)
        {
            // On a whole line the window is the line, so anything outside it is stale state to park away.
            // On a truncated one it means the loaded chain no longer reaches the cabin, and dragging it back
            // to the last loaded tower would be the false-endpoint teleport again. Hold for the chunk.
            if (line.Truncated)
            {
                // Hold rather than a bare stop: a call whose route has just gone out from under it cannot be
                // honoured, and a destination left pending would restart the trip on every tick.
                Hold("ropeway:call-abandoned-truncated");
                return;
            }

            Travelled = GameMath.Clamp(Travelled, 0, line.TotalLength);
        }

        if (!departed)
        {
            // A cabin that was called before the save resumes instead of parking: the destination outlived
            // the process (departed did not), and with nobody aboard there is nothing to park for.
            // lastSegment is cleared with it, so the span it resumes into is re-checked for clearance.
            if (HasDestination)
            {
                departed = true;
                lastSegment = -1;
            }

            // Server restart, chunk reload, or a tower broken under us: never resume from mid-span. Standing
            // at a tower is not mid-span, at ANY tower - a cabin called to an interior one is parked at a
            // station, and testing "is it at an end" instead would drag it off again on the next tick.
            else if (!line.IsAtTower(Travelled, ArrivalTolerance)) ParkAtNearestEnd(line);

            if (boarding && HasPassenger)
            {
                boardAccum += dt;
                if (boardAccum >= BoardingGraceSeconds)
                {
                    departed = true;
                    boarding = false;
                    lastSegment = -1;
                }
            }
            else
            {
                boarding = false;
                boardAccum = 0;
            }

            IsMoving = false;
            Place(line);
            return;
        }

        // Direction-aware, or an inbound cabin standing at an interior tower certifies the span in FRONT of
        // the tower while it is about to travel the one behind it.
        var segment = line.SpanAheadOf(Travelled, Outbound);
        if (segment != lastSegment)
        {
            lastSegment = segment;
            if (!SegmentClear(line, segment))
            {
                // Mounted riders have no block collision, so this is a safety gate rather than polish - and
                // the gate must not itself be the thing that moves them. Travelled is deliberately NOT
                // written: the cabin is standing on the tower it was about to leave in every case this fires
                // for, so there is nowhere to snap it to that is not ACROSS the span just proven blocked.
                // The one exception - a mid-span resume - is caught by the !departed mid-span recovery on
                // the very next tick, which parks at a proven end rather than driving through the block.
                Hold("ropeway:call-abandoned-blocked");
                Place(line);
                return;
            }
        }

        Travelled += (Outbound ? 1 : -1) * speed * dt;

        // Called to a tower: stop exactly there rather than running on to the end of the line. Clamped here
        // but held below the endpoint branches, so a call to a genuine end still leaves the cabin turned
        // around for the next ride - which is the only thing those branches do that this must not skip.
        var arrived = HasDestination && Reached(Travelled, Destination, Outbound);
        if (arrived) Travelled = Destination;

        // Reverse only at a proven endpoint. The unloaded end of a truncated chain is not one, so the cabin
        // runs up to the last loaded tower, holds there with Outbound unchanged, and carries on outward on
        // the next boarding once the chunk has loaded and the window has widened.
        if (Travelled >= line.MaxTravel)
        {
            Travelled = line.MaxTravel;
            if (line.MaxTravel >= line.TotalLength) Outbound = false;
            else NotifyRiders("ropeway-line-truncated", "ropeway:cabin-held-truncated");

            // Stopping at a proven end IS the arrival for a call to it. Stopping at the last loaded tower
            // short of one is a trip that gave up, and the caller is not aboard to be told by NotifyRiders.
            Hold(arrived || line.MaxTravel >= line.TotalLength ? null : "ropeway:call-abandoned-truncated");
        }
        else if (Travelled <= line.MinTravel)
        {
            Travelled = line.MinTravel;
            if (line.MinTravel <= 0) Outbound = true;
            else NotifyRiders("ropeway-line-truncated", "ropeway:cabin-held-truncated");
            Hold(arrived || line.MinTravel <= 0 ? null : "ropeway:call-abandoned-truncated");
        }

        if (arrived) Hold();

        IsMoving = departed;
        Place(line);
    }

    /// <summary>
    /// Whether a cabin running in the given direction has reached the point it was called to. Pure, and
    /// therefore tested: an inverted comparison here is a cabin that sails straight through its own station.
    /// </summary>
    public static bool Reached(double travelled, double destination, bool outbound)
    {
        return outbound ? travelled >= destination : travelled <= destination;
    }

    /// <summary>
    /// Everything that stops the cabin. Drops the destination with it: a hold is either the arrival itself
    /// or a route that stopped being travellable - a blocked span, an endpoint, a re-based chain - and in
    /// none of those cases may the trip silently resume later toward a number that no longer means anything.
    /// <para>
    /// <paramref name="abandonedReason"/> is the lang key for "your call is not happening", passed by every
    /// hold that is NOT the arrival. Null means the trip ended the way it was meant to.
    /// </para>
    /// </summary>
    private void Hold(string abandonedReason = null)
    {
        if (abandonedReason != null) NotifyCaller(abandonedReason);

        departed = false;
        boarding = false;
        boardAccum = 0;
        lastSegment = -1;
        Destination = NoDestination;
        calledBy = null;
        IsMoving = false;
    }

    /// <summary>
    /// Tells whoever called the cabin that it gave up on the way. The click already banked a "Cabin called
    /// to X" message, a call requires an EMPTY cabin, and <see cref="NotifyRiders"/> only reaches passengers
    /// - so without this every abandoned call is the silent no-op the successful-looking message promised
    /// against. Offline callers are dropped rather than queued: a login toast about a trip that ended an
    /// hour ago tells them nothing they can act on.
    /// </summary>
    private void NotifyCaller(string langKey)
    {
        if (!HasDestination || calledBy == null) return;

        if (World?.PlayerByUid(calledBy) is IServerPlayer player)
        {
            player.SendMessage(GlobalConstants.InfoLogChatGroup, Lang.Get(langKey), EnumChatType.Notification);
        }
    }

    /// <summary>
    /// The single place anything decides where "the end of the line" is, which is why the truncation rule
    /// lives here rather than at each caller: every park - the tick's mid-span recovery,
    /// <see cref="RebaseTo"/> and <see cref="DropGhostPassengers"/> - would otherwise happily drop a cabin
    /// on a tower that only looks like an endpoint because the chunk past it is unloaded.
    /// </summary>
    private void ParkAtNearestEnd(RopewayLine line)
    {
        var middle = (line.MinTravel + line.MaxTravel) / 2;
        Travelled = Travelled <= middle ? line.MinTravel : line.MaxTravel;
        Outbound = Travelled <= middle;

        // Parking is never an arrival - RebaseTo reaches here with a live destination whenever a line is
        // linked or cut under a called cabin.
        Hold("ropeway:call-abandoned-line");
    }

    /// <summary>
    /// Re-key onto a line whose tower chain just changed, and park at the nearer end. <see cref="Travelled"/>
    /// is an absolute scalar measured from Towers[0], so a chain that grew, shrank, merged or flipped
    /// canonical order points it at a completely different place - moving a seated rider tens of blocks
    /// sideways in one tick. Parking is the whole re-base, and it lands inside the loaded window only.
    /// </summary>
    public void RebaseTo(RopewayLine line)
    {
        if (line?.Towers == null || World?.Side != EnumAppSide.Server) return;

        LineKey = line.Towers[0].Copy();
        ParkAtNearestEnd(line);
        Place(line);
    }

    /// <summary>
    /// Unseats everyone where the cabin currently is. <see cref="RopewayCabinSeat.CanUnmount"/> refuses while
    /// the cabin is moving, so this stops it first.
    /// </summary>
    public void UnseatAll()
    {
        if (World?.Side != EnumAppSide.Server) return;

        IsMoving = false;

        var seats = GetBehavior<EntityBehaviorSeatable>()?.Seats;
        if (seats == null) return;

        for (var i = 0; i < seats.Length; i++) (seats[i]?.Passenger as EntityAgent)?.TryUnmount();
    }

    /// <summary>Tells whoever is aboard why the cabin stopped. Server side; the toast de-dupes on its code.</summary>
    private void NotifyRiders(string code, string langKey)
    {
        var seats = GetBehavior<EntityBehaviorSeatable>()?.Seats;
        if (seats == null) return;

        for (var i = 0; i < seats.Length; i++)
        {
            if (seats[i]?.Passenger is EntityPlayer player && World.PlayerByUid(player.PlayerUID) is IServerPlayer sp)
            {
                sp.SendIngameError(code, Lang.Get(langKey));
            }
        }
    }

    /// <summary>
    /// The line is gone. Unseat everyone, hand the cabin item back, and despawn. There is no other way to
    /// remove a cabin - it has no health behavior, <see cref="CanCollect"/> is false and it never expires.
    /// </summary>
    public void DropAndDie(IPlayer giveTo)
    {
        if (World?.Side != EnumAppSide.Server) return;

        UnseatAll();

        var item = World.GetItem(new AssetLocation(BlockPylonBase.CabinItemCode));
        if (item != null)
        {
            var stack = new ItemStack(item);
            if (giveTo?.InventoryManager?.TryGiveItemstack(stack, slotNotifyEffect: true) != true)
            {
                World.SpawnItemEntity(stack, Pos.XYZ);
            }
        }

        Die(EnumDespawnReason.Removed);
    }

    /// <summary>
    /// Where a call to <paramref name="tower"/> would send a cabin standing at <paramref name="travelled"/>,
    /// and whether it can go at all. Any tower on the line is a station, not only the two ends: the target is
    /// simply that tower's entry in <see cref="RopewayLine.Cumulative"/>. Pure - the entity half of a call is
    /// only the state it writes - and therefore tested.
    /// </summary>
    public static CabinCall PlanCall(RopewayLine line, BlockPos tower, double travelled, out double destination)
    {
        destination = NoDestination;

        var index = line?.IndexOf(tower) ?? -1;
        if (index < 0) return CabinCall.Unreachable;

        var target = line.Cumulative[index];

        // Neither end of the trip may sit outside the loaded window. Past it the cabin cannot be proven to
        // be where its Travelled says, and the target cannot be proven to still be on this line at all.
        if (target < line.MinTravel || target > line.MaxTravel) return CabinCall.Unreachable;
        if (travelled < line.MinTravel || travelled > line.MaxTravel) return CabinCall.Unreachable;

        if (Math.Abs(travelled - target) < ArrivalTolerance) return CabinCall.AlreadyHere;

        destination = target;
        return CabinCall.Called;
    }

    /// <summary>
    /// The index of the tower a rider's next press of the stop key aims at, or -1 when the line has nothing
    /// to offer. Pure, and therefore tested: this is the whole of the rider's control over the ride.
    /// <para>
    /// The cycle steps one tower at a time in the cabin's current direction of travel and wraps at the ends,
    /// starting from whatever tower is already requested - so the first press picks the stop coming up, a
    /// second press the one after it, and pressing on past the far end brings the selection back down the
    /// other side. That wrap is also the only direction control a rider has: it is what lets someone who
    /// boarded at an interior station, on a cabin pointing the wrong way, ask for a tower behind it.
    /// <paramref name="acceptable"/> is <see cref="PlanCall"/> in practice, which rejects the tower the cabin
    /// is already standing on and anything outside the loaded window.
    /// </para>
    /// </summary>
    public static int NextStop(RopewayLine line, double travelled, int requested, bool outbound, System.Func<int, bool> acceptable)
    {
        if (line?.Towers == null || line.Towers.Length == 0 || acceptable == null) return -1;

        var count = line.Towers.Length;
        var step = outbound ? 1 : -1;

        // With nothing requested the cycle starts at the cabin: AnchorIndexAt names the span it is in, whose
        // two ends are the towers either side of it, so one step from the near end lands on the far one.
        var from = requested >= 0 ? requested : line.AnchorIndexAt(travelled) + (outbound ? 0 : 1);

        for (var i = 1; i <= count; i++)
        {
            var index = ((from + i * step) % count + count) % count;
            if (acceptable(index)) return index;
        }

        return -1;
    }

    /// <summary>
    /// The rider's own call, from inside the cabin: the counterpart to <see cref="CallTo"/>, which refuses
    /// outright while anyone is aboard. Same destination machinery, same arrival, same abandonment messages -
    /// a rider choosing a stop IS a call, aimed from the seat instead of from the ground.
    /// </summary>
    public CabinCall RequestStop(RopewayLine line, string riderUid, out BlockPos tower)
    {
        tower = null;
        if (line?.Towers == null || World?.Side != EnumAppSide.Server) return CabinCall.Unreachable;

        // Same guard as CallTo: Travelled and every Cumulative are measured from Towers[0], so under a chain
        // that re-canonicalised they name different places. The tick re-bases and the next press works.
        if (!line.Towers[0].Equals(LineKey)) return CabinCall.Unreachable;

        var target = NoDestination;
        var index = NextStop(
            line,
            Travelled,
            HasDestination ? line.TowerAt(Destination, ArrivalTolerance) : -1,
            Outbound,
            i => PlanCall(line, line.Towers[i], Travelled, out target) == CabinCall.Called);

        if (index < 0) return CabinCall.Unreachable;

        tower = line.Towers[index];
        Aim(target, riderUid);
        return CabinCall.Called;
    }

    /// <summary>
    /// Sends an empty cabin to any tower on the line, where it stops. Reports why not rather than reporting
    /// success and then not moving.
    /// </summary>
    public CabinCall CallTo(RopewayLine line, BlockPos tower, string callerUid)
    {
        if (line?.Towers == null || HasPassenger) return CabinCall.Unreachable;

        // Travelled is measured from Towers[0]; a chain that re-canonicalised makes both it and the target
        // mean different places. The tick re-bases, and the call works on the next click.
        if (!line.Towers[0].Equals(LineKey)) return CabinCall.Unreachable;

        var outcome = PlanCall(line, tower, Travelled, out var target);
        if (outcome != CabinCall.Called) return outcome;

        Aim(target, callerUid);
        return CabinCall.Called;
    }

    /// <summary>
    /// Commit to a destination and leave. The one place a trip starts, shared by the ground call and the
    /// rider's stop key, so the two cannot drift into setting different halves of the state.
    /// </summary>
    private void Aim(double target, string byUid)
    {
        Destination = target;
        Outbound = target > Travelled;
        departed = true;
        boarding = false;
        lastSegment = -1;
        calledBy = byUid;

        // Written here rather than left to the end of the next ServerTick: CanMount reads IsMoving, so one
        // tick of "departed but not moving" lets a player board a cabin that has already left - skipping the
        // boarding grace (DidMount early-returns on departed) and producing the passenger-with-a-live-
        // destination combination OnTowerInteract's HasPassenger guard assumes cannot exist.
        IsMoving = true;
    }

    /// <summary>
    /// The cabin belonging to a line, if it is loaded. Sided by hand because <c>LoadedEntities</c> is
    /// declared on the two side-specific world interfaces and not on <see cref="IWorldAccessor"/>, and the
    /// block-info panel that reads this runs client side.
    /// ponytail: O(loaded entities) scan, on a click, a block break, or a block-info refresh. Index by line
    /// if a profile ever shows it.
    /// </summary>
    public static EntityRopewayCabin FindOn(IWorldAccessor world, RopewayLine line)
    {
        if (line == null) return null;

        var entities = world switch
        {
            IServerWorldAccessor server => (ICollection<Entity>)server.LoadedEntities.Values,
            IClientWorldAccessor client => client.LoadedEntities.Values,
            _ => null
        };

        if (entities == null) return null;

        foreach (var entity in entities)
        {
            if (entity is EntityRopewayCabin cabin && line.IndexOf(cabin.LineKey) >= 0) return cabin;
        }

        return null;
    }

    /// <summary>
    /// A rider who logged out leaves a passenger reference behind. Park at the nearer end tower, unseat
    /// them and put them on that tower, so they relog on the platform they built rather than in the air.
    /// </summary>
    private void DropGhostPassengers(RopewayLine line)
    {
        var seats = GetBehavior<EntityBehaviorSeatable>()?.Seats;
        if (seats == null) return;

        for (var i = 0; i < seats.Length; i++)
        {
            if (seats[i]?.Passenger is not EntityAgent agent) continue;

            var gone = agent.State == EnumEntityState.Despawned
                || (agent is EntityPlayer player && World.PlayerByUid(player.PlayerUID) == null);
            if (!gone) continue;

            ParkAtNearestEnd(line);
            Place(line);
            agent.TryUnmount();

            // Not TeleportTo: that defers behind a chunk load the despawning player will not wait for, and
            // its Pos.SetPos is dimension-unaware. Anchors carry a dimension-encoded Y, so strip it.
            // Dropped at the tower's own FOOTING level rather than the cabin's, which is the whole point of
            // this - they relog standing on the tower they built, not 1.25 blocks above it in the cabin's
            // floor. ParkAtNearestEnd ran just above, so Travelled is on a tower and this is that tower.
            // park.Y is the SHEAVE centre = footingY + SheaveHeight + 0.5, so subtracting SheaveHeight
            // alone lands on footingY + 0.5 - the top of the footing's plinth. Subtracting the 0.5 as well
            // would drop them at footingY, which is inside that plinth's own collision box.
            var park = line.PositionAt(Travelled);
            var footing = park.Y % BlockPos.DimensionBoundary - SpanMath.SheaveHeight;
            agent.Pos.SetPos(park.X, footing, park.Z);
        }
    }

    private bool SegmentClear(RopewayLine line, int segment)
    {
        if (line.Anchors == null || segment < 0 || segment + 1 >= line.Anchors.Length) return true;
        return SpanMath.IsSpanClear(World, line.Anchors[segment], line.Anchors[segment + 1], out _);
    }

    private void Place(RopewayLine line)
    {
        var point = line.PositionAt(Travelled);
        if (point == null) return;

        // Never TeleportTo - it sets IsTeleport and resets the client interpolation queue into a visible snap.
        Pos.SetPosWithDimension(new Vec3d(point.X, point.Y - hangDrop, point.Z));

        var dir = line.DirectionAt(Travelled);
        Pos.Yaw = (float)Math.Atan2(dir.X, dir.Z);
    }

    private RopewayModSystem ModSystem => Api?.ModLoader?.GetModSystem<RopewayModSystem>();

    private RopewayLine ResolveLine()
    {
        return RopewayLine.GetOrBuild(ModSystem, LineKey);
    }

    public override void ToBytes(BinaryWriter writer, bool forClient)
    {
        Attributes.SetDouble("travelled", Travelled);
        Attributes.SetBool("outbound", Outbound);
        if (calledBy != null) Attributes.SetString("calledBy", calledBy);
        else Attributes.RemoveAttribute("calledBy");
        base.ToBytes(writer, forClient);
    }

    public override void FromBytes(BinaryReader reader, bool isSync)
    {
        base.FromBytes(reader, isSync);
        Travelled = Attributes.GetDouble("travelled");
        Outbound = Attributes.GetBool("outbound", defaultValue: true);
        calledBy = Attributes.GetString("calledBy");

        // Cabins saved before the key moved to WatchedAttributes still carry it in Attributes. Without the
        // carry-over they resolve no line at all, and a null LineKey also skips the DropAndDie backstop -
        // an immortal cabin nothing can remove.
        if (!isSync && LineKey == null) LineKey = BEPylonBase.ReadPos(Attributes, "lineKey");
    }
}
