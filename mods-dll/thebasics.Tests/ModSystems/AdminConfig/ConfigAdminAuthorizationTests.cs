using System.Reflection;
using NSubstitute;
using thebasics.Configs;
using thebasics.Models;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.AdminConfig;

public class ConfigAdminAuthorizationTests
{
    [Fact]
    public void UnauthorizedSaveReturnsOnlyDenialWithoutConfigurationOrPrivileges()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var channel = Substitute.For<IServerNetworkChannel>();
        TheBasicsConfigAdminResultMessage? response = null;
        channel.When(x => x.SendPacket(Arg.Any<TheBasicsConfigAdminResultMessage>(), Arg.Any<IServerPlayer[]>()))
            .Do(call => response = call.Arg<TheBasicsConfigAdminResultMessage>());
        var config = new ModConfig();
        config.InitializeDefaultsIfNeeded();
        var system = new RPProximityChatSystem { API = api, Config = config };
        typeof(RPProximityChatSystem).GetField("_serverConfigChannel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(system, channel);
        var player = new FakeServerPlayer { PrivilegeCheck = _ => false };

        typeof(RPProximityChatSystem).GetMethod("OnConfigAdminSaveMessage", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(system, [player, new TheBasicsConfigAdminSaveMessage
            {
                Values = [new() { Key = "EnableChatter", Value = "0" }]
            }]);

        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("You do not have permission to edit The BASICs config.", response.Message);
        Assert.Null(response.Config);
        Assert.Empty(response.Values);
        Assert.Empty(response.ReviewedKeys);
        Assert.Empty(response.LiveAppliedKeys);
        Assert.Empty(response.RestartRequiredKeys);
        Assert.Empty(response.PrivilegeSuggestions);
        Assert.True(system.Config.EnableChatter);
        channel.Received(1).SendPacket(Arg.Any<TheBasicsConfigAdminResultMessage>(), player);
        api.DidNotReceive().StoreModConfig(Arg.Any<ModConfig>(), Arg.Any<string>());
    }
}
