using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class ConfigAdminSettingRegistryTests
{
    [Fact]
    public void ValidateConfig_RejectsRangeAtOrBelowObfuscationStart()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatModeDistances.Normal");

        var success = setting.TrySetValue(config, "15", out var error);
        var errors = ConfigAdminSettingRegistry.ValidateConfig(config);

        success.Should().BeTrue(error);
        errors.Should().ContainSingle().Which.Should().Contain("Normal range must be greater than its obfuscation start");
        config.ProximityChatModeDistances[ProximityChatMode.Normal].Should().Be(15);
    }

    [Fact]
    public void ValidateConfig_RejectsObfuscationStartAtOrAboveRange()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatModeObfuscationRanges.Normal");

        var success = setting.TrySetValue(config, "35", out var error);
        var errors = ConfigAdminSettingRegistry.ValidateConfig(config);

        success.Should().BeTrue(error);
        errors.Should().ContainSingle().Which.Should().Contain("Normal range must be greater than its obfuscation start");
        config.ProximityChatModeObfuscationRanges[ProximityChatMode.Normal].Should().Be(35);
    }

    [Fact]
    public void TrySetValue_ParsesCommaSeparatedClampFontSizes()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatClampFontSizes");

        var success = setting.TrySetValue(config, "28, 16, 9", out var error);

        success.Should().BeTrue(error);
        config.ProximityChatClampFontSizes.Should().Equal(28, 16, 9);
        setting.GetValue(config).Should().Be("28, 16, 9");
    }

    [Fact]
    public void TrySetValue_RejectsInvalidClampFontSizeValues()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatClampFontSizes");

        var success = setting.TrySetValue(config, "12, nope", out var error);

        success.Should().BeFalse();
        error.Should().Contain("whole numbers from 1 to 128");
        config.ProximityChatClampFontSizes.Should().Equal(30, 16, 12, 6);
    }

    [Fact]
    public void TrySetValue_RejectsEmptyClampFontSizes()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatClampFontSizes");

        var success = setting.TrySetValue(config, "", out var error);

        success.Should().BeFalse();
        error.Should().Contain("must contain at least one whole number");
        config.ProximityChatClampFontSizes.Should().Equal(30, 16, 12, 6);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void TrySetValue_RejectsNonFiniteDecimalValues(string value)
    {
        var config = CreateConfig();
        var setting = GetSetting("TpaCooldownInGameHours");

        var success = setting.TrySetValue(config, value, out var error);

        success.Should().BeFalse();
        error.Should().Contain("must be a number from 0 to 720");
        config.TpaCooldownInGameHours.Should().Be(0.5);
    }

    [Fact]
    public void TrySetValue_ParsesCommaSeparatedModeVerbs()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatModeVerbs.Whisper");

        var success = setting.TrySetValue(config, "murmurs, breathes", out var error);

        success.Should().BeTrue(error);
        config.ProximityChatModeVerbs[ProximityChatMode.Whisper].Should().Equal("murmurs", "breathes");
        setting.GetValue(config).Should().Be("murmurs, breathes");
    }

    [Fact]
    public void TrySetValue_RejectsEmptyModeVerbs()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatModeVerbs.Whisper");

        var success = setting.TrySetValue(config, "", out var error);

        success.Should().BeFalse();
        error.Should().Contain("must contain at least one value");
        config.ProximityChatModeVerbs[ProximityChatMode.Whisper].Should().Equal("whispers", "mumbles", "mutters");
    }

    [Fact]
    public void TrySetValue_RejectsEmptyDelimiterStart()
    {
        var config = CreateConfig();
        var setting = GetSetting("ChatDelimiters.Emote.Start");

        var success = setting.TrySetValue(config, "", out var error);

        success.Should().BeFalse();
        error.Should().Contain("cannot be empty");
        config.ChatDelimiters.Emote.Start.Should().Be("*");
    }

    [Fact]
    public void TrySetValue_AllowsEmptyDelimiterEnd()
    {
        var config = CreateConfig();
        var setting = GetSetting("ChatDelimiters.OOC.End");

        var success = setting.TrySetValue(config, "", out var error);

        success.Should().BeTrue(error);
        config.ChatDelimiters.OOC.End.Should().BeEmpty();
    }

    [Fact]
    public void TrySetValue_UpdatesPlayerStatToggle()
    {
        var config = CreateConfig();
        var setting = GetSetting("PlayerStatToggles.Deaths");

        var success = setting.TrySetValue(config, "false", out var error);

        success.Should().BeTrue(error);
        config.PlayerStatToggles[PlayerStatType.Deaths].Should().BeFalse();
        setting.GetValue(config).Should().Be("0");
    }

    [Fact]
    public void TrySetValue_RejectsLongModePunctuation()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatModePunctuation.Yell");

        var success = setting.TrySetValue(config, "!!!!!!!!!!", out var error);

        success.Should().BeFalse();
        error.Should().Contain("8 characters or fewer");
        config.ProximityChatModePunctuation[ProximityChatMode.Yell].Should().Be("!");
    }

    [Fact]
    public void GetValue_UsesModeSpecificFallbacksForMissingLegacyEntries()
    {
        var config = CreateConfig();
        config.ProximityChatModeDistances.Remove(ProximityChatMode.Yell);
        config.ProximityChatModeVerbs.Remove(ProximityChatMode.Whisper);
        config.RPTTS_ModeFalloff.Remove(ProximityChatMode.Whisper);
        config.ChatterModeVolume.Remove(ProximityChatMode.Yell);

        GetSetting("ProximityChatModeDistances.Yell").GetValue(config).Should().Be("90");
        GetSetting("ProximityChatModeVerbs.Whisper").GetValue(config).Should().Be("whispers, mumbles, mutters");
        GetSetting("RPTTS_ModeFalloff.Whisper").GetValue(config).Should().Be("5");
        GetSetting("ChatterModeVolume.Yell").GetValue(config).Should().Be("1.4");
    }

    [Fact]
    public void TrySetValue_UpdatesTh3EssentialsDiscordRelayToggle()
    {
        var config = CreateConfig();
        var setting = GetSetting("EnableTh3EssentialsDiscordRelay");

        var success = setting.TrySetValue(config, "true", out var error);

        success.Should().BeTrue(error);
        config.EnableTh3EssentialsDiscordRelay.Should().BeTrue();
        setting.GetValue(config).Should().Be("1");
    }

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    private static ConfigAdminSettingDefinition GetSetting(string key)
    {
        ConfigAdminSettingRegistry.TryGet(key, out var setting).Should().BeTrue($"{key} should be registered");
        return setting;
    }
}
