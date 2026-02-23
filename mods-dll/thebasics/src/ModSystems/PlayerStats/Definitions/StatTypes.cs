using System.Collections.Generic;
using thebasics.ModSystems.PlayerStats.Models;

namespace thebasics.ModSystems.PlayerStats.Definitions
{
    public static class StatTypes
    {
        public static readonly IDictionary<PlayerStatType, PlayerStatDefinition> Types = new Dictionary<PlayerStatType, PlayerStatDefinition>
        {
            {
                PlayerStatType.Deaths, new PlayerStatDefinition
                {
                    Title = "Deaths",
                    ID = "DEATHS",
                    LangKey = "thebasics:stats-title-deaths",
                }
            },
            {
                PlayerStatType.PlayerKills, new PlayerStatDefinition
                {
                    Title = "Player Kills",
                    ID = "KILLS_PLAYER",
                    LangKey = "thebasics:stats-title-player-kills",
                }
            },
            {
                PlayerStatType.NpcKills, new PlayerStatDefinition
                {
                    Title = "NPC Kills",
                    ID = "KILLS_NPC",
                    LangKey = "thebasics:stats-title-npc-kills",
                }
            },
            {
                PlayerStatType.BlockBreaks, new PlayerStatDefinition
                {
                    Title = "Block Breaks",
                    ID = "BLOCK_BREAKS",
                    LangKey = "thebasics:stats-title-block-breaks",
                }
            },
            {
                PlayerStatType.DistanceTravelled, new PlayerStatDefinition
                {
                    Title = "Distance Travelled",
                    ID = "DISTANCE_TRAVELLED",
                    LangKey = "thebasics:stats-title-distance-travelled",
                }
            },
        };
    }
}