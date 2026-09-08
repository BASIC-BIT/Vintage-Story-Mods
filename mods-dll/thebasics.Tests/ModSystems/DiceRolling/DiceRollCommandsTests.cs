using System.Linq;
using NSubstitute;
using thebasics.Configs;
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
