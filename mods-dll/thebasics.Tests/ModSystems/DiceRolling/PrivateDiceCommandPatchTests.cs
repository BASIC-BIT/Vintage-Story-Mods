using System;
using System.Linq;
using NSubstitute;
using thebasics.Configs;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat;
using thebasics.Tests.Support;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

public class PrivateDiceCommandPatchTests
{
    public PrivateDiceCommandPatchTests() => LangTestHelper.EnsureEnglish();
    [Theory]
    [InlineData("proll", "d6 # secret")]
    [InlineData("privateroll", "d6 # secret")]
    [InlineData("thebasics", "proll d6 # secret")]
    [InlineData("tb", "PROLL d6 # secret")]
    [InlineData("basic", "proll d6 # secret")]
    public void RealAuditedOverloadBypassesAuditOnlyForOwnedPrivateCommand(string name, string input)
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        chat.GetOrCreate("thebasics").WithRootAlias("tb").WithRootAlias("basic").RequiresPrivilege(Privilege.chat);
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        var evaluated = 0;
        using var commands = new DiceRollCommands(system, text => { evaluated++; return DiceEvaluator.EvaluateInput(text, _ => 4); });
        commands.Register();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        chat.Execute(name, player, 7, input);
        Assert.Equal(1, evaluated);
        Assert.Single(player.SentMessages);
        Assert.Contains("secret", player.SentMessages[0].Message);
        Assert.Empty(api.Logger.ReceivedCalls());
    }
    [Theory]
    [InlineData("roll", "d6 # scene")]
    [InlineData("r", "d6 # scene")]
    [InlineData("thebasics", "roll d6 # scene")]
    public void PublicAliasesUseOneEvaluationAndOnePublicEvent(string name, string input)
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        var evaluated = 0;
        var published = 0;
        system.ProximityChatMessageProcessed += (_, _) => published++;
        using var commands = new DiceRollCommands(system, text => { evaluated++; return DiceEvaluator.EvaluateInput(text, _ => 4); });
        commands.Register();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true, Entity = new EntityPlayer() };
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { player });
        chat.Execute(name, player, 7, input);
        Assert.Equal(1, evaluated);
        Assert.Equal(1, published);
        Assert.Single(player.SentMessages);
        Assert.Contains(api.Logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Audit");
        Assert.Single(api.Logger.ReceivedCalls().Where(call => call.GetMethodInfo().Name == "Chat"));
    }
    [Fact]
    public void CollisionRemainsOwnedByOtherModAndAudited()
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var other = chat.Create("proll").RequiresPrivilege(Privilege.chat).IgnoreAdditionalArgs().HandleWith(_ => TextCommandResult.Success());
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        using var commands = new DiceRollCommands(system);
        commands.Register();
        Assert.Same(other, chat.Get("proll"));
        chat.Execute("proll", new FakeServerPlayer { PrivilegeCheck = _ => true }, 7, "other-mod-argument");
        Assert.Contains(api.Logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Audit");
    }
    [Fact]
    public void PrivatePrivilegeFailureNeverEvaluatesOrAudits()
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        using var commands = new DiceRollCommands(system, _ => throw new Xunit.Sdk.XunitException("Must not run"));
        commands.Register();
        TextCommandResult? result = null;
        chat.Execute("proll", new FakeServerPlayer(), 7, "secret", value => result = value);
        Assert.Equal("noprivilege", result?.ErrorCode);
        Assert.Empty(api.Logger.ReceivedCalls());
    }
    [Fact]
    public void PrivateThrowingPreconditionFailsClosedWithoutExceptionContents()
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        using var commands = new DiceRollCommands(system);
        commands.Register();
        chat.Get("proll").WithPreCondition(_ => throw new InvalidOperationException("secret"));
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        chat.Execute("proll", player, 7, "d6 # secret");
        Assert.Empty(api.Logger.ReceivedCalls());
        Assert.Single(player.SentMessages);
        Assert.DoesNotContain("secret", player.SentMessages[0].Message);
    }
    [Fact]
    public void PatchInstallationFailureLeavesPublicCommandsAvailableWithoutPrivateHandlers()
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        using var commands = new DiceRollCommands(system, createPrivatePatch: _ => throw new InvalidOperationException("patch failure details"));
        commands.Register();
        Assert.Null(chat.Get("proll"));
        Assert.Null(chat.Get("privateroll"));
        Assert.False(chat.Get("thebasics").AllSubcommands.ContainsKey("proll"));
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true, Entity = new EntityPlayer() };
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { player });
        chat.Execute("r", player, 7, "d1");
        Assert.Single(player.SentMessages);
        Assert.Contains("1", player.SentMessages[0].Message);
        Assert.Contains(api.Logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Warning");
    }
}
