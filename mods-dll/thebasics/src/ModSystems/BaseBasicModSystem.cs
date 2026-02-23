using System;
using thebasics.Configs;
using Newtonsoft.Json;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public abstract class BaseBasicModSystem : ModSystem
    {
        public ICoreServerAPI API;
        public ModConfig Config;
        private const string ConfigName = "the_basics.json";

        private static bool _loggedConfigLoadFailure;
        private static bool _loggedConfigRepair;

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
            catch (Exception e)
            {
                // Fail safe: do not crash mod systems if config deserialization fails.
                // This can happen if the JSON file is corrupted, or if the config file was accidentally stored
                // as a JSON *string* containing JSON (e.g. "{ ... }").

                // Recovery path: if the file is a JSON string containing object JSON, repair it in-place.
                try
                {
                    var maybeJsonString = API.LoadModConfig<string>(ConfigName);
                    if (!string.IsNullOrWhiteSpace(maybeJsonString) && maybeJsonString.TrimStart().StartsWith("{"))
                    {
                        var repaired = JsonConvert.DeserializeObject<ModConfig>(maybeJsonString);
                        if (repaired != null)
                        {
                            repaired.InitializeDefaultsIfNeeded();
                            Config = repaired;
                            API.StoreModConfig(Config, ConfigName);

                            if (!_loggedConfigRepair)
                            {
                                _loggedConfigRepair = true;
                                API.Server.LogWarning($"The BASICs: Repaired malformed config file '{ConfigName}' (was JSON string). Saved corrected config.");
                            }

                            return;
                        }
                    }
                }
                catch
                {
                    // ignore - fall back to defaults
                }

                if (!_loggedConfigLoadFailure)
                {
                    _loggedConfigLoadFailure = true;
                    // Avoid logging the raw exception text: it may contain braces/newlines that some loggers try to format.
                    API.Server.LogError($"The BASICs: Failed to load mod config '{ConfigName}'. Using defaults. (Exception type: {e.GetType().Name})");
                }

                Config = new ModConfig();
                Config.InitializeDefaultsIfNeeded();
                // Intentionally do not overwrite the existing config file here.
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
