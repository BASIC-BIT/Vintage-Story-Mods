using System;
using System.Collections.Generic;
using thebasics.Extensions;
using thebasics.ModSystems.Analytics;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.HomeSpawn;

public class HomeSpawnSystem : BaseBasicModSystem
{
    private const string SpawnLocationSaveDataKey = "thebasics:home-spawn:spawn";
    private const string HomeLocationModDataKey = "BASIC_HOME_LOCATION";

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
            changedKeys.Contains(nameof(Config.SetSpawnCommandPrivilege)))
        {
            RegisterConfiguredPrivileges();
            RefreshCommandPrivileges();
        }
    }

    private void RegisterCommands()
    {
        API.ChatCommands.GetOrCreate("home")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-home-desc"))
            .RequiresPrivilege(GetHomePrivilege())
            .RequiresPlayer()
            .HandleWith(HandleHome);

        API.ChatCommands.GetOrCreate("sethome")
            .WithDescription(Lang.Get("thebasics:home-spawn-cmd-sethome-desc"))
            .RequiresPrivilege(GetSetHomePrivilege())
            .RequiresPlayer()
            .HandleWith(HandleSetHome);

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
    }

    private void RefreshCommandPrivileges()
    {
        API.ChatCommands.Get("home")?.RequiresPrivilege(GetHomePrivilege());
        API.ChatCommands.Get("sethome")?.RequiresPrivilege(GetSetHomePrivilege());
        API.ChatCommands.Get("spawn")?.RequiresPrivilege(GetSpawnPrivilege());
        API.ChatCommands.Get("setspawn")?.RequiresPrivilege(GetSetSpawnPrivilege());
    }

    private static TextCommandResult HandleSetHome(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var location = HomeSpawnLocation.From(player.Entity.Pos);
        player.SetModData(HomeLocationModDataKey, location);

        AnalyticsService.TrackCommandUsed("sethome", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "set_home");

        return Success(Lang.Get("thebasics:home-spawn-success-home-set", location.Format()));
    }

    private static TextCommandResult HandleHome(TextCommandCallingArgs args)
    {
        var player = args.Caller.Player as IServerPlayer;
        if (player?.Entity == null)
        {
            return Error("thebasics:home-spawn-error-player-required", "player-required");
        }

        var location = player.GetModData<HomeSpawnLocation>(HomeLocationModDataKey, null);

        if (location == null)
        {
            AnalyticsService.TrackCommandUsed("home", false, "home_not_set");
            AnalyticsService.TrackFeatureUsed("home-spawn", "home", false, "home_not_set");
            return Error("thebasics:home-spawn-error-no-home", "home-not-set");
        }

        Teleport(player, location);

        AnalyticsService.TrackCommandUsed("home", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "home");

        return Success(Lang.Get("thebasics:home-spawn-success-home-teleported"));
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

        var location = GetStoredSpawnLocation() ?? GetDefaultSpawnLocation();
        Teleport(player, location);

        AnalyticsService.TrackCommandUsed("spawn", true);
        AnalyticsService.TrackFeatureUsed("home-spawn", "spawn");

        return Success(Lang.Get("thebasics:home-spawn-success-spawn-teleported"));
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

    private void RegisterConfiguredPrivileges()
    {
        RegisterPrivilegeIfCustom(GetHomePrivilege(), "thebasics:home-spawn-home-privilege-desc");
        RegisterPrivilegeIfCustom(GetSetHomePrivilege(), "thebasics:home-spawn-sethome-privilege-desc");
        RegisterPrivilegeIfCustom(GetSpawnPrivilege(), "thebasics:home-spawn-spawn-privilege-desc");
        RegisterPrivilegeIfCustom(GetSetSpawnPrivilege(), "thebasics:home-spawn-setspawn-privilege-desc");
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
}
