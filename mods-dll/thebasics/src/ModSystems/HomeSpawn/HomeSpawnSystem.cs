using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.Teleportation;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.HomeSpawn;

public class HomeSpawnSystem : BaseBasicModSystem
{
    private const string SpawnLocationSaveDataKey = "thebasics:home-spawn:spawn";
    private const string HomeLocationsModDataKey = "BASIC_HOME_LOCATIONS";
    private const string HomeCooldownModDataKey = "BASIC_HOME_LAST_TELEPORT_TICKS";
    private const string SpawnCooldownModDataKey = "BASIC_SPAWN_LAST_TELEPORT_TICKS";
    private const string StuckCooldownModDataKey = "BASIC_STUCK_LAST_TELEPORT_TICKS";
    private const string TeleportationStuckCommandPrivilegeKey = "Teleportation.StuckCommandPrivilege";
    private const string TeleportationStuckAdminNotifyPrivilegeKey = "Teleportation.StuckAdminNotifyPrivilege";

    protected override void BasicStartServerSide()
    {
        RegisterConfiguredPrivileges();
        RegisterCommands();
    }

    protected override void OnConfigReloaded(IReadOnlySet<string> changedKeys)
    {
        if (changedKeys.Contains(nameof(Config.HomeCommandPrivilege)) ||
            changedKeys.Contains(nameof(Config.SetHomeCommandPrivilege)) ||
            changedKeys.Contains(nameof(Config.SpawnCommandPrivilege)) ||
            changedKeys.Contains(nameof(Config.SetSpawnCommandPrivilege)) ||
            changedKeys.Contains(TeleportationStuckCommandPrivilegeKey) ||
            changedKeys.Contains(TeleportationStuckAdminNotifyPrivilegeKey))
        {
            RegisterConfiguredPrivileges();
            RefreshCommandPrivileges();
        }
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate("home")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-home-desc"))
            .WithArgs(new WordArgParser("home", false))
            .RequiresPrivilege(GetHomePrivilege())
            .RequiresPlayer()
            .HandleWith(HandleHome);

        API.ChatCommands.GetOrCreate("sethome")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-sethome-desc"))
            .WithArgs(new WordArgParser("home", false))
            .RequiresPrivilege(GetSetHomePrivilege())
            .RequiresPlayer()
            .HandleWith(HandleSetHome);

        API.ChatCommands.GetOrCreate("homes")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-homes-desc"))
            .RequiresPrivilege(GetHomePrivilege())
            .RequiresPlayer()
            .HandleWith(HandleHomes);

        API.ChatCommands.GetOrCreate("delhome")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-delhome-desc"))
            .WithArgs(new WordArgParser("home", true))
            .RequiresPrivilege(GetSetHomePrivilege())
            .RequiresPlayer()
            .HandleWith(HandleDelHome);

        API.ChatCommands.GetOrCreate("spawn")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-spawn-desc"))
            .RequiresPrivilege(GetSpawnPrivilege())
            .RequiresPlayer()
            .HandleWith(HandleSpawn);

        API.ChatCommands.GetOrCreate("setspawn")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-setspawn-desc"))
            .RequiresPrivilege(GetSetSpawnPrivilege())
            .RequiresPlayer()
            .HandleWith(HandleSetSpawn);

        API.ChatCommands.GetOrCreate("stuck")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-stuck-desc"))
            .RequiresPrivilege(GetStuckPrivilege())
            .RequiresPlayer()
            .HandleWith(HandleStuck);
    }

    private void RefreshCommandPrivileges()
    {
        API.ChatCommands.Get("home")?.RequiresPrivilege(GetHomePrivilege());
        API.ChatCommands.Get("sethome")?.RequiresPrivilege(GetSetHomePrivilege());
        API.ChatCommands.Get("homes")?.RequiresPrivilege(GetHomePrivilege());
        API.ChatCommands.Get("delhome")?.RequiresPrivilege(GetSetHomePrivilege());
        API.ChatCommands.Get("spawn")?.RequiresPrivilege(GetSpawnPrivilege());
        API.ChatCommands.Get("setspawn")?.RequiresPrivilege(GetSetSpawnPrivilege());
        API.ChatCommands.Get("stuck")?.RequiresPrivilege(GetStuckPrivilege());
    }

