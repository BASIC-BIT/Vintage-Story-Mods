using FluentAssertions;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.ProximityChat.Models;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class SetupWizardStateTests
{
    [Fact]
    public void Catalog_UsesReadOnlyTopicsWithRegistryBackedPages()
    {
        var topicIds = new[] { "chat", "teleportation", "notifications", "appearance", "map", "characters", "roleplay", "server" };
        SetupWizardCatalog.Topics.Select(topic => topic.Id).Should().OnlyHaveUniqueItems().And.BeEquivalentTo(topicIds);
        if (SetupWizardCatalog.Topics is System.Collections.IList topics) topics.IsReadOnly.Should().BeTrue();
        foreach (var topic in SetupWizardCatalog.Topics)
        {
            topic.Title.Should().NotBeNullOrWhiteSpace();
            topic.Description.Should().NotBeNullOrWhiteSpace();
            SetupWizardCatalog.TryGetPage(topic.EntryPageId, out var entry).Should().BeTrue();
            entry.TopicId.Should().Be(topic.Id);
        }
        SetupWizardCatalog.Pages.Should().NotBeEmpty();
        SetupWizardCatalog.Pages.Select(page => page.Id).Should().OnlyHaveUniqueItems();
        SetupWizardCatalog.Pages.Select(page => page.TopicId).Distinct()
            .Should().BeEquivalentTo(topicIds);

        foreach (var page in SetupWizardCatalog.Pages)
        {
            page.Title.Should().NotBeNullOrWhiteSpace();
            page.Description.Should().NotBeNullOrWhiteSpace();
            if (page.Id is "chat.languageeditor" or "characters.builder") page.SettingKeys.Should().BeEmpty();
            else page.SettingKeys.Should().NotBeEmpty();
            SetupWizardCatalog.TryGetPage(page.Id, out var found).Should().BeTrue();
            found.Should().Be(page);
            foreach (var key in page.SettingKeys)
            {
                SetupWizardCatalog.IsSettingKey(key).Should().BeTrue();
                ConfigAdminSettingRegistry.TryGet(key, out _).Should().BeTrue($"{key} must use the real registry");
            }
        }

        SetupWizardCatalog.TryGetPage("unknown-page", out _).Should().BeFalse();
        SetupWizardCatalog.IsSettingKey("UnknownSetting").Should().BeFalse();
        SetupWizardCatalog.IsSettingKey("DebugMode").Should().BeTrue();
        SetupWizardCatalog.IsSettingKey("Languages").Should().BeFalse();
    }

    [Fact]
    public void Catalog_CoversIndependentChatTravelAndAnnouncementControls()
    {
        KeysFor("chat").Should().Contain(new[]
        {
            "DisableRPChat", "UseGeneralChannelAsProximityChat", "ProximityChatPresentationMode",
            "EnableLanguageSystem", "EnableGlobalOOC", "AllowOOCToggle", "EnableDistanceObfuscationSystem",
            "ProximityChatModeDistances.Whisper", "ProximityChatModeDistances.Normal", "ProximityChatModeDistances.Yell",
            "ProximityChatModeObfuscationRanges.Normal", "ProximityChatAsDefault", "PreserveDefaultChatChoice"
        });
        KeysFor("teleportation").Should().Contain(new[]
        {
            "AllowPlayerTpa", "Teleportation.RegisterHomeCommands", "Teleportation.RegisterSpawnCommands",
            "Teleportation.RegisterTopCommand", "Teleportation.RegisterBackCommand", "Teleportation.RegisterStuckCommand",
            "TpaRequireTemporalGear", "TpaCooldownInGameHours", "Teleportation.TpaWarmupSeconds",
            "Teleportation.MaxHomes", "TpaRequestPrivilege", "Teleportation.StuckBlockedByOnlinePrivilege"
        });
        KeysFor("notifications").Should().Contain(new[]
        {
            "SendServerSaveAnnouncement", "ServerSaveAnnouncementAsNotification", "TEXT_ServerSaveAnnouncement",
            "SendServerSaveFinishedAnnouncement", "ServerSaveFinishedAsNotification", "TEXT_ServerSaveFinished",
            "EnableSleepNotifications", "SleepNotificationThreshold", "TEXT_SleepNotification"
        });
    }

    [Fact]
    public void Catalog_CoversAppearanceMapCharactersRoleplayAndServerControls()
    {
        KeysFor("appearance").Should().Contain(new[] { "OverheadChatBubbleMode", "ShowNicknameInNametag", "EnableTypingIndicator" });
        KeysFor("map").Should().Contain(new[] { "ManageMapPlayerVisibility", "MapHideOtherPlayers", "MapPlayerRenderDistance" });
        KeysFor("characters").Should().Contain(new[] { "EnableCharacterSheets", "EnableRpCharacterSlots", "MaxRpCharacterSlots" });
        KeysFor("roleplay").Should().Contain(new[] { "EnableDiceRolling", "EnableSceneMarkers" });
        KeysFor("server").Should().Contain(new[] { "PlayerStatSystem", "EnableChatHistory", "EnableAdminNotes" });
    }

    [Theory]
    [InlineData("UnknownSetting")]
    [InlineData("Teleportation.UnknownCommand")]
    [InlineData("ReviewedConfigSettingKeys")]
    public void TryValidatePatch_RejectsUnknownAndNonWizardKeysWithoutMutatingCurrent(string key)
    {
        var current = CreateConfig();
        var state = new SetupWizardState(current);
        var request = Request([Value("EnableChatter", "0"), Value(key, "1")],
            [Value("EnableChatter", "1"), Value(key, "0")]);

        state.TryValidatePatch(current, request, out _, out _, out _, out var errors).Should().BeFalse();

        errors.Should().NotBeEmpty();
        current.EnableChatter.Should().BeTrue();
        current.DebugMode.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TryValidatePatch_RejectsDuplicateKeysCaseInsensitively(bool duplicateOriginals)
    {
        var current = CreateConfig();
        var request = Request([Value("EnableChatter", "0")], [Value("EnableChatter", "1")]);
        (duplicateOriginals ? request.OriginalValues : request.Values).Add(Value("enablechatter", "0"));

        new SetupWizardState(current).TryValidatePatch(current, request, out _, out _, out _, out var errors)
            .Should().BeFalse();

        errors.Should().NotBeEmpty();
        current.EnableChatter.Should().BeTrue();
    }

    [Fact]
    public void TryValidatePatch_RejectsConflictingOriginalAtomically()
    {
        var current = CreateConfig();
        var state = new SetupWizardState(current);
        current.TpaCooldownInGameHours = 3;
        var request = Request([Value("EnableChatter", "0"), Value("TpaCooldownInGameHours", "2")],
            [Value("EnableChatter", "1"), Value("TpaCooldownInGameHours", "0.5")]);

        state.TryValidatePatch(current, request, out _, out _, out var conflicts, out _).Should().BeFalse();

        conflicts.Should().Equal("TpaCooldownInGameHours");
        current.EnableChatter.Should().BeTrue();
        current.TpaCooldownInGameHours.Should().Be(3);
    }

    [Fact]
    public void TryValidatePatch_UsesRegistryCrossValidation()
    {
        var current = CreateConfig();
        var request = Request([Value("ProximityChatModeDistances.Normal", "15")],
            [Value("ProximityChatModeDistances.Normal", "35")]);

        new SetupWizardState(current).TryValidatePatch(current, request, out _, out _, out var conflicts, out var errors)
            .Should().BeFalse();

        conflicts.Should().BeEmpty();
        errors.Should().Contain(error => error.Contains("Normal range must be greater than its obfuscation start"));
        current.ProximityChatModeDistances[ProximityChatMode.Normal].Should().Be(35);
    }

    [Fact]
    public void TryValidatePatch_ReturnsDetachedDraftAndPreservesUnrelatedCurrentValues()
    {
        var current = CreateConfig();
        var state = new SetupWizardState(current);
        current.Teleportation.HomeWarmupSeconds = 42;
        current.IsolateChatMessageFormatting = false;
        var request = Request([Value("EnableChatter", "false")], [Value("EnableChatter", "1")]);

        state.TryValidatePatch(current, request, out var draft, out var changed, out var conflicts, out var errors)
            .Should().BeTrue();

        errors.Should().BeEmpty();
        conflicts.Should().BeEmpty();
        changed.Should().Equal("EnableChatter");
        draft.Should().NotBeSameAs(current);
        draft.EnableChatter.Should().BeFalse();
        draft.Teleportation.HomeWarmupSeconds.Should().Be(42);
        draft.IsolateChatMessageFormatting.Should().BeFalse();
        draft.Teleportation.HomeWarmupSeconds = 7;
        current.EnableChatter.Should().BeTrue();
        current.Teleportation.HomeWarmupSeconds.Should().Be(42);
    }

    [Fact]
    public void PendingRestart_UsesCapturedStartupValuesAcrossReopenNoOpSaveAndRevert()
    {
        var current = CreateConfig();
        var state = new SetupWizardState(current);
        state.GetPendingRestartKeys(current).Should().BeEmpty();

        current.DisableRPChat = true;
        current.ChatHistoryFlushIntervalMilliseconds += 100;
        current.EnableChatter = false;
        var expected = new[] { "DisableRPChat", "ChatHistoryFlushIntervalMilliseconds" };
        state.GetPendingRestartKeys(current).Should().BeEquivalentTo(expected);
        state.TryValidatePatch(current, Request([], []), out var reopened, out var changed, out _, out _)
            .Should().BeTrue();
        changed.Should().BeEmpty();
        state.GetPendingRestartKeys(reopened).Should().BeEquivalentTo(expected);

        current.DisableRPChat = false;
        current.ChatHistoryFlushIntervalMilliseconds -= 100;
        state.GetPendingRestartKeys(current).Should().BeEmpty();
    }

    [Fact]
    public void ValidateRequest_RejectsMissingOrExtraOriginals()
    {
        SetupWizardState.ValidateRequest(Request([Value("EnableChatter", "0")], [])).Should().NotBeEmpty();
        SetupWizardState.ValidateRequest(Request([], [Value("EnableChatter", "1")])).Should().NotBeEmpty();
    }

    [Fact]
    public void LanguagePatchAndSettingsProduceOneDetachedDraftPreservingGrants()
    {
        var current = CreateConfig();
        var originals = LanguageConfigAdmin.BuildEntries(current);
        var request = Request([Value("EnableChatter", "0")], [Value("EnableChatter", "1")]);
        request.OriginalLanguages = LanguageConfigAdmin.CloneEntries(originals);
        request.Languages = LanguageConfigAdmin.CloneEntries(originals);
        request.Languages[0].Name = "River speech";
        request.Languages[0].Prefix = "river";
        request.Languages[0].Color = "#34AACC";
        request.Languages[0].GrantedToClasses = "hunter, tailor";
        request.Languages[0].GrantedToModels = "seraph";
        request.Languages[0].GrantedToModelGroups = "folk";
        request.Languages[0].GrantedToTraits = "keenears";

        new SetupWizardState(current).TryValidatePatch(current, request, out var draft, out var changed, out var conflicts, out var errors)
            .Should().BeTrue();

        errors.Should().BeEmpty();
        conflicts.Should().BeEmpty();
        changed.Should().BeEquivalentTo("EnableChatter", "Languages");
        draft.EnableChatter.Should().BeFalse();
        draft.Languages[0].Name.Should().Be("River speech");
        draft.Languages[0].GrantedToClasses.Should().Equal("hunter", "tailor");
        draft.Languages[0].GrantedToModels.Should().Equal("seraph");
        draft.Languages[0].GrantedToModelGroups.Should().Equal("folk");
        draft.Languages[0].GrantedToTraits.Should().Equal("keenears");
        LanguageConfigAdmin.EntriesEqual(LanguageConfigAdmin.BuildEntries(current), originals).Should().BeTrue();
        current.EnableChatter.Should().BeTrue();
    }

    [Fact]
    public void StaleLanguageSnapshotRejectsTheWholePatch()
    {
        var current = CreateConfig();
        var state = new SetupWizardState(current);
        var request = Request([Value("EnableChatter", "0")], [Value("EnableChatter", "1")]);
        request.OriginalLanguages = LanguageConfigAdmin.BuildEntries(current);
        request.Languages = LanguageConfigAdmin.CloneEntries(request.OriginalLanguages);
        request.Languages[0].Color = "#ABCDEF";
        current.Languages[0] = current.Languages[0] with { Description = "Changed by another administrator" };

        state.TryValidatePatch(current, request, out var draft, out _, out var conflicts, out var errors).Should().BeFalse();

        draft.Should().BeNull();
        conflicts.Should().Equal("Languages");
        errors.Should().Contain(error => error.Contains("changed since setup opened"));
        current.EnableChatter.Should().BeTrue();
        current.Languages[0].Description.Should().Be("Changed by another administrator");
    }

    [Fact]
    public void ScalarPatchPreservesLanguagesChangedSinceSetupOpened()
    {
        var current = CreateConfig();
        var state = new SetupWizardState(current);
        current.Languages[0] = current.Languages[0] with { Description = "Changed by another administrator" };

        state.TryValidatePatch(current, Request([Value("EnableChatter", "0")], [Value("EnableChatter", "1")]),
            out var draft, out var changed, out _, out var errors).Should().BeTrue();

        errors.Should().BeEmpty();
        changed.Should().Equal("EnableChatter");
        draft.Languages[0].Description.Should().Be("Changed by another administrator");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvalidLanguagesRejectScalarChangesAtomically(bool removeAll)
    {
        var current = CreateConfig();
        var request = Request([Value("EnableChatter", "0")], [Value("EnableChatter", "1")]);
        request.OriginalLanguages = LanguageConfigAdmin.BuildEntries(current);
        request.Languages = removeAll ? [] : LanguageConfigAdmin.CloneEntries(request.OriginalLanguages);
        if (!removeAll) request.Languages[0].Color = "not-a-color";

        new SetupWizardState(current).TryValidatePatch(current, request, out var draft, out _, out _, out var errors)
            .Should().BeFalse();

        draft.Should().BeNull();
        errors.Should().Contain(error => error.Contains(removeAll ? "At least one language" : "hex color"));
        current.EnableChatter.Should().BeTrue();
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    public void InvalidRenameIdentityRejectsLanguagePatch(string issue)
    {
        var current = CreateConfig();
        var request = Request([], []);
        request.OriginalLanguages = LanguageConfigAdmin.BuildEntries(current);
        request.Languages = LanguageConfigAdmin.CloneEntries(request.OriginalLanguages);
        if (issue == "unknown") request.Languages[0].OriginalName = "Missing language";
        else request.Languages.Add(request.Languages[0] with { Name = "New speech", Prefix = "new" });

        new SetupWizardState(current).TryValidatePatch(current, request, out _, out _, out _, out var errors)
            .Should().BeFalse();

        errors.Should().Contain(error => error.Contains("original name"));
    }

    [Fact]
    public void NoOpLanguagePatchDoesNotReportChangedLanguages()
    {
        var current = CreateConfig();
        var request = Request([], []);
        request.OriginalLanguages = LanguageConfigAdmin.BuildEntries(current);
        request.Languages = LanguageConfigAdmin.CloneEntries(request.OriginalLanguages);

        new SetupWizardState(current).TryValidatePatch(current, request, out _, out var changed, out _, out var errors)
            .Should().BeTrue();

        errors.Should().BeEmpty();
        changed.Should().BeEmpty();
    }

    [Theory]
    [InlineData("rows")]
    [InlineData("field")]
    [InlineData("payload")]
    [InlineData("null-row")]
    [InlineData("null-list")]
    [InlineData("non-save")]
    public void LanguagePayloadRejectsOversizedMissingAndNonSaveData(string issue)
    {
        var request = Request([], []);
        var entry = LanguageConfigAdmin.BuildEntries(CreateConfig())[0];
        request.Languages = issue switch
        {
            "rows" => Enumerable.Repeat(entry, 257).ToList(),
            "field" => [entry with { GrantedToTraits = new string('x', 4097) }],
            "payload" => Enumerable.Repeat(entry with { Description = new string('x', 4096), GrantedToClasses = new string('x', 4096) }, 40).ToList(),
            "null-row" => [null!],
            "null-list" => null!,
            _ => [entry]
        };
        if (issue == "non-save") request.Kind = SetupWizardRequestKind.Open;

        SetupWizardState.ValidateRequest(request).Should().NotBeEmpty();
    }

    [Theory]
    [InlineData(SetupWizardRequestKind.InviteStart)]
    [InlineData(SetupWizardRequestKind.InviteDismiss)]
    [InlineData(SetupWizardRequestKind.Save)]
    [InlineData(SetupWizardRequestKind.Track)]
    [InlineData(SetupWizardRequestKind.Closed)]
    public void ValidateRequest_RejectsCaptureModeOutsideOpen(SetupWizardRequestKind kind)
    {
        SetupWizardState.ValidateRequest(new TheBasicsSetupWizardRequestMessage
        {
            Kind = kind,
            RequestId = 1,
            CaptureOnly = true
        }).Should().Contain("Only open requests may use QA capture mode.");
    }

    private static IEnumerable<string> KeysFor(string topic) => SetupWizardCatalog.Pages
        .Where(page => page.TopicId == topic).SelectMany(page => page.SettingKeys);

    private static ModConfig CreateConfig()
    {
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        return config;
    }

    private static ConfigAdminSettingValue Value(string key, string value) => new() { Key = key, Value = value };

    private static TheBasicsSetupWizardRequestMessage Request(
        List<ConfigAdminSettingValue> values, List<ConfigAdminSettingValue> originals) => new()
        {
            Kind = SetupWizardRequestKind.Save,
            RequestId = 1,
            RunId = "run-a",
            Values = values,
            OriginalValues = originals
        };
}

public class SetupWizardInvitationStateTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryOffer_RequiresBothReadinessEventsInEitherOrderAndOnlyOffersOnce(bool readyFirst)
    {
        var state = new SetupWizardInvitationState();
        if (readyFirst) state.MarkClientReady("admin");
        else state.MarkPlaying("admin");
        state.TryOffer("admin", alreadySeen: false, authorized: true).Should().BeFalse();

        if (readyFirst) state.MarkPlaying("admin");
        else state.MarkClientReady("admin");
        state.TryOffer("admin", alreadySeen: false, authorized: true).Should().BeTrue();
        state.TryOffer("admin", alreadySeen: false, authorized: true).Should().BeFalse();
    }

    [Fact]
    public void TryOffer_DoesNotConsumeUnauthorizedOpportunityOrOfferToAcknowledgedAdmins()
    {
        var state = new SetupWizardInvitationState();
        state.MarkClientReady("admin");
        state.MarkPlaying("admin");

        state.TryOffer("admin", alreadySeen: false, authorized: false).Should().BeFalse();
        state.TryOffer("admin", alreadySeen: true, authorized: true).Should().BeFalse();
        state.TryOffer("admin", alreadySeen: false, authorized: true).Should().BeTrue();
    }

    [Fact]
    public void Clear_ResetsOnlyThatConnectionAndRequiresBothReadinessEventsAgain()
    {
        var state = new SetupWizardInvitationState();
        foreach (var uid in new[] { "first", "second" })
        {
            state.MarkClientReady(uid);
            state.MarkPlaying(uid);
        }
        state.TryOffer("first", alreadySeen: false, authorized: true).Should().BeTrue();

        state.Clear("first");

        state.TryOffer("first", alreadySeen: false, authorized: true).Should().BeFalse();
        state.TryOffer("second", alreadySeen: false, authorized: true).Should().BeTrue();
        state.MarkClientReady("first");
        state.TryOffer("first", alreadySeen: false, authorized: true).Should().BeFalse();
        state.MarkPlaying("first");
        state.TryOffer("first", alreadySeen: false, authorized: true).Should().BeTrue();
    }
}
