using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Ropeway;

/// <summary>
/// A cabin seat. Mounting and unmounting are refused while the cabin moves - vanilla's sanctioned
/// dodge (EntityElevatorSeat) for every mid-transit edge case. Vanilla omits the toast; without it the
/// player concludes the mod is broken. The one way past the unmount refusal is the deliberate bail-out:
/// see <see cref="EntityRopewayCabin.BailHoldSeconds"/>.
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

    public override bool CanUnmount(EntityAgent entityAgent)
    {
        if (!Moving || Bailing(entityAgent)) return true;

        // The refusal is also the only place the bail-out is ever advertised, and that is deliberate: a
        // rider learns it at the exact moment they want it, from the press that was already going to fail.
        Toast(entityAgent, "cantride-moving", Lang.Get(
            "ropeway:cantunmount-moving",
            EntityRopewayCabin.Binding(entityAgent?.World?.Api as ICoreClientAPI, SneakHotkey, "game:Sneak"),
            (int)EntityRopewayCabin.BailHoldSeconds));
        return false;
    }

    /// <summary>Vanilla's own sneak binding, HotkeyManager.cs:64 - the key that already means "get off".</summary>
    private const string SneakHotkey = "sneak";

    private bool Moving => Entity is EntityRopewayCabin { IsMoving: true };

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
