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
    public override EnumMountAngleMode AngleMode => EnumMountAngleMode.Unaffected;

    public RopewayCabinSeat(IMountable mountablesupplier, string seatId, SeatConfig config)
        : base(mountablesupplier, seatId, config)
    {
        RideableClassName = "ropewaycabin";
    }

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