    private TextCommandResult HandleSetHome(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var homeName = GetOptionalHomeName(args);
        var nameError = ValidateHomeName(homeName);
        if (nameError != null)
        {
            TrackHomeSpawnFailure("sethome", "set_home", nameError.ErrorCode);
            return nameError;
        }

        var registry = ReadHomeRegistry(player);
        var location = HomeSpawnLocation.From(player.Entity.Pos);
        var maxHomes = GetTeleportationConfig().MaxHomes;
        if (!registry.TrySetHome(homeName, location, maxHomes, out var normalizedName))
        {
            AnalyticsService.TrackCommandUsed("sethome", false, "max_homes");
            AnalyticsService.TrackFeatureUsed("home-spawn", "set_home", false, "max_homes");
            return ErrorMessage(Lang.Get("thebasics:home-spawn-error-max-homes", maxHomes), "max-homes");
        }

        SaveHomeRegistry(player, registry);

        AnalyticsService.TrackCommandUsed("sethome", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "set_home");

        return Success(Lang.Get("thebasics:home-spawn-success-home-set", normalizedName, location.Format()));
    }

    private TextCommandResult HandleHome(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var homeName = GetOptionalHomeName(args);
        var nameError = ValidateHomeName(homeName);
        if (nameError != null)
        {
            TrackHomeSpawnFailure("home", "home", nameError.ErrorCode);
            return nameError;
        }

        var normalizedName = HomeSpawnHomeRegistry.NormalizeName(homeName);
        var registry = ReadHomeRegistry(player);
        if (!registry.TryGetHome(normalizedName, out var location))
        {
            AnalyticsService.TrackCommandUsed("home", false, "home_not_set");
            AnalyticsService.TrackFeatureUsed("home-spawn", "home", false, "home_not_set");
            return ErrorMessage(Lang.Get("thebasics:home-spawn-error-no-home", normalizedName), "home-not-set");
        }

        var cooldownError = TryCheckCooldown(player, HomeCooldownModDataKey, GetTeleportationConfig().HomeCooldownSeconds, "home", "home");
        if (cooldownError != null)
        {
            return cooldownError;
        }

        var gearError = TryValidateTemporalGearForTeleport(player, "home", "home");
        if (gearError != null)
        {
            return gearError;
        }

        return BeginPlayerTeleport(player, GetTeleportationConfig().HomeWarmupSeconds, "home", "home", p => ExecuteHomeTeleport(p, location, normalizedName),
            (p, reason) => TrackCancelledTeleport("home", "home", reason));
    }

    private static TextCommandResult HandleHomes(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var homes = ReadHomeRegistry(player).ListHomes();
        if (homes.Count == 0)
        {
            AnalyticsService.TrackCommandUsed("homes", true, "empty");
            AnalyticsService.TrackFeatureUsed("home-spawn", "list_homes", result: "empty");
            return Success(Lang.Get("thebasics:home-spawn-homes-empty"));
        }

        var message = new StringBuilder(Lang.Get("thebasics:home-spawn-homes-header", homes.Count));
        foreach (var home in homes)
        {
            message.AppendLine();
            message.Append(Lang.Get("thebasics:home-spawn-homes-item", home.Name, home.Location.Format()));
        }

        AnalyticsService.TrackCommandUsed("homes", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "list_homes");
        return Success(message.ToString());
    }

