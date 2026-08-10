using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Ropeway;

/// <summary>
/// A cabin seat. Mounting is refused while the cabin moves - vanilla's sanctioned dodge
/// (EntityElevatorSeat) for every mid-transit edge case. Vanilla omits the toast; without it the player
/// concludes the mod is broken. Unmounting is refused on a stricter question than that one: see
/// <see cref="CanUnmount"/>. The one way past it is the deliberate bail-out, see
/// <see cref="EntityRopewayCabin.BailHoldSeconds"/>.
/// </summary>
public class RopewayCabinSeat : EntityRideableSeat
{
    /// <summary>
    /// Player meta-anim code, game/entities/humanoid/player.json:509 - bench sit, legs forward, upright
    /// torso, no triggeredBy to fight (sitflooridle has one, and EntityPlayer.onAnimControls swaps it out
    /// from under you). Only a fallback: entities/cabin.json sets it per seat.
    /// </summary>
    private const string DefaultSitAnimation = "sitboatidle";

    /// <summary>
    /// Seconds of continuous sneak this rider has held while the cabin moves, the bail-out timer.
    /// It lives on the SEAT rather than on the cabin because two riders can hold at once and only the one
    /// who held gets out. Ticked by <c>EntityRopewayCabin.BailOut</c>, server side; never persisted, so a
    /// hold interrupted by a save simply starts again.
    /// </summary>
    public double SneakHeld;

    /// <summary>
    /// Whether this rider has been seen NOT sneaking since the cabin started moving - the edge trigger the
    /// hold is gated on. See <c>EntityRopewayCabin.HoldSneak</c> for why a held flag alone is not input.
    /// </summary>
    public bool SneakReleased;

    /// <summary>
    /// The server's clearance for one rider to leave a moving cabin, on the RIDER'S OWN WatchedAttributes
    /// and set immediately before the unmount it authorises. The tree matters: <c>TryUnmount</c> ends in
    /// <c>RemoveAttribute("mountedOn")</c> on this same tree, and <c>SyncedTreeAttribute.RemoveAttribute</c>
    /// calls <c>MarkAllDirty</c>, so clearance and removal leave in ONE full update and there is no ordering
    /// left to get wrong. On the cabin's tree it was a PARTIAL update racing the rider's full one, and
    /// <c>ClientSystemEntities.HandleEntityBulkAttributesPacket</c> applies every full update before any
    /// partial - so the permission arrived after the removal it authorised, <see cref="CanUnmount"/>
    /// refused, and the rider stayed drawn inside a cabin they had already left.
    /// </summary>
    public const string BailKey = "ropewaybail";

    public override EnumMountAngleMode AngleMode => EnumMountAngleMode.Unaffected;

    public RopewayCabinSeat(IMountable mountablesupplier, string seatId, SeatConfig config)
        : base(mountablesupplier, seatId, config)
    {
        RideableClassName = "ropewaycabin";
    }

    /// <summary>
    /// The sit pose. Nothing plays it for us: EntityRideableSeat.DidMount starts "idle" on the CABIN, and
    /// SuggestedAnimation is permanently null because the cabin has no rideable behavior - so without this
    /// the rider is mounted, snapped to the seat and left standing. Every vanilla seat writes the same call
    /// by hand (EntityBoatSeat.cs:51-55). Both sides run it, as in vanilla.
    /// </summary>
    public override void DidMount(EntityAgent entityAgent)
    {
        base.DidMount(entityAgent);
        entityAgent?.AnimManager?.StartAnimation(SitAnimation);

        // The clearance CANNOT be cleared by the tick that spends it: attributes flush every 0.2 s
        // (PhysicsManager.cs:313), not per tick, so a set-and-clear inside one flush window never reaches
        // the wire at all. Boarding is the next moment it could possibly matter and is unambiguously after.
        // Without this a rider who bailed and got back in is ejected by a single TAP of sneak - and the flag
        // rides on the player entity, which is persisted, so it would survive a relog. Both sides: a client
        // holding a stale copy would otherwise unmount locally while the server refused.
        if (entityAgent?.WatchedAttributes?.HasAttribute(BailKey) == true) entityAgent.WatchedAttributes.RemoveAttribute(BailKey);

        // Seats outlive their riders, and an inherited hold is somebody else's two seconds.
        SneakHeld = 0;
        SneakReleased = false;
    }

