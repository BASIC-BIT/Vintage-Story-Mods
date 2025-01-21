using System;
using System.Collections.Generic;
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
                SubscribeToEvents();
                SetupCommands();
            }
        }

        private IDictionary<string, EntityPos> _prevPlayerPositions = new Dictionary<string, EntityPos>();

        private void SetupCommands()
        {
            API.ChatCommands.GetOrCreate("playerstats")
                .WithAlias("pstats")
                .WithDescription("Get your player stats, or another players")
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(new PlayersArgParser("player", API, false))
                .HandleWith(GetStats);

            API.ChatCommands.GetOrCreate("clearstats")
                .WithDescription("Clear a players stats")
                .RequiresPrivilege(Config.PlayerStatClearPermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("confirm", false))
                .HandleWith(ClearStats);

            API.ChatCommands.GetOrCreate("clearstat")
                .WithDescription("Clear a specific stat on a player")
                .RequiresPrivilege(Config.PlayerStatClearPermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("statName", true),
                    new WordArgParser("confirm", false))
                .HandleWith(ClearOneStat);
        }

        private void SubscribeToEvents()
        {
            if (Config.AnyPlayerStatEnabled(PlayerStatType.Deaths,PlayerStatType.PlayerKills))
            {
                API.Event.PlayerDeath += OnPlayerDeath;
            }
            if (Config.PlayerStatToggles[PlayerStatType.NpcKills])
            {
                API.Event.OnEntityDeath += OnEntityDeath;
            }
            if (Config.PlayerStatEnabled(PlayerStatType.BlockBreaks))
            {
                API.Event.BreakBlock += OnBreakBlock;
            }
            if (Config.PlayerStatEnabled(PlayerStatType.DistanceTravelled))
            {
                API.Event.RegisterGameTickListener(AddToPlayerMovement, Config.PlayerStatDistanceTravelledTimer);
            }
        }

        private void AddToPlayerMovement(float dt)
        {
            foreach (var player1 in API.World.AllOnlinePlayers)
            {
                var player = (IServerPlayer)player1;
                var newPos = player.Entity.Pos;
                if (_prevPlayerPositions.TryGetValue(player.PlayerUID, out EntityPos prevPos))
                {
                    var movement = (int) Math.Round(newPos.DistanceTo(prevPos));
                    // only track if current game mode is survival
                    if (player.WorldData.CurrentGameMode == EnumGameMode.Survival)
                    {
                        player.AddPlayerStat(PlayerStatType.DistanceTravelled, movement);
                    }
                    _prevPlayerPositions[player.PlayerUID] = newPos.Copy();
                }
                else
                {
                    _prevPlayerPositions[player.PlayerUID] = newPos.Copy();
                }
            }
        }

        private void OnBreakBlock(IServerPlayer byplayer, BlockSelection blocksel, ref float dropquantitymultiplier, ref EnumHandling handling)
        {
            if (byplayer.WorldData.CurrentGameMode == EnumGameMode.Survival)
            {
                byplayer.AddPlayerStat(PlayerStatType.BlockBreaks);
            }
        }

        private TextCommandResult ClearStats(TextCommandCallingArgs args)
        {
            var confirmed = !args.Parsers[1].IsMissing && args.Parsers[1].GetValue().ToString()?.ToLower() == "confirm";
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);

            if (!confirmed)
            {
                return new TextCommandResult()
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage =
                        $"Are you SURE you want to clear this players stats? Type \"/clearstats {targetPlayer.PlayerName} confirm\" to confirm.",
                };
            }
            
            targetPlayer.ClearPlayerStats();
            return new TextCommandResult()
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {targetPlayer.PlayerName} stats cleared.",
            };
        }

        private PlayerStatType? ResolveStat(string input)
        {
            var lowerInput = input.ToLower();
            foreach (var playerStatDefinition in StatTypes.Types)
            {
                if (playerStatDefinition.Key.ToString().ToLower() == lowerInput ||
                    playerStatDefinition.Value.Title.ToLower() == lowerInput ||
                    playerStatDefinition.Value.ID.ToLower() == lowerInput)
                {
                    return playerStatDefinition.Key;
                }
            }

            return null;
        }
        private TextCommandResult ClearOneStat(TextCommandCallingArgs args)
        {
            var confirmed = !args.Parsers[2].IsMissing && args.Parsers[2].GetValue().ToString()?.ToLower() == "confirm";
            var statName = args.Parsers[1].GetValue().ToString()!.ToLower();
            var targetPlayer = API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid);

            
            var resolvedStat = ResolveStat(statName);
            
            if(resolvedStat == null)
            {
                return new TextCommandResult()
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = $"Cannot find stat {statName}.",
                };
            }
            
            if (!confirmed)
            {
                return new TextCommandResult()
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage =
                        $"Are you SURE you want to clear this players stats? Type \"/clearstats {targetPlayer.PlayerName} {statName} confirm\" to confirm.",
                };
            }

            targetPlayer.ClearPlayerStat(resolvedStat.Value);
            return new TextCommandResult()
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = $"Player {targetPlayer.PlayerName} stat {statName} cleared.",
            };
        }

        private TextCommandResult GetStats(TextCommandCallingArgs args)
        {
            var player = API.GetPlayerByUID(args.Caller.Player.PlayerUID);
            var isOtherPlayer = !args.Parsers[0].IsMissing;
            var otherPlayer = args.Parsers[0].GetValue();

            if (!isOtherPlayer && args.Caller.Type != EnumCallerType.Player)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = "You must be a player to use this command.",
                };
            }

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
                    var statValue = targetPlayer.GetPlayerStat(stat.Key);
                    message.Append(ChatHelper.Build("\n", stat.Value.Title, ": ", statValue.ToString()));
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
                damageSource is { Source: EnumDamageSource.Player })
            {
                // var wasRangedKill = damageSource.SourceEntity != damageSource.CauseEntity;
                var player = (damageSource.CauseEntity ?? damageSource.SourceEntity).GetPlayer();
                player.AddPlayerStat(PlayerStatType.NpcKills);
            }
        }
    }
}