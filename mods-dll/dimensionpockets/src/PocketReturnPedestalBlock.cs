using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PocketDimensions;

public sealed class PocketReturnPedestalBlock : Block
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

        var service = GetWaystoneService();
        if (service == null)
        {
            player.SendIngameError("pocketreturnpedestal-service-unavailable", "Pocket Dimensions is not available.");
            return true;
        }

        var result = service.ReturnFromPocket(player, blockSel);
        if (!result.Success)
        {
            player.SendIngameError(result.ErrorCode ?? "pocketreturnpedestal-return-failed", result.Message);
            return true;
        }

        player.SendMessage(GlobalConstants.GeneralChatGroup, result.Message, EnumChatType.Notification);
        return true;
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer)
    {
        var info = base.GetPlacedBlockInfo(world, pos, forPlayer);
        return string.IsNullOrWhiteSpace(info)
            ? "Managed Pocket Dimensions return pedestal.\nRight-click to return to your entry point."
            : info + "\nManaged Pocket Dimensions return pedestal.\nRight-click to return to your entry point.";
    }

    private IPocketWaystoneService GetWaystoneService()
    {
        return api.ObjectCache.TryGetValue(WaystoneServiceCacheKey, out var service) ? service as IPocketWaystoneService : null;
    }
}
