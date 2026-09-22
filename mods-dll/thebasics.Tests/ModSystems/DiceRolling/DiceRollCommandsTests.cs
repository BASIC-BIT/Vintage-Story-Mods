using System.Linq;
using System.Reflection;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat;
using thebasics.Tests.Support;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;
namespace thebasics.Tests.ModSystems.DiceRolling;

public class DiceRollCommandsTests
{
    [Fact]
    public void PublicMultiDieRollSendsOneSoundToEachEligibleTextRecipient()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        var channel = Substitute.For<IServerNetworkChannel>();
        typeof(RPProximityChatSystem).GetField("_serverConfigChannel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(system, channel);
        var roller = new FakeServerPlayer("roller") { Entity = new EntityPlayer() };
        var nearby = new FakeServerPlayer("nearby") { Entity = new EntityPlayer() };
        nearby.Entity.Pos.X = 2;
        roller.SetDiceRollSoundsEnabled(false);
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { roller, nearby });
        var commands = new DiceRollCommands(system, input => DiceEvaluator.EvaluateInput(input, _ => 3));

        var response = commands.Handle(new TextCommandCallingArgs
        {
            Caller = new Caller { Player = roller }, RawArgs = new CmdArgs("3d6")
        }, false);

        Assert.Equal(EnumCommandStatus.Success, response.Status);
        Assert.Single(roller.SentMessages);
        Assert.Single(nearby.SentMessages);
        channel.Received(1).SendPacket(Arg.Any<DiceRollSoundMessage>(), nearby);
        channel.DidNotReceive().SendPacket(Arg.Any<DiceRollSoundMessage>(), roller);
    }

    [Fact]
    public void PrivateRollSendsSoundOnlyToRoller()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        var channel = Substitute.For<IServerNetworkChannel>();
        typeof(RPProximityChatSystem).GetField("_serverConfigChannel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(system, channel);
        var roller = new FakeServerPlayer("roller") { Entity = new EntityPlayer() };
        var listener = new FakeServerPlayer("listener") { Entity = new EntityPlayer() };
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { roller, listener });
        var commands = new DiceRollCommands(system, input => DiceEvaluator.EvaluateInput(input, _ => 3));

        var response = commands.Handle(new TextCommandCallingArgs
        {
            Caller = new Caller { Player = roller }, RawArgs = new CmdArgs("d6 # secret")
        }, true);

        Assert.Equal(EnumCommandStatus.Success, response.Status);
        Assert.Single(roller.SentMessages);
        Assert.Empty(listener.SentMessages);
        channel.Received(1).SendPacket(Arg.Any<DiceRollSoundMessage>(), roller);
        channel.DidNotReceive().SendPacket(Arg.Any<DiceRollSoundMessage>(), listener);
    }

    [Fact]
    public void FailedHelpDisabledAndRateLimitedRollsSendNoSound()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        var channel = Substitute.For<IServerNetworkChannel>();
        typeof(RPProximityChatSystem).GetField("_serverConfigChannel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(system, channel);
        var roller = new FakeServerPlayer("roller") { Entity = new EntityPlayer() };
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { roller });
        var commands = new DiceRollCommands(system);
        TextCommandResult Roll(string input) => commands.Handle(new TextCommandCallingArgs
        {
            Caller = new Caller { Player = roller }, RawArgs = new CmdArgs(input)
        }, false);

        Assert.Equal(EnumCommandStatus.Error, Roll("2d0").Status);
        Assert.Equal(EnumCommandStatus.Success, Roll("").Status);
        system.Config.EnableDiceRolling = false;
        Assert.Equal(EnumCommandStatus.Error, Roll("d6").Status);
        system.Config.EnableDiceRolling = true;
        Assert.Equal(EnumCommandStatus.Error, Roll("2d0").Status);
        Assert.Equal(EnumCommandStatus.Error, Roll("2d0").Status);
        Assert.Equal(EnumCommandStatus.Error, Roll("d6").Status);
        channel.DidNotReceive().SendPacket(Arg.Any<DiceRollSoundMessage>(), Arg.Any<IServerPlayer[]>());
    }

    [Fact]
    public void AudioSendFailureLeavesDeliveredPublicRollSuccessful()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var config = new ModConfig { EnableChatHistory = true };
        var history = new thebasics.ModSystems.ChatHistory.ChatHistorySystem { API = api, Config = config };
        api.ModLoader.GetModSystem<thebasics.ModSystems.ChatHistory.ChatHistorySystem>().Returns(history);
        var system = new RPProximityChatSystem { API = api, Config = config };
        var channel = Substitute.For<IServerNetworkChannel>();
        channel.When(x => x.SendPacket(Arg.Any<DiceRollSoundMessage>(), Arg.Any<IServerPlayer[]>()))
            .Do(_ => throw new System.InvalidOperationException("network"));
        typeof(RPProximityChatSystem).GetField("_serverConfigChannel", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(system, channel);
        var roller = new FakeServerPlayer("roller") { Entity = new EntityPlayer() };
        var listener = new FakeServerPlayer("listener") { Entity = new EntityPlayer() };
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { roller, listener });
        var events = 0;
        system.ProximityChatMessageProcessed += (_, _) => events++;
        var commands = new DiceRollCommands(system, input => DiceEvaluator.EvaluateInput(input, _ => 3));

        var response = commands.Handle(new TextCommandCallingArgs
        {
            Caller = new Caller { Player = roller }, RawArgs = new CmdArgs("d6")
        }, false);

        Assert.Equal(EnumCommandStatus.Success, response.Status);
        Assert.Single(roller.SentMessages);
        Assert.Single(listener.SentMessages);
        Assert.Equal(1, events);
        channel.Received(1).SendPacket(Arg.Any<DiceRollSoundMessage>(), roller);
        channel.Received(1).SendPacket(Arg.Any<DiceRollSoundMessage>(), listener);
        var entries = (System.Collections.Generic.List<thebasics.ModSystems.ChatHistory.Models.ChatHistoryEntry>)
            typeof(thebasics.ModSystems.ChatHistory.ChatHistorySystem)
                .GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(history)!;
        Assert.Single(entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedCompleteResultPublishesNothing(bool isPrivate)
    {
        var api = Substitute.For<ICoreServerAPI>();
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        var published = 0;
        system.ProximityChatMessageProcessed += (_, _) => published++;
        var player = new FakeServerPlayer();
        var commands = new DiceRollCommands(system, input => DiceEvaluator.EvaluateInput(input, _ => 1000000));
        var response = commands.Handle(new TextCommandCallingArgs
        {
            Caller = new Caller { Player = player },
            RawArgs = new CmdArgs("50d1000000ro<=1000000kh49")
        }, isPrivate);
        Assert.Equal(EnumCommandStatus.Error, response.Status);
        Assert.Empty(player.SentMessages);
        Assert.Equal(0, published);
        Assert.Contains("output", response.StatusMessage);
    }

    [Fact]
    public void PrivateRollEvaluatesOnceAndNeverPublishes()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig(), ProximityChatId = 7 };
        var published = 0;
        system.ProximityChatMessageProcessed += (_, _) => published++;
        var evaluated = 0;
        var commands = new DiceRollCommands(system, input => { evaluated++; return DiceEvaluator.EvaluateInput(input, _ => 3); });
        var player = new FakeServerPlayer();
        commands.Handle(new TextCommandCallingArgs { Caller = new Caller { Player = player }, RawArgs = new CmdArgs("d6 # secret") }, true);
        Assert.Equal(1, evaluated);
        Assert.Equal(0, published);
        Assert.Single(player.SentMessages);
        Assert.Contains("[Private Roll]", player.SentMessages[0].Message);
        Assert.Null(player.SentMessages[0].Data);
    }
    [Fact]
    public void AttemptGuardRejectsSixthAttemptAndExpires()
    {
        var guard = new DiceAttemptGuard();
        for (var i = 0; i < 5; i++) Assert.True(guard.TryAcquire("a", 0));
        Assert.False(guard.TryAcquire("a", 9999));
        Assert.True(guard.TryAcquire("b", 9999));
        Assert.True(guard.TryAcquire("a", 10000));
        guard.Remove("a");
        Assert.True(guard.TryAcquire("a", 10001));
    }
    [Fact]
    public void DisabledAndBareCommandsNeverEvaluate()
    {
        var api = Substitute.For<ICoreServerAPI>();
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        var commands = new DiceRollCommands(system, _ => throw new Xunit.Sdk.XunitException("Unexpected evaluation"));
        var args = new TextCommandCallingArgs { Caller = new Caller { Player = new FakeServerPlayer() }, RawArgs = new CmdArgs("") };
        Assert.Contains("Examples", commands.Handle(args, false).StatusMessage);
        system.Config.EnableDiceRolling = false;
        Assert.Contains("disabled", commands.Handle(args, true).StatusMessage);
    }
}
