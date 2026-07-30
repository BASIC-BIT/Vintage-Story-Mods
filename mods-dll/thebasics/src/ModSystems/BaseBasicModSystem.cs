using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.Analytics;
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

        /// <summary>
        /// Single exit on purpose. This has four ways of producing a config (loaded, JSON-string
        /// repaired, fallback after a parse failure, and freshly created), and validation warnings
        /// have to cover all of them. Validating at each return is how the repaired path silently
        /// skipped it.
        /// </summary>
        private static ModConfig LoadConfigFromDisk(ICoreServerAPI api)
        {
            var config = LoadOrRecoverConfig(api);
            LogConfigValidationWarnings(api, config);
            return config;
        }

        private static ModConfig LoadOrRecoverConfig(ICoreServerAPI api)
        {
            ModConfig config;

            try
            {
                config = api.LoadModConfig<ModConfig>(ConfigName);
            }
            catch (Exception e)
            {
                return TryRepairJsonStringConfig(api) ?? CreateFallbackConfig(api, e);
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

        /// <summary>
        /// The admin panel rejects invalid combinations on save, but a hand-edited file never passes
        /// through that path. Warn rather than reject: refusing to boot on a bad value would be a
        /// worse failure than running with the documented fallback, but the admin needs to know
        /// their setting is not doing what they think.
        /// </summary>
        private static void LogConfigValidationWarnings(ICoreServerAPI api, ModConfig config)
        {
            if (config == null)
            {
                return;
            }

            try
            {
                foreach (var error in ConfigAdminSettingRegistry.ValidateConfig(config))
                {
                    api.Server.LogWarning("The BASICs config: " + error);
                }
            }
            catch (Exception e)
            {
                api.Server.LogWarning("The BASICs: config validation failed to run: " + e.Message);
            }
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
                AnalyticsService.TrackFailure("config", "load", "warning", "json_string_repaired");
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
                AnalyticsService.TrackFailure("config", "load", "critical", "load_failed_using_defaults", exception);
            }

            var config = new ModConfig();
            config.InitializeDefaultsIfNeeded();
            // Intentionally do not overwrite the existing config file here.
            return config;
        }
    }
}
