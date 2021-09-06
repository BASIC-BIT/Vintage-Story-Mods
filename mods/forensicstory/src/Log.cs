using System;
using System.Globalization;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace forensicstory.src
{
    public abstract class Log
    {
        public abstract string FormatLog(ICoreAPI api);

        public abstract string FileName { get; }
    }

    public class BlockUseLog : Log
    {
        private readonly IServerPlayer _byPlayer;
        private readonly BlockSelection _blockSel;

        public override string FileName => "BlockAccess";

        public BlockUseLog(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            _byPlayer = byPlayer;
            _blockSel = blockSel;
        }

        public override string FormatLog(ICoreAPI api)
        {
            String ingameDate = api.World.Calendar.PrettyDate();
            String playerId = _byPlayer.PlayerUID;
            String playerName = _byPlayer.PlayerName;
            IBlockAccessor blockAccessor = api.World.GetBlockAccessor(false, false, false, false);
            String blockName = blockAccessor.GetBlock(_blockSel.Position).GetPlacedBlockName(api.World, _blockSel.Position);

            return playerName + " | " + playerId + " | Position:" + _blockSel.Position.GetPrettyString() + " | " +
                   blockName + " | Ingame date: " + ingameDate + " | " + DateTime.Now;
        }
    }

    public class SystemErrorLog : Log
    {
        private readonly Exception _e;

        public override string FileName => "BigBrotherError";

        public SystemErrorLog(Exception e)
        {
            _e = e;
        }

        public override string FormatLog(ICoreAPI api)
        {
            return _e.ToString();
        }
    }

    public class BlockBreakLog : Log
    {
        private readonly IServerPlayer _byPlayer;
        private readonly BlockSelection _blockSel;
        private readonly int _oldblockId;

        public override string FileName => "BlockBreak";
        
        public BlockBreakLog(IServerPlayer byPlayer,
            int oldblockId,
            BlockSelection blockSel)
        {
            _byPlayer = byPlayer;
            _blockSel = blockSel;
            _oldblockId = oldblockId;
        }

        public override string FormatLog(ICoreAPI api)
        {
            String ingameDate = api.World.Calendar.PrettyDate();
            String playerId = _byPlayer.PlayerUID;
            String playerName = _byPlayer.PlayerName;
            IBlockAccessor blockAccessor = api.World.GetBlockAccessor(false, false, false);
            String blockName = blockAccessor.GetBlock(_oldblockId).ToString();

            return playerName + " | " + playerId + " | Position: " + _blockSel.Position.GetPrettyString() + " | Block ID:" +
                   blockName + " | Ingame date: " + ingameDate + " | " + DateTime.Now;
        }
    }
    public class ExplosionLog : Log
    {
        private readonly EntityAgent _byEntity;
        private readonly BlockPos _pos;

        public override string FileName => "IgniteBomb";
        
        public ExplosionLog(EntityAgent byEntity, BlockPos pos)
        {
            _byEntity = byEntity;
            _pos = pos;
        }

        public override string FormatLog(ICoreAPI api)
        {

            StringBuilder log = new StringBuilder();
            if (_byEntity is EntityPlayer)
            {
                var player = (_byEntity as EntityPlayer).Player;
                log.Append(player.PlayerName);
                log.AddSeparator();
                log.Append(player.PlayerUID);
            }
            else
            {
                log.Append("Nonplayer");
            }
            log.AddSeparator();

            String ingameDate = api.World.Calendar.PrettyDate();
            log.AddLogSection("Position", _pos.GetPrettyString());
            log.AddLogSection("Ingame date", ingameDate);
            log.AddLogSection("Time", DateTime.Now.ToString(CultureInfo.InvariantCulture));

            return log.ToString();
        }
    }
}