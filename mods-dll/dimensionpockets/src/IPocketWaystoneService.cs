using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PocketDimensions;

internal interface IPocketWaystoneService
{
    string DisplayName(string dimensionId);

    DimensionLibResult EnterBoundPocket(IServerPlayer player, BlockSelection blockSelection);

    DimensionLibResult ReturnFromPocket(IServerPlayer player, BlockSelection blockSelection = null);

    DimensionLibResult TravelElevator(IServerPlayer player, int direction, bool createMissingLayer = false);

    void ForgetWaystoneEndpoint(string endpointId);

    DimensionLibResult CanPlaceWaystone(IServerPlayer player);

    DimensionLibResult CanBreakWaystone(IServerPlayer player, BlockPos pos);
}