    /// <summary>
    /// Stopped BEFORE base, because EntitySeat.DidUnmount nulls Passenger (EntitySeat.cs:133-146) and there
    /// is then nobody left to stop the animation on. Same ordering as EntityBoatSeat.cs:57-69.
    /// </summary>
    public override void DidUnmount(EntityAgent entityAgent)
    {
        Passenger?.AnimManager?.StopAnimation(SitAnimation);
        base.DidUnmount(entityAgent);

        // THE FALL DATUM, and it belongs on every dismount rather than only on the bail-out. A mounted
        // player never touches the ground - EntityBehaviorPlayerPhysics forces OnGround = false and pins Pos
        // to the seat, so EntityBehaviorControlledPhysics never refreshes PositionBeforeFalling - and
        // Entity.OnFallToGround bills the drop as PositionBeforeFalling.Y - Pos.Y. Left alone that is the
        // platform they BOARDED at: ride a line downhill, step off at the bottom station, and vanilla
        // charges you for the whole descent. Re-datuming here is what makes fall damage mean the drop the
        // rider can actually see, which is the entire consequence the bail-out is priced on.
        entityAgent?.PositionBeforeFalling.Set(entityAgent.Pos.X, entityAgent.Pos.Y, entityAgent.Pos.Z);
    }

    private string SitAnimation => Config?.Animation ?? DefaultSitAnimation;

    public override bool CanMount(EntityAgent entityAgent)
    {
        if (Moving)
        {
            Toast(entityAgent, "cantride-moving", Lang.Get("ropeway:cantride-moving"));
            return false;
        }

        return base.CanMount(entityAgent);
    }

    /// <summary>
    /// The ordinary dismount, and the question it asks is NOT "is the cabin moving". A tap of sneak reaches
    /// here through <c>EntitySeat.onControls</c>, and what it leaves behind is vanilla's
    /// <c>EntityRideableSeat.tryTeleportToFreeLocation</c> (EntityRideableSeat.cs:239-259): exactly TWO
    /// candidate blocks, one to each side of the mount, both needing <c>SideSolid[UP]</c>. Out on a span both
    /// are air, so the teleport is skipped, the rider is left at the seat - and <see cref="DidUnmount"/> has
    /// just re-datumed <c>PositionBeforeFalling</c> to that point, so the game bills them for the whole drop.
    /// <para>
    /// "Not moving" was the wrong permission for that. <c>IsMoving</c> goes false the moment the mechanical
    /// network stalls (<c>EntityRopewayCabin.ServerTick</c>, the <c>speed &lt;= 0</c> branch) and again on
    /// every <c>Hold</c> - a blocked span, a re-based chain, an unloaded chunk - so a rider suspended over a
    /// valley, the exact state the two-second bail-out exists to price, got the ordinary dismount on ONE TAP
    /// with <c>DoTeleportOnUnmount</c> still true. A stall silently converted a deliberate act into a reflex.
    /// The right question is whether there is anything under them, which is also why a cabin standing at a
    /// tower stays free to step out of whether or not the drive is turning: the tower's own footing is there.
    /// </para>
    /// </summary>
    public override bool CanUnmount(EntityAgent entityAgent)
    {
        // The clearance outranks both refusals and is read first: it is the server saying THIS rider is
        // leaving, and every machine has to answer that the same way. See BailKey.
        if (Bailing(entityAgent)) return true;

        // Only the server and the rider's OWN machine may say no. Every other client reaches here through
        // the mountedOn listener, answering an unmount the server has ALREADY made, exactly once
        // (EntityAgent.cs:223) - a watcher that refuses there keeps the rider drawn inside a cabin they have
        // left for the rest of the session, and it has nothing to refuse WITH that the server did not have.
        // Survivable while the answer was one synced bool; not now it is a block lookup, which a client can
        // legitimately get differently at the edge of the chunks it has.
        if (!Answering(entityAgent)) return true;

        // The refusal is also the only place the bail-out is ever advertised, and that is deliberate: a
        // rider learns it at the exact moment they want it, from the press that was already going to fail.
        string Refusal(string key) => Lang.Get(
            key,
            EntityRopewayCabin.Binding(entityAgent?.World?.Api as ICoreClientAPI, SneakHotkey, "game:Sneak"),
            (int)EntityRopewayCabin.BailHoldSeconds);

        if (Moving)
        {
            Toast(entityAgent, "cantride-moving", Refusal("ropeway:cantunmount-moving"));
            return false;
        }

        if (!OverGround())
        {
            Toast(entityAgent, "cantunmount-noground", Refusal("ropeway:cantunmount-noground"));
            return false;
        }

        return base.CanUnmount(entityAgent);
    }

