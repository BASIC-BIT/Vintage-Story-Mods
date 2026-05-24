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
        public ICoreServerAPI API { get; set; }
        public ModConfig Config { get; set; }
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
                var repaired = TryRepairJsonStringConfig(api);
                if (repaired != null)
                {
                    return repaired;
                }

                return CreateFallbackConfig(api, e);
            }

            if (config == null)
            {
                api.Server.LogNotification("The BASICs: non-existent modconfig at 'ModConfig/" + ConfigName +
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

        private static ModConfig TryRepairJsonStringConfig(ICoreServerAPI api)
        {
            try
            {
                var maybeJsonString = api.LoadModConfig<string>(ConfigName);
                if (string.IsNullOrWhiteSpace(maybeJsonString) || !maybeJsonString.TrimStart().StartsWith('{'))
                {
                    return null;
                }

                var repaired = JsonConvert.DeserializeObject<ModConfig>(maybeJsonString);
                if (repaired == null)
                {
                    return null;
                }

                repaired.InitializeDefaultsIfNeeded();
                api.StoreModConfig(repaired, ConfigName);
                LogConfigRepairOnce(api);
                return repaired;
            }
            catch
            {
                return null;
            }
        }

        private static void LogConfigRepairOnce(ICoreServerAPI api)
        {
            if (_loggedConfigRepair)
            {
                return;
            }

            _loggedConfigRepair = true;
            api.Server.LogWarning($"The BASICs: Repaired malformed config file '{ConfigName}' (was JSON string). Saved corrected config.");
        }

        private static ModConfig CreateFallbackConfig(ICoreServerAPI api, Exception exception)
        {
            if (!_loggedConfigLoadFailure)
            {
                _loggedConfigLoadFailure = true;
                api.Server.LogError($"The BASICs: Failed to load mod config '{ConfigName}'. Using defaults. (Exception type: {exception.GetType().Name})");
            }

            var config = new ModConfig();
            config.InitializeDefaultsIfNeeded();
            // Intentionally do not overwrite the existing config file here.
            return config;
        }
    }
}
