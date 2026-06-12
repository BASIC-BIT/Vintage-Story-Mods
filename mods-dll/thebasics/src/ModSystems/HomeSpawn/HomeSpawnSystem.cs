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
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.HomeSpawn;

public class HomeSpawnSystem : BaseBasicModSystem
{
    private const string SpawnLocationSaveDataKey = "thebasics:home-spawn:spawn";
    private const string HomeLocationsModDataKey = "BASIC_HOME_LOCATIONS";
    private const string HomeCooldownModDataKey = "BASIC_HOME_LAST_TELEPORT_TICKS";
    private const string SpawnCooldownModDataKey = "BASIC_SPAWN_LAST_TELEPORT_TICKS";
    private const string StuckCooldownModDataKey = "BASIC_STUCK_LAST_TELEPORT_TICKS";
    private const string TopCooldownModDataKey = "BASIC_TOP_LAST_TELEPORT_TICKS";
    private const string BackCooldownModDataKey = "BASIC_BACK_LAST_TELEPORT_TICKS";
    private const string TeleportationStuckCommandPrivilegeKey = "Teleportation.StuckCommandPrivilege";
    private const string TeleportationStuckAdminNotifyPrivilegeKey = "Teleportation.StuckAdminNotifyPrivilege";
    private const string TeleportationStuckBlockedByOnlinePrivilegeKey = "Teleportation.StuckBlockedByOnlinePrivilege";
    private const string TeleportationTopCommandPrivilegeKey = "Teleportation.TopCommandPrivilege";
    private const string TeleportationBackCommandPrivilegeKey = "Teleportation.BackCommandPrivilege";
    private static readonly (int X, int Z)[] TopSearchOffsets =
    [
        (0, 0),
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1),
        (1, 1),
        (1, -1),
        (-1, 1),
        (-1, -1)
    ];

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
            changedKeys.Contains(TeleportationStuckAdminNotifyPrivilegeKey) ||
            changedKeys.Contains(TeleportationStuckBlockedByOnlinePrivilegeKey) ||
            changedKeys.Contains(TeleportationTopCommandPrivilegeKey) ||
            changedKeys.Contains(TeleportationBackCommandPrivilegeKey))
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
            .WithArgs(new WordArgParser("home", false))
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

        API.ChatCommands.GetOrCreate("top")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-top-desc"))
            .RequiresPrivilege(GetTopPrivilege())
            .RequiresPlayer()
            .HandleWith(HandleTop);

        API.ChatCommands.GetOrCreate("back")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-back-desc"))
            .RequiresPrivilege(GetBackPrivilege())
            .RequiresPlayer()
            .HandleWith(HandleBack);
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
        API.ChatCommands.Get("top")?.RequiresPrivilege(GetTopPrivilege());
        API.ChatCommands.Get("back")?.RequiresPrivilege(GetBackPrivilege());
    }

    private TextCommandResult HandleSetHome(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var homeName = GetOptionalHomeName(args);
        var nameError = ValidateHomeName("sethome", homeName);
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
        var nameError = ValidateHomeName("home", homeName);
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
            return ErrorMessage(MissingHomeMessage(normalizedName), "home-not-set");
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

        return BeginPlayerTeleport(player, GetTeleportationConfig().HomeWarmupSeconds, "home", p => ExecuteHomeTeleport(p, location, normalizedName),
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
        var nameError = ValidateHomeName("delhome", homeName);
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
            return ErrorMessage(MissingHomeMessage(normalizedName), "home-not-set");
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
        return BeginPlayerTeleport(player, GetTeleportationConfig().SpawnWarmupSeconds, "spawn", p => ExecuteSpawnTeleport(p, location),
            (p, reason) => TrackCancelledTeleport("spawn", "spawn", reason));
    }

    private TextCommandResult HandleStuck(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var adminOnlinePrivilege = GetStuckBlockedByOnlinePrivilege();
        if (!string.IsNullOrWhiteSpace(adminOnlinePrivilege) && IsOtherPlayerWithPrivilegeOnline(player, adminOnlinePrivilege))
        {
            TrackHomeSpawnFailure("stuck", "stuck", "admin_online");
            return Error("thebasics:home-spawn-error-stuck-admin-online", "admin-online");
        }

        var cooldownError = TryCheckCooldown(player, StuckCooldownModDataKey, GetTeleportationConfig().StuckCooldownSeconds, "stuck", "stuck");
        if (cooldownError != null)
        {
            return cooldownError;
        }

        var location = GetStoredSpawnLocation() ?? GetDefaultSpawnLocation();
        var warmupSeconds = GetTeleportationConfig().StuckWarmupSeconds;
        var result = BeginPlayerTeleport(player, warmupSeconds, "stuck", p => ExecuteStuckTeleport(p, location), OnStuckCancelled,
            GetTeleportationConfig().StuckReminderIntervalSeconds,
            remainingSeconds => Lang.Get("thebasics:home-spawn-stuck-reminder", FormatDuration(TimeSpan.FromSeconds(remainingSeconds))));
        if (result.Status == EnumCommandStatus.Success && warmupSeconds > 0)
        {
            NotifyAdmins(Lang.Get("thebasics:home-spawn-admin-stuck-started", player.PlayerName, player.Entity.Pos.AsBlockPos, warmupSeconds));
            API.Logger.Audit($"Player {player.PlayerName} ({player.PlayerUID}) started /stuck at {player.Entity.Pos.AsBlockPos}.");
        }

        return result;
    }

    private TextCommandResult HandleTop(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var cooldownError = TryCheckCooldown(player, TopCooldownModDataKey, GetTeleportationConfig().TopCooldownSeconds, "top", "top");
        if (cooldownError != null)
        {
            return cooldownError;
        }

        if (!TryFindTopLocation(player, out var location))
        {
            TrackHomeSpawnFailure("top", "top", "no_safe_destination");
            return Error("thebasics:home-spawn-error-no-safe-top", "no-safe-top");
        }

        return BeginPlayerTeleport(player, GetTeleportationConfig().TopWarmupSeconds, "top", p => ExecuteTopTeleport(p, location),
            (p, reason) => TrackCancelledTeleport("top", "top", reason));
    }

    private TextCommandResult HandleBack(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        if (!TeleportBackUtil.TryGetPreviousLocation(player, GetTeleportationConfig().BackExpiresAfterSeconds, out var location, out var expired))
        {
            var result = expired ? "back_expired" : "back_not_set";
            TrackHomeSpawnFailure("back", "back", result);
            return Error(expired ? "thebasics:home-spawn-error-back-expired" : "thebasics:home-spawn-error-no-back", result);
        }

        if (!location.IsSameDimensionAs(player.Entity.Pos))
        {
            TrackHomeSpawnFailure("back", "back", "back_dimension_mismatch");
            return Error("thebasics:home-spawn-error-back-dimension-mismatch", "back-dimension-mismatch");
        }

        var cooldownError = TryCheckCooldown(player, BackCooldownModDataKey, GetTeleportationConfig().BackCooldownSeconds, "back", "back");
        if (cooldownError != null)
        {
            return cooldownError;
        }

        var gearError = TryValidateTemporalGearForTeleport(player, GetTeleportationConfig().BackRequireTemporalGear, "back", "back");
        if (gearError != null)
        {
            return gearError;
        }

        return BeginPlayerTeleport(player, GetTeleportationConfig().BackWarmupSeconds, "back", p => ExecuteBackTeleport(p, location),
            (p, reason) => TrackCancelledTeleport("back", "back", reason));
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

    private static TextCommandResult ExecuteTopTeleport(IServerPlayer player, HomeSpawnLocation location)
    {
        Teleport(player, location);
        MarkCooldown(player, TopCooldownModDataKey);

        AnalyticsService.TrackCommandUsed("top", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "top");

        return Success(Lang.Get("thebasics:home-spawn-success-top-teleported", location.Format()));
    }

    private TextCommandResult ExecuteBackTeleport(IServerPlayer player, HomeSpawnLocation location)
    {
        var gearError = TryConsumeTemporalGearForTeleport(player, GetTeleportationConfig().BackRequireTemporalGear, "back", "back");
        if (gearError != null)
        {
            return gearError;
        }

        using (TeleportBackUtil.SuppressRecording())
        {
            Teleport(player, location);
        }

        TeleportBackUtil.ClearPreviousLocation(player);
        MarkCooldown(player, BackCooldownModDataKey);

        AnalyticsService.TrackCommandUsed("back", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "back");

        return Success(Lang.Get("thebasics:home-spawn-success-back-teleported", location.Format()));
    }

    private void OnStuckCancelled(IServerPlayer player, string reason)
    {
        TrackCancelledTeleport("stuck", "stuck", reason);
        NotifyAdmins(Lang.Get("thebasics:home-spawn-admin-stuck-cancelled", player.PlayerName, reason));
        API.Logger.Audit($"Player {player.PlayerName} ({player.PlayerUID}) cancelled /stuck warmup: {reason}.");
    }

    private TextCommandResult BeginPlayerTeleport(IServerPlayer player, int warmupSeconds, string commandName, System.Func<IServerPlayer, TextCommandResult> execute, Action<IServerPlayer, string> onCancelled, int reminderIntervalSeconds = 0, System.Func<int, string> reminderMessage = null)
    {
        if (warmupSeconds <= 0)
        {
            return execute(player);
        }

        var teleportation = API.ModLoader.GetModSystem<TeleportationSystem>();
        if (teleportation == null)
        {
            TrackHomeSpawnFailure(commandName, commandName, "teleport_unavailable");
            return Error("thebasics:teleport-warmup-error-unavailable", "teleport-unavailable");
        }

        var result = teleportation.BeginWarmup(new TeleportWarmupRequest
        {
            Player = player,
            WarmupSeconds = warmupSeconds,
            CancelOnDamage = GetTeleportationConfig().CancelWarmupOnDamage,
            CancelOnInteraction = GetTeleportationConfig().CancelWarmupOnInteraction,
            StartMessage = Lang.Get("thebasics:teleport-warmup-start", warmupSeconds),
            ReminderIntervalSeconds = reminderIntervalSeconds,
            ReminderMessage = reminderMessage,
            Execute = execute,
            OnCancelled = onCancelled
        });

        if (result.Status == EnumCommandStatus.Success)
        {
            AnalyticsService.TrackFeatureUsed("home-spawn", commandName + "_warmup_start", properties: new Dictionary<string, object>
            {
                ["warmup_seconds_bucket"] = AnalyticsBuckets.Count(warmupSeconds)
            });
        }
        else
        {
            TrackHomeSpawnFailure(commandName, commandName, result.ErrorCode ?? "warmup_failed");
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

    private bool TryFindTopLocation(IServerPlayer player, out HomeSpawnLocation location)
    {
        location = null;
        var current = player.Entity.Pos.AsBlockPos;
        var minY = Math.Max(0, current.Y);
        var maxY = API.World.BlockAccessor.MapSizeY - 3;
        if (maxY < minY)
        {
            return false;
        }

        foreach (var offset in TopSearchOffsets)
        {
            if (TryFindTopLocationInColumn(current.X + offset.X, current.Z + offset.Z, current.dimension, minY, maxY, player, out location))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindTopLocationInColumn(int x, int z, int dimension, int minY, int maxY, IServerPlayer player, out HomeSpawnLocation location)
    {
        location = null;
        for (var y = minY; y <= maxY; y++)
        {
            var groundPos = new BlockPos(x, y, z, dimension);
            if (!TryGetSafeLandingHeight(groundPos, out var landingY))
            {
                continue;
            }

            location = new HomeSpawnLocation
            {
                X = x + 0.5,
                Y = landingY,
                Z = z + 0.5,
                Yaw = player.Entity.Pos.Yaw,
                Pitch = player.Entity.Pos.Pitch,
                Roll = player.Entity.Pos.Roll,
                Dimension = groundPos.dimension
            };
            return true;
        }

        return false;
    }

    private bool TryGetSafeLandingHeight(BlockPos groundPos, out double landingY)
    {
        landingY = 0;
        var blockAccessor = API.World.BlockAccessor;
        var ground = blockAccessor.GetBlock(groundPos);
        var groundBoxes = ground?.GetCollisionBoxes(blockAccessor, groundPos);
        if (groundBoxes == null || groundBoxes.Length == 0 || blockAccessor.GetBlock(groundPos, 2).IsLiquid())
        {
            return false;
        }

        var top = groundBoxes.Max(box => box.Y2);
        if (top < 0.875f)
        {
            return false;
        }

        var feetPos = new BlockPos(groundPos.X, groundPos.Y + 1, groundPos.Z, groundPos.dimension);
        var headPos = new BlockPos(groundPos.X, groundPos.Y + 2, groundPos.Z, groundPos.dimension);
        if (!IsClearForPlayer(feetPos) || !IsClearForPlayer(headPos))
        {
            return false;
        }

        landingY = groundPos.Y + top;
        return true;
    }

    private bool IsClearForPlayer(BlockPos pos)
    {
        var blockAccessor = API.World.BlockAccessor;
        var block = blockAccessor.GetBlock(pos);
        var collisionBoxes = block?.GetCollisionBoxes(blockAccessor, pos);
        return (collisionBoxes == null || collisionBoxes.Length == 0) && !blockAccessor.GetBlock(pos, 2).IsLiquid();
    }

    private TextCommandResult TryValidateTemporalGearForTeleport(IServerPlayer player, string commandName, string featureAction)
    {
        return TryValidateTemporalGearForTeleport(player, Config.HomeSpawnRequireTemporalGear, commandName, featureAction);
    }

    private static TextCommandResult TryValidateTemporalGearForTeleport(IServerPlayer player, bool requireTemporalGear, string commandName, string featureAction)
    {
        if (!requireTemporalGear)
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
        return TryConsumeTemporalGearForTeleport(player, Config.HomeSpawnRequireTemporalGear, commandName, featureAction);
    }

    private static TextCommandResult TryConsumeTemporalGearForTeleport(IServerPlayer player, bool requireTemporalGear, string commandName, string featureAction)
    {
        if (!requireTemporalGear)
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

    private static string MissingHomeMessage(string normalizedName)
    {
        return string.Equals(normalizedName, HomeSpawnHomeRegistry.DefaultHomeName, StringComparison.OrdinalIgnoreCase)
            ? Lang.Get("thebasics:home-spawn-error-no-default-home")
            : Lang.Get("thebasics:home-spawn-error-no-home", normalizedName);
    }

    internal static string GetHomeNameArgumentErrorCode(string commandName, string homeName)
    {
        if (homeName == null && string.Equals(commandName, "delhome", StringComparison.OrdinalIgnoreCase))
        {
            return "home-name-required";
        }

        if (HomeSpawnHomeRegistry.IsValidName(homeName, out var errorCode))
        {
            return null;
        }

        return errorCode == "too-long" ? "home-name-too-long" : "home-name-invalid";
    }

    private static TextCommandResult ValidateHomeName(string commandName, string homeName)
    {
        var errorCode = GetHomeNameArgumentErrorCode(commandName, homeName);
        return errorCode switch
        {
            null => null,
            "home-name-required" => Error("thebasics:home-spawn-error-delhome-name-required", errorCode),
            "home-name-too-long" => ErrorMessage(Lang.Get("thebasics:home-spawn-error-home-name-too-long", HomeSpawnHomeRegistry.MaxHomeNameLength), errorCode),
            _ => Error("thebasics:home-spawn-error-home-name-invalid", errorCode)
        };
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
        RegisterPrivilegeIfCustom(GetStuckBlockedByOnlinePrivilege(), "thebasics:home-spawn-stuck-blocked-by-online-privilege-desc");
        RegisterPrivilegeIfCustom(GetTopPrivilege(), "thebasics:home-spawn-top-privilege-desc");
        RegisterPrivilegeIfCustom(GetBackPrivilege(), "thebasics:home-spawn-back-privilege-desc");
    }

    private void RegisterPrivilegeIfCustom(string privilege, string descriptionKey)
    {
        if (string.IsNullOrWhiteSpace(privilege))
        {
            return;
        }

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

    private string GetStuckBlockedByOnlinePrivilege()
    {
        return GetTeleportationConfig().StuckBlockedByOnlinePrivilege?.Trim() ?? string.Empty;
    }

    private string GetTopPrivilege()
    {
        return NormalizePrivilege(GetTeleportationConfig().TopCommandPrivilege, Privilege.chat);
    }

    private string GetBackPrivilege()
    {
        return NormalizePrivilege(GetTeleportationConfig().BackCommandPrivilege, Privilege.chat);
    }

    private bool IsOtherPlayerWithPrivilegeOnline(IServerPlayer currentPlayer, string privilege)
    {
        return API.World.AllOnlinePlayers
            .OfType<IServerPlayer>()
            .Any(player => player.PlayerUID != currentPlayer.PlayerUID && player.HasPrivilege(privilege));
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
