using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace PocketDimensions;

internal interface IPocketWaystoneService
{
    string DisplayName(string dimensionId);

    DimensionLibResult EnterBoundPocket(IServerPlayer player, BlockSelection blockSelection);

    DimensionLibResult ReturnFromPocket(IServerPlayer player, BlockSelection blockSelection = null);

    void ForgetWaystoneEndpoint(string endpointId);
}
