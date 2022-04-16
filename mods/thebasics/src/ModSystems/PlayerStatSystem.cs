using System.Text;
using thebasics.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public class PlayerStatSystem : BaseBasicModSystem
    {
        protected override void BasicStartServerSide()
        {
            if (Config.PlayerStatSystem)
            {
                if (Config.TrackPlayerDeaths || Config.TrackPlayerOnPlayerKills)
                {
                    API.Event.PlayerDeath += OnPlayerDeath;
                }

                if (Config.TrackPlayerOnNpcKills)
                {
                    API.Event.OnEntityDeath += OnEntityDeath;
                }

                API.RegisterCommand("playerstats", "Get your player stats", "/stats", GetStats);
            }
        }

        private void GetStats(IServerPlayer player, int groupId, CmdArgs args)
        {
            var message = new StringBuilder();
            message.Append("Player Stats:\n");
            if (Config.TrackPlayerDeaths)
            {
                message.Append("Deaths: ");
                message.Append(player.GetDeathCount());
                message.Append("\n");
            }

            if (Config.TrackPlayerOnPlayerKills)
            {
                message.Append("Player Kills: ");
                message.Append(player.GetPlayerKillCount());
                message.Append("\n");
            }

            if (Config.TrackPlayerOnNpcKills)
            {
                message.Append("NPC Kills: ");
                message.Append(player.GetNpcKillCount());
                message.Append("\n");
            }
            
            player.SendMessage(groupId, message.ToString(), EnumChatType.CommandSuccess);
        }

        private void OnPlayerDeath(IServerPlayer byPlayer, DamageSource damageSource)
        {
            if (Config.TrackPlayerDeaths)
            {
                byPlayer.AddDeathCount();
            }

            if (Config.TrackPlayerOnPlayerKills && damageSource.Source == EnumDamageSource.Player)
            {
                var player = damageSource.SourceEntity.GetPlayer();
                player.AddPlayerKillCount();
            }
        }
        
        private void OnEntityDeath(Entity entity, DamageSource damageSource)
        {
            if (Config.TrackPlayerOnNpcKills && entity.GetPlayer() == null && damageSource.Source == EnumDamageSource.Player)
            {
                var player = damageSource.SourceEntity.GetPlayer();
                player.AddNpcKillCount();
            }
        }
    }
}