using System.Collections.Generic;
using System.Linq;
using thebasics.Configs;
using thebasics.ModSystems.PlayerStats.Models;

namespace thebasics.ModSystems.PlayerStats.Extensions
{
    public static class ModConfigExtensions
    {
        public static bool AllPlayerStatsEnabled(this ModConfig config, List<PlayerStatType> types)
        {
            return types.All(config.PlayerStatEnabled);
        }
        public static bool AnyPlayerStatEnabled(this ModConfig config, List<PlayerStatType> types)
        {
            return types.Any(config.PlayerStatEnabled);
        }
        
        public static bool PlayerStatEnabled(this ModConfig config, PlayerStatType type)
        {
            return Definitions.StatTypes.Types[type].Enabled(config);
        }
    }
}