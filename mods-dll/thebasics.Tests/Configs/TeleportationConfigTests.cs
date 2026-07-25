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
        config.Teleportation.TopRequireTemporalGear.Should().BeFalse();
        config.Teleportation.BackCommandPrivilege.Should().Be("chat");
    }

    [Fact]
    public void ConfigWithMissingTeleportationSection_DefaultsAllCommandFamiliesOff()
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
    public void ExplicitSerializedValues_ArePreservedWithoutChangingPrivileges()
    {
        var config = JsonConvert.DeserializeObject<ModConfig>("""
            {
              "HomeCommandPrivilege": "custom-home",
              "SetSpawnCommandPrivilege": "custom-setspawn",
              "Teleportation": {
                "RegisterHomeCommands": true,
                "RegisterSpawnCommands": false,
                "RegisterStuckCommand": true,
                "RegisterTopCommand": false,
                "RegisterBackCommand": true,
                "StuckCommandPrivilege": "custom-stuck",
                "TopCommandPrivilege": "custom-top",
                "TopRequireTemporalGear": true,
                "BackCommandPrivilege": "custom-back"
              }
            }
            """)!;

        config.InitializeDefaultsIfNeeded();

        config.Teleportation.RegisterHomeCommands.Should().BeTrue();
        config.Teleportation.RegisterSpawnCommands.Should().BeFalse();
        config.Teleportation.RegisterStuckCommand.Should().BeTrue();
        config.Teleportation.RegisterTopCommand.Should().BeFalse();
        config.Teleportation.RegisterBackCommand.Should().BeTrue();
        config.HomeCommandPrivilege.Should().Be("custom-home");
        config.SetSpawnCommandPrivilege.Should().Be("custom-setspawn");
        config.Teleportation.StuckCommandPrivilege.Should().Be("custom-stuck");
        config.Teleportation.TopCommandPrivilege.Should().Be("custom-top");
        config.Teleportation.TopRequireTemporalGear.Should().BeTrue();
        config.Teleportation.BackCommandPrivilege.Should().Be("custom-back");
    }

    [Fact]
    public void BackRecorder_TracksTheBackCommandSetting()
    {
        var defaultConfig = new ModConfig();
        defaultConfig.InitializeDefaultsIfNeeded();

        var optedInConfig = new ModConfig
        {
            Teleportation = new TeleportationConfig
            {
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
    }
}