    /// <summary>Vanilla's own sneak binding, HotkeyManager.cs:64 - the key that already means "get off".</summary>
    private const string SneakHotkey = "sneak";

    /// <summary>
    /// Blocks of drop an ordinary dismount may cost, and it is vanilla's own free-fall allowance rather than
    /// a number chosen here: <c>EntityBehaviorHealth.OnFallToGround</c> returns without damage while the fall
    /// is under <c>3.5 * fallDamageThreshold</c> (EntityBehaviorHealth.cs:381-387). Ground within this of the
    /// cabin is ground a rider steps onto for nothing.
    /// <para>
    /// Measured from the CABIN's origin, which is 1.25 blocks above the rider's own feet, so the refusal is
    /// conservative by that much - nobody is ever told no about a drop they would have survived by more than
    /// a block and a quarter. The cabin is the datum because it is the position both sides agree on: a
    /// passenger's Pos is pinned to the seat by physics a tick behind, and <c>DropGhostPassengers</c> unmounts
    /// a rider on the very tick it parks the cabin at a tower.
    /// </para>
    /// </summary>
    public const double FreeFall = 3.5;

    /// <summary>
    /// Whether anything solid stands within <see cref="FreeFall"/> under a cabin whose origin is at
    /// <paramref name="cabinY"/>. The block probe is a parameter so the arithmetic is pure and therefore
    /// tested - against the shipped tower geometry, where the answer has to be YES or a cabin parked at a
    /// station refuses to let anybody out.
    /// </summary>
    // System.Func by name: Vintagestory.API.Common declares a Func<T1, TResult> of its own and the two are
    // ambiguous in this file.
    public static bool GroundUnder(double cabinY, System.Func<int, bool> solidAt)
    {
        if (solidAt == null) return false;

        for (var y = (int)Math.Floor(cabinY); y >= (int)Math.Floor(cabinY - FreeFall); y--)
        {
            if (solidAt(y)) return true;
        }

        return false;
    }

    /// <summary>
    /// The same question against the world under this cabin. Straight down one column, because the cabin
    /// origin sits on the rope and the rider sits under it - a sideways search is what vanilla already does
    /// and it is the thing that fails here.
    /// <para>
    /// Not <c>SpanMath.RopewayBlockFilter</c>, which is the other half of this pair and would be exactly
    /// wrong: it passes our own blocks so a line of towers does not block itself, and the block under a
    /// parked cabin IS one of our own - the footing it is standing on.
    /// </para>
    /// </summary>
    private bool OverGround()
    {
        var pos = Entity?.Pos;
        var blocks = Entity?.World?.BlockAccessor;

        // Never refuse on an answer we do not have. A refusal that cannot be justified is a rider who cannot
        // get out, and that is a worse failure than the fall this is guarding.
        if (pos == null || blocks == null) return true;

        // Floor, not a cast. (int) truncates TOWARD ZERO, so a cabin at x -99.5 probes column -99 - the
        // neighbouring one - and a station anywhere at negative X or Z answers this question about a block
        // the rider will not land on. GroundUnder three lines below already uses Math.Floor for Y, so the
        // convention was known here and simply not applied.
        var x = (int)Math.Floor(pos.X);
        var z = (int)Math.Floor(pos.Z);

        // MostSolid and the dimension-encoded Y straight off Pos, exactly as vanilla's own
        // tryTeleportToFreeLocation reads the two blocks beside the mount. An unloaded chunk comes back null
        // and reads as no ground: a rider cannot see a landing there either, and refusing is the answer that
        // cannot kill anyone.
        return GroundUnder(pos.Y, y =>
        {
            var block = blocks.GetBlockOrNull(x, y, z, BlockLayersAccess.MostSolid);
            return block != null && (block.SideSolid.Any || block.CollisionBoxes is { Length: > 0 });
        });
    }

