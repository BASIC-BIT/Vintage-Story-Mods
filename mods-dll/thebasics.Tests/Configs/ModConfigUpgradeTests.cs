using FluentAssertions;
using Newtonsoft.Json;
using ProtoBuf;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;

namespace thebasics.Tests.Configs;

/// <summary>
/// Upgrade-path coverage for a live mod. An existing server's the_basics.json was written by an
/// older version and knows nothing about the fields added here. The per-mode dictionaries are
/// initialized with <c>??=</c> on the whole dictionary, so a config that already carries a
/// dictionary never gains new keys inside it, and every new lookup has to survive that.
/// </summary>
public class ModConfigUpgradeTests
{
    /// <summary>
    /// The proximity-chat shape a pre-upgrade server config carries: the old dictionaries present
    /// and fully populated, none of the new keys present anywhere.
    /// </summary>
    private const string LegacyConfigJson = """
    {
      "ProximityChatModeDistances": { "Yell": 90, "Normal": 35, "Whisper": 5 },
      "ProximityChatModeObfuscationRanges": { "Yell": 45, "Normal": 15, "Whisper": 2 },
      "ProximityChatDefaultFontSize": { "Yell": 30, "Normal": 16, "Whisper": 12 },
      "ProximityChatClampFontSizes": [30, 16, 12, 6],
      "ProximityChatModeVerbs": {
        "Yell": ["yells", "shouts"],
        "Normal": ["says"],
        "Whisper": ["whispers"]
      },
      "ProximityChatModePunctuation": { "Yell": "!", "Normal": ".", "Whisper": "." },
      "EnableDistanceObfuscationSystem": true,
      "EnableGlobalOOC": true,
      "EnableLanguageSystem": true
    }
    """;

    private static ModConfig LoadLegacyConfig()
    {
        var config = JsonConvert.DeserializeObject<ModConfig>(LegacyConfigJson);
        config.Should().NotBeNull();

        // Mirrors BaseBasicModSystem, which calls this after loading the JSON from disk.
        config!.InitializeDefaultsIfNeeded();
        return config;
    }

    [Fact]
    public void LegacyConfigGainsQuestionVerbsWithoutLosingConfiguredVerbs()
    {
        var config = LoadLegacyConfig();

        config.ProximityChatModeQuestionVerbs.Should().NotBeNull();
        config.ProximityChatModeVerbs[ProximityChatMode.Yell].Should().BeEquivalentTo(["yells", "shouts"]);
    }

    [Fact]
    public void LegacyConfigGainsOcclusionDefaultsThatKeepBothExperimentsOff()
    {
        var config = LoadLegacyConfig();

        config.SpeechOcclusionWallPenaltyBlocks.Should().Be(0);
        config.RequireClearSoundPathForSpeech.Should().NotBeNull();
        config.RequireClearSoundPathForSpeech.Values.Should().AllSatisfy(v => v.Should().BeFalse());
    }

    [Fact]
    public void LegacyConfigGainsSpectatorChatDefaults()
    {
        var config = LoadLegacyConfig();

        config.UseNicknameInSpectatorOOC.Should().BeFalse();
        config.AllowSpectatorPlacedEnvironmentalMessages.Should().BeTrue();
        config.ProtectSpectatorRoleplayChat.Should().BeTrue();
    }

    [Fact]
    public void LegacyConfigGainsEmptySightBlockOverridePatterns()
    {
        var config = LoadLegacyConfig();

        config.SightPassThroughBlockCodePatterns.Should().BeEmpty();
        config.SightBlockingBlockCodePatterns.Should().BeEmpty();
    }

    [Fact]
    public void TheRetiredFontFloorIsUpgradedOnAnExistingConfig()
    {
        // Every successful load rewrites the config, so a running server has the retired default
        // written out explicitly. Without an upgrade path the readability fix reaches nobody who
        // already runs the mod.
        var config = LoadLegacyConfig();

        config.ProximityChatClampFontSizes.Should().Equal(30, 16, 12, 9);
    }

    [Fact]
    public void CustomClampFontSizesAreLeftAlone()
    {
        // Only the exact retired array is replaced. Anything a server actually chose survives.
        const string json = """
        { "ProximityChatClampFontSizes": [28, 14, 10, 6] }
        """;

        var config = JsonConvert.DeserializeObject<ModConfig>(json);
        config!.InitializeDefaultsIfNeeded();

        config.ProximityChatClampFontSizes.Should().Equal(28, 14, 10, 6);
    }

    [Fact]
    public void RenamedSoundPathKeyIsReadFromTheOldConfigKey()
    {
        // A live server's the_basics.json still says RequireLineOfSightForSpeech. Dropping it on
        // rename would silently turn the experiment off for anyone who had switched it on.
        const string json = """
        {
          "RequireLineOfSightForSpeech": { "Yell": true, "Normal": false, "Whisper": false }
        }
        """;

        var config = JsonConvert.DeserializeObject<ModConfig>(json);
        config!.InitializeDefaultsIfNeeded();

        config.RequireClearSoundPathForSpeech[ProximityChatMode.Yell].Should().BeTrue();
        config.RequireClearSoundPathForSpeech[ProximityChatMode.Normal].Should().BeFalse();
    }

