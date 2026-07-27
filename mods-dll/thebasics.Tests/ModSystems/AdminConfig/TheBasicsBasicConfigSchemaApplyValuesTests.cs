using BasicConfig;
using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class TheBasicsBasicConfigSchemaApplyValuesTests
{
    [Fact]
    public void ApplyValues_ReportsUnknownSettingWithoutMutatingKnownSettings()
    {
        var config = CreateConfig();

        var errors = TheBasicsBasicConfigSchema.Build().ApplyValues(config, new[]
        {
            new BasicConfigSettingValue { Key = "UnknownSetting", Value = "42" }
        });

        errors.Should().ContainSingle().Which.Should().Contain("Unknown setting: UnknownSetting");
        config.TpaCooldownInGameHours.Should().Be(0.5);
    }

    [Fact]
    public void ApplyValues_AppliesKnownSettingValues()
    {
        var config = CreateConfig();

        var errors = TheBasicsBasicConfigSchema.Build().ApplyValues(config, new[]
        {
            new BasicConfigSettingValue { Key = "TpaCooldownInGameHours", Value = "2.5" }
        });

        errors.Should().BeEmpty();
        config.TpaCooldownInGameHours.Should().Be(2.5);
    }

    [Fact]
    public void GetChangedKeys_DetectsChangedSettingValues()
    {
        var before = CreateConfig();
        var after = CreateConfig();
        after.EnableChatter = !before.EnableChatter;

        var changed = TheBasicsBasicConfigSchema.Build().GetChangedKeys(before, after);

        changed.Should().ContainSingle().Which.Should().Be("EnableChatter");
    }

    [Fact]
    public void GetRestartRequiredKeys_ReportsOnlyRestartRequiredSettings()
    {
        var schema = TheBasicsBasicConfigSchema.Build();

        schema.GetRestartRequiredKeys(["EnableChatter", "DisableRPChat"])
            .Should().Equal("DisableRPChat");
    }

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }
}
