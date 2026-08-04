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
/// different things to tell the player, and reporting either as failure is what made calling look broken.
/// </summary>
public enum CabinCall
{
    Called,
    AlreadyHere,
    Unreachable,

    /// <summary>
    /// Nothing on the line is turning, the whole of the line is loaded, and the cabin has not left a station.
    /// This is the outcome the design spent a paragraph refusing to have - a quern with no wind accepts the
    /// grain and waits - and the reason it exists anyway is that aiming is not free. <c>Aim</c> latches
    /// <c>departed</c>, only <c>Hold</c> clears it and every <c>Hold</c> needs the cabin to have got
    /// somewhere, so a call on a dead line pins the full haul load on every drive of that line forever, for a
    /// cabin that has never moved. The boarding path already had this guard; the call and the stop key were
    /// the two doors left open.
    /// <para>
    /// The loaded clause is <see cref="MayStart"/>'s, and it is an EXEMPTION rather than an equivalence.
    /// Truncation and drivelessness are independent: a truncated chain's zero MAY be a housing in a chunk
    /// that has not landed, and it may equally be a line nobody ever built a drive for. So the exemption does
    /// re-open the latch above - a three-second sit-down on a driveless truncated line latches
    /// <c>departed</c>, and it stays latched after the chunk lands. It is taken anyway, because the two
    /// mistakes are not the same size. The false refusal tells a player to go and build the drive they are
    /// standing beside, which is a message they cannot act on. What the exemption costs is one cabin
    /// declaring the haul load on a line where nothing turns - the same pin any departure that stalls
    /// mid-span already carries, and it clears the first time something does turn and the cabin reaches a
    /// tower.
    /// </para>
    /// </summary>
    NoDrive
}

/// <summary>
/// The rideable cabin. Server-authoritative motion along the line polyline; the client only
/// interpolates and plays sound. Deliberately has no physics behavior - one would let the controlling
/// client predict and steal authority from the server.
/// </summary>
public class EntityRopewayCabin : Entity, ISeatInstSupplier, IMountableListener
{
    public const double BoardingGraceSeconds = 3.0;

    /// <summary>
    /// How long a rider has to hold the SNEAK key, continuously, before a moving cabin lets them jump out.
    /// <para>
    /// Sneak because it is already the dismount input - <c>EntitySeat.onControls</c> calls TryUnmount on the
    /// press, and that press is exactly what raises the refusal that names this hold - so the emergency exit
    /// is the ordinary verb held down rather than a third hotkey to discover. A HOLD rather than a tap or a
    /// second tap because a tap is what a rider does by reflex and the drop is what makes waiting for the
    /// tower the sensible answer: two seconds is longer than any accident and shorter than any emergency.
    /// </para>
    /// </summary>
    public const double BailHoldSeconds = 2.0;

    /// <summary>
    /// How far the cabin's origin hangs below the rope. Derived, not chosen: with the sheave
    /// <see cref="SpanMath.SheaveHeight"/> = 4 cells above the footing, the roof (origin + 1.25) has to
    /// clear the crossarm's underside and the floor (origin - 1.25) has to clear the footing's top, which
    /// leaves 1.75 &lt; hangDrop &lt; 2.75 (plinth top at +0.5, crossarm underside at +4.0 - the crossarm's
    /// foot plate reaches the block boundary so it sits flat on the posts). 2.25 is that window's midpoint.
    /// <para>
    /// This sat low in the window at 2.0 while the only question was which way to run out of room, and
    /// clipping the crossarm reads worse than a low floor. The station rails changed the question: the band
    /// between the cabin roof and the crossarm underside is now hardware, not air, and at 2.0 that band is
    /// 4 units against rails that want 4 on their own - no room for the guide rollers at all. The midpoint
    /// doubles it to 8 and both fit. It is bought with floor clearance, 0.75 -&gt; 0.50 blocks over the
    /// footing. Changing this or SheaveHeight alone breaks the fit; TheCabinFitsThroughTheTower catches it.
    /// </para>
    /// </summary>
    public const double DefaultHangDrop = 2.25;

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

    /// <summary>
    /// The cabin is trying to move: it has left a station and has not stopped. Read by every powered tower
    /// on the line to decide what load to declare - see <c>BEDriveHousing.DeclareLoad</c> for why this and not
    /// <see cref="IsMoving"/>, which is false whenever the drive has stalled and would oscillate.
    /// </summary>
    public bool IsHauling => departed;

    /// <summary>
    /// How steeply the cabin is climbing right now, as the vertical component of its unit direction of
    /// travel: 0 parked or level, negative descending. The load term the towers add to their resistance.
    /// </summary>
    public double ClimbOn(RopewayLine line)
    {
        if (!departed || line == null) return 0;
        return (Outbound ? 1 : -1) * line.DirectionAt(Travelled).Y;
    }

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
    /// <para>
    /// The top is 2.45, not 2.05: the old mast stopped at shape y 32 = 2.00 blocks, the hanger's jaw now
    /// reaches y 38.4 = 2.40, and the box has to circumscribe on the VERTICAL axis too or the top of the
    /// hanger is not ray-hittable. Same 0.05 pad as the other five faces.
    /// </para>
    /// </summary>
    public override void SetSelectionBox(float length, float height)
    {
        SelectionBox = new Cuboidf(-2.05f, -1.3f, -2.05f, 2.05f, 2.45f, 2.05f);
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
        var help = base.GetInteractionHelp(world, es, player)
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

        // The attach/detach line is generated for us by EntityBehaviorAttachable.GetInteractionHelp, which
        // is already in the walk above. What it does NOT advertise is the verb that actually moves goods:
        // a plain right-click on a LOADED bench opens the container (CollectibleBehaviorHeldBag.OnInteract
        // fires whenever Ctrl is not held). Unadvertised, the only discoverable thing about a loaded cabin
        // is how to take the container off again. The line can promise ONE verb because both containers that
        // attach - basket and chest - carry BoatableGenericTypedContainer and so share HeldBag's OnInteract.
        // See entities/cabin.json //attachable-capacity for why the crate, which does not, is off the list.
        return LoadedBench(es) == null
            ? help
            : help.Append(new WorldInteraction
            {
                ActionLangCode = "ropeway:entityhelp-cargo",
                MouseButton = EnumMouseButton.Right
            });
    }

