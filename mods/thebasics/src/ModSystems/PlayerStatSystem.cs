﻿using System.Text;
using thebasics.Extensions;
using thebasics.Utilities;
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

                API.RegisterCommand("playerstats", "Get your player stats, or another players", "/playerstats (name)", GetStats);
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
                
            var message = new StringBuilder();
            var messageName = otherPlayer ? ChatHelper.Build(targetPlayer.PlayerName, "'s") : "Your";
            message.Append(messageName);
            message.Append(" Stats:\n");
            if (Config.TrackPlayerDeaths)
            {
                message.Append("Deaths: ");
                message.Append(targetPlayer.GetDeathCount());
                message.Append("\n");
            }

            if (Config.TrackPlayerOnPlayerKills)
            {
                message.Append("Player Kills: ");
                message.Append(targetPlayer.GetPlayerKillCount());
                message.Append("\n");
            }

            if (Config.TrackPlayerOnNpcKills)
            {
                message.Append("NPC Kills: ");
                message.Append(targetPlayer.GetNpcKillCount());
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