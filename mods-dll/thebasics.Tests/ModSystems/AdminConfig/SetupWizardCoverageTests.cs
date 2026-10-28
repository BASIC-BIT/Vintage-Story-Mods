using FluentAssertions;
using thebasics.Configs;
using thebasics.ModSystems.AdminConfig;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class SetupWizardCoverageTests
{
    // Remove a key only when its destination is implemented and verified.
    private static readonly string[] PendingMigrationKeys =
    [
        "UseNicknameInGlobalOOC",
        "RequireClearSoundPathForSpeech.Normal",
        "RequireClearSoundPathForSpeech.Whisper",
        "RequireClearSoundPathForSpeech.Yell",
        "SightBlockingBlockCodePatterns",
        "SightPassThroughBlockCodePatterns",
        "SpeechOcclusionWallPenaltyBlocks",
        "ChatDelimiters.Bold.End",
        "ChatDelimiters.Bold.Start",
        "ChatDelimiters.Emote.End",
        "ChatDelimiters.Emote.Start",
        "ChatDelimiters.Environmental.End",
        "ChatDelimiters.Environmental.Start",
        "ChatDelimiters.GlobalOOC.End",
        "ChatDelimiters.GlobalOOC.Start",
        "ChatDelimiters.Italic.End",
        "ChatDelimiters.Italic.Start",
        "ChatDelimiters.OOC.End",
        "ChatDelimiters.OOC.Start",
        "ChatDelimiters.PlacedEnvironmental.End",
        "ChatDelimiters.PlacedEnvironmental.Start",
        "ChatDelimiters.Quote.End",
        "ChatDelimiters.Quote.Start",
        "ChatDelimiters.SignLanguageQuote.End",
        "ChatDelimiters.SignLanguageQuote.Start",
        "ProximityChatName",
        "ProximityChatTabPosition",
        "ApplyColorsToNicknames",
        "ApplyColorsToPlayerNames",
        "BoldNicknames",
        "EnableDistanceFontSizeSystem",
        "IsolateChatMessageFormatting",
        "ProximityChatClampFontSizes",
        "ProximityChatDefaultFontSize.Normal",
        "ProximityChatDefaultFontSize.Whisper",
        "ProximityChatDefaultFontSize.Yell",
        "RPTTS_ModeFalloff.Normal",
        "RPTTS_ModeFalloff.Whisper",
        "RPTTS_ModeFalloff.Yell",
        "RPTTS_ModeGain.Normal",
        "RPTTS_ModeGain.Whisper",
        "RPTTS_ModeGain.Yell",
        "ChatterModePitch.Normal",
        "ChatterModePitch.Whisper",
        "ChatterModePitch.Yell",
        "ChatterModeVolume.Normal",
        "ChatterModeVolume.Whisper",
        "ChatterModeVolume.Yell",
        "ChatterSelfVolumeMultiplier",
        "AttributeFreeformMessagesToPlayerName",
        "ProseNicknameToken",
        "ProximityChatModeBabbleVerb",
        "ProximityChatModePunctuation.Normal",
        "ProximityChatModePunctuation.Whisper",
        "ProximityChatModePunctuation.Yell",
        "ProximityChatModeQuestionVerbs.Normal",
        "ProximityChatModeQuestionVerbs.Whisper",
        "ProximityChatModeQuestionVerbs.Yell",
        "ProximityChatModeVerbs.Normal",
        "ProximityChatModeVerbs.Whisper",
        "ProximityChatModeVerbs.Yell",
        "RPTextTogglePermission",
        "EnableNearbyDeathMessagesInProximityChat",
        "MaxEnvironmentPlacementDistance",
        "DebugMode",
        "EnableTh3EssentialsDiscordRelay",
        "ChatHistoryFlushIntervalMilliseconds",
        "PlayerStatDistanceTravelledTimer",
        "PlayerStatToggles.BlockBreaks",
        "PlayerStatToggles.Deaths",
        "PlayerStatToggles.DistanceTravelled",
        "PlayerStatToggles.NpcKills",
        "PlayerStatToggles.PlayerKills",
        "SetHomeCommandPrivilege",
        "SetSpawnCommandPrivilege",
    ];

    private static string[] RegisteredKeys => ConfigAdminSettingRegistry.Settings.Select(setting => setting.Key).ToArray();
    private static string[] CoveredKeys => SetupWizardCatalog.Pages.SelectMany(page => page.SettingKeys).ToArray();

    [Fact]
    public void CatalogAndPendingInventoryPartitionTheScalarRegistry()
    {
        RegisteredKeys.Should().HaveCount(210).And.OnlyHaveUniqueItems();
        CoveredKeys.Distinct(StringComparer.Ordinal).Should().HaveCount(135);
        PendingMigrationKeys.Should().HaveCount(75).And.OnlyHaveUniqueItems();
        CoverageErrors(RegisteredKeys, CoveredKeys, PendingMigrationKeys).Should().BeEmpty();
        foreach (var key in PendingMigrationKeys)
            SetupWizardCatalog.IsSettingKey(key).Should().BeFalse($"{key} has no implemented wizard destination yet");
    }

    [Fact]
    public void CoverageRejectsANewUnassignedRegistryKey()
    {
        CoverageErrors(RegisteredKeys.Append("FutureSetting"), CoveredKeys, PendingMigrationKeys)
            .Should().Contain("Unassigned: FutureSetting");
    }

    [Fact]
    public void CoverageRejectsADestinationReferencingAnUnknownKey()
    {
        CoverageErrors(RegisteredKeys, CoveredKeys.Append("UnknownSetting"), PendingMigrationKeys)
            .Should().Contain("Unknown destination: UnknownSetting");
    }

    [Fact]
    public void CoverageRejectsAPendingKeyClaimedAsComplete()
    {
        var pending = PendingMigrationKeys[0];
        CoverageErrors(RegisteredKeys, CoveredKeys.Append(pending), PendingMigrationKeys)
            .Should().Contain($"Still pending: {pending}");
    }

    [Fact]
    public void CoverageAllowsTheSameSettingOnMultiplePages()
    {
        CoverageErrors(RegisteredKeys, CoveredKeys.Append(CoveredKeys[0]), PendingMigrationKeys)
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("chat.languageeditor", nameof(ModConfig.Languages))]
    [InlineData("characters.builder", nameof(ModConfig.CharacterSheetFields))]
    public void StructuredCollectionsHaveTheirOwnPagesOutsideScalarCoverage(string pageId, string collectionKey)
    {
        SetupWizardCatalog.TryGetPage(pageId, out var page).Should().BeTrue();
        page.SettingKeys.Should().BeEmpty();
        RegisteredKeys.Should().NotContain(collectionKey);
        CoveredKeys.Should().NotContain(collectionKey);
        PendingMigrationKeys.Should().NotContain(collectionKey);
        SetupWizardCatalog.IsSettingKey(collectionKey).Should().BeFalse();
    }

    private static IEnumerable<string> CoverageErrors(IEnumerable<string> registered, IEnumerable<string> covered,
        IEnumerable<string> pending)
    {
        var registeredKeys = registered.ToHashSet(StringComparer.Ordinal);
        var coveredKeys = covered.ToHashSet(StringComparer.Ordinal);
        var pendingKeys = pending.ToHashSet(StringComparer.Ordinal);
        return registeredKeys.Except(coveredKeys).Except(pendingKeys).Select(key => $"Unassigned: {key}")
            .Concat(coveredKeys.Except(registeredKeys).Select(key => $"Unknown destination: {key}"))
            .Concat(pendingKeys.Except(registeredKeys).Select(key => $"Unknown pending: {key}"))
            .Concat(coveredKeys.Intersect(pendingKeys).Select(key => $"Still pending: {key}"));
    }
}

