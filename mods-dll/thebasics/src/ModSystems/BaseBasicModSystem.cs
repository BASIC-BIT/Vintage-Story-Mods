using System;
using System.IO;
using thebasics.Configs;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public abstract class BaseBasicModSystem : ModSystem
    {
        private const string AssetDomain = "thebasics";
        public ICoreServerAPI API;
        public ModConfig Config;
        private const string ConfigName = "the_basics.json";

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            API = api;

            RegisterAssetOrigin(api);

            LoadConfig();

            BasicStartServerSide();
        }

        protected abstract void BasicStartServerSide();

        private void RegisterAssetOrigin(ICoreServerAPI api)
        {
            if (string.IsNullOrEmpty(Mod.SourcePath))
            {
                return;
            }

            try
            {
                if (Directory.Exists(Mod.SourcePath))
                {
                    var assetsPath = Path.Combine(Mod.SourcePath, "assets");
                    if (Directory.Exists(assetsPath))
                    {
                        api.Assets.AddModOrigin(AssetDomain, assetsPath);
                    }

                    return;
                }

                if (File.Exists(Mod.SourcePath))
                {
                    api.Assets.AddModOrigin(AssetDomain, Mod.SourcePath);
                }
            }
            catch (Exception ex)
            {
                api.Logger.Warning($"THEBASICS: Failed to register asset origin: {ex.Message}");
            }
        }

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
                Config.InitializeDefaultsIfNeeded();
                API.StoreModConfig(this.Config, ConfigName);
                return;
            }

            // Ensure defaults are applied when loading existing/legacy configs (JSON won't trigger ProtoBuf hooks)
            Config.InitializeDefaultsIfNeeded();
            // Optionally persist any backfilled defaults for future runs
            API.StoreModConfig(this.Config, ConfigName);
        }
    }
}