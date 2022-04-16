using System.Collections.Generic;
using System.Text;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public class PlayerStatSystem : BaseBasicModSystem
    {
        private class StatDefinition
        {
            public Func<ModConfig, bool> Enabled;
            public string Title;
            public Func<IServerPlayer, string> Stat;
        }

        private List<StatDefinition> _stats = new List<StatDefinition>
        {
            new StatDefinition
            {
                Enabled = config => config.TrackPlayerDeaths,
                Stat = player => player.GetDeathCount().ToString(),
                Title = "Deaths",
            },
            new StatDefinition
            {
                Enabled = config => config.TrackPlayerOnPlayerKills,
                Stat = player => player.GetPlayerKillCount().ToString(),
                Title = "Player Kills",
            },
            new StatDefinition
            {
                Enabled = config => config.TrackPlayerOnNpcKills,
                Stat = player => player.GetNpcKillCount().ToString(),
                Title = "NPC Kills",
            },
        };
        
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

            if (targetPlayer == null)
            {
                player.SendMessage(groupId, "Could not find target player", EnumChatType.CommandError);
                return;
            }

            var message = new StringBuilder();
            var messageName = otherPlayer ? ChatHelper.Build(targetPlayer.PlayerName, "'s") : "Your";
            message.Append(messageName);
            message.Append(" Stats:\n");
            
            foreach (var stat in _stats)
            {
                if (stat.Enabled(Config))
                {
                    message.Append(stat.Title + ": ");
                    message.Append(stat.Stat(targetPlayer));
                    message.Append("\n");
                }
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