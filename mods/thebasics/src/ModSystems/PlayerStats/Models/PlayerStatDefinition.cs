using System;
using thebasics.Configs;

namespace thebasics.ModSystems.PlayerStats.Models
{
    public class PlayerStatDefinition
    {
        public Func<ModConfig, bool> Enabled;
        public string Title;
        public string ID;
    }
}