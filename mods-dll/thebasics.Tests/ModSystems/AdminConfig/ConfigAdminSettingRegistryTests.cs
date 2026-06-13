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

    [Fact]
    public void ChatHistorySettings_AreRegisteredAndValidateRanges()
    {
        var config = CreateConfig();

        GetSetting("EnableChatHistory").TrySetValue(config, "false", out var boolError).Should().BeTrue(boolError);
        GetSetting("ChatHistoryPermission").TrySetValue(config, "chatlog", out var permissionError).Should().BeTrue(permissionError);
        GetSetting("ChatHistoryRetentionDays").TrySetValue(config, "30", out var retentionError).Should().BeTrue(retentionError);
        var invalid = GetSetting("ChatHistorySearchMaxResults").TrySetValue(config, "0", out var maxResultsError);

        config.EnableChatHistory.Should().BeFalse();
        config.ChatHistoryPermission.Should().Be("chatlog");
        config.ChatHistoryRetentionDays.Should().Be(30);
        invalid.Should().BeFalse();
        maxResultsError.Should().Contain("whole number from 1 to 1000");
    }

    [Fact]
    public void HomeSpawnPrivileges_AreRegisteredAndDefaulted()
    {
        var config = CreateConfig();

        config.HomeCommandPrivilege.Should().Be("chat");
        config.SetHomeCommandPrivilege.Should().Be("chat");
        config.SpawnCommandPrivilege.Should().Be("chat");
        config.SetSpawnCommandPrivilege.Should().Be("commandplayer");
        config.HomeSpawnRequireTemporalGear.Should().BeFalse();
        config.Teleportation.MaxHomes.Should().Be(3);
        config.Teleportation.HomeWarmupSeconds.Should().Be(5);
        config.Teleportation.SpawnWarmupSeconds.Should().Be(5);
        config.Teleportation.TpaWarmupSeconds.Should().Be(5);
        config.Teleportation.StuckWarmupSeconds.Should().Be(300);
        config.Teleportation.HomeCooldownSeconds.Should().Be(300);
        config.Teleportation.SpawnCooldownSeconds.Should().Be(300);
        config.Teleportation.StuckCooldownSeconds.Should().Be(3600);
        config.Teleportation.StuckReminderIntervalSeconds.Should().Be(60);
        config.Teleportation.StuckBlockedByOnlinePrivilege.Should().Be("commandplayer");
        config.Teleportation.TopWarmupSeconds.Should().Be(5);
        config.Teleportation.TopCooldownSeconds.Should().Be(300);
        config.Teleportation.TopCommandPrivilege.Should().Be("chat");
        config.Teleportation.BackWarmupSeconds.Should().Be(5);
        config.Teleportation.BackCooldownSeconds.Should().Be(300);
        config.Teleportation.BackExpiresAfterSeconds.Should().Be(300);
        config.Teleportation.BackCommandPrivilege.Should().Be("chat");
        config.Teleportation.BackRequireTemporalGear.Should().BeFalse();
        config.Teleportation.RegisterHomeCommands.Should().BeTrue();
        config.Teleportation.RegisterSpawnCommands.Should().BeTrue();
        config.Teleportation.RegisterStuckCommand.Should().BeTrue();
        config.Teleportation.RegisterTopCommand.Should().BeTrue();
        config.Teleportation.RegisterBackCommand.Should().BeTrue();
        config.Teleportation.CancelWarmupOnDamage.Should().BeTrue();
        config.Teleportation.CancelWarmupOnInteraction.Should().BeTrue();
        config.Teleportation.StuckCommandPrivilege.Should().Be("chat");
        config.Teleportation.StuckAdminNotifyPrivilege.Should().Be("commandplayer");

        GetSetting("HomeCommandPrivilege").TrySetValue(config, "home", out var homeError).Should().BeTrue(homeError);
        GetSetting("SetHomeCommandPrivilege").TrySetValue(config, "sethome", out var setHomeError).Should().BeTrue(setHomeError);
        GetSetting("SpawnCommandPrivilege").TrySetValue(config, "spawn", out var spawnError).Should().BeTrue(spawnError);
        GetSetting("SetSpawnCommandPrivilege").TrySetValue(config, "setspawn", out var setSpawnError).Should().BeTrue(setSpawnError);
        GetSetting("HomeSpawnRequireTemporalGear").TrySetValue(config, "true", out var gearError).Should().BeTrue(gearError);
        GetSetting("Teleportation.RegisterHomeCommands").TrySetValue(config, "false", out var registerHomeError).Should().BeTrue(registerHomeError);
        GetSetting("Teleportation.RegisterSpawnCommands").TrySetValue(config, "false", out var registerSpawnError).Should().BeTrue(registerSpawnError);
        GetSetting("Teleportation.RegisterStuckCommand").TrySetValue(config, "false", out var registerStuckError).Should().BeTrue(registerStuckError);
        GetSetting("Teleportation.RegisterTopCommand").TrySetValue(config, "false", out var registerTopError).Should().BeTrue(registerTopError);
        GetSetting("Teleportation.RegisterBackCommand").TrySetValue(config, "false", out var registerBackError).Should().BeTrue(registerBackError);
        GetSetting("Teleportation.MaxHomes").TrySetValue(config, "5", out var maxHomesError).Should().BeTrue(maxHomesError);
        GetSetting("Teleportation.HomeWarmupSeconds").TrySetValue(config, "6", out var homeWarmupError).Should().BeTrue(homeWarmupError);
        GetSetting("Teleportation.SpawnWarmupSeconds").TrySetValue(config, "7", out var spawnWarmupError).Should().BeTrue(spawnWarmupError);
        GetSetting("Teleportation.TpaWarmupSeconds").TrySetValue(config, "8", out var tpaWarmupError).Should().BeTrue(tpaWarmupError);
        GetSetting("Teleportation.TopWarmupSeconds").TrySetValue(config, "9", out var topWarmupError).Should().BeTrue(topWarmupError);
        GetSetting("Teleportation.BackWarmupSeconds").TrySetValue(config, "10", out var backWarmupError).Should().BeTrue(backWarmupError);
        GetSetting("Teleportation.StuckWarmupSeconds").TrySetValue(config, "90", out var stuckWarmupError).Should().BeTrue(stuckWarmupError);
        GetSetting("Teleportation.HomeCooldownSeconds").TrySetValue(config, "120", out var homeCooldownError).Should().BeTrue(homeCooldownError);
        GetSetting("Teleportation.SpawnCooldownSeconds").TrySetValue(config, "180", out var spawnCooldownError).Should().BeTrue(spawnCooldownError);
        GetSetting("Teleportation.TopCooldownSeconds").TrySetValue(config, "240", out var topCooldownError).Should().BeTrue(topCooldownError);
        GetSetting("Teleportation.BackCooldownSeconds").TrySetValue(config, "300", out var backCooldownError).Should().BeTrue(backCooldownError);
        GetSetting("Teleportation.BackExpiresAfterSeconds").TrySetValue(config, "600", out var backExpiryError).Should().BeTrue(backExpiryError);
        GetSetting("Teleportation.BackRequireTemporalGear").TrySetValue(config, "true", out var backGearError).Should().BeTrue(backGearError);
        GetSetting("Teleportation.StuckCooldownSeconds").TrySetValue(config, "7200", out var stuckCooldownError).Should().BeTrue(stuckCooldownError);
        GetSetting("Teleportation.StuckReminderIntervalSeconds").TrySetValue(config, "60", out var stuckReminderError).Should().BeTrue(stuckReminderError);
        var damageSetting = GetSetting("Teleportation.CancelWarmupOnDamage");
        damageSetting.Group.Should().Be("Teleportation");
        damageSetting.TrySetValue(config, "false", out var damageError).Should().BeTrue(damageError);
        GetSetting("Teleportation.CancelWarmupOnInteraction").TrySetValue(config, "false", out var interactionError).Should().BeTrue(interactionError);
        GetSetting("Teleportation.StuckCommandPrivilege").TrySetValue(config, "stuck", out var stuckPrivilegeError).Should().BeTrue(stuckPrivilegeError);
        GetSetting("Teleportation.StuckAdminNotifyPrivilege").TrySetValue(config, "staff", out var stuckNotifyError).Should().BeTrue(stuckNotifyError);
        GetSetting("Teleportation.StuckBlockedByOnlinePrivilege").TrySetValue(config, "helper", out var stuckBlockedError).Should().BeTrue(stuckBlockedError);
        GetSetting("Teleportation.TopCommandPrivilege").TrySetValue(config, "top", out var topPrivilegeError).Should().BeTrue(topPrivilegeError);
        GetSetting("Teleportation.BackCommandPrivilege").TrySetValue(config, "back", out var backPrivilegeError).Should().BeTrue(backPrivilegeError);

        config.HomeCommandPrivilege.Should().Be("home");
        config.SetHomeCommandPrivilege.Should().Be("sethome");
        config.SpawnCommandPrivilege.Should().Be("spawn");
        config.SetSpawnCommandPrivilege.Should().Be("setspawn");
        config.HomeSpawnRequireTemporalGear.Should().BeTrue();
        config.Teleportation.RegisterHomeCommands.Should().BeFalse();
        config.Teleportation.RegisterSpawnCommands.Should().BeFalse();
        config.Teleportation.RegisterStuckCommand.Should().BeFalse();
        config.Teleportation.RegisterTopCommand.Should().BeFalse();
        config.Teleportation.RegisterBackCommand.Should().BeFalse();
        config.Teleportation.MaxHomes.Should().Be(5);
        config.Teleportation.HomeWarmupSeconds.Should().Be(6);
        config.Teleportation.SpawnWarmupSeconds.Should().Be(7);
        config.Teleportation.TpaWarmupSeconds.Should().Be(8);
        config.Teleportation.TopWarmupSeconds.Should().Be(9);
        config.Teleportation.BackWarmupSeconds.Should().Be(10);
        config.Teleportation.StuckWarmupSeconds.Should().Be(90);
        config.Teleportation.HomeCooldownSeconds.Should().Be(120);
        config.Teleportation.SpawnCooldownSeconds.Should().Be(180);
        config.Teleportation.TopCooldownSeconds.Should().Be(240);
        config.Teleportation.BackCooldownSeconds.Should().Be(300);
        config.Teleportation.BackExpiresAfterSeconds.Should().Be(600);
        config.Teleportation.BackRequireTemporalGear.Should().BeTrue();
        config.Teleportation.StuckCooldownSeconds.Should().Be(7200);
        config.Teleportation.StuckReminderIntervalSeconds.Should().Be(60);
        config.Teleportation.CancelWarmupOnDamage.Should().BeFalse();
        config.Teleportation.CancelWarmupOnInteraction.Should().BeFalse();
        config.Teleportation.StuckCommandPrivilege.Should().Be("stuck");
        config.Teleportation.StuckAdminNotifyPrivilege.Should().Be("staff");
        config.Teleportation.StuckBlockedByOnlinePrivilege.Should().Be("helper");
        config.Teleportation.TopCommandPrivilege.Should().Be("top");
        config.Teleportation.BackCommandPrivilege.Should().Be("back");
    }

    [Fact]
    public void MapVisibilitySettings_AreRegisteredAndAllowUnlimitedRenderDistance()
    {
        var config = CreateConfig();

        GetSetting("ManageMapPlayerVisibility").TrySetValue(config, "true", out var manageError).Should().BeTrue(manageError);
        GetSetting("MapHideOtherPlayers").TrySetValue(config, "true", out var hideError).Should().BeTrue(hideError);
        GetSetting("MapPlayerRenderDistance").TrySetValue(config, "-1", out var rangeError).Should().BeTrue(rangeError);

        config.ManageMapPlayerVisibility.Should().BeTrue();
        config.MapHideOtherPlayers.Should().BeTrue();
        config.MapPlayerRenderDistance.Should().Be(-1);
    }

    [Fact]
    public void NametagStyleSettings_AcceptOptionalHexColors()
    {
        var config = CreateConfig();

        GetSetting("NametagBackgroundColor").TrySetValue(config, " #403529BF ", out var backgroundError).Should().BeTrue(backgroundError);
        GetSetting("NametagBorderColor").TrySetValue(config, "", out var borderError).Should().BeTrue(borderError);
        var invalid = GetSetting("NametagBorderColor").TrySetValue(config, "brown", out var invalidError);
        GetSetting("AllowPlayersToChangeNametagColors").TrySetValue(config, "false", out var allowError).Should().BeTrue(allowError);
        GetSetting("ChangeNametagColorPermission").TrySetValue(config, "nametagstyle", out var permissionError).Should().BeTrue(permissionError);

        config.NametagBackgroundColor.Should().Be("#403529BF");
        config.NametagBorderColor.Should().BeEmpty();
        config.AllowPlayersToChangeNametagColors.Should().BeFalse();
        config.ChangeNametagColorPermission.Should().Be("nametagstyle");
        invalid.Should().BeFalse();
        invalidError.Should().Contain("hex color");
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
