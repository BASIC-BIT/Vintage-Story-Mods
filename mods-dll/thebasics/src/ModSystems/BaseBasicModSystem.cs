using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using thebasics.Configs;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.ModSystems
{
    public abstract class BaseBasicModSystem : ModSystem
    {
        public ICoreServerAPI API;
        public ModConfig Config;
        protected const string ConfigName = "the_basics.json";

        private static bool _loggedConfigLoadFailure;
        private static bool _loggedConfigRepair;
        private static ModConfig _sharedConfig;
        private static readonly List<BaseBasicModSystem> LoadedSystems = new();

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            API = api;

            Config = GetOrLoadSharedConfig(api);
            if (!LoadedSystems.Contains(this))
            {
                LoadedSystems.Add(this);
            }

            BasicStartServerSide();
        }

        protected abstract void BasicStartServerSide();

        public override void Dispose()
        {
            LoadedSystems.Remove(this);
            base.Dispose();
        }

        protected virtual void OnConfigReloaded(IReadOnlySet<string> changedKeys)
        {
        }

        protected static void NotifyConfigReloaded(IReadOnlySet<string> changedKeys)
        {
            foreach (var system in LoadedSystems.ToArray())
            {
                system.OnConfigReloaded(changedKeys);
            }
        }

        protected static ModConfig CloneConfig(ModConfig source)
        {
            var json = JsonConvert.SerializeObject(source);
            var clone = JsonConvert.DeserializeObject<ModConfig>(json) ?? new ModConfig();
            clone.InitializeDefaultsIfNeeded();
            return clone;
        }

        protected static void CopyConfigValues(ModConfig source, ModConfig target)
        {
            var json = JsonConvert.SerializeObject(source);
            JsonConvert.PopulateObject(json, target, new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            });
            target.InitializeDefaultsIfNeeded();
        }

        protected static ModConfig ReloadSharedConfigFromDisk(ICoreServerAPI api)
        {
            var loaded = LoadConfigFromDisk(api);
            if (_sharedConfig == null)
            {
                _sharedConfig = loaded;
            }
            else
            {
                CopyConfigValues(loaded, _sharedConfig);
            }

            return _sharedConfig;
        }

        protected static void SaveSharedConfig(ICoreServerAPI api)
        {
            if (_sharedConfig != null)
            {
                api.StoreModConfig(_sharedConfig, ConfigName);
            }
        }

        private static ModConfig GetOrLoadSharedConfig(ICoreServerAPI api)
        {
            if (_sharedConfig == null)
            {
                _sharedConfig = LoadConfigFromDisk(api);
            }

            return _sharedConfig;
        }

        private static ModConfig LoadConfigFromDisk(ICoreServerAPI api)
        {
            ModConfig config;

            try
            {
                config = api.LoadModConfig<ModConfig>(ConfigName);
            }
            catch (Exception e)
            {
                // Fail safe: do not crash mod systems if config deserialization fails.
                // This can happen if the JSON file is corrupted, or if the config file was accidentally stored
                // as a JSON *string* containing JSON (e.g. "{ ... }").

                // Recovery path: if the file is a JSON string containing object JSON, repair it in-place.
                try
                {
                    var maybeJsonString = api.LoadModConfig<string>(ConfigName);
                    if (!string.IsNullOrWhiteSpace(maybeJsonString) && maybeJsonString.TrimStart().StartsWith('{'))
                    {
                        var repaired = JsonConvert.DeserializeObject<ModConfig>(maybeJsonString);
                        if (repaired != null)
                        {
                            repaired.InitializeDefaultsIfNeeded();
                            api.StoreModConfig(repaired, ConfigName);

                            if (!_loggedConfigRepair)
                            {
                                _loggedConfigRepair = true;
                                api.Server.LogWarning($"The BASICs: Repaired malformed config file '{ConfigName}' (was JSON string). Saved corrected config.");
                            }

                            return repaired;
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
                    api.Server.LogError($"The BASICs: Failed to load mod config '{ConfigName}'. Using defaults. (Exception type: {e.GetType().Name})");
                }

                config = new ModConfig();
                config.InitializeDefaultsIfNeeded();
                // Intentionally do not overwrite the existing config file here.
                return config;
            }

            if (config == null)
            {
                api.Server.LogNotification("The BASICs: non-existant modconfig at 'ModConfig/" + ConfigName +
                                           "', creating default...");
                config = new ModConfig();
                config.InitializeDefaultsIfNeeded();
                api.StoreModConfig(config, ConfigName);
                return config;
            }

            // Ensure defaults are applied when loading existing/legacy configs (JSON won't trigger ProtoBuf hooks)
            config.InitializeDefaultsIfNeeded();
            // Optionally persist any backfilled defaults for future runs
            api.StoreModConfig(config, ConfigName);
            return config;
        }
    }
}
