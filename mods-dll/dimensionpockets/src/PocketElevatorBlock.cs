using DimensionLib.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PocketDimensions;

public sealed class PocketElevatorBlock : Block
{
    private const string WaystoneServiceCacheKey = "pocketdimensions:waystone-service";

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.Side != EnumAppSide.Server)
        {
            return true;
        }

        var player = byPlayer as IServerPlayer;
        if (player == null)
        {
            return true;
        }

        var service = GetService();
        if (service == null)
        {
            player.SendIngameError("pocketelevator-service-unavailable", "Pocket Dimensions is not available.");
            return true;
        }

        var direction = byPlayer.Entity?.Controls?.Sneak == true ? -1 : 1;
        var result = service.TravelElevator(player, direction);
        if (!result.Success)
        {
            player.SendIngameError(result.ErrorCode ?? "pocketelevator-travel-failed", result.Message);
            return true;
        }

        player.SendMessage(GlobalConstants.GeneralChatGroup, result.Message, EnumChatType.Notification);
        return true;
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        var info = base.GetPlacedBlockInfo(world, pos, forPlayer);
        var elevatorInfo = "Pocket Elevator. Right-click to move up; sneak-right-click to move down.";
        return string.IsNullOrWhiteSpace(info) ? elevatorInfo : info + "\n" + elevatorInfo;
    }

    private IPocketWaystoneService GetService()
    {
        return api.ObjectCache.TryGetValue(WaystoneServiceCacheKey, out var service) ? service as IPocketWaystoneService : null;
    }
}