    private static TextCommandResult HandleDelHome(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var homeName = GetOptionalHomeName(args);
        var nameError = ValidateHomeName(homeName);
        if (nameError != null)
        {
            TrackHomeSpawnFailure("delhome", "delete_home", nameError.ErrorCode);
            return nameError;
        }

        var registry = ReadHomeRegistry(player);
        if (!registry.RemoveHome(homeName, out var normalizedName))
        {
            AnalyticsService.TrackCommandUsed("delhome", false, "home_not_set");
            AnalyticsService.TrackFeatureUsed("home-spawn", "delete_home", false, "home_not_set");
            return ErrorMessage(Lang.Get("thebasics:home-spawn-error-no-home", normalizedName), "home-not-set");
        }

        SaveHomeRegistry(player, registry);
        AnalyticsService.TrackCommandUsed("delhome", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "delete_home");
        return Success(Lang.Get("thebasics:home-spawn-success-home-deleted", normalizedName));
    }

    private TextCommandResult HandleSetSpawn(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var location = HomeSpawnLocation.From(player.Entity.Pos);
        API.WorldManager.SaveGame.StoreData(SpawnLocationSaveDataKey, location);
        API.Logger.Audit($"Player {player.PlayerName} ({player.PlayerUID}) set The BASICs spawn to {location.Format()}.");

        AnalyticsService.TrackCommandUsed("setspawn", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "set_spawn");

        return Success(Lang.Get("thebasics:home-spawn-success-spawn-set", location.Format()));
    }

    private TextCommandResult HandleSpawn(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var cooldownError = TryCheckCooldown(player, SpawnCooldownModDataKey, GetTeleportationConfig().SpawnCooldownSeconds, "spawn", "spawn");
        if (cooldownError != null)
        {
            return cooldownError;
        }

        var gearError = TryValidateTemporalGearForTeleport(player, "spawn", "spawn");
        if (gearError != null)
        {
            return gearError;
        }

        var location = GetStoredSpawnLocation() ?? GetDefaultSpawnLocation();
        return BeginPlayerTeleport(player, GetTeleportationConfig().SpawnWarmupSeconds, "spawn", "spawn", p => ExecuteSpawnTeleport(p, location),
            (p, reason) => TrackCancelledTeleport("spawn", "spawn", reason));
    }

    private TextCommandResult HandleStuck(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var cooldownError = TryCheckCooldown(player, StuckCooldownModDataKey, GetTeleportationConfig().StuckCooldownSeconds, "stuck", "stuck");
        if (cooldownError != null)
        {
            return cooldownError;
        }

        var location = GetStoredSpawnLocation() ?? GetDefaultSpawnLocation();
        var warmupSeconds = GetTeleportationConfig().StuckWarmupSeconds;
        var result = BeginPlayerTeleport(player, warmupSeconds, "stuck", "stuck", p => ExecuteStuckTeleport(p, location), OnStuckCancelled);
        if (result.Status == EnumCommandStatus.Success)
        {
            NotifyAdmins(Lang.Get("thebasics:home-spawn-admin-stuck-started", player.PlayerName, player.Entity.Pos.AsBlockPos, warmupSeconds));
            API.Logger.Audit($"Player {player.PlayerName} ({player.PlayerUID}) started /stuck at {player.Entity.Pos.AsBlockPos}.");
        }

        return result;
    }

    private TextCommandResult ExecuteHomeTeleport(IServerPlayer player, HomeSpawnLocation location, string homeName)
    {
        var gearError = TryConsumeTemporalGearForTeleport(player, "home", "home");
        if (gearError != null)
        {
            return gearError;
        }

        Teleport(player, location);
        MarkCooldown(player, HomeCooldownModDataKey);

        AnalyticsService.TrackCommandUsed("home", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "home");

        return Success(Lang.Get("thebasics:home-spawn-success-home-teleported", homeName));
    }

