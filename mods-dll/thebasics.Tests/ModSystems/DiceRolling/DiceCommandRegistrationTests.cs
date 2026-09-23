using System;
using System.Linq;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat;
using thebasics.Tests.Support;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.Common;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

[Collection(thebasics.Tests.ModSystems.AnalyticsServiceTestCollection.Name)]
public class DiceCommandRegistrationTests
{
    public DiceCommandRegistrationTests() => LangTestHelper.EnsureEnglish();
    [Fact]
    public void DiceSoundsStatusAndSettingWorkWhileServerAudioIsDisabled()
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig { EnableDiceRollSounds = false, EnableDiceRolling = false } };
        using var commands = new DiceRollCommands(system);
        commands.Register();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };

        TextCommandResult? response = null;
        chat.Execute("dicesounds", player, 7, "", value => response = value);
        Assert.True(player.GetDiceRollSoundsEnabled());
        Assert.Null(player.GetModdata("thebasics-dice-roll-sounds-enabled"));
        Assert.Contains("dicesounds-status", response?.StatusMessage);
        Assert.Contains("dicesounds-server-off", response?.StatusMessage);

        chat.Execute("dicesounds", player, 7, "off", value => response = value);
        Assert.False(player.GetDiceRollSoundsEnabled());
        chat.Execute("dicesounds", player, 7, "", value => response = value);
        Assert.Contains("dicesounds-status", response?.StatusMessage);
        Assert.False(player.GetDiceRollSoundsEnabled());
        chat.Execute("dicesounds", player, 7, "on", value => response = value);
        Assert.True(player.GetDiceRollSoundsEnabled());
    }

    [Fact]
    public void DiceSoundsStatusRendersEnglishPreferenceAndServerDisable()
    {
        const string locale = "test-dice-sounds-status";
        var previousLocale = Lang.CurrentLocale;
        var translations = Substitute.For<ITranslationService>();
        translations.HasTranslation(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>()).Returns(true);
        translations.Get(Arg.Any<string>(), Arg.Any<object[]>()).Returns(call =>
        {
            var english = call.ArgAt<string>(0) switch
            {
                "thebasics:dicesounds-description" => "Control whether you hear dice roll sounds.",
                "thebasics:dicesounds-status" => "Your dice roll sounds are {0}.",
                "thebasics:dicesounds-server-off" => "Dice roll sounds are disabled on this server.",
                "thebasics:util-on" => "on",
                "thebasics:util-off" => "off",
                var key => key
            };
            return string.Format(english, call.ArgAt<object[]>(1));
        });
        Lang.AvailableLanguages[locale] = translations;
        try
        {
            Lang.ChangeLanguage(locale);
            var api = Substitute.For<ICoreServerAPI>();
            api.Side.Returns(EnumAppSide.Server);
            var chat = new ChatCommandApi(api);
            api.ChatCommands.Returns(chat);
            chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
            var system = new RPProximityChatSystem { API = api, Config = new ModConfig { EnableDiceRollSounds = false } };
            using var commands = new DiceRollCommands(system);
            commands.Register();
            var player = new FakeServerPlayer { PrivilegeCheck = _ => true };

            TextCommandResult? response = null;
            chat.Execute("dicesounds", player, 7, "", value => response = value);
            Assert.Equal("Your dice roll sounds are on. Dice roll sounds are disabled on this server.", response?.StatusMessage);
            chat.Execute("dicesounds", player, 7, "off", value => response = value);
            Assert.Equal("Your dice roll sounds are off. Dice roll sounds are disabled on this server.", response?.StatusMessage);
            chat.Execute("dicesounds", player, 7, "", value => response = value);
            Assert.Equal("Your dice roll sounds are off. Dice roll sounds are disabled on this server.", response?.StatusMessage);
            chat.Execute("dicesounds", player, 7, "on", value => response = value);
            Assert.Equal("Your dice roll sounds are on. Dice roll sounds are disabled on this server.", response?.StatusMessage);
        }
        finally
        {
            Lang.ChangeLanguage(previousLocale);
            Lang.AvailableLanguages.Remove(locale);
        }
    }

    [Fact]
    public void DiceSoundsCollisionKeepsShortCommandAndNamespacedFallbackWorks()
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var other = chat.Create("dicesounds").RequiresPrivilege(Privilege.chat).IgnoreAdditionalArgs().HandleWith(_ => TextCommandResult.Success("other"));
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        using var commands = new DiceRollCommands(system);
        commands.Register();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        Assert.Same(other, chat.Get("dicesounds"));
        chat.Execute("thebasics", player, 7, "dicesounds off");
        Assert.False(player.GetDiceRollSoundsEnabled());
    }

    [Fact]
    public void InvalidDiceSoundsArgumentDoesNotChangePreference()
    {
        var api = Substitute.For<ICoreServerAPI>();
        api.Side.Returns(EnumAppSide.Server);
        var chat = new ChatCommandApi(api);
        api.ChatCommands.Returns(chat);
        chat.GetOrCreate("thebasics").RequiresPrivilege(Privilege.chat);
        var system = new RPProximityChatSystem { API = api, Config = new ModConfig() };
        using var commands = new DiceRollCommands(system);
        commands.Register();
        var player = new FakeServerPlayer { PrivilegeCheck = _ => true };
        TextCommandResult? response = null;
        chat.Execute("dicesounds", player, 7, "invalid", value => response = value);
        Assert.True(player.GetDiceRollSoundsEnabled());
        Assert.NotEqual(EnumCommandStatus.Success, response?.Status);
    }
    [Theory]
    [InlineData("proll", "d6 # secret")]
    [InlineData("privateroll", "d6 # secret")]
    [InlineData("thebasics", "proll d6 # secret")]
    [InlineData("tb", "PROLL d6 # secret")]
    [InlineData("basic", "proll d6 # secret")]
    public void PrivateAliasesUseNormalAuditedDispatch(string name, string input)
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
        Assert.Contains(api.Logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Audit");
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
    public void PrivatePrivilegeFailureNeverEvaluates()
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
        Assert.Contains(api.Logger.ReceivedCalls(), call => call.GetMethodInfo().Name == "Audit");
    }
}
