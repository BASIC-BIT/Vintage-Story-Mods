using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace PocketDimensions;

public sealed class PocketWaystoneBlock : Block
{
    private const string WaystoneServiceCacheKey = "pocketdimensions:waystone-service";

    public override bool CanPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref string failureCode)
    {
        if (world.Side == EnumAppSide.Server && byPlayer is IServerPlayer player)
        {
            var allowed = GetWaystoneService()?.CanPlaceWaystone(player);
            if (allowed?.Success == false)
            {
                failureCode = allowed.ErrorCode;
                player.SendIngameError(allowed.ErrorCode ?? "pocketwaystone-place-denied", allowed.Message);
                return false;
            }
        }

        return base.CanPlaceBlock(world, byPlayer, blockSel, ref failureCode);
    }

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
            player.SendIngameError("pocketwaystone-service-unavailable", "Pocket Dimensions is not available.");
            return true;
        }

        var result = service.EnterBoundPocket(player, blockSel);
        if (!result.Success)
        {
            player.SendIngameError(result.ErrorCode ?? "pocketwaystone-enter-failed", result.Message);
            return true;
        }

        player.SendMessage(GlobalConstants.GeneralChatGroup, result.Message, EnumChatType.Notification);
        return true;
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (world.Side == EnumAppSide.Server)
        {
            var player = byPlayer as IServerPlayer;
            var allowed = GetWaystoneService()?.CanBreakWaystone(player, pos);
            if (allowed?.Success == false)
            {
                player?.SendIngameError(allowed.ErrorCode ?? "pocketwaystone-break-denied", allowed.Message);
                return;
            }

            var blockEntity = world.BlockAccessor.GetBlockEntity(pos) as PocketWaystoneBlockEntity;
            GetWaystoneService()?.ForgetWaystoneEndpoint(blockEntity?.EndpointId);
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }

    private IPocketWaystoneService GetWaystoneService()
    {
        return api.ObjectCache.TryGetValue(WaystoneServiceCacheKey, out var service) ? service as IPocketWaystoneService : null;
    }
}
