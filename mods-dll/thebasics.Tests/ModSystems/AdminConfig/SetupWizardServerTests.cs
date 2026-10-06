using System.Reflection;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.AdminConfig;
using thebasics.ModSystems.Analytics;
using thebasics.ModSystems.ProximityChat;
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

        system.OnSetupWizardRequest(player, request);

        var saved = results.Last();
        saved.Success.Should().BeTrue();
        saved.RuntimeApplyFailed.Should().BeTrue();
        saved.Message.Should().Contain("Saved").And.Contain("live application failed").And.Contain("Restart");
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