    private TextCommandResult ExecuteSpawnTeleport(IServerPlayer player, HomeSpawnLocation location)
    {
        var gearError = TryConsumeTemporalGearForTeleport(player, "spawn", "spawn");
        if (gearError != null)
        {
            return gearError;
        }

        Teleport(player, location);
        MarkCooldown(player, SpawnCooldownModDataKey);

        AnalyticsService.TrackCommandUsed("spawn", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "spawn");

        return Success(Lang.Get("thebasics:home-spawn-success-spawn-teleported"));
    }

    private TextCommandResult ExecuteStuckTeleport(IServerPlayer player, HomeSpawnLocation location)
    {
        Teleport(player, location);
        MarkCooldown(player, StuckCooldownModDataKey);
        NotifyAdmins(Lang.Get("thebasics:home-spawn-admin-stuck-completed", player.PlayerName));
        API.Logger.Audit($"Player {player.PlayerName} ({player.PlayerUID}) completed /stuck and was teleported to spawn.");

        AnalyticsService.TrackCommandUsed("stuck", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "stuck");

        return Success(Lang.Get("thebasics:home-spawn-success-stuck-teleported"));
    }

    private void OnStuckCancelled(IServerPlayer player, string reason)
    {
        TrackCancelledTeleport("stuck", "stuck", reason);
        NotifyAdmins(Lang.Get("thebasics:home-spawn-admin-stuck-cancelled", player.PlayerName, reason));
        API.Logger.Audit($"Player {player.PlayerName} ({player.PlayerUID}) cancelled /stuck warmup: {reason}.");
    }

    private TextCommandResult BeginPlayerTeleport(IServerPlayer player, int warmupSeconds, string commandName, string featureAction, System.Func<IServerPlayer, TextCommandResult> execute, Action<IServerPlayer, string> onCancelled)
    {
        if (warmupSeconds <= 0)
        {
            return execute(player);
        }

        var teleportation = API.ModLoader.GetModSystem<TeleportationSystem>();
        if (teleportation == null)
        {
            TrackHomeSpawnFailure(commandName, featureAction, "teleport_unavailable");
            return Error("thebasics:teleport-warmup-error-unavailable", "teleport-unavailable");
        }

        var result = teleportation.BeginWarmup(new TeleportWarmupRequest
        {
            Player = player,
            WarmupSeconds = warmupSeconds,
            CancelOnDamage = GetTeleportationConfig().CancelWarmupOnDamage,
            CancelOnInteraction = GetTeleportationConfig().CancelWarmupOnInteraction,
            StartMessage = Lang.Get("thebasics:teleport-warmup-start", warmupSeconds),
            Execute = execute,
            OnCancelled = onCancelled
        });

        if (result.Status == EnumCommandStatus.Success)
        {
            AnalyticsService.TrackFeatureUsed("home-spawn", featureAction + "_warmup_start", properties: new Dictionary<string, object>
            {
                ["warmup_seconds_bucket"] = AnalyticsBuckets.Count(warmupSeconds)
            });
        }
        else
        {
            TrackHomeSpawnFailure(commandName, featureAction, result.ErrorCode ?? "warmup_failed");
        }

        return result;
    }

    private static HomeSpawnHomeRegistry ReadHomeRegistry(IServerPlayer player)
    {
        return player.GetModData<HomeSpawnHomeRegistry>(HomeLocationsModDataKey, new HomeSpawnHomeRegistry()) ?? new HomeSpawnHomeRegistry();
    }

    private static void SaveHomeRegistry(IServerPlayer player, HomeSpawnHomeRegistry registry)
    {
        player.SetModData(HomeLocationsModDataKey, registry);
    }

    private HomeSpawnLocation GetStoredSpawnLocation()
    {
        return API.WorldManager.SaveGame.GetData<HomeSpawnLocation>(SpawnLocationSaveDataKey, null);
    }

    private HomeSpawnLocation GetDefaultSpawnLocation()
    {
        return HomeSpawnLocation.From(API.World.DefaultSpawnPosition);
    }

