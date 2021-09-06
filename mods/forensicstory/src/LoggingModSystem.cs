using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace forensicstory.src
{
    public class LoggingSystem : ModSystem
    {
        private Logger _logger;
        private ICoreServerAPI _api;

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            _api = api;
            _logger = new Logger(api);
            
            api.Event.DidUseBlock += E_DidUseBlock;
            api.Event.DidBreakBlock += E_DidBreakBlock;
        }

        private void E_DidUseBlock(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            _logger.Log(new BlockUseLog(byPlayer, blockSel));
        }
        
        private void E_DidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            IBlockAccessor blockAccessor = _api.World.GetBlockAccessor(false, false, false, false);
            bool didBreak = blockAccessor.GetBlock(blockSel.Position).BlockId != oldblockId;

            if (didBreak)
            {
                _logger.Log(new BlockBreakLog(byPlayer, oldblockId, blockSel));
            }
        }
    }
}