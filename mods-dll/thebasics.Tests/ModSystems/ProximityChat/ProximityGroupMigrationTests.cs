using System.Reflection;
using FluentAssertions;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.ProximityChat;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class ProximityGroupMigrationTests
{
    [Theory]
    [InlineData("Proximity")]
    [InlineData("Local RP")]
    public void GeneralMode_RemovesOnlineAndOfflineMembershipsWithoutTouchingOtherGroups(string name)
    {
        var api = Substitute.For<ICoreServerAPI>();
        var group = new PlayerGroup { Uid = 42, Name = name };
        api.Groups.GetPlayerGroupByName(name).Returns(group);
        var players = new Dictionary<string, IServerPlayerData>();
        foreach (var uid in new[] { "online", "offline" })
        {
            var data = Substitute.For<IServerPlayerData>();
            data.PlayerGroupMemberships.Returns(new Dictionary<int, PlayerGroupMembership>
            {
                [42] = new() { GroupUid = 42, GroupName = name },
                [99] = new() { GroupUid = 99, GroupName = "Friends" }
            });
            players.Add(uid, data);
        }
        group.OnlinePlayers.Add(new FakeServerPlayer("online"));
        api.PlayerData.PlayerDataByUid.Returns(players);
        var system = new RPProximityChatSystem
        {
            API = api,
            Config = new ModConfig { UseGeneralChannelAsProximityChat = true, ProximityChatName = name }
        };

        typeof(RPProximityChatSystem).GetMethod("SetupProximityGroup", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(system, null);

        system.ProximityChatId.Should().Be(GlobalConstants.GeneralChatGroup);
        foreach (var data in players.Values)
        {
            data.PlayerGroupMemberships.Keys.Should().Equal(99);
        }
        api.Groups.Received(1).RemovePlayerGroup(group);
        api.Groups.DidNotReceive().AddPlayerGroup(Arg.Any<PlayerGroup>());
    }
}
