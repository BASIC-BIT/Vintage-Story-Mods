using System.Reflection;
using FluentAssertions;
using NSubstitute;
using NSubstitute.Extensions;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.AdminConfig;

[Collection(AnalyticsServiceTestCollection.Name)]
public class SetupWizardServerTests : IDisposable
{
    public SetupWizardServerTests() => AnalyticsService.Shutdown();
    public void Dispose() => AnalyticsService.Shutdown();

    [Fact]
    public void RevokedPrivilegeDeniesSaveWithoutWritingConfig()
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        var run = results.Last().RunId;
        player.PrivilegeCheck = _ => false;

        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 4, RunId = run,
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }]
        });

        results.Last().Kind.Should().Be(SetupWizardResultKind.Denied);
        results.Last().RequestId.Should().Be(4);
        results.Last().Success.Should().BeFalse();
        results.Last().Languages.Should().BeEmpty();
        system.Config.EnableChatter.Should().BeTrue();
        api.DidNotReceive().StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());
    }

    [Fact]
    public void DismissingInvitationLeavesOneReopenMessageAndEndsRun()
    {
        var (system, _, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true, ConnectionState = EnumClientState.Playing };
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        var request = new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.InviteDismiss, RunId = results.Last().RunId };

        system.OnSetupWizardRequest(player, request);
        system.OnSetupWizardRequest(player, request);

        player.SentMessages.Should().ContainSingle().Which.Message.Should()
            .Be("You can configure The BASICs later with /basic setup.");
        results.Last().Kind.Should().Be(SetupWizardResultKind.Denied);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InvitationJourneySeparatesOfferViewAndAcceptanceFromCommandEntry(bool analyticsEnabled)
    {
        var (system, _, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        var sink = Substitute.For<IAnalyticsSink>();
        sink.IsEnabled.Returns(analyticsEnabled);
        var events = new List<IDictionary<string, object>>();
        sink.When(x => x.Track("setup wizard journey", Arg.Any<IDictionary<string, object>>()))
            .Do(call => events.Add(call.Arg<IDictionary<string, object>>()));
        AnalyticsService.Configure(sink);
        OfferInvitation(system, player);
        var runId = results.Last().RunId;
        events.Should().HaveCount(analyticsEnabled ? 1 : 0);

        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Track, RunId = runId, StepId = "invitation", JourneyAction = "viewed"
        });
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.InviteStart, RequestId = 1, RunId = runId
        });
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });

        events.Should().HaveCount(analyticsEnabled ? 4 : 0);
        if (!analyticsEnabled) return;
        events.Select(properties => (properties["wizard_step_id"], properties["wizard_action"]))
            .Should().Equal(("invitation", "offered"), ("invitation", "viewed"),
                ("invitation", "started"), ("start", "opened"));
        events.Select(properties => properties["wizard_sequence"]).Should().Equal(1, 2, 3, 4);
        events.Should().OnlyContain(properties => (string)properties["wizard_run_id"] == runId &&
            !properties.ContainsKey("pseudonymous_player_id"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaleOrUnauthorizedInvitationViewIsNotTracked(bool revokedPrivilege)
    {
        var (system, _, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        OfferInvitation(system, player);
        var runId = results.Last().RunId;
        var sink = Substitute.For<IAnalyticsSink>();
        sink.IsEnabled.Returns(true);
        AnalyticsService.Configure(sink);
        if (revokedPrivilege) player.PrivilegeCheck = _ => false;

        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Track, RunId = revokedPrivilege ? runId : new string('e', 32),
            StepId = "invitation", JourneyAction = "viewed"
        });

        results.Last().Kind.Should().Be(SetupWizardResultKind.Denied);
        sink.DidNotReceive().Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
    }

    [Fact]
    public void CaptureOpenDoesNotAcknowledgeInvitationTrackJourneyOrReplaceNormalRun()
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        var sink = Substitute.For<IAnalyticsSink>();
        sink.IsEnabled.Returns(true);
        AnalyticsService.Configure(sink);
        var capture = new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open, CaptureOnly = true, RequestId = 1 };

        system.OnSetupWizardRequest(player, capture);

        results.Last().Kind.Should().Be(SetupWizardResultKind.Open);
        results.Last().Values.Should().NotBeEmpty();
        player.GetModData("thebasics:setup-invitation-seen-v1", false).Should().BeFalse();
        sink.DidNotReceive().Track(Arg.Any<string>(), Arg.Any<IDictionary<string, object>>());
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 2, RunId = results.Last().RunId,
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }]
        });
        results.Last().Kind.Should().Be(SetupWizardResultKind.Denied);
        api.DidNotReceive().StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());

        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        var normalRun = results.Last().RunId;
        system.OnSetupWizardRequest(player, capture);
        results.Last().RunId.Should().NotBe(normalRun);
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        results.Last().RunId.Should().Be(normalRun);
        sink.Received(2).Track("setup wizard journey", Arg.Any<IDictionary<string, object>>());
    }

    [Fact]
    public void PersistedSaveAcknowledgesLiveFailureAndCachesSuccessForRetry()
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        api.World.AllOnlinePlayers.Returns(_ => throw new InvalidOperationException("broadcast unavailable"));
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 3, RunId = results.Last().RunId,
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }]
        };

        request.OriginalSheetFields = CharacterSheetFieldConfigAdmin.BuildEntries(system.Config);
        request.SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(request.OriginalSheetFields);
        request.SheetFields[0].Label = "Persisted before live failure";
        system.OnSetupWizardRequest(player, request);

        var saved = results.Last();
        saved.SheetFields[0].Label.Should().Be("Persisted before live failure");
        system.Config.CharacterSheetFields[0].Label.Should().Be("Persisted before live failure");
        saved.Success.Should().BeTrue();
        saved.RuntimeApplyFailed.Should().BeTrue();
        saved.Message.Should().Contain("Settings saved").And.Contain("Restart");
        saved.RestartRequiredKeys.Should().BeEmpty();
        system.Config.EnableChatter.Should().BeFalse();
        system.OnSetupWizardRequest(player, request);
        results.Last().Should().BeSameAs(saved);
        api.Received(1).StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());

        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        results.Last().RuntimeApplyFailed.Should().BeTrue();
    }

    [Fact]
    public void LostSaveAcknowledgementCanBeRetriedWithoutWritingAgain()
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        api.World.AllOnlinePlayers.Returns(Array.Empty<IPlayer>());
        var channel = (IServerNetworkChannel)typeof(RPProximityChatSystem)
            .GetField("_serverConfigChannel", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(system)!;
        var rejectSaveReply = true;
        channel.When(x => x.SendPacket(Arg.Is<TheBasicsSetupWizardResultMessage>(result => result.Kind == SetupWizardResultKind.SaveResult), Arg.Any<IServerPlayer[]>()))
            .Do(_ => { if (rejectSaveReply) throw new InvalidOperationException("reply transport unavailable"); });
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 4, RunId = results.Last().RunId,
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }]
        };

        var firstSend = () => system.OnSetupWizardRequest(player, request);
        firstSend.Should().Throw<InvalidOperationException>();
        rejectSaveReply = false;
        system.OnSetupWizardRequest(player, request);

        api.Received(1).StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());
        channel.Received(2).SendPacket(Arg.Is<TheBasicsSetupWizardResultMessage>(result =>
            result.Kind == SetupWizardResultKind.SaveResult && result.RequestId == 4 && result.Success), Arg.Any<IServerPlayer[]>());
    }

    [Fact]
    public void OpenAndCaptureReturnActualConfiguredLanguagesWithoutWriting()
    {
        var (system, api, results) = CreateSystem();
        system.Config.Languages = [new Language("River speech", "A local dialect", "river", ["ra", "vo"], "#34AACC", true,
            GrantedToClasses: ["hunter"], GrantedToModels: ["seraph"], GrantedToModelGroups: ["folk"], GrantedToTraits: ["keenears"])];
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };

        foreach (var captureOnly in new[] { false, true })
        {
            system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open, CaptureOnly = captureOnly });

            results.Last().Languages.Should().BeEquivalentTo(LanguageConfigAdmin.BuildEntries(system.Config));
            results.Last().Languages.Single().OriginalName.Should().Be("River speech");
            results.Last().Languages.Single().GrantedToTraits.Should().Be("keenears");
        }
        api.DidNotReceive().StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());
    }

    [Fact]
    public void SaveLanguagesPersistsOneAtomicDraftReconcilesRenamesAndCachesRetry()
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        var languageIndex = system.Config.Languages.ToList().FindIndex(language => !language.Default);
        var oldLanguage = system.Config.Languages[languageIndex];
        player.SetLanguages([oldLanguage.Name]);
        player.SetDefaultLanguage(oldLanguage);
        player.SetLanguageSkills(new Dictionary<string, int> { [oldLanguage.Name] = 65 });
        var learner = new FakeServerPlayer("learner");
        learner.SetLanguages([]);
        learner.SetLanguageSkills(new Dictionary<string, int> { [oldLanguage.Name] = 65 });
        api.World.AllOnlinePlayers.Returns([player, learner]);
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 5, RunId = results.Last().RunId,
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }],
            OriginalLanguages = LanguageConfigAdmin.CloneEntries(results.Last().Languages),
            Languages = LanguageConfigAdmin.CloneEntries(results.Last().Languages)
        };
        request.OriginalSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(results.Last().SheetFields);
        request.SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(results.Last().SheetFields);
        request.SheetFields[0].Label = "Atomic field";
        request.Languages[languageIndex].Name = "River speech";
        request.Languages[languageIndex].Prefix = "river";
        request.Languages[languageIndex].GrantedToClasses = "hunter";

        system.OnSetupWizardRequest(player, request);

        var saved = results.Last();
        saved.Success.Should().BeTrue();
        system.Config.EnableChatter.Should().BeFalse();
        system.Config.Languages[languageIndex].Name.Should().Be("River speech");
        system.Config.Languages[languageIndex].GrantedToClasses.Should().Equal("hunter");
        player.GetLanguages().Should().Equal("River speech");
        player.GetDefaultLanguageName().Should().Be("River speech");
        player.GetLanguageSkills().Should().BeEmpty();
        learner.GetLanguageSkills().Should().ContainKey("River speech").WhoseValue.Should().Be(65);
        GetPendingRenames(system).Should().ContainKey(oldLanguage.Name).WhoseValue.Should().Be("River speech");
        saved.Languages[languageIndex].OriginalName.Should().Be("River speech");
        saved.SheetFields[0].Label.Should().Be("Atomic field");
        system.Config.CharacterSheetFields[0].Label.Should().Be("Atomic field");
        system.OnSetupWizardRequest(player, request);
        results.Last().Should().BeSameAs(saved);
        api.Received(1).StoreModConfig(Arg.Is<ModConfig>(config => !config.EnableChatter && config.Languages[languageIndex].Name == "River speech"), Arg.Any<string>());
    }

    [Fact]
    public void FailedLanguageWriteLeavesSettingsAndPlayerKnowledgeUnchanged()
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        var oldName = system.Config.Languages[0].Name;
        player.SetLanguages([oldName]);
        api.World.AllOnlinePlayers.Returns([player]);
        system.OnSetupWizardRequest(player, new TheBasicsSetupWizardRequestMessage { Kind = SetupWizardRequestKind.Open });
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 6, RunId = results.Last().RunId,
            Values = [new() { Key = "EnableChatter", Value = "0" }],
            OriginalValues = [new() { Key = "EnableChatter", Value = "1" }],
            OriginalLanguages = LanguageConfigAdmin.CloneEntries(results.Last().Languages),
            Languages = LanguageConfigAdmin.CloneEntries(results.Last().Languages)
        };
        var oldFields = CharacterSheetFieldConfigAdmin.BuildEntries(system.Config);
        request.OriginalSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(oldFields);
        request.SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(oldFields);
        request.SheetFields[0].Label = "Must not persist";
        request.Languages[0].Name = "River speech";
        api.When(value => value.StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>()))
            .Do(_ => throw new IOException("test write failure"));

        system.OnSetupWizardRequest(player, request);

        results.Last().Success.Should().BeFalse();
        results.Last().Message.Should().Contain("Could not write");
        system.Config.EnableChatter.Should().BeTrue();
        system.Config.Languages[0].Name.Should().Be(oldName);
        player.GetLanguages().Should().Equal(oldName);
        GetPendingRenames(system).Should().BeEmpty();
        CharacterSheetFieldConfigAdmin.EntriesEqual(CharacterSheetFieldConfigAdmin.BuildEntries(system.Config), oldFields).Should().BeTrue();
    }


    [Theory]
    [InlineData("invalid")]
    [InlineData("delete-all")]
    [InlineData("duplicate-binding")]
    [InlineData("stale")]
    public void RejectedFieldPatchNeverWritesAnyConfiguration(string fault)
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open });
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 20, RunId = results.Last().RunId,
            Values = [new() { Key = "EnableChatter", Value = "0" }], OriginalValues = [new() { Key = "EnableChatter", Value = "1" }],
            SheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(results.Last().SheetFields),
            OriginalSheetFields = CharacterSheetFieldConfigAdmin.CloneEntries(results.Last().SheetFields)
        };
        switch (fault)
        {
            case "invalid": request.SheetFields[0].Label = ""; break;
            case "delete-all": request.SheetFields.Clear(); break;
            case "duplicate-binding": request.SheetFields[0].BindTo = request.SheetFields[1].BindTo = "thebasics.fullName"; break;
            case "stale": system.Config.CharacterSheetFields[0].Label = "Legacy editor save"; break;
        }
        system.OnSetupWizardRequest(player, request);
        results.Last().Success.Should().BeFalse();
        system.Config.EnableChatter.Should().BeTrue();
        api.DidNotReceive().StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());
        if (fault == "stale") results.Last().ConflictKeys.Should().Contain("CharacterSheetFields");
    }

    [Theory]
    [InlineData("SetHomeCommandPrivilege", "sethome")]
    [InlineData("SetSpawnCommandPrivilege", "setspawn")]
    public void SetupSaveRefreshesRegisteredHomeSpawnCommandPermissions(string key, string commandName)
    {
        thebasics.Tests.Support.LangTestHelper.EnsureEnglish();
        var (system, api, results) = CreateSystem();
        var command = Substitute.For<IChatCommand>();
        api.ChatCommands.Get(commandName).Returns(command);
        system.Config.Teleportation.RegisterHomeCommands = true;
        system.Config.Teleportation.RegisterSpawnCommands = true;
        var home = new thebasics.ModSystems.HomeSpawn.HomeSpawnSystem { API = api, Config = system.Config };
        var loaded = (List<thebasics.ModSystems.BaseBasicModSystem>)typeof(thebasics.ModSystems.BaseBasicModSystem)
            .GetField("LoadedSystems", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        loaded.Add(home);
        try
        {
            var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
            system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open });
            ConfigAdminSettingRegistry.TryGet(key, out var setting);
            system.OnSetupWizardRequest(player, new()
            {
                Kind = SetupWizardRequestKind.Save, RequestId = 10, RunId = results.Last().RunId,
                Values = [new() { Key = key, Value = "custom.travel" }],
                OriginalValues = [new() { Key = key, Value = setting.GetValue(system.Config) }]
            });
            results.Last().Success.Should().BeTrue();
            command.Received().RequiresPrivilege("custom.travel");
        }
        finally { loaded.Remove(home); }
    }

    [Theory]
    [InlineData("ProximityChatName", "Nearby")]
    [InlineData("ChatHistoryFlushIntervalMilliseconds", "1250")]
    [InlineData("PlayerStatDistanceTravelledTimer", "1500")]
    public void MigratedRestartSettingsRemainClassifiedAfterSaving(string key, string value)
    {
        ConfigAdminSettingRegistry.TryGet(key, out var setting).Should().BeTrue();
        setting.ReloadBehavior.Should().Be(ConfigAdminReloadBehavior.RestartRequired);
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var state = new SetupWizardState(config);
        var request = new TheBasicsSetupWizardRequestMessage
        {
            Kind = SetupWizardRequestKind.Save, RequestId = 1,
            Values = [new() { Key = key, Value = value }],
            OriginalValues = [new() { Key = key, Value = setting.GetValue(config) }]
        };
        state.TryValidatePatch(config, request, out var candidate, out _, out _, out var errors).Should().BeTrue(string.Join("; ", errors));
        state.GetPendingRestartKeys(candidate).Should().Contain(key);
    }

    [Fact]
    public void ReviewedMetadataChangesOnlyOnExplicitActionAndRetainsSettings()
    {
        var (system, api, results) = CreateSystem();
        system.Config.ReviewedConfigSettingKeys = ["EnableChatter"];
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open, Destination = "chat.languageeditor" });
        results.Last().SupportsMaintenance.Should().BeTrue();
        results.Last().ReviewedKeys.Should().Equal("EnableChatter");
        results.Last().Destination.Should().Be("chat.languageeditor");
        var run = results.Last().RunId;
        system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Save, RequestId = 1, RunId = run });
        results.Last().ReviewedKeys.Should().Equal("EnableChatter");
        system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.MarkReviewed, RequestId = 2, RunId = run });
        results.Last().Success.Should().BeTrue();
        system.Config.EnableChatter.Should().BeTrue();
        results.Last().ReviewedKeys.Should().Contain(ConfigAdminSettingRegistry.Settings.Select(setting => setting.Key));
        api.Received().StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());
    }

    [Theory]
    [InlineData(SetupWizardRequestKind.MarkReviewed)]
    [InlineData(SetupWizardRequestKind.Reload)]
    public void MaintenanceRequiresRootAndRealSession(SetupWizardRequestKind kind)
    {
        var (system, api, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open, CaptureOnly = true });
        system.OnSetupWizardRequest(player, new() { Kind = kind, RequestId = 1, RunId = results.Last().RunId });
        results.Last().Success.Should().BeFalse();
        player.PrivilegeCheck = _ => false;
        system.OnSetupWizardRequest(player, new() { Kind = kind, RequestId = 2 });
        results.Last().Kind.Should().Be(SetupWizardResultKind.Denied);
        api.DidNotReceive().StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());
    }

    [Fact]
    public void UnknownDestinationFallsBackWithoutReplacingRunOrMarkingReviewed()
    {
        var (system, _, results) = CreateSystem();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open });
        var run = results.Last().RunId;
        var reviewed = results.Last().ReviewedKeys.ToArray();
        system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open, Destination = "future.page" });
        results.Last().RunId.Should().Be(run);
        results.Last().Destination.Should().BeNull();
        results.Last().ReviewedKeys.Should().Equal(reviewed);
        system.OnSetupWizardRequest(player, new() { Kind = (SetupWizardRequestKind)999, RequestId = 7, RunId = run });
        results.Last().Kind.Should().Be(SetupWizardResultKind.RequestFailed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiskReloadUsesSafeReaderAndMalformedFileLeavesRuntimeUnchanged(bool malformed)
    {
        var (system, api, results) = CreateSystem();
        var shared = typeof(thebasics.ModSystems.BaseBasicModSystem).GetField("_sharedConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = shared.GetValue(null);
        shared.SetValue(null, system.Config);
        var file = Path.Combine(Path.GetTempPath(), "setup-reload-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var disk = new ModConfig(); disk.InitializeDefaultsIfNeeded(); disk.EnableChatter = false;
            File.WriteAllText(file, malformed ? "{ broken json" : Newtonsoft.Json.JsonConvert.SerializeObject(disk));
            api.LoadModConfig<ModConfig>(Arg.Any<string>()).Returns(_ => Newtonsoft.Json.JsonConvert.DeserializeObject<ModConfig>(File.ReadAllText(file)));
            var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
            system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open });
            system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Reload, RequestId = 1, RunId = results.Last().RunId });
            results.Last().Kind.Should().Be(SetupWizardResultKind.MaintenanceResult);
            results.Last().Success.Should().Be(!malformed);
            system.Config.EnableChatter.Should().Be(malformed);
        }
        finally { shared.SetValue(null, previous); File.Delete(file); }
    }

    [Fact]
    public void ConfigAndSetupAliasesRetainRootAndPlayerRequirements()
    {
        LangTestHelper.EnsureEnglish();
        var (system, api, _) = CreateSystem();
        var commands = new Dictionary<string, IChatCommand>();
        IChatCommand Command(string path, IChatCommand? parent = null)
        {
            if (commands.TryGetValue(path, out var existing)) return existing;
            var command = Substitute.For<IChatCommand>(); commands[path] = command;
            command.ReturnsForAll<IChatCommand>(command);
            command.EndSubCommand().Returns(parent ?? command);
            command.BeginSubCommand(Arg.Any<string>()).Returns(call => Command(path + "/" + call.Arg<string>(), command));
            return command;
        }
        var root = Command("thebasics"); api.ChatCommands.GetOrCreate("thebasics").Returns(root);
        typeof(RPProximityChatSystem).GetMethod("RegisterAdminConfigCommands", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(system, null);
        root.Received().WithRootAlias("tb"); root.Received().WithRootAlias("basic");
        foreach (var path in new[] { "config", "config/advanced", "config/languages", "config/charsheetfields", "setup", "setup/languages", "setup/charsheetfields", "charsheetfields" })
        {
            commands["thebasics/" + path].Received().RequiresPrivilege(Privilege.root);
            commands["thebasics/" + path].Received().RequiresPlayer();
        }
        commands["thebasics/config/languages"].Received().WithAlias("language");
        commands["thebasics/setup/languages"].Received().WithAlias("language");
        foreach (var path in new[] { "config/charsheetfields", "setup/charsheetfields", "charsheetfields" })
            commands["thebasics/" + path].Received().WithAlias("sheetfields", "biofields");
    }

    [Fact]
    public void LoadedConfigWithBroadcastFailureIsDistinctFromBrokenFileEvenWithStickyFailure()
    {
        var (system, api, results) = CreateSystem();
        var shared = typeof(thebasics.ModSystems.BaseBasicModSystem).GetField("_sharedConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = shared.GetValue(null); shared.SetValue(null, system.Config);
        try
        {
            var disk = new ModConfig(); disk.InitializeDefaultsIfNeeded(); disk.EnableChatter = false;
            api.LoadModConfig<ModConfig>(Arg.Any<string>()).Returns(disk);
            var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
            system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Open });
            var run = results.Last().RunId;
            api.World.AllOnlinePlayers.Returns(_ => throw new InvalidOperationException("broadcast unavailable"));
            system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Reload, RequestId = 1, RunId = run });
            var partial = results.Last();
            partial.Kind.Should().Be(SetupWizardResultKind.ReloadPartiallyApplied);
            partial.Success.Should().BeFalse(); partial.RuntimeApplyFailed.Should().BeTrue();
            system.Config.EnableChatter.Should().BeFalse();
            partial.Values.Single(x => x.Key == "EnableChatter").Value.Should().Be("0");
            partial.Message.Should().Contain("loaded").And.Contain("Restart");
            api.LoadModConfig<ModConfig>(Arg.Any<string>()).Returns(_ => throw new FormatException("broken config"));
            system.OnSetupWizardRequest(player, new() { Kind = SetupWizardRequestKind.Reload, RequestId = 2, RunId = run });
            results.Last().Kind.Should().Be(SetupWizardResultKind.MaintenanceResult);
            results.Last().Success.Should().BeFalse(); results.Last().RuntimeApplyFailed.Should().BeTrue();
            system.Config.EnableChatter.Should().BeFalse();
        }
        finally { shared.SetValue(null, previous); }
    }

    private static Dictionary<string, string> GetPendingRenames(RPProximityChatSystem system) =>
        (Dictionary<string, string>)typeof(RPProximityChatSystem)
            .GetField("_languageRenameMapForJoiningPlayers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(system)!;

    private static void OfferInvitation(RPProximityChatSystem system, IServerPlayer player)
    {
        var state = (SetupWizardInvitationState)typeof(RPProximityChatSystem)
            .GetField("_setupWizardInvitation", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(system)!;
        state.MarkClientReady(player.PlayerUID);
        state.MarkPlaying(player.PlayerUID);
        typeof(RPProximityChatSystem).GetMethod("TryOfferSetupWizard", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(system, [player]);
    }

    private static (RPProximityChatSystem System, ICoreServerAPI Api, List<TheBasicsSetupWizardResultMessage> Results) CreateSystem()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var channel = Substitute.For<IServerNetworkChannel>();
        var results = new List<TheBasicsSetupWizardResultMessage>();
        channel.When(x => x.SendPacket(Arg.Any<TheBasicsSetupWizardResultMessage>(), Arg.Any<IServerPlayer[]>()))
            .Do(call => results.Add(call.Arg<TheBasicsSetupWizardResultMessage>()));
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var system = new RPProximityChatSystem { API = api, Config = config };
        typeof(RPProximityChatSystem).GetField("_serverConfigChannel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(system, channel);
        return (system, api, results);
    }
}
