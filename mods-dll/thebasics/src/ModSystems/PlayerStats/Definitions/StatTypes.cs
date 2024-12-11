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
                }
            },
            {
                PlayerStatType.PlayerKills, new PlayerStatDefinition
                {
                    Title = "Player Kills",
                    ID = "KILLS_PLAYER",
                }
            },
            {
                PlayerStatType.NpcKills, new PlayerStatDefinition
                {
                    Title = "NPC Kills",
                    ID = "KILLS_NPC",
                }
            },
            {
                PlayerStatType.BlockBreaks, new PlayerStatDefinition
                {
                    Title = "Block Breaks",
                    ID = "BLOCK_BREAKS",
                }
            },
            {
                PlayerStatType.DistanceTravelled, new PlayerStatDefinition
                {
                    Title = "Distance Travelled",
                    ID = "DISTANCE_TRAVELLED",
                }
            },
        };
    }
}