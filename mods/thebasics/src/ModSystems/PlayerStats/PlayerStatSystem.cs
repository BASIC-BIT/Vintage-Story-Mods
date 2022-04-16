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
                if (Config.PlayerStatToggles[PlayerStatType.Deaths] || Config.PlayerStatToggles[PlayerStatType.PlayerKills])
                {
                    API.Event.PlayerDeath += OnPlayerDeath;
                }

                if (Config.PlayerStatToggles[PlayerStatType.NpcKills])
                {
                    API.Event.OnEntityDeath += OnEntityDeath;
                }

                API.RegisterCommand("playerstats", "Get your player stats, or another players", "/playerstats (name)",
                    GetStats);
            }
        }

        private void GetStats(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (args.Length > 2)
            {
                player.SendMessage(groupId, "Usage: /playerstats (name)", EnumChatType.CommandError);
            }

            var otherPlayer = args.Length > 0;
            var targetPlayer = otherPlayer ? API.GetPlayerByName(args[0]) : player;

            if (targetPlayer == null)
            {
                player.SendMessage(groupId, "Could not find target player", EnumChatType.CommandError);
                return;
            }

            var message = new StringBuilder();
            var messageName = otherPlayer ? ChatHelper.Build(targetPlayer.PlayerName, "'s") : "Your";
            message.Append(messageName);
            message.Append(" Stats:\n");

            foreach (var stat in StatTypes.Types)
            {
                if (stat.Value.Enabled(Config))
                {
                    message.Append(ChatHelper.Build(stat.Value.Title, ": ", targetPlayer.GetPlayerStat(stat.Key).ToString(), "\n"));
                }
            }

            player.SendMessage(groupId, message.ToString(), EnumChatType.CommandSuccess);
        }

        private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
        {
            if (Config.PlayerStatToggles[PlayerStatType.Deaths])
            {
                byPlayer.AddPlayerStat(PlayerStatType.Deaths);
            }

            if (Config.PlayerStatToggles[PlayerStatType.PlayerKills] && damageSource.Source == EnumDamageSource.Player)
            {
                var player = damageSource.SourceEntity.GetPlayer();
                player.AddPlayerStat(PlayerStatType.PlayerKills);
            }
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (Config.PlayerStatEnabled(PlayerStatType.NpcKills) &&
                entity.GetPlayer() == null &&
                damageSource.Source == EnumDamageSource.Player)
            {
                var player = damageSource.SourceEntity.GetPlayer();
                player.AddPlayerStat(PlayerStatType.NpcKills);
            }
        }
    }
}