    private static void Teleport(IServerPlayer player, HomeSpawnLocation location)
    {
        player.Entity.TeleportTo(location.ToEntityPos());
    }

    private TextCommandResult TryValidateTemporalGearForTeleport(IServerPlayer player, string commandName, string featureAction)
    {
        if (!Config.HomeSpawnRequireTemporalGear)
        {
            return null;
        }

        if (TemporalGearUtil.IsPlayerHoldingTemporalGear(player))
        {
            return null;
        }

        AnalyticsService.TrackCommandUsed(commandName, false, "missing_temporal_gear");
        AnalyticsService.TrackFeatureUsed("home-spawn", featureAction, false, "missing_temporal_gear");
        return Error("thebasics:home-spawn-error-need-temporal-gear", "missing-temporal-gear");
    }

    private TextCommandResult TryConsumeTemporalGearForTeleport(IServerPlayer player, string commandName, string featureAction)
    {
        if (!Config.HomeSpawnRequireTemporalGear)
        {
            return null;
        }

        if (!TemporalGearUtil.TryConsumeTemporalGear(player))
        {
            AnalyticsService.TrackCommandUsed(commandName, false, "consume_gear_failed");
            AnalyticsService.TrackFeatureUsed("home-spawn", featureAction, false, "consume_gear_failed");
            return Error("thebasics:home-spawn-error-consume-gear-failed", "consume-gear-failed");
        }

        return null;
    }

    private static TextCommandResult TryCheckCooldown(IServerPlayer player, string modDataKey, int cooldownSeconds, string commandName, string featureAction)
    {
        if (cooldownSeconds <= 0)
        {
            return null;
        }

        var lastTeleportTicks = player.GetModData(modDataKey, 0L);
        if (lastTeleportTicks <= 0)
        {
            return null;
        }

        var remaining = TimeSpan.FromSeconds(cooldownSeconds) - (DateTime.UtcNow - new DateTime(lastTeleportTicks, DateTimeKind.Utc));
        if (remaining <= TimeSpan.Zero)
        {
            return null;
        }

        AnalyticsService.TrackCommandUsed(commandName, false, "cooldown");
        AnalyticsService.TrackFeatureUsed("home-spawn", featureAction, false, "cooldown");
        return ErrorMessage(Lang.Get("thebasics:home-spawn-error-cooldown", FormatDuration(remaining)), "cooldown");
    }

    private static void MarkCooldown(IServerPlayer player, string modDataKey)
    {
        player.SetModData(modDataKey, DateTime.UtcNow.Ticks);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes >= 1)
        {
            return Lang.Get("thebasics:tpa-time-minutes-seconds", (int)duration.TotalMinutes, duration.Seconds);
        }