    /// <summary>
    /// Whether this machine's answer counts: the server, and the rider's own client. See
    /// <see cref="CanUnmount"/> for why every other client must answer yes.
    /// </summary>
    private static bool Answering(EntityAgent entityAgent)
    {
        return entityAgent?.World?.Api is not ICoreClientAPI capi || capi.World?.Player?.Entity == entityAgent;
    }

    private bool Moving => Entity is EntityRopewayCabin { IsMoving: true };

    /// <summary>
    /// Whether this seat is refusing the ordinary step out right now, for either reason. This is what the
    /// bail-out hold must be armed against - NOT <see cref="Moving"/>.
    /// <para>
    /// The two used to be the same question and are not any more. The hold was armed off IsMoving because
    /// while the cabin moved that WAS the only refusal; then the over-air refusal was added, and IsMoving is
    /// written false the instant the drive stalls. So the one state the refusal exists to protect - stopped
    /// over a valley - had the tap refused AND the hold counting to nothing, and the message advertised a
    /// key that did nothing. A rider whose line lost its drive had no exit at all.
    /// </para>
    /// </summary>
    public bool HeldIn => Moving || !OverGround();

    /// <summary>
    /// Clear everyone aboard to leave, for the paths that THROW a rider out rather than asking on their
    /// behalf - <see cref="CanUnmount"/> refuses an ordinary dismount over air, and none of those are one.
    /// Without it a tower blown out from under an occupied cabin leaves the rider aboard, to be carried off
    /// by the re-base (the teleport the unseat exists to prevent) or left mounted to an entity
    /// <c>DropAndDie</c> is about to despawn, which is a softlock rather than a fall.
    /// <para>
    /// The same clearance the bail-out uses, on the rider's own tree, for the reason given at
    /// <see cref="BailKey"/> - and spent a flush window later for the reason given at
    /// <c>EntityRopewayCabin.Jump</c>: a live permission left on a seated rider turns their next single TAP
    /// of sneak into an instant dismount.
    /// </para>
    /// </summary>
    public static void ClearToLeave(Entity cabin)
    {
        // Server only, like every other write to this flag: a client that grants itself the clearance is a
        // client that lets itself out of a cabin the server is still holding it in.
        if (cabin?.World?.Side != EnumAppSide.Server) return;

        var seats = cabin.GetBehavior<EntityBehaviorSeatable>()?.Seats;
        if (seats == null) return;

        for (var i = 0; i < seats.Length; i++)
        {
            if (seats[i]?.Passenger is not EntityAgent rider) continue;

            rider.WatchedAttributes.SetBool(BailKey, true);
            cabin.World?.RegisterCallback(_ => rider.WatchedAttributes.RemoveAttribute(BailKey), 1000);
        }
    }

    /// <summary>
    /// Whether the cabin has cleared THIS rider to jump. Read off the rider's own WatchedAttributes rather
    /// than off <see cref="SneakHeld"/>, because the answer has to be the same on every machine: the server
    /// removes the rider's <c>mountedOn</c>, and each client answers that by calling <c>TryUnmount</c> - and
    /// so this - exactly once, from a WatchedAttributes listener (EntityAgent.cs:223) that never fires
    /// again. A client that says no there keeps the rider drawn inside a cabin they have already left, for
    /// the rest of the session. Sharing the rider's tree is what makes that impossible: see
    /// <see cref="BailKey"/>.
    /// </summary>
    private static bool Bailing(EntityAgent entityAgent)
    {
        return entityAgent?.WatchedAttributes?.GetBool(BailKey) == true;
    }

    /// <summary>
    /// Client side and to the player it is about. Both gates matter: this runs on the server too (where
    /// there is no toast to show) and on every OTHER client that is merely watching this rider get on or
    /// off, where the toast would be a message about somebody else's key press.
    /// </summary>
    private static void Toast(EntityAgent entityAgent, string code, string message)
    {
        if (entityAgent?.World?.Api is not ICoreClientAPI capi || capi.World?.Player?.Entity != entityAgent) return;

        capi.TriggerIngameError(entityAgent, code, message);
    }
}
