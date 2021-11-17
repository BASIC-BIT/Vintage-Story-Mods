using System;
using System.Globalization;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace forensicstory
{
    public abstract class Log
    {
        protected readonly DateTime LogTime = DateTime.Now;
        public abstract string FormatLog(ICoreAPI api);

        public abstract string FileName { get; }
    }

    public abstract class PositionLog : Log
    {
        public Vec3d Pos { get; set; }
    }

    public class BlockUseLog : PositionLog
    {
        private readonly IServerPlayer _byPlayer;
        private readonly BlockSelection _blockSel;

        public override string FileName => "BlockAccess";

        public BlockUseLog(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            _byPlayer = byPlayer;
            _blockSel = blockSel;
            Pos = _blockSel.Position.ToVec3d();
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

    public class EntityInteractLog : PositionLog
    {
        private readonly Entity _entity;
        private readonly IPlayer _byPlayer;
        private readonly ItemSlot _slot;
        private readonly Vec3d _hitPosition;

        public override string FileName => "EntityInteract";

        public EntityInteractLog(
            Entity entity,
            IPlayer byPlayer,
            ItemSlot slot,
            Vec3d hitPosition)
        {
            _entity = entity;
            _byPlayer = byPlayer;
            _slot = slot;
            _hitPosition = hitPosition;
            Pos = _entity.Pos.XYZ;
        }

        public override string FormatLog(ICoreAPI api)
        {
            String ingameDate = api.World.Calendar.PrettyDate();
            String playerId = _byPlayer.PlayerUID;
            String playerName = _byPlayer.PlayerName;
            String itemName = _slot.GetStackName() ?? "Hands";

            return playerName + " | " + playerId + " | " + itemName + " | Position:" + _entity.Pos.GetPrettyString() + " | " +
                   _entity.GetName() + " | Ingame date: " + ingameDate + " | " + DateTime.Now;
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

    public class BlockBreakLog : PositionLog
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
            Pos = _blockSel.Position.ToVec3d();
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
    public class IgniteBombLog : PositionLog
    {
        private readonly EntityAgent _byEntity;
        private readonly BlockPos _pos;

        public override string FileName => "IgniteBomb";
        
        public IgniteBombLog(EntityAgent byEntity, BlockPos pos)
        {
            _byEntity = byEntity;
            _pos = pos;
            Pos = _pos.ToVec3d();
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
    public class ExplosionLog : PositionLog
    {
        private readonly BlockPos _pos;

        public override string FileName => "Explosion";
        
        public ExplosionLog(BlockPos pos)
        {
            _pos = pos;
            Pos = _pos.ToVec3d();
        }

        public override string FormatLog(ICoreAPI api)
        {

            StringBuilder log = new StringBuilder();

            String ingameDate = api.World.Calendar.PrettyDate();
            log.AddLogSection("Position", _pos.GetPrettyString());
            log.AddLogSection("Ingame date", ingameDate);
            log.AddLogSection("Time", DateTime.Now.ToString(CultureInfo.InvariantCulture));

            return log.ToString();
        }
    }
    public class PlaceBombLog : PositionLog
    {
        private readonly IServerPlayer _byPlayer;
        private readonly BlockSelection _blockSel;

        public override string FileName => "IgniteBomb";
        
        public PlaceBombLog(IServerPlayer byPlayer, BlockSelection blockSel)
        {
            _byPlayer = byPlayer;
            _blockSel = blockSel;
            Pos = _blockSel.Position.ToVec3d();
        }

        public override string FormatLog(ICoreAPI api)
        {

            StringBuilder log = new StringBuilder();

            log.Append(_byPlayer.PlayerName);
            log.AddSeparator();
            log.Append(_byPlayer.PlayerUID);
            
            log.AddSeparator();

            String ingameDate = api.World.Calendar.PrettyDate();
            log.AddLogSection("Position", _blockSel.Position.GetPrettyString());
            log.AddLogSection("Ingame date", ingameDate);
            log.AddLogSection("Time", DateTime.Now.ToString(CultureInfo.InvariantCulture));

            return log.ToString();
        }
    }
}