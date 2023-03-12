using System;
using thebasics.Configs;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public abstract class BaseBasicModSystem : ModSystem
    {
        protected ICoreServerAPI API;
        protected ModConfig Config;
        private const string ConfigName = "the_basics.json";

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            API = api;

            LoadConfig();

            BasicStartServerSide();
        }

        protected abstract void BasicStartServerSide();

        private void LoadConfig()
        {
            try
            {
                Config = API.LoadModConfig<ModConfig>(ConfigName);
            }
            catch (Exception)
            {
                API.Server.LogError("The BASICs: Failed to load mod config!");
                return;
            }

            if (Config == null)
            {
                API.Server.LogNotification("The BASICs: non-existant modconfig at 'ModConfig/" + ConfigName +
                                           "', creating default...");
                Config = new ModConfig();
                API.StoreModConfig(this.Config, ConfigName);
            }
        }
    }
}