        return Lang.Get("thebasics:tpa-time-seconds", Math.Max(1, (int)Math.Ceiling(duration.TotalSeconds)));
    }

    private static TextCommandResult ValidateHomeName(string homeName)
    {
        if (HomeSpawnHomeRegistry.IsValidName(homeName, out var errorCode))
        {
            return null;
        }

        return errorCode == "too-long"
            ? ErrorMessage(Lang.Get("thebasics:home-spawn-error-home-name-too-long", HomeSpawnHomeRegistry.MaxHomeNameLength), "home-name-too-long")
            : Error("thebasics:home-spawn-error-home-name-invalid", "home-name-invalid");
    }

    private static string GetOptionalHomeName(TextCommandCallingArgs args)
    {
        return args.Parsers.Count > 0 && !args.Parsers[0].IsMissing
            ? (string)args.Parsers[0].GetValue()
            : null;
    }

    private static void TrackCancelledTeleport(string commandName, string featureAction, string reason)
    {
        TrackHomeSpawnFailure(commandName, featureAction, "warmup_cancelled_" + reason);
    }

    private static void TrackHomeSpawnFailure(string commandName, string featureAction, string result)
    {
        AnalyticsService.TrackCommandUsed(commandName, false, result);
        AnalyticsService.TrackFeatureUsed("home-spawn", featureAction, false, result);
    }

    private void NotifyAdmins(string message)
    {
        var privilege = GetStuckAdminNotifyPrivilege();
        foreach (var player in API.World.AllOnlinePlayers.OfType<IServerPlayer>().Where(player => player.HasPrivilege(privilege)))
        {
            player.SendMessage(GlobalConstants.CurrentChatGroup, message, EnumChatType.Notification);
        }
    }

    private void RegisterConfiguredPrivileges()
    {
        RegisterPrivilegeIfCustom(GetHomePrivilege(), "thebasics:home-spawn-home-privilege-desc");
        RegisterPrivilegeIfCustom(GetSetHomePrivilege(), "thebasics:home-spawn-sethome-privilege-desc");
        RegisterPrivilegeIfCustom(GetSpawnPrivilege(), "thebasics:home-spawn-spawn-privilege-desc");
        RegisterPrivilegeIfCustom(GetSetSpawnPrivilege(), "thebasics:home-spawn-setspawn-privilege-desc");
        RegisterPrivilegeIfCustom(GetStuckPrivilege(), "thebasics:home-spawn-stuck-privilege-desc");
        RegisterPrivilegeIfCustom(GetStuckAdminNotifyPrivilege(), "thebasics:home-spawn-stuck-admin-notify-privilege-desc");
    }

    private void RegisterPrivilegeIfCustom(string privilege, string descriptionKey)
    {
        if (IsBuiltInPrivilege(privilege))
        {
            return;
        }

        API.Permissions.RegisterPrivilege(privilege, Lang.Get(descriptionKey));
    }

    private static bool IsBuiltInPrivilege(string privilege)
    {
        return string.Equals(privilege, Privilege.chat, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(privilege, Privilege.commandplayer, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(privilege, Privilege.controlserver, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(privilege, Privilege.root, StringComparison.OrdinalIgnoreCase);
    }

    private string GetHomePrivilege()
    {
        return NormalizePrivilege(Config.HomeCommandPrivilege, Privilege.chat);
    }

    private string GetSetHomePrivilege()
    {
        return NormalizePrivilege(Config.SetHomeCommandPrivilege, Privilege.chat);
    }

    private string GetSpawnPrivilege()
    {
        return NormalizePrivilege(Config.SpawnCommandPrivilege, Privilege.chat);
    }

    private string GetSetSpawnPrivilege()
    {
        return NormalizePrivilege(Config.SetSpawnCommandPrivilege, Privilege.commandplayer);
    }

    private string GetStuckPrivilege()
    {
        return NormalizePrivilege(GetTeleportationConfig().StuckCommandPrivilege, Privilege.chat);
    }

    private string GetStuckAdminNotifyPrivilege()
    {
        return NormalizePrivilege(GetTeleportationConfig().StuckAdminNotifyPrivilege, Privilege.commandplayer);
    }

    private TeleportationConfig GetTeleportationConfig()
    {
        Config.Teleportation ??= new TeleportationConfig();
        Config.Teleportation.InitializeDefaultsIfNeeded();
        return Config.Teleportation;
    }

    private static string NormalizePrivilege(string configuredPrivilege, string fallback)
    {
        return string.IsNullOrWhiteSpace(configuredPrivilege) ? fallback : configuredPrivilege.Trim();
    }

    private static TextCommandResult Success(string message)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Success,
            StatusMessage = message
        };
    }

    private static TextCommandResult Error(string langKey, string errorCode)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Error,
            StatusMessage = Lang.Get(langKey),
            ErrorCode = errorCode
        };
    }

    private static TextCommandResult ErrorMessage(string message, string errorCode)
    {
        return new TextCommandResult
        {
            Status = EnumCommandStatus.Error,
            StatusMessage = message,
            ErrorCode = errorCode
        };
    }
}
