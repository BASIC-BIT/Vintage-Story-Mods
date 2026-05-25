using FluentAssertions;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class ConfigAdminSaveWorkflowTests
{
    [Fact]
    public void ApplyValues_ReportsUnknownSettingWithoutMutatingKnownSettings()
    {
        var config = CreateConfig();

        var errors = ConfigAdminSaveWorkflow.ApplyValues(config, new[]
        {
            new ConfigAdminSettingValue { Key = "UnknownSetting", Value = "42" }
        });

        errors.Should().ContainSingle().Which.Should().Contain("Unknown setting: UnknownSetting");
        config.TpaCooldownInGameHours.Should().Be(0.5);
    }

    [Fact]
    public void ApplyValues_AppliesKnownSettingValues()
    {
        var config = CreateConfig();

        var errors = ConfigAdminSaveWorkflow.ApplyValues(config, new[]
        {
            new ConfigAdminSettingValue { Key = "TpaCooldownInGameHours", Value = "2.5" }
        });

        errors.Should().BeEmpty();
        config.TpaCooldownInGameHours.Should().Be(2.5);
    }

    [Fact]
    public void MarkReviewedKeys_KeepsOnlyRegisteredKeysAndSortsCaseInsensitively()
    {
        var config = CreateConfig();
        config.ReviewedConfigSettingKeys = ["TpaCooldownInGameHours"];

        ConfigAdminSaveWorkflow.MarkReviewedKeys(config, ["UnknownSetting", "EnableChatter"]);

        config.ReviewedConfigSettingKeys.Should().Equal("EnableChatter", "TpaCooldownInGameHours");
    }

    [Fact]
    public void BuildConfigSaveMessage_ReportsLiveAppliedOrRestartRequiredOutcome()
    {
        ConfigAdminSaveWorkflow.BuildConfigSaveMessage(["EnableChatter"], [])
            .Should().Be("Saved The BASICs config. Live-applied settings: 1.");

        ConfigAdminSaveWorkflow.BuildConfigSaveMessage(["EnableChatter"], ["RequireRestart"])
            .Should().Be("Saved The BASICs config. Restart required for: RequireRestart.");
    }

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }
}
