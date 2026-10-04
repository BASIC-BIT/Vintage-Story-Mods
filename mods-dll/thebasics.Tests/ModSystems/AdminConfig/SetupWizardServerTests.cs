using System.Reflection;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class SetupWizardServerTests
{
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
