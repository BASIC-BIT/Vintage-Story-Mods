using NLog;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace forensicstory.src
{
    public class LoggingSystem : ModSystem
    {
        private ICoreServerAPI _api;
        private NLog.Config.LoggingConfiguration _loggingConfig;
        
        private static string FolderPrefix = "./data/Logs/bigbrother-";
        public static string Extension = ".txt";
        
        private static Logger _blockBreakLogger;
        private static Logger _blockAccessLogger;
        private static Logger _entityInteractLogger;
        
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            _api = api;

            api.Event.DidUseBlock += E_DidUseBlock;
            api.Event.DidBreakBlock += E_DidBreakBlock;
            api.Event.OnPlayerInteractEntity += E_OnPlayerInteractEntity;

            SetupLogging();
        }

        private void SetupLogging()
        {
            _loggingConfig = new NLog.Config.LoggingConfiguration();

            LogManager.Configuration = _loggingConfig;
            
            _blockAccessLogger = RegisterLogEvent<BlockUseLog>("BlockAccess");
            _blockBreakLogger = RegisterLogEvent<BlockBreakLog>("BlockBreak");
            _entityInteractLogger = RegisterLogEvent<EntityInteractLog>("EntityInteract");
            RegisterLogEvent<SystemErrorLog>("BigBrotherError");
            RegisterLogEvent<IgniteBombLog>("IgniteBomb");
            RegisterLogEvent<ExplosionLog>("Explosion");
        }

        private Logger RegisterLogEvent<T>(string name) where T : Log
        {
            var logfile = WrapTarget(new FileTarget(name) { FileName = $"{FolderPrefix}{name}{Extension}" });
            _loggingConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, logfile, name);
            return LogManager.GetLogger(name);
        }

        private Target WrapTarget(Target t)
        {
            return new AsyncTargetWrapper(new BufferingTargetWrapper(new RetryingTargetWrapper(t, 3, 1000)));
        }

        private void E_DidUseBlock(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            _blockAccessLogger.Info(new BlockUseLog(byPlayer, blockSel).FormatLog(_api));
        }

        private void E_DidBreakBlock(IServerPlayer byPlayer, int oldblockId, BlockSelection blockSel)
        {
            IBlockAccessor blockAccessor = _api.World.GetBlockAccessor(false, false, false, false);
            bool didBreak = blockAccessor.GetBlock(blockSel.Position).BlockId != oldblockId;

            if (didBreak)
            {
                _blockBreakLogger.Info(new BlockBreakLog(byPlayer, oldblockId, blockSel).FormatLog(_api));
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
            _entityInteractLogger.Info(new EntityInteractLog(entity, byPlayer, slot, hitPosition).FormatLog(_api));
        }
    }
}