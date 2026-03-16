using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using thebasics.Extensions;
using thebasics.ModSystems.PlayerStats.Definitions;
using thebasics.ModSystems.PlayerStats.Extensions;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
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

        private readonly IDictionary<string, EntityPos> _prevPlayerPositions = new Dictionary<string, EntityPos>();

        private void SetupCommands()
        {
            API.ChatCommands.GetOrCreate("playerstats")
                .WithAlias("pstats")
                .WithDescription(Lang.Get("thebasics:stats-cmd-playerstats-desc"))
                .RequiresPrivilege(Privilege.chat)
                .WithArgs(new PlayersArgParser("player", API, false))
                .HandleWith(GetStats);

            API.ChatCommands.GetOrCreate("clearstats")
                .WithDescription(Lang.Get("thebasics:stats-cmd-clearstats-desc"))
                .RequiresPrivilege(Config.PlayerStatClearPermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("confirm", false))
                .HandleWith(ClearStats);

            API.ChatCommands.GetOrCreate("clearstat")
                .WithDescription(Lang.Get("thebasics:stats-cmd-clearstat-desc"))
                .RequiresPrivilege(Config.PlayerStatClearPermission)
                .WithArgs(new PlayersArgParser("player", API, true),
                    new WordArgParser("statName", true),
                    new WordArgParser("confirm", false))
                .HandleWith(ClearOneStat);
        }

        private void SubscribeToEvents()
        {
            if (Config.AnyPlayerStatEnabled(PlayerStatType.Deaths, PlayerStatType.PlayerKills))
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
                    var movement = (int)Math.Round(newPos.DistanceTo(prevPos));
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
            var target = ((PlayerUidName[])args.Parsers[0].GetValue())[0];
            var targetPlayer = API.GetPlayerByUID(target.Uid);
            var targetName = targetPlayer?.PlayerName ?? target.Name ?? target.Uid;

            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:stats-error-player-offline", targetName),
                };
            }

            if (!confirmed)
            {
                return new TextCommandResult()
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = Lang.Get("thebasics:stats-confirm-clearstats", targetName),
                };
            }

            targetPlayer.ClearPlayerStats();
            return new TextCommandResult()
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:stats-success-cleared-all", targetName),
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
            var target = ((PlayerUidName[])args.Parsers[0].GetValue())[0];
            var targetPlayer = API.GetPlayerByUID(target.Uid);
            var targetName = targetPlayer?.PlayerName ?? target.Name ?? target.Uid;

            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:stats-error-player-offline", targetName),
                };
            }


            var resolvedStat = ResolveStat(statName);

            if (resolvedStat == null)
            {
                var statNames = string.Join(", ", StatTypes.Types.Keys.Select(k => k.ToString().ToLowerInvariant()));
                var statIds = string.Join(", ", StatTypes.Types.Values.Select(v => v.ID));
                return new TextCommandResult()
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:stats-error-stat-not-found", statName, statNames, statIds),
                };
            }

            if (!confirmed)
            {
                return new TextCommandResult()
                {
                    Status = EnumCommandStatus.Success,
                    StatusMessage = Lang.Get("thebasics:stats-confirm-clearstat", targetName, statName),
                };
            }

            targetPlayer.ClearPlayerStat(resolvedStat.Value);
            return new TextCommandResult()
            {
                Status = EnumCommandStatus.Success,
                StatusMessage = Lang.Get("thebasics:stats-success-cleared-one", targetName, statName),
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
                    StatusMessage = Lang.Get("thebasics:stats-error-must-be-player"),
                };
            }

            if (isOtherPlayer && otherPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:stats-error-player-not-found"),
                };
            }
            var targetPlayer = isOtherPlayer ? API.GetPlayerByUID(((PlayerUidName[])args.Parsers[0].GetValue())[0].Uid) : player;

            if (targetPlayer == null)
            {
                return new TextCommandResult
                {
                    Status = EnumCommandStatus.Error,
                    StatusMessage = Lang.Get("thebasics:stats-error-player-not-found"),
                };
            }

            var message = new StringBuilder();
            message.Append(isOtherPlayer ? Lang.Get("thebasics:stats-header-other", targetPlayer.PlayerName) : Lang.Get("thebasics:stats-header-own"));

            foreach (var stat in StatTypes.Types)
            {
                if (Config.PlayerStatEnabled(stat.Key))
                {
                    var statValue = targetPlayer.GetPlayerStat(stat.Key);
                    var statTitle = stat.Value.LangKey != null ? Lang.Get(stat.Value.LangKey) : stat.Value.Title;
                    message.Append(ChatHelper.Build("\n", statTitle, ": ", statValue.ToString()));
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
