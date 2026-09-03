using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.PlayerStats.Models;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class ConfigAdminSettingRegistryTests
{
    [Fact]
    public void ValidateConfig_AcceptsTheUnlimitedSentinel()
    {
        var config = CreateConfig();
        config.ProximityChatModeDistances[ProximityChatMode.Normal] = ModConfig.UnlimitedRange;

        ConfigAdminSettingRegistry.ValidateConfig(config).Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfig_RejectsNegativeRangesOtherThanTheSentinel()
    {
        // A typo like -2 must be reported, not silently read as a server-wide channel.
        var config = CreateConfig();
        config.ProximityChatModeDistances[ProximityChatMode.Normal] = -2;

        ConfigAdminSettingRegistry.ValidateConfig(config)
            .Should().ContainSingle().Which.Should().Contain("Normal range must be a positive block count");
    }

    [Fact]
    public void ValidateConfig_RejectsUnlimitedRangeCombinedWithClearSoundPath()
    {
        // Line of sight needs a bounded range to raycast against, so the combination is refused
        // rather than the setting being silently ignored.
        var config = CreateConfig();
        config.ProximityChatModeDistances[ProximityChatMode.Normal] = ModConfig.UnlimitedRange;
        config.RequireClearSoundPathForSpeech[ProximityChatMode.Normal] = true;

        ConfigAdminSettingRegistry.ValidateConfig(config)
            .Should().ContainSingle().Which.Should().Contain("cannot combine an unlimited range with RequireClearSoundPathForSpeech");
    }

    [Fact]
    public void GetSignLanguageRange_FallsBackForNegativeValues()
    {
        // Both the recipient filter and the deferred-delivery retry read through this. Normalising
        // in only one of them would queue a listener the retry could then never deliver to.
        var config = CreateConfig();
        config.SignLanguageRange = -1;

        config.GetSignLanguageRange().Should().Be(60);

        config.SignLanguageRange = 25;
        config.GetSignLanguageRange().Should().Be(25);
    }

    [Fact]
    public void ValidateConfig_RejectsNegativeSignLanguageRange()
    {
        var config = CreateConfig();
        config.SignLanguageRange = -1;

        ConfigAdminSettingRegistry.ValidateConfig(config)
            .Should().ContainSingle().Which.Should().Contain("SignLanguageRange must be a positive block count");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(129)]
    public void ValidateConfig_RejectsOutOfBoundsWallPenalty(int penalty)
    {
        // The admin setting declares 0..128, but a hand-edited the_basics.json skips that check.
        // A negative one is clamped to zero downstream, silently disabling configured muffling.
        var config = CreateConfig();
        config.SpeechOcclusionWallPenaltyBlocks = penalty;

        ConfigAdminSettingRegistry.ValidateConfig(config)
            .Should().ContainSingle().Which.Should().Contain("SpeechOcclusionWallPenaltyBlocks must be a whole number from 0 to 128");
    }

    [Fact]
    public void ValidateConfig_RejectsOversizedSignLanguageRange()
    {
        var config = CreateConfig();
        config.SignLanguageRange = 513;

        ConfigAdminSettingRegistry.ValidateConfig(config)
            .Should().ContainSingle().Which.Should().Contain("SignLanguageRange must be 512 blocks or fewer");
    }

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
        config.ProximityChatClampFontSizes.Should().Equal(30, 16, 12, 9);
    }

    [Fact]
    public void TrySetValue_RejectsEmptyClampFontSizes()
    {
        var config = CreateConfig();
        var setting = GetSetting("ProximityChatClampFontSizes");

        var success = setting.TrySetValue(config, "", out var error);

        success.Should().BeFalse();
        error.Should().Contain("must contain at least one whole number");
        config.ProximityChatClampFontSizes.Should().Equal(30, 16, 12, 9);
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
        config.Teleportation.TopRequireTemporalGear.Should().BeFalse();
        config.Teleportation.BackWarmupSeconds.Should().Be(5);
        config.Teleportation.BackCooldownSeconds.Should().Be(300);
        config.Teleportation.BackExpiresAfterSeconds.Should().Be(300);
        config.Teleportation.BackCommandPrivilege.Should().Be("chat");
        config.Teleportation.BackRequireTemporalGear.Should().BeFalse();
        config.Teleportation.RegisterHomeCommands.Should().BeFalse();
        config.Teleportation.RegisterSpawnCommands.Should().BeFalse();
        config.Teleportation.RegisterStuckCommand.Should().BeFalse();
        config.Teleportation.RegisterTopCommand.Should().BeFalse();
        config.Teleportation.RegisterBackCommand.Should().BeFalse();
        config.Teleportation.CancelWarmupOnDamage.Should().BeTrue();
        config.Teleportation.CancelWarmupOnInteraction.Should().BeTrue();
        config.Teleportation.StuckCommandPrivilege.Should().Be("chat");
        config.Teleportation.StuckAdminNotifyPrivilege.Should().Be("commandplayer");

        GetSetting("HomeCommandPrivilege").TrySetValue(config, "home", out var homeError).Should().BeTrue(homeError);
        GetSetting("SetHomeCommandPrivilege").TrySetValue(config, "sethome", out var setHomeError).Should().BeTrue(setHomeError);
        GetSetting("SpawnCommandPrivilege").TrySetValue(config, "spawn", out var spawnError).Should().BeTrue(spawnError);
        GetSetting("SetSpawnCommandPrivilege").TrySetValue(config, "setspawn", out var setSpawnError).Should().BeTrue(setSpawnError);
        GetSetting("HomeSpawnRequireTemporalGear").TrySetValue(config, "true", out var gearError).Should().BeTrue(gearError);
        GetSetting("Teleportation.RegisterHomeCommands").TrySetValue(config, "true", out var registerHomeError).Should().BeTrue(registerHomeError);
        GetSetting("Teleportation.RegisterSpawnCommands").TrySetValue(config, "true", out var registerSpawnError).Should().BeTrue(registerSpawnError);
        GetSetting("Teleportation.RegisterStuckCommand").TrySetValue(config, "true", out var registerStuckError).Should().BeTrue(registerStuckError);
        GetSetting("Teleportation.RegisterTopCommand").TrySetValue(config, "true", out var registerTopError).Should().BeTrue(registerTopError);
        GetSetting("Teleportation.RegisterBackCommand").TrySetValue(config, "true", out var registerBackError).Should().BeTrue(registerBackError);
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
        var topGearSetting = GetSetting("Teleportation.TopRequireTemporalGear");
        topGearSetting.ReloadBehavior.Should().Be(ConfigAdminReloadBehavior.Live);
        topGearSetting.TrySetValue(config, "true", out var topGearError).Should().BeTrue(topGearError);
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
        config.Teleportation.RegisterHomeCommands.Should().BeTrue();
        config.Teleportation.RegisterSpawnCommands.Should().BeTrue();
        config.Teleportation.RegisterStuckCommand.Should().BeTrue();
        config.Teleportation.RegisterTopCommand.Should().BeTrue();
        config.Teleportation.RegisterBackCommand.Should().BeTrue();
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
        config.Teleportation.TopRequireTemporalGear.Should().BeTrue();
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
    public void SpectatorChatSettings_AreRegisteredAsLiveSettings()
    {
        var config = CreateConfig();
        var nicknameSetting = GetSetting("UseNicknameInSpectatorOOC");
        var placementSetting = GetSetting("AllowSpectatorPlacedEnvironmentalMessages");
        var protectionSetting = GetSetting("ProtectSpectatorRoleplayChat");

        nicknameSetting.Group.Should().Be("Chat/RP");
        nicknameSetting.ReloadBehavior.Should().Be(ConfigAdminReloadBehavior.Live);
        placementSetting.Group.Should().Be("Chat/RP");
        placementSetting.ReloadBehavior.Should().Be(ConfigAdminReloadBehavior.Live);
        protectionSetting.Group.Should().Be("Chat/RP");
        protectionSetting.ReloadBehavior.Should().Be(ConfigAdminReloadBehavior.Live);
        nicknameSetting.TrySetValue(config, "true", out var nicknameError).Should().BeTrue(nicknameError);
        placementSetting.TrySetValue(config, "false", out var placementError).Should().BeTrue(placementError);
        protectionSetting.TrySetValue(config, "false", out var protectionError).Should().BeTrue(protectionError);

        config.UseNicknameInSpectatorOOC.Should().BeTrue();
        config.AllowSpectatorPlacedEnvironmentalMessages.Should().BeFalse();
        config.ProtectSpectatorRoleplayChat.Should().BeFalse();
    }

    [Fact]
    public void SightBlockOverrideSettings_AreOptionalLivePatternLists()
    {
        var config = CreateConfig();
        var passThrough = GetSetting("SightPassThroughBlockCodePatterns");
        var blocking = GetSetting("SightBlockingBlockCodePatterns");

        passThrough.Group.Should().Be("Chat/Occlusion");
        passThrough.ReloadBehavior.Should().Be(ConfigAdminReloadBehavior.Live);
        blocking.Group.Should().Be("Chat/Occlusion");
        blocking.ReloadBehavior.Should().Be(ConfigAdminReloadBehavior.Live);
        passThrough.TrySetValue(config, " decorplus:brass-lattice-*, game:glass-* ", out var passError).Should().BeTrue(passError);
        blocking.TrySetValue(config, string.Empty, out var blockError).Should().BeTrue(blockError);

        config.SightPassThroughBlockCodePatterns.Should().Equal("decorplus:brass-lattice-*", "game:glass-*");
        config.SightBlockingBlockCodePatterns.Should().BeEmpty();
    }

    [Fact]
    public void ValidateConfig_RejectsUnqualifiedSightBlockPatternsAndExactConflicts()
    {
        var config = CreateConfig();
        config.SightPassThroughBlockCodePatterns = ["curtain-*", "decorplus:privacy-curtain-*"];
        config.SightBlockingBlockCodePatterns = ["DECORPLUS:PRIVACY-CURTAIN-*"];

        var errors = ConfigAdminSettingRegistry.ValidateConfig(config);

        errors.Should().Contain(error => error.Contains("fully qualified"));
        errors.Should().Contain(error => error.Contains("both sight override lists"));
    }

    [Fact]
    public void ValidateResolvedSightBlockPatterns_RejectsWildcardOverlap()
    {
        var config = CreateConfig();
        config.SightPassThroughBlockCodePatterns = ["decorplus:privacy-*"];
        config.SightBlockingBlockCodePatterns = ["decorplus:*-red"];
        var curtain = new Block
        {
            BlockId = 91,
            Code = new AssetLocation("decorplus:privacy-curtain-red")
        };

        var errors = ConfigAdminSettingRegistry.ValidateResolvedSightBlockPatterns(config, [curtain]);

        errors.Should().ContainSingle().Which.Should().Contain("decorplus:privacy-curtain-red");
    }

    [Fact]
    public void ValidateResolvedSightBlockPatterns_BoundsBroadWildcardOverlapErrors()
    {
        var config = CreateConfig();
        config.SightPassThroughBlockCodePatterns = ["game:block-*"];
        config.SightBlockingBlockCodePatterns = ["game:*-granite"];
        var blocks = Enumerable.Range(0, 12)
            .Select(index => new Block
            {
                BlockId = index + 1,
                Code = new AssetLocation($"game:block-{index:D2}-granite")
            });

        var errors = ConfigAdminSettingRegistry.ValidateResolvedSightBlockPatterns(config, blocks);

        errors.Should().ContainSingle();
        errors[0].Should().Contain("12 blocks");
        errors[0].Should().Contain("game:block-09-granite");
        errors[0].Should().NotContain("game:block-10-granite");
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