    [Fact]
    public void CurrentSoundPathKeyWinsWhenAConfigCarriesBoth()
    {
        // A config rewritten by the admin panel can carry the new key while the hand-edited old one
        // lingers. The current key has to win regardless of which appears first in the document.
        const string json = """
        {
          "RequireClearSoundPathForSpeech": { "Yell": true, "Normal": true, "Whisper": true },
          "RequireLineOfSightForSpeech": { "Yell": false, "Normal": false, "Whisper": false }
        }
        """;

        var config = JsonConvert.DeserializeObject<ModConfig>(json);
        config!.InitializeDefaultsIfNeeded();

        config.RequireClearSoundPathForSpeech.Values.Should().AllSatisfy(required => required.Should().BeTrue());
    }

    [Theory]
    [InlineData(ProximityChatMode.Normal)]
    [InlineData(ProximityChatMode.Whisper)]
    [InlineData(ProximityChatMode.Yell)]
    public void VerbResolutionNeverThrowsOnALegacyConfig(ProximityChatMode mode)
    {
        var config = LoadLegacyConfig();

        // Both branches: statement and question, on a config that predates question verbs.
        ChatHelper.GetProximityChatVerb(null, mode, config, "hello.").Should().NotBeNullOrWhiteSpace();
        ChatHelper.GetProximityChatVerb(null, mode, config, "hello?").Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void PartialModeDictionaryFallsBackInsteadOfThrowing()
    {
        // A hand-edited config can carry a dictionary that is missing a mode entirely. The old code
        // indexed these directly; every lookup must degrade instead of throwing on chat.
        //
        // Degrading means the mode's real default, not a placeholder. An earlier version of this
        // fallback used the enum name and rendered as `Alice whisper "anyone?"`.
        var config = LoadLegacyConfig();
        config.ProximityChatModeQuestionVerbs.Remove(ProximityChatMode.Whisper);
        config.ProximityChatModeVerbs.Remove(ProximityChatMode.Whisper);
        config.RequireClearSoundPathForSpeech.Remove(ProximityChatMode.Whisper);

        var verb = ChatHelper.GetProximityChatVerb(null, ProximityChatMode.Whisper, config, "anyone?");

        verb.Should().BeOneOf("whispers", "mumbles", "mutters");
    }

    [Fact]
    public void NewFieldsSurviveProtobufRoundTripToClients()
    {
        // ModConfig is ProtoContract because it is synced to clients. New ProtoMember ids must not
        // collide with existing ones, which would silently corrupt an unrelated field.
        var config = LoadLegacyConfig();
        config.ProximityChatModeQuestionVerbs[ProximityChatMode.Normal] = ["inquires"];
        config.RequireClearSoundPathForSpeech[ProximityChatMode.Yell] = true;
        config.SpeechOcclusionWallPenaltyBlocks = 7;
        config.ProximityChatModeDistances[ProximityChatMode.Normal] = ModConfig.UnlimitedRange;
        config.UseNicknameInSpectatorOOC = true;
        config.AllowSpectatorPlacedEnvironmentalMessages = false;
        config.ProtectSpectatorRoleplayChat = false;
        config.SightPassThroughBlockCodePatterns = ["decorplus:brass-lattice-*"];
        config.SightBlockingBlockCodePatterns = ["decorplus:privacy-curtain-*"];

        using var stream = new MemoryStream();
        Serializer.Serialize(stream, config);
        stream.Position = 0;
        var restored = Serializer.Deserialize<ModConfig>(stream);

        restored.ProximityChatModeQuestionVerbs[ProximityChatMode.Normal].Should().BeEquivalentTo(["inquires"]);
        restored.RequireClearSoundPathForSpeech[ProximityChatMode.Yell].Should().BeTrue();
        restored.SpeechOcclusionWallPenaltyBlocks.Should().Be(7);
        restored.ProximityChatModeDistances[ProximityChatMode.Normal].Should().Be(ModConfig.UnlimitedRange);
        restored.UseNicknameInSpectatorOOC.Should().BeTrue();
        restored.AllowSpectatorPlacedEnvironmentalMessages.Should().BeFalse();
        restored.ProtectSpectatorRoleplayChat.Should().BeFalse();
        restored.SightPassThroughBlockCodePatterns.Should().Equal("decorplus:brass-lattice-*");
        restored.SightBlockingBlockCodePatterns.Should().Equal("decorplus:privacy-curtain-*");

        // Neighbouring fields must be untouched by the new ids.
        restored.ProximityChatModeVerbs[ProximityChatMode.Yell].Should().BeEquivalentTo(["yells", "shouts"]);
        restored.ProximityChatModeBabbleVerb.Should().Be(config.ProximityChatModeBabbleVerb);
        restored.ProximityChatModePunctuation[ProximityChatMode.Yell].Should().Be("!");
    }

    [Fact]
    public void UnlimitedRangeIsAcceptedByValidationButZeroIsNot()
    {
        var config = LoadLegacyConfig();

        config.ProximityChatModeDistances[ProximityChatMode.Yell] = ModConfig.UnlimitedRange;
        ConfigAdminSettingRegistry.ValidateConfig(config)
            .Should().NotContain(error => error.Contains("Yell"));

        config.ProximityChatModeDistances[ProximityChatMode.Yell] = 0;
        ConfigAdminSettingRegistry.ValidateConfig(config)
            .Should().Contain(error => error.Contains("Yell"));
    }
}
