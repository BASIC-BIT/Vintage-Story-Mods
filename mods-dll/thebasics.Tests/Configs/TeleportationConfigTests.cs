using FluentAssertions;
using Newtonsoft.Json;
using thebasics.Configs;
using thebasics.ModSystems.Teleportation;

namespace thebasics.Tests.Configs;

public class TeleportationConfigTests
{
    [Fact]
    public void FreshConfig_DefaultsAllTeleportCommandFamiliesOff()
    {
        var config = new ModConfig();

        config.InitializeDefaultsIfNeeded();

        AssertAllCommandFamiliesDisabled(config.Teleportation);
        config.HomeCommandPrivilege.Should().Be("chat");
        config.SetHomeCommandPrivilege.Should().Be("chat");
        config.SpawnCommandPrivilege.Should().Be("chat");
        config.SetSpawnCommandPrivilege.Should().Be("commandplayer");
        config.Teleportation.StuckCommandPrivilege.Should().Be("chat");
        config.Teleportation.TopCommandPrivilege.Should().Be("chat");
        config.Teleportation.BackCommandPrivilege.Should().Be("chat");
    }

    [Fact]
    public void LegacyPre580Config_BackfillsSafeTeleportCommandDefaults()
    {
        var config = JsonConvert.DeserializeObject<ModConfig>("""
            {
              "EnableLanguageSystem": true,
              "TpaRequestPrivilege": "chat"
            }
            """)!;

        config.InitializeDefaultsIfNeeded();

        AssertAllCommandFamiliesDisabled(config.Teleportation);
    }

    [Fact]
    public void V580MaterializedTrueDefaults_AreResetOnceWithoutChangingPrivileges()
    {
        var config = JsonConvert.DeserializeObject<ModConfig>("""
            {
              "HomeCommandPrivilege": "custom-home",
              "SetSpawnCommandPrivilege": "custom-setspawn",
              "Teleportation": {
                "RegisterHomeCommands": true,
                "RegisterSpawnCommands": true,
                "RegisterStuckCommand": true,
                "RegisterTopCommand": true,
                "RegisterBackCommand": true,
                "StuckCommandPrivilege": "custom-stuck",
                "TopCommandPrivilege": "custom-top",
                "BackCommandPrivilege": "custom-back"
              }
            }
            """)!;

        config.InitializeDefaultsIfNeeded();

        AssertAllCommandFamiliesDisabled(config.Teleportation);
        config.HomeCommandPrivilege.Should().Be("custom-home");
        config.SetSpawnCommandPrivilege.Should().Be("custom-setspawn");
        config.Teleportation.StuckCommandPrivilege.Should().Be("custom-stuck");
        config.Teleportation.TopCommandPrivilege.Should().Be("custom-top");
        config.Teleportation.BackCommandPrivilege.Should().Be("custom-back");
    }

    [Fact]
    public void Migration_PreservesExplicitFalseValuesWhileResettingTrueValues()
    {
        var config = JsonConvert.DeserializeObject<ModConfig>("""
            {
              "Teleportation": {
                "RegisterHomeCommands": false,
                "RegisterSpawnCommands": true,
                "RegisterStuckCommand": false,
                "RegisterTopCommand": true,
                "RegisterBackCommand": false
              }
            }
            """)!;

        config.InitializeDefaultsIfNeeded();

        AssertAllCommandFamiliesDisabled(config.Teleportation);
    }

    [Fact]
    public void MigratedConfig_PreservesLaterExplicitOptIn()
    {
        var config = new ModConfig
        {
            Teleportation = new TeleportationConfig
            {
                CommandRegistrationDefaultsVersion = 1,
                RegisterHomeCommands = true,
                RegisterSpawnCommands = true,
                RegisterStuckCommand = true,
                RegisterTopCommand = true,
                RegisterBackCommand = true
            }
        };

        config.InitializeDefaultsIfNeeded();

        config.Teleportation.RegisterHomeCommands.Should().BeTrue();
        config.Teleportation.RegisterSpawnCommands.Should().BeTrue();
        config.Teleportation.RegisterStuckCommand.Should().BeTrue();
        config.Teleportation.RegisterTopCommand.Should().BeTrue();
        config.Teleportation.RegisterBackCommand.Should().BeTrue();
        config.Teleportation.CommandRegistrationDefaultsVersion.Should().Be(1);
    }

    [Fact]
    public void BackRecorder_TracksTheMigratedBackCommandSetting()
    {
        var defaultConfig = new ModConfig();
        defaultConfig.InitializeDefaultsIfNeeded();

        var optedInConfig = new ModConfig
        {
            Teleportation = new TeleportationConfig
            {
                CommandRegistrationDefaultsVersion = 1,
                RegisterBackCommand = true
            }
        };
        optedInConfig.InitializeDefaultsIfNeeded();

        TeleportationSystem.ShouldEnableBackRecorder(defaultConfig).Should().BeFalse();
        TeleportationSystem.ShouldEnableBackRecorder(optedInConfig).Should().BeTrue();
    }

    private static void AssertAllCommandFamiliesDisabled(TeleportationConfig teleportation)
    {
        teleportation.RegisterHomeCommands.Should().BeFalse();
        teleportation.RegisterSpawnCommands.Should().BeFalse();
        teleportation.RegisterStuckCommand.Should().BeFalse();
        teleportation.RegisterTopCommand.Should().BeFalse();
        teleportation.RegisterBackCommand.Should().BeFalse();
        teleportation.CommandRegistrationDefaultsVersion.Should().Be(1);
    }
}
