using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace forensicstory
{
    public class LoggingSystem : ModSystem
    {
        private ICoreServerAPI _api;
        
        private static Logger<BlockBreakLog> _blockBreakLogger;
        private static Logger<BlockUseLog> _blockAccessLogger;
        private static Logger<EntityInteractLog> _entityInteractLogger;
        private static Logger<PlaceBombLog> _placeBombLogger;

        private ModConfig config;

        private const string CONFIGNAME = "forensicstory.json";
        
        
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            _api = api;

            _blockAccessLogger = new Logger<BlockUseLog>(_api);
            _blockBreakLogger = new Logger<BlockBreakLog>(_api);
            _entityInteractLogger = new Logger<EntityInteractLog>(_api);
            _placeBombLogger = new Logger<PlaceBombLog>(_api);
            
            try
            {
                this.config = api.LoadModConfig<ModConfig>(CONFIGNAME);
            }
            catch (Exception e)
            {
                api.Server.LogError("Forensic Story: Failed to load mod config!");
                return;
            }

            if (this.config == null)
            {
                api.Server.LogNotification($"Forensic Story: Non-existant modconfig at 'ModConfig/{CONFIGNAME}', creating default and disabling mod...");
                api.StoreModConfig(new ModConfig(), CONFIGNAME);

                return;
            }

            api.Event.DidUseBlock += E_DidUseBlock;
            api.Event.DidBreakBlock += E_DidBreakBlock;
            api.Event.OnPlayerInteractEntity += E_OnPlayerInteractEntity;
            api.Event.DidPlaceBlock += E_DidPlaceBlock;
        }

        private void E_DidUseBlock(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            _blockAccessLogger.Log(new BlockUseLog(byPlayer, blockSel));
        }

        private void E_DidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            IBlockAccessor blockAccessor = _api.World.GetBlockAccessor(false, false, false, false);
            bool didBreak = blockAccessor.GetBlock(blockSel.Position).BlockId != oldblockId;

            if (didBreak)
            {
                _blockBreakLogger.Log(new BlockBreakLog(byPlayer, oldblockId, blockSel));
            }
        }

        private void E_OnPlayerInteractEntity(
            Entity entity,
            IPlayer byPlayer,
            ItemSlot slot,
            Vec3d hitPosition,
            int mode,
            ref EnumHandling handling)
        {
            _entityInteractLogger.Log(new EntityInteractLog(entity, byPlayer, slot, hitPosition));
        }

        private void E_DidPlaceBlock(IServerPlayer byPlayer,
            int oldblockId,
            BlockSelection blockSel,
            ItemStack withItemStack)
        {
            
        }
    }
}