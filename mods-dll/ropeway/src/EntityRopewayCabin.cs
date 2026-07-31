using System;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Ropeway;

/// <summary>
/// The rideable cabin. Server-authoritative motion along the line polyline; the client only
/// interpolates and plays sound. Deliberately has no physics behavior - one would let the controlling
/// client predict and steal authority from the server.
/// </summary>
public class EntityRopewayCabin : Entity, ISeatInstSupplier, IMountableListener
{
    public const double BoardingGraceSeconds = 3.0;
    public const double DefaultSpeed = 2.2;
    public const double DefaultHangDrop = 2.0;

    /// <summary>The tower at travelled == 0. Every other bit of route state is derived from the blocks.</summary>
    public BlockPos LineKey;

    /// <summary>
    /// Metres from the line's canonical <c>Towers[0]</c>, which <see cref="RopewayLine.WalkChain"/> picks by
    /// position and not by which tower the walk started from. <see cref="LineKey"/> is kept equal to it so
    /// the two agree, but it is only a handle into the chain - a chain that re-canonicalises under a stale
    /// LineKey makes this number mean somewhere else entirely, which is what
    /// <see cref="RebaseTo"/> exists to repair. The one seam the cabin has on route state.
    /// </summary>
    public double Travelled;

    public bool Outbound = true;

    private double speed = DefaultSpeed;
    private double hangDrop = DefaultHangDrop;
    private bool departed;
    private bool boarding;
    private double boardAccum;
    private int lastSegment = -1;

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
        if (World?.Side != EnumAppSide.Server || departed) return;

        boarding = true;
        boardAccum = 0;
    }

    public void DidUnmount(EntityAgent entityAgent)
    {
        if (World?.Side != EnumAppSide.Server || HasPassenger) return;

        boarding = false;
        boardAccum = 0;
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
            Hold();
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
                IsMoving = false;
                departed = false;
                return;
            }

            Travelled = GameMath.Clamp(Travelled, 0, line.TotalLength);
        }

        if (!departed)
        {
            // Server restart, chunk reload, or a tower broken under us: never resume from mid-span.
            if (Travelled > line.MinTravel && Travelled < line.MaxTravel) ParkAtNearestEnd(line);

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

        var segment = line.AnchorIndexAt(Travelled);
        if (segment != lastSegment)
        {
            lastSegment = segment;
            if (!SegmentClear(line, segment))
            {
                // Mounted riders have no block collision, so this is a safety gate rather than polish.
                Travelled = Outbound ? line.Cumulative[segment] : line.Cumulative[segment + 1];
                Hold();
                IsMoving = false;
                Place(line);
                return;
            }
        }

        Travelled += (Outbound ? 1 : -1) * speed * dt;

        // Reverse only at a proven endpoint. The unloaded end of a truncated chain is not one, so the cabin
        // runs up to the last loaded tower, holds there with Outbound unchanged, and carries on outward on
        // the next boarding once the chunk has loaded and the window has widened.
        if (Travelled >= line.MaxTravel)
        {
            Travelled = line.MaxTravel;
            if (line.MaxTravel >= line.TotalLength) Outbound = false;
            else NotifyRiders("ropeway-line-truncated", "ropeway:cabin-held-truncated");
            Hold();
        }
        else if (Travelled <= line.MinTravel)
        {
            Travelled = line.MinTravel;
            if (line.MinTravel <= 0) Outbound = true;
            else NotifyRiders("ropeway-line-truncated", "ropeway:cabin-held-truncated");
            Hold();
        }

        IsMoving = departed;
        Place(line);
    }

    private void Hold()
    {
        departed = false;
        boarding = false;
        boardAccum = 0;
        lastSegment = -1;
        IsMoving = false;
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
        Hold();
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

        var item = World.GetItem(new AssetLocation(BlockPylonHead.CabinItemCode));
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
    /// Sends an empty cabin back to an end tower. False when it is already there, occupied, or the trip
    /// would cross into an unloaded stretch - the caller reports that rather than reporting success and
    /// then not moving.
    /// </summary>
    public bool CallTo(RopewayLine line, BlockPos tower)
    {
        if (line?.Towers == null || tower == null || HasPassenger) return false;

        // Travelled is measured from Towers[0]; a chain that re-canonicalised makes both it and the target
        // below mean different places. The tick re-bases, and the call works on the next click.
        if (!line.Towers[0].Equals(LineKey)) return false;

        var atStart = tower.Equals(line.Towers[0]);
        var target = atStart ? 0 : line.TotalLength;
        if (target < line.MinTravel || target > line.MaxTravel) return false;
        if (Travelled < line.MinTravel || Travelled > line.MaxTravel) return false;
        if (Math.Abs(Travelled - target) < 0.5) return false;

        Outbound = target > Travelled;
        departed = true;
        boarding = false;
        lastSegment = -1;
        return true;
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
            var park = line.PositionAt(Travelled);
            agent.Pos.SetPos(park.X, park.Y % BlockPos.DimensionBoundary - hangDrop, park.Z);
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
        if (LineKey != null) BEPylonHead.WritePos(Attributes, "lineKey", LineKey);
        base.ToBytes(writer, forClient);
    }

    public override void FromBytes(BinaryReader reader, bool isSync)
    {
        base.FromBytes(reader, isSync);
        Travelled = Attributes.GetDouble("travelled");
        Outbound = Attributes.GetBool("outbound", defaultValue: true);
        LineKey = BEPylonHead.ReadPos(Attributes, "lineKey");
    }
}
