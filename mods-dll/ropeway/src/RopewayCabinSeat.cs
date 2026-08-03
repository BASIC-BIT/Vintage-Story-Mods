using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace Ropeway;

/// <summary>
/// A cabin seat. Mounting and unmounting are refused while the cabin moves - vanilla's sanctioned
/// dodge (EntityElevatorSeat) for every mid-transit edge case. Vanilla omits the toast; without it the
/// player concludes the mod is broken.
/// </summary>
public class RopewayCabinSeat : EntityRideableSeat
{
    /// <summary>
    /// Player meta-anim code, game/entities/humanoid/player.json:509 - bench sit, legs forward, upright
    /// torso, no triggeredBy to fight (sitflooridle has one, and EntityPlayer.onAnimControls swaps it out
    /// from under you). Only a fallback: entities/cabin.json sets it per seat.
    /// </summary>
    private const string DefaultSitAnimation = "sitboatidle";

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
    }

    /// <summary>
    /// Stopped BEFORE base, because EntitySeat.DidUnmount nulls Passenger (EntitySeat.cs:133-146) and there
    /// is then nobody left to stop the animation on. Same ordering as EntityBoatSeat.cs:57-69.
    /// </summary>
    public override void DidUnmount(EntityAgent entityAgent)
    {
        Passenger?.AnimManager?.StopAnimation(SitAnimation);
        base.DidUnmount(entityAgent);
    }

    private string SitAnimation => Config?.Animation ?? DefaultSitAnimation;

    public override bool CanMount(EntityAgent entityAgent)
    {
        if (Moving)
        {
            Complain(entityAgent);
            return false;
        }

        return base.CanMount(entityAgent);
    }

    public override bool CanUnmount(EntityAgent entityAgent)
    {
        if (!Moving) return true;

        Complain(entityAgent);
        return false;
    }

    private bool Moving => Entity is EntityRopewayCabin { IsMoving: true };

    private static void Complain(EntityAgent entityAgent)
    {
        (entityAgent?.World?.Api as ICoreClientAPI)?.TriggerIngameError(
            entityAgent, "cantride-moving", Lang.Get("ropeway:cantride-moving"));
    }
}