    /// <summary>
    /// The cargo slot behind the selection box the player is looking at, if it has anything in it.
    /// Bounds-checked on our side because the box list is rebuilt on every tesselation
    /// (<c>EntityBehaviorSelectionBoxes.OnTesselated</c>) and a stale index would otherwise index past it.
    /// </summary>
    private ItemSlot LoadedBench(EntitySelection es)
    {
        var boxes = GetBehavior<EntityBehaviorSelectionBoxes>()?.selectionBoxes;
        var index = (es?.SelectionBoxIndex ?? 0) - 1;
        if (boxes == null || index < 0 || index >= boxes.Length) return null;

        var slot = GetBehavior<EntityBehaviorAttachable>()?.GetSlotFromSelectionBoxIndex(index);
        return slot?.Empty == false ? slot : null;
    }

    public override void OnGameTick(float dt)
    {
        if (World.Side == EnumAppSide.Server) ServerTick(dt);
        ConstrainRiderYaw();
        base.OnGameTick(dt);
    }

    /// <summary>
    /// Stops a rider swivelling right round on a bench that faces one way. <c>bodyYawLimit</c> was dead JSON
    /// in this entity, and not because the key is decorative: <see cref="SeatConfig.BodyYawLimit"/> is only
    /// ever READ by <c>EntityBoat.SeatsToMotion</c> and <c>EntityBehaviorRideable.SeatsToMotion</c>, and the
    /// cabin is neither a boat nor rideable - nothing was going to apply it for us. This is those eight
    /// lines, kept identical to vanilla's so the JSON key means what the wiki says it means, and it needs no
    /// controllable seat: it is a constraint on the PASSENGER, not a control on the mount.
    /// <para>
    /// What it actually clamps, exactly: <see cref="EntityPlayer.HeadYawLimits"/> is read by
    /// <c>ClientMain.UpdateCameraYawPitch</c> (:2377-2383), which clamps <c>mouseYaw</c> - so this limits
    /// the seated player's own CAMERA, client side, and only for the player sitting there.
    /// <see cref="EntityPlayer.BodyYawLimits"/> clamps that same player's rendered body through the BodyYaw
    /// setter (EntityPlayer.cs:140-143). Neither reaches what OTHER players see of the rider: for a mounted
    /// player who is not you, <c>EntityPlayerShapeRenderer</c> (:429-431) forces the drawn body yaw to the
    /// MOUNT's Pos.Yaw outright, so onlookers already see the rider squared to the cabin no matter which way
    /// he is looking. Running it server side too would not change that - the server assigns
    /// <c>BodyYawServer</c> from the position packet, not BodyYaw, so the clamping setter never sees it.
    /// </para>
    /// <para>
    /// Centred on the cabin's own yaw plus the seat's <c>mountRotation.y</c>, which is vanilla's formula.
    /// That centre is the bench facing and not an approximation of it: the yaw those benches were built
    /// around IS this one - the renderer above pins a rider's body to <c>Pos.Yaw</c>, which is why both rows
    /// face cabin -X with their backrests at +X of their own pan (see the shape's //backrest note) - and it
    /// is the same yaw <c>EntityRideableSeat.DidMount</c> snaps the camera to on boarding. mountRotation.y
    /// stays the calibration knob if in-game says otherwise; it is the only thing here a render cannot
    /// settle. Cleanup is vanilla's: <c>EntityRideableSeat.DidUnmount</c> (:228-232) nulls both.
    /// </para>
    /// </summary>
    private void ConstrainRiderYaw()
    {
        var seats = GetBehavior<EntityBehaviorSeatable>()?.Seats;
        if (seats == null) return;

        for (var i = 0; i < seats.Length; i++)
        {
            if (seats[i]?.Passenger is not EntityPlayer rider) continue;

            var config = seats[i].Config;
            var centre = Pos.Yaw + config.MountRotation.Y * GameMath.DEG2RAD;

            // Both, not just one: they are only ever set and cleared as a pair, and mutating a null one
            // because the other happened to be set is a crash on the client's own player entity.
            if (rider.BodyYawLimits == null || rider.HeadYawLimits == null)
            {
                rider.BodyYawLimits = new AngleConstraint(centre, config.BodyYawLimit ?? GameMath.PIHALF);
                rider.HeadYawLimits = new AngleConstraint(centre, GameMath.PIHALF);
            }
            else
            {
                rider.BodyYawLimits.X = centre;
                rider.BodyYawLimits.Y = config.BodyYawLimit ?? GameMath.PIHALF;
                rider.HeadYawLimits.X = centre;
                rider.HeadYawLimits.Y = GameMath.PIHALF;
            }
        }
    }

    /// <summary>
    /// "The world is not ready" - every way there is to be in that state, in one predicate, because there is
    /// exactly one safe recovery from it and three branches each writing their own version of it is how one
    /// of them ended up calling <see cref="Hold"/> and undoing the other two. See the call site in
    /// <see cref="ServerTick"/> for the recovery; this is only the question. Pure, and therefore tested.
    /// <para>
    /// Order matters. A truncated chain cannot be asked about the cabin's position until it has been asked
    /// whether it is even measuring from the same tower, and neither question may be answered by falling
    /// through into the rest of the tick - that would run it with <see cref="Travelled"/> measured from one
    /// base and the chain from another.
    /// </para>
    /// </summary>
    public static bool NotReady(RopewayLine line, BlockPos lineKey, double travelled)
    {
        // Block entity and entity load order is not guaranteed, and a chunk under the line can unload.
        if (line?.Towers == null || line.TotalLength <= 0) return true;

        // A whole chain is proof of everything below, so nothing else can disqualify it.
        if (!line.Truncated) return false;

        // A truncated chain's canonical order is decided by the two ends the WALK could reach, not by the
        // line's real ends (RopewayLine.WalkChain), so a mismatch here is not evidence the chain changed - it
        // is evidence the chunks are not all in yet, which is the normal state for the first seconds of a
        // world load. This was the reload teleport: a partial chain that sorts the other way reverses,
        // Towers[0] stops being the cabin's LineKey, and the re-base branch rewrites LineKey, Travelled and
        // Pos from a chain nobody can vouch for. MarkLoadedEnds widens the window by itself and
        // BEPylonBase.Initialize drops the cached line, so a genuine re-base still runs on the tick after
        // the last tower registers.
        if (!line.Towers[0].Equals(lineKey)) return true;

        // Same base, but the loaded chain no longer reaches the cabin. Dragging it back to the last loaded
        // tower is the false-endpoint teleport by another road - and this is the branch that used to Hold,
        // which cleared `departed` and handed the cabin to the mid-span park the moment the chunk landed.
        return travelled < line.MinTravel || travelled > line.MaxTravel;
    }

    private void ServerTick(float dt)
    {
        dt = Math.Min(0.5f, dt);

        var line = ResolveLine();
        if (NotReady(line, LineKey, Travelled))
        {
            // THE RULE, and the reason NotReady exists as one predicate rather than three branches: a cabin
            // whose world is not ready stands still and changes NOTHING ELSE. Not Travelled, not Pos, not
            // departed, not Destination. All of those are route state and only Hold may clear it - clearing
            // it here is how a cabin saved in motion used to lose the fact it was moving, after which the
            // mid-span recovery below read "stopped in mid-air" and parked it at an end tower. Standing
            // still without forgetting the trip is what lets it carry on where it left off. A fourth way to
            // be not-ready belongs in NotReady, where it gets this recovery for free.
            IsMoving = false;

            // Unless the anchor tower is loaded and simply has no spans left: then the line is gone for
            // good, not merely unloaded. Nothing else can ever remove this entity, so without the drop it
            // is immortal litter and the cabin item that paid for it is destroyed.
            var gone = line == null || line.TotalLength <= 0;
            if (gone && LineKey != null && ModSystem?.LoadedTowers.ContainsKey(LineKey) == true) DropAndDie(null);
            return;
        }

        if (!line.Towers[0].Equals(LineKey))
        {
            // NotReady already absorbed the truncated case, so the chain really did re-canonicalise under us
            // - it shrank, grew or flipped - and Travelled is measured from a tower that is no longer index 0
            // and points somewhere else entirely. The trip goes either way: the destination it named is a
            // distance on a scale that no longer exists, so a rider's cabin drops its call exactly as an
            // empty one does. What a rider is spared is the RE-BASE, which parks at a known tower and is
            // therefore a teleport of up to the whole line; their cabin keeps its stale key and stops where
            // it is, and the re-base runs for real once the seat is empty.
            Hold("ropeway:call-abandoned-line");
            if (!HasPassenger) RebaseTo(line);
            return;
        }

        DropGhostPassengers(line);

        // AFTER the ghost drop, never before: a rider who logged out is unseated and put on a tower there,
        // and running the bail first would hand the same seat to both. Once DropGhostPassengers has run, a
        // seat that still has a passenger has a passenger who is really there.
        BailOut(line, dt);

        // NotReady already returned for every truncated chain the cabin sits outside of, so this only ever
        // fires on a whole one - where the window IS the line, and anything outside it is stale state to
        // clamp away rather than a chunk that has not landed yet.
        if (Travelled < line.MinTravel || Travelled > line.MaxTravel)
        {
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

            // A tower broken under us, or a trip that gave up: never resume from mid-span. Standing at a
            // tower is not mid-span, at ANY tower - a cabin called to an interior one is parked at a
            // station, and testing "is it at an end" instead would drag it off again on the next tick.
            // Never with anyone aboard, though. Parking is a teleport of up to the whole line, and a seated
            // rider flung across it is the exact failure this area exists to prevent - the one the re-base
            // branch above is guarded against, reachable here too because Hold clears `departed` and every
            // hold lands in this branch on the next tick. Their cabin stops where it stands instead, and the
            // way out is the dismount: it is not moving, so they can simply step out. The stop key is the
            // lesser half of it - a hold cleared `departed`, so aiming again is a first latch and MayStart
            // asks for a drive, which means a rider held on a line that is whole and has nothing turning
            // gets a refusal rather than a new heading. That is the one control this state costs them, and
            // it is the price of not letting a parked cabin latch IsMoving on a driveless line and take the
            // ordinary sneak-exit away.
            else if (!HasPassenger && !line.IsAtTower(Travelled, ArrivalTolerance)) ParkAtNearestEnd(line);

            if (boarding && HasPassenger)
            {
                boardAccum += dt;

                // The grace is up AND the drive is turning. This is NOT the departure gate the store design
                // had, which refused a trip the cabin could not afford: nothing is being checked against a
                // budget and nobody is told no here - the machine simply does not start while nothing turns,
                // the same way a quern with no wind does not start, and the rider is sitting in it and can
                // see that. The two doors with nobody watching - the ground call and the stop key - answer
                // the same question through MayStart and say so out loud. Once it HAS started a stall does
                // not undo it (see the speed check below), because that is what stops the load oscillating
                // mid-ride. Without this, boarding a line with no drive at all latches `departed` for good:
                // only Hold clears it and every Hold needs the cabin to move, so the drives would declare the
                // full haul load on the player's network forever for a cabin that has never gone anywhere.
                if (boardAccum >= BoardingGraceSeconds && MayStart(line)) Depart();
            }
            else
            {
                boarding = false;
                boardAccum = 0;
            }

            // `departed`, not false: both the departure above and the resume-after-save at the top of this
            // branch set it, and leaving IsMoving false until the end of the NEXT tick is a window in which
            // CanMount lets a second player board a cabin that has already left. Aim closes the same window
            // by hand for the call path; this is the boarding path's half of it.
            IsMoving = departed;
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
                // The one exception - a mid-span resume - is caught for an EMPTY cabin by the !departed
                // mid-span recovery on the very next tick, which parks at a proven end rather than driving
                // through the block. A cabin with a rider aboard is deliberately left standing there: see
                // that branch for why nothing may teleport a passenger, and for the way out they have.
                Hold("ropeway:call-abandoned-blocked");
                Place(line);
                return;
            }
        }

