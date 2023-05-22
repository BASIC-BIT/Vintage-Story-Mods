using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.PlayerStats.Definitions;
using thebasics.ModSystems.PlayerStats.Extensions;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.PlayerStats
{
    public class PlayerStatSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            if (Config.PlayerStatSystem)
            {
                if (Config.AnyPlayerStatEnabled(PlayerStatType.Deaths,PlayerStatType.PlayerKills))
                {
                    API.Event.PlayerDeath += OnPlayerDeath;
                }

                if (Config.PlayerStatToggles[PlayerStatType.NpcKills])
                {
                    API.Event.OnEntityDeath += OnEntityDeath;
                }

                API.ChatCommands.GetOrCreate("playerstats")
                    .WithAlias("pstats")
                    .WithDescription("Get your player stats, or another players")
                    .RequiresPrivilege(Privilege.chat)
                    .WithArgs(new PlayersArgParser("player", API, false))
                    .HandleWith(GetStats);
            }
        }

        private TextCommandResult GetStats(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var isOtherPlayer = !args.Parsers[0].IsMissing;
            var otherPlayer = args.Parsers[0].GetValue();

            if (isOtherPlayer && otherPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player.",
                };
            }
            var targetPlayer = isOtherPlayer ? API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid) : player;
            
            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "Cannot find player.",
                };
            }
            
            var message = new StringBuilder();
            message.Append(isOtherPlayer ? ChatHelper.Build(targetPlayer.PlayerName, "'s") : "Your");
            message.Append(" Stats:");

            foreach (var stat in StatTypes.Types)
            {
                if (Config.PlayerStatEnabled(stat.Key))
                {
                    message.Append(ChatHelper.Build("\n", stat.Value.Title, ": ", targetPlayer.GetPlayerStat(stat.Key).ToString()));
                }
            }

            return new TextCommandResult
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = message.ToString(),
            };
        }

        private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
        {
            if (Config.PlayerStatEnabled(PlayerStatType.Deaths))
            {
                byPlayer.AddPlayerStat(PlayerStatType.Deaths);
            }

            if (Config.PlayerStatEnabled(PlayerStatType.PlayerKills) && damageSource.Source == EnumDamageSource.Player)
            {
                var player = (damageSource.CauseEntity ?? damageSource.SourceEntity).GetPlayer();
                player.AddPlayerStat(PlayerStatType.PlayerKills);
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (Config.PlayerStatEnabled(PlayerStatType.NpcKills) &&
                entity.GetPlayer() == null &&
                damageSource != null && 
                damageSource.Source == EnumDamageSource.Player)
            {
                // var wasRangedKill = damageSource.SourceEntity != damageSource.CauseEntity;
                var player = (damageSource.CauseEntity ?? damageSource.SourceEntity).GetPlayer();
                player.AddPlayerStat(PlayerStatType.NpcKills);
            }
        }
    }
}