        // LIVE network speed, which the store design forbade and this one is built on. The cabin is a load
        // on the line's drives and runs at whatever they are turning: no wind is no motion, and it picks up
        // again by itself when the wind does. Nothing here can strand a rider - the sneak-hold bail-out gets
        // them out of a stopped cabin anywhere - and BEPylonBase.DriveSpeedOn documents why the drives are
        // always loaded when a rider is on the line.
        var speed = RopewayPower.CabinSpeed(BEPylonBase.DriveSpeedOn(ModSystem, line));
        if (speed <= 0)
        {
            // Standing still is not stopping: `departed` stays set, so the towers keep declaring the haul
            // load (or the network would speed up the moment the cabin stalled and cycle) and the trip
            // resumes on the tick power returns.
            IsMoving = false;
            Place(line);
            return;
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

            // Stopping at a proven end IS the arrival for a call to it. Stopping at the last loaded tower
            // short of one is a trip that gave up, and the caller is not aboard to be told by NotifyRiders.
            var ended = arrived || line.MaxTravel >= line.TotalLength;
            if (line.MaxTravel >= line.TotalLength) Outbound = false;
            else NotifyRiders("ropeway-line-truncated", "ropeway:cabin-held-truncated");

            Hold(ended ? null : "ropeway:call-abandoned-truncated");
        }
        else if (Travelled <= line.MinTravel)
        {
            Travelled = line.MinTravel;

            var ended = arrived || line.MinTravel <= 0;
            if (line.MinTravel <= 0) Outbound = true;
            else NotifyRiders("ropeway-line-truncated", "ropeway:cabin-held-truncated");

            Hold(ended ? null : "ropeway:call-abandoned-truncated");
        }

        if (arrived) Hold();

        IsMoving = departed;
        Place(line);
    }

    /// <summary>
    /// May the cabin be set going right now: is anything on the line actually turning, and has it not left a
    /// station already. Every latch of <c>departed</c> asks this - the boarding grace, the ground call and
    /// the rider's stop key - and it is one method rather than three copies of the expression because the
    /// three of them drifting apart is exactly how the call and the stop key ended up without the guard the
    /// boarding path has always had. Pure, and therefore tested.
    /// <para>
    /// <paramref name="departed"/> is in the question, and that clause is the whole subtlety. A cabin that
    /// has left and then stalled mid-span must still accept a re-aim: it is already latched, so aiming it
    /// somewhere else costs the network nothing it is not already paying, and refusing there would take the
    /// stop key off a rider at the exact moment the wind drops - which is the moment they most want to choose
    /// where they end up. Only the first latch is worth a refusal.
    /// </para>
    /// <para>
    /// <paramref name="truncated"/> is an EXEMPTION and not evidence. A truncated chain says part of the
    /// line is dark; it does not say the dark part holds a drive, and a line with no drive anywhere can be
    /// truncated just as easily. The window a rider holds open is 256 blocks at the shipped view distance
    /// against a 320-block line cap, so a housing beside the far end of a long line really does read as
    /// absent (<see cref="BEPylonBase.DriveSpeedOn"/>). Refusing there tells the player to build the drive
    /// they are standing next to, on a click that used to bank and complete once the chunk landed; accepting
    /// there latches <c>departed</c> on a line that may have no drive at all. The second is the cheaper
    /// mistake and <see cref="CabinCall.NoDrive"/> weighs the two. It lives here rather than at the two
    /// refusal sites so the boarding grace gets it too: the rider who presses the stop key and the rider who
    /// simply sits down have the same drive, and answering them differently is how these three drifted apart
    /// the first time.
    /// </para>
    /// </summary>
    public static bool MayStart(bool departed, double lineSpeed, bool truncated)
    {
        return departed || truncated || lineSpeed > 0;
    }

    private bool MayStart(RopewayLine line)
    {
        return MayStart(departed, RopewayPower.CabinSpeed(BEPylonBase.DriveSpeedOn(ModSystem, line)), line?.Truncated == true);
    }

    /// <summary>
    /// A plain ride leaving a station: nobody named a stop, so it runs to the end of the line the cabin is
    /// pointing at. Nothing is checked HERE - the drive is a load, not a permission, and there is no budget
    /// to fail - but the caller only starts the cabin while the drive is actually turning, for the reason
    /// given there.
    /// </summary>
    private void Depart()
    {
        departed = true;
        boarding = false;
        lastSegment = -1;
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
    /// <para>
    /// A cabin standing still because its drive stopped is NOT a hold and must never reach here: it has not
    /// finished, its route is still travellable, and it carries on the moment the wind comes back.
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
    /// <summary>
    /// Whether a re-base has to wait for chunks rather than re-key. Same rule as <see cref="NotReady"/>, for
    /// the link-service callers: <c>TryUnlink</c> and <c>UnlinkAll</c> can hand over a survivor whose far end
    /// is unloaded, and a truncated chain's <c>Towers[0]</c> is whichever end the walk happened to reach -
    /// re-keying onto it puts <see cref="Travelled"/> on a scale that means somewhere else.
    /// <para>
    /// Only while the old key is still ON that chain, though, and that clause is the whole of this method.
    /// <c>LineKey</c> is ALWAYS an end tower - <c>TryPlaceCabin</c> sets it to <c>Towers[0]</c> and the tick
    /// keeps it there - so "the broken tower was the cabin's key" is <c>UnlinkAll</c>'s ordinary case, not an
    /// exotic one, and <c>PickSurvivor</c> then falls back to the first survivor. Waiting for THAT is waiting
    /// forever: <c>BEPylonBase.Forget</c> drops the tower from <c>LoadedTowers</c> a moment later,
    /// <c>ResolveLine</c> returns null for good, and the <c>DropAndDie</c> backstop cannot fire because it
    /// requires <c>LoadedTowers</c> to contain <c>LineKey</c> - an immortal uncollectable cabin with its item
    /// destroyed, which is strictly worse than the teleport the guard exists to prevent. Hold only while
    /// there is something to hold FOR. Pure, and therefore tested.
    /// </para>
    /// </summary>
    public static bool RebaseMustWait(RopewayLine line, BlockPos lineKey)
    {
        return line != null && line.Truncated && line.IndexOf(lineKey) >= 0;
    }

    public void RebaseTo(RopewayLine line)
    {
        if (line?.Towers == null || World?.Side != EnumAppSide.Server) return;

        if (RebaseMustWait(line, LineKey)) return;

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

    /// <summary>
    /// One step of a rider's bail-out hold. Zero the moment they let go OR the cabin stops, so the hold has
    /// to be unbroken and it only ever counts while there is something to bail out of. Pure, and therefore
    /// tested: an accumulator that failed to reset is a rider who falls out of a cabin they were happily
    /// riding in.
    /// <para>
    /// <paramref name="released"/> is the accident guard, and it is why the arm is EDGE-triggered rather
    /// than level-triggered on the held flag. <c>EntityAgent.TryMount</c> copies the boarder's live control
    /// flags into the seat (EntityAgent.cs:273) BEFORE <c>Passenger</c> is set, so a player who crouch-walks
    /// onto a platform and right-clicks to board arrives with the seat's Sneak already true - and the
    /// false-&gt;true handler that is the ONLY thing advertising the bail-out already fired into a null
    /// Passenger, so no toast was shown and no further edge is coming. Counting the flag alone would eject
    /// them two seconds after departure having pressed nothing and read nothing, which is easier to hit by
    /// accident than on purpose. So the hold may only start once this rider has been seen NOT sneaking while
    /// the cabin moves: a flag inherited from boarding can never arm anything, and the refusal toast that
    /// explains the bail-out always comes first.
    /// </para>
    /// </summary>
    public static double HoldSneak(double held, bool sneaking, bool moving, float dt, ref bool released)
    {
        if (!moving) released = false;
        else if (!sneaking) released = true;

        return sneaking && moving && released ? held + dt : 0;
    }

    /// <summary>
    /// THE EMERGENCY EXIT. <see cref="RopewayCabinSeat.CanUnmount"/> refuses while the cabin moves and that
    /// stays the default answer - an accidental dismount thirty blocks up is worse than riding on to the
    /// next tower - but a rider who holds sneak for <see cref="BailHoldSeconds"/> gets out anyway, and takes
    /// the drop for it.
    /// <para>
    /// Everything here is PER SEAT and per rider - the hold, the edge trigger and the clearance
    /// (<see cref="RopewayCabinSeat.BailKey"/>, on the rider's own tree). Two riders can finish their holds
    /// on the same tick and both get out; nothing on the cabin can be overwritten by the second one.
    /// </para>
    /// <para>
    /// The seat's <c>Controls</c> are the server's copy of the rider's keys: <c>ServerMain</c>
    /// (:934-947) routes a mounted player's MoveKeyChange packets into <c>MountedOn.Controls</c> rather than
    /// into the player's own, so <c>Sneak</c> here is the live held state with nothing to sync ourselves.
    /// </para>
    /// </summary>
    private void BailOut(RopewayLine line, float dt)
    {
        var seats = GetBehavior<EntityBehaviorSeatable>()?.Seats;
        if (seats == null) return;

        for (var i = 0; i < seats.Length; i++)
        {
            if (seats[i] is not RopewayCabinSeat seat) continue;

            if (seat.Passenger is not EntityPlayer rider)
            {
                seat.SneakHeld = 0;
                seat.SneakReleased = false;
                continue;
            }

            seat.SneakHeld = HoldSneak(seat.SneakHeld, seat.Controls?.Sneak == true, IsMoving, dt, ref seat.SneakReleased);
            if (seat.SneakHeld < BailHoldSeconds) continue;

            seat.SneakHeld = 0;
            Jump(line, seat, rider);
        }
    }

    /// <summary>
    /// The rider jumps, and the CABIN IS DELIBERATELY LEFT ALONE. It keeps its destination and its direction,
    /// and it carries on: the drive does not care who is aboard, losing a passenger is not an arrival and
    /// there is nothing about the trip that was contingent on them - it simply gets there empty.
    /// Every HasPassenger gate flips open behind them and each one is safe to open, because each is guarded
    /// for a cabin that is empty and mid-span already: breaking a tower re-bases and parks, a ground call
    /// re-aims it, and the mid-span park recovery is exactly what rescues a cabin the rider has just
    /// abandoned between two towers.
    /// </summary>
    private void Jump(RopewayLine line, RopewayCabinSeat seat, EntityPlayer rider)
    {
        // The clearance and the unmount it authorises, one tree and therefore one packet: TryUnmount ends in
        // RemoveAttribute("mountedOn") on this same tree, which marks all of it dirty. See BailKey.
        rider.WatchedAttributes.SetBool(RopewayCabinSeat.BailKey, true);

        // Vanilla's EntityRideableSeat.DidUnmount hunts for a free block beside the mount and teleports the
        // rider onto it. That is the right answer at a tower and the wrong one here: the reason "wait for
        // the tower" is the sensible default is that bailing out costs you the fall. Restored in a finally
        // because TryUnmount runs third-party listeners (Event.TriggerEntityUnmounted, every
        // IMountableListener): one throw leaves the flag stuck false for the life of the entity, TickEntities
        // swallows it, and every subsequent ORDINARY dismount at a tower silently drops the rider instead of
        // placing them. The clearance dies in the same finally when the jump did not happen: a live
        // permission on a rider who is still seated turns their next single TAP of sneak into an instant
        // teleporting dismount, the exact inverse of the design.
        var jumped = false;
        seat.DoTeleportOnUnmount = false;
        try
        {
            jumped = rider.TryUnmount();
        }
        finally
        {
            seat.DoTeleportOnUnmount = true;
            if (!jumped) rider.WatchedAttributes.RemoveAttribute(RopewayCabinSeat.BailKey);
        }

        if (!jumped) return;

        // Spend the clearance, one flush window later. It cannot be cleared inline: attributes go out every
        // 0.2 s (PhysicsManager.cs:313), so a set-and-remove inside one window never reaches the wire at all
        // and every client would refuse the unmount this authorised - the exact failure BailKey documents.
        // A second later it has been seen, and clearing it stops a spent permission riding on the player's
        // persisted attributes until their next boarding, across a save and a relog. DidMount clears it too;
        // whichever gets there first, the other is a no-op.
        World?.RegisterCallback(_ => rider.WatchedAttributes.RemoveAttribute(RopewayCabinSeat.BailKey), 1000);

        if (World?.PlayerByUid(rider.PlayerUID) is not IServerPlayer player) return;

        var span = line.AnchorIndexAt(Travelled);
        player.SendMessage(
            GlobalConstants.InfoLogChatGroup,
            Lang.Get("ropeway:bailed-out", TowerName(line, span), TowerName(line, span + 1)),
            EnumChatType.Notification);
    }

    /// <summary>
    /// A tower's name for a message, falling back to its compass bearing from the cabin the way every other
    /// tower message does.
    /// </summary>
    private string TowerName(RopewayLine line, int index)
    {
        if (line?.Towers == null || index < 0 || index >= line.Towers.Length) return "?";

        var pos = line.Towers[index];
        var towers = ModSystem?.LoadedTowers;
        var be = towers != null && towers.TryGetValue(pos, out var found) ? found : null;
        var anchor = SpanMath.AnchorOf(pos);

        return RopewayLinkService.DisplayName(be, anchor.X - Pos.X, anchor.Z - Pos.Z);
    }

    /// <summary>
    /// Tells whoever is aboard why the cabin stopped. Server side. The code is only an identifier the API
    /// asks for - HudIngameError.Event_InGameError ignores errorCode entirely, so it neither de-dupes nor
    /// suppresses anything, and repeating a call repeats the toast.
    /// </summary>
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
        UnloadCargo(giveTo);

        var item = World.GetItem(new AssetLocation(BlockPylonBase.CabinItemCode));
        if (item != null) HandBack(new ItemStack(item), giveTo);

        Die(EnumDespawnReason.Removed);
    }

    /// <summary>
    /// The cargo guard lives here rather than in <see cref="DropAndDie"/> because <c>DropAndDie</c> is not
    /// the only door. <c>/entity remove</c> (<c>CmdEntity.cs:213</c>, <c>:230</c>, <c>:727</c>) and WorldEdit's
    /// entity removal (<c>BlockAccessorRevertable.cs:401</c>, <c>:490</c>) call <c>Die(Removed)</c> straight,
    /// and <c>/entity kill</c> calls <c>Die(Death)</c>; vanilla covers neither, because its only unprompted
    /// drop is gated on <c>Death</c> and even that spills the goods loose while binning the emptied container
    /// itself. One guard here covers every caller, present and future, and it deliberately has no reason
    /// check: unloading first leaves the despawn fan-out empty slots, so nothing can drop twice - which is
    /// also why <c>dropContentsOnDeath</c> came off <c>cabin.json</c>, it was the one real dupe vector.
    /// <para>
    /// The mod's own three paths have already run <see cref="UnloadCargo(IPlayer)"/> with a player to hand to;
    /// this second pass finds the slots empty and does nothing.
    /// </para>
    /// </summary>
    public override void Die(EnumDespawnReason reason = EnumDespawnReason.Death, DamageSource damageSourceForDeath = null)
    {
        if (World?.Side == EnumAppSide.Server) UnloadCargo(null);

        base.Die(reason, damageSourceForDeath);
    }

    /// <summary>
    /// Into <paramref name="giveTo"/>'s inventory if it fits, on the ground under the cabin if it does not.
    /// The second half is not a fallback nobody hits - <see cref="DropAndDie"/> runs for the tower-vanished
    /// backstop with no player at all.
    /// </summary>
    private void HandBack(ItemStack stack, IPlayer giveTo)
    {
        if (giveTo?.InventoryManager?.TryGiveItemstack(stack, slotNotifyEffect: true) != true)
        {
            World.SpawnItemEntity(stack, Pos.XYZ);
        }
    }

    /// <summary>
    /// Empties the benches before the cabin dies. THE thing this feature had to get right: vanilla's only
    /// unprompted drop is <c>CollectibleBehaviorHeldBag.OnEntityDespawn</c> gated on <c>Reason == Death</c>,
    /// and even that spills the goods loose while binning the emptied container itself - while every way this
    /// cabin actually goes away despawns with <c>Removed</c>, where the fan-out reaches the held bag, fails
    /// its own reason check and does nothing but close the dialog. Stop there and every basket on the cabin,
    /// and everything in it, is deleted in silence.
    /// <para>
    /// Called from <see cref="DropAndDie"/> with the player who took the line down, and again from
    /// <see cref="Die"/> with nobody - see there for why the second call site is what makes this complete.
    /// </para>
    /// </summary>
    private void UnloadCargo(IPlayer giveTo)
    {
        var attachable = GetBehavior<EntityBehaviorAttachable>();
        var inv = attachable?.Inventory;
        if (inv == null) return;

        // Close any open cargo dialog BEFORE the slots are emptied. Vanilla's despawn fan-out
        // (EntityBehaviorAttachable.OnEntityDespawn:411-428) dereferences slot.Itemstack, so once we null it
        // the hook is skipped: CollectibleBehaviorHeldBag.OnEntityDespawn never reaches
        // AttachedContainerWorkspace.OnDespawn, the server's wrapperInv is never CloseInventoryAndSync'd and
        // leaks in player.InventoryManager.OpenedInventories for the rest of the session, and the dialog sits
        // open over a cabin that is gone. Not OnDetached, which is what vanilla's own detach calls - it does
        // (byEntity as EntityPlayer).Player and this path has no player at all. The index is the workspace's
        // cache key: HeldBag keys it by SELECTION BOX index, which here is the inventory index, because the
        // cargo slots ARE the selection boxes and the two lists are pinned in the same order.
        var despawn = new EntityDespawnData { Reason = EnumDespawnReason.Removed };
        for (var i = 0; i < inv.Count; i++)
        {
            inv[i]?.Itemstack?.Collectible?.GetCollectibleInterface<IAttachedInteractions>()
                ?.OnEntityDespawn(inv[i], i, this, despawn);
        }

        // storeInv, not MarkDirty: this mutates the behavior's inventory outside the attach/detach flow that
        // normally writes it back, and the tree in WatchedAttributes is the only copy (EntityBehaviorContainer
        // :395). Vanilla does the same after its own detach (EntityBehaviorAttachable.cs:270).
        if (UnloadCargo(inv, World, stack => HandBack(stack, giveTo)) > 0) attachable.storeInv();
    }

    /// <summary>
    /// Hands back the goods first and the emptied container second, and only then clears the slot. Returns
    /// how many containers it emptied.
    /// <para>
    /// The goods come out rather than riding along inside the container item because a container itemstack
    /// carrying a <c>backpack</c> tree loses it the moment the block is placed:
    /// <c>BlockEntityGenericTypedContainer.OnBlockPlaced</c> reads only <c>type</c> and <c>isPerPlayer</c>
    /// off the stack and then calls <c>base.OnBlockPlaced(null)</c>. Vanilla never has to care because
    /// <c>CollectibleBehaviorHeldBag.OnTryDetach</c> refuses to let a player pull a loaded container off a
    /// mount at all - a guard this path does not go through. So: cargo SPILLS. It does not ride inside the
    /// cabin item, and it is not left inside a container item we hand someone. Both halves land in the
    /// player's inventory when there is room and on the ground when there is not, so nothing is destroyed
    /// either way.
    /// </para>
    /// </summary>
    public static int UnloadCargo(IEnumerable<ItemSlot> slots, IWorldAccessor world, Action<ItemStack> handBack)
    {
        if (slots == null) return 0;

        var emptied = 0;
        foreach (var slot in slots)
        {
            var container = slot?.Itemstack;
            if (container == null) continue;

            var bag = container.Collectible?.GetCollectibleInterface<IHeldBag>();

            // Null contents means the container was never opened, so it has no `backpack` tree at all -
            // and Clear would dereference that missing tree (CollectibleBehaviorHeldBag.cs:37-40). Guarding
            // on the read is what keeps a pristine basket from throwing on the way out.
            var contents = bag?.GetContents(container, world);
            if (contents != null)
            {
                foreach (var goods in contents)
                {
                    if (goods?.StackSize > 0) handBack(goods);
                }

                bag.Clear(container);
            }

            handBack(container);
            slot.Itemstack = null;
            emptied++;
        }

        return emptied;
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

        // A rider on a cabin that is already hauling passes this whatever the wind is doing - see MayStart.
        // The one refused here is the rider sitting in a parked cabin on a line with nothing turning, who
        // would otherwise pin the haul load on the line for as long as the world lives.
        if (!MayStart(line)) return CabinCall.NoDrive;

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

        // Last, not first: "the cabin is already at this tower" is the more useful answer to a click at a
        // tower the cabin is standing at, whether or not the wind is blowing.
        if (!MayStart(line)) return CabinCall.NoDrive;

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
            // this - they relog standing on the tower they built, not a block above it in the cabin's
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
        var leg = (float)Math.Atan2(dir.X, dir.Z);
        Pos.Yaw = departed ? leg : StationYaw(line, leg);
    }

    /// <summary>
    /// The yaw of a cabin STOPPED at a tower: square to that tower's own passage, so it sits flush with the
    /// platform instead of already pointing at the next station. Falls back to the leg bearing whenever the
    /// tower cannot be asked.
    /// <para>
    /// <c>!departed</c> is the whole safety condition and it is not a proxy for anything - it is the exact
    /// predicate under which <see cref="Travelled"/> cannot change. Every write to Travelled in
    /// <see cref="ServerTick"/> is below the <c>!departed</c> early return except the stale-state clamp and
    /// <see cref="ParkAtNearestEnd"/>, both of which are teleports onto a tower. So the cabin never rotates
    /// while it is moving: this is the narrow half of the REVERTED angle-station law (see
    /// <see cref="RopewayLine.DirectionAt"/>), which held the passage axis across a WINDOW around the vertex
    /// and crab-walked because <see cref="RopewayLine.PositionAt"/> had already swung the origin onto the
    /// outgoing leg. Stationary, the origin does not move, so there is nothing to crab away from. A cabin
    /// merely PASSING a tower is byte-identical to the shipped law - it never stops, so it never gets here.
    /// </para>
    /// <para>
    /// Turning in place at the tower centre sweeps the cabin's half-diagonal, sqrt(2.0^2 + 1.4375^2) = 2.463
    /// blocks, against post inner faces at 2.5 - 0.037 blocks of margin, and only the 5-wide passage buys it.
    /// <c>TheCabinCanTurnSquareAtATowerWithoutSweepingThroughAPost</c> asserts it off the shipped shape and
    /// the shipped multiblock; if either moves, that test is the thing that fails.
    /// </para>
    /// </summary>
    private float StationYaw(RopewayLine line, float leg)
    {
        var index = line.TowerAt(Travelled, ArrivalTolerance);
        if (index < 0) return leg;

        var modSystem = ModSystem;
        return modSystem != null && modSystem.LoadedTowers.TryGetValue(line.Towers[index], out var tower)
            ? SquareTo(tower.PassageFacing, leg)
            : leg;
    }

    /// <summary>
    /// The passage axis, as the one of its two yaws nearer <paramref name="leg"/>. The cabin is symmetric
    /// front-to-back so both are equally correct to look at, and taking the nearer one is what makes it turn
    /// the short way - at most a quarter turn, and never past the axis and back. Pure, and therefore tested.
    /// <para>
    /// SNAP, not a hand-rolled blend: the cabin carries <c>interpolateposition</c>, whose
    /// <c>LerpRotation</c> already eases Pos.Yaw with a ~0.1 s time constant, so one written yaw is rendered
    /// as a rotation in place. A blend of our own would be a second easing on top of that one, with a phase
    /// of its own that would have to be proven not to overlap travel.
    /// </para>
    /// </summary>
    public static float SquareTo(BlockFacing passage, float leg)
    {
        if (passage == null) return leg;

        var axis = (float)Math.Atan2(passage.Normalf.X, passage.Normalf.Z);
        return Math.Abs(GameMath.AngleRadDistance(leg, axis)) > Math.PI / 2 ? axis + (float)Math.PI : axis;
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
        Attributes.SetBool("departed", departed);
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

        // Persisted for one reason: without it a cabin saved MID-SPAN came back as "stopped somewhere it
        // cannot legitimately be" and ServerTick's !departed recovery parked it at an end tower - a second
        // reload teleport, independent of the truncated-chain one, and the one that fires for an ordinary
        // ride (a called trip survived already, through Destination). With it, a cabin saved in motion
        // resumes from exactly where it stopped, in the direction it was going. lastSegment stays -1, so
        // the span it resumes into is re-checked for clearance before it moves.
        departed = Attributes.GetBool("departed");

        // Cabins saved before the key moved to WatchedAttributes still carry it in Attributes. Without the
        // carry-over they resolve no line at all, and a null LineKey also skips the DropAndDie backstop -
        // an immortal cabin nothing can remove.
        if (!isSync && LineKey == null) LineKey = BEPylonBase.ReadPos(Attributes, "lineKey");
    }
}
