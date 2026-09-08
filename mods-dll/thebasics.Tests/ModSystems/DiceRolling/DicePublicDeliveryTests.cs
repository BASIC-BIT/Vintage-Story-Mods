using System.Collections.Generic;
using System.Reflection;
using NSubstitute;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.Models;
using thebasics.ModSystems.ChatHistory;
using thebasics.ModSystems.ChatHistory.Models;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Xunit;
namespace thebasics.Tests.ModSystems.DiceRolling;

public class DicePublicDeliveryTests
{
    [Theory]
    [InlineData(ProximityChatMode.Normal, "")]
    [InlineData(ProximityChatMode.Whisper, "(W) ")]
    [InlineData(ProximityChatMode.Yell, "(Y) ")]
    public void OneResultFeedsRangeScopedChatHistoryAndOneEvent(ProximityChatMode mode, string marker)
    {
        var api = Substitute.For<ICoreServerAPI>();
        var config = new ModConfig { EnableChatHistory = true, ApplyColorsToNicknames = true };
        var history = new ChatHistorySystem { API = api, Config = config };
        api.ModLoader.GetModSystem<ChatHistorySystem>().Returns(history);
        var system = new RPProximityChatSystem { API = api, Config = config };
        var sender = Player("s", 0);
        sender.SetChatMode(mode);
        sender.SetChatOverrideMode(ChatOverrideMode.GlobalOoc);
        sender.SetNicknameColor("#ff0000");
        var nearby = Player("n", config.GetModeDistance(mode) - 1);
        nearby.SetChatVisualPreferences(new ChatVisualPreferences { NicknameColorsEnabled = false });
        var boundary = Player("b", config.GetModeDistance(mode));
        var otherDimension = Player("d", 0);
        otherDimension.Entity.Pos.Dimension = 1;
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { sender, nearby, boundary, otherDimension });
        var events = new List<ProximityChatMessageEventArgs>();
        system.ProximityChatMessageProcessed += (_, message) => events.Add(message);
        var evaluations = 0;
        var commands = new DiceRollCommands(system, input => { evaluations++; return DiceEvaluator.EvaluateInput(input, _ => 4); });
        var outcome = commands.Handle(new TextCommandCallingArgs { Caller = new Caller { Player = sender }, RawArgs = new CmdArgs("d6 # test") }, false);
        Assert.Equal(EnumCommandStatus.Success, outcome.Status);
        Assert.Equal(1, evaluations);
        Assert.Single(sender.SentMessages);
        Assert.Single(nearby.SentMessages);
        Assert.Empty(boundary.SentMessages);
        Assert.Empty(otherDimension.SentMessages);
        Assert.StartsWith(marker, nearby.SentMessages[0].Message);
        Assert.Contains("4", nearby.SentMessages[0].Message);
        Assert.DoesNotContain("#ff0000", nearby.SentMessages[0].Message);
        Assert.Contains("#ff0000", sender.SentMessages[0].Message);
        var published = Assert.Single(events);
        Assert.Equal(ProximityChatMessageKind.Roll, published.Kind);
        Assert.Equal(mode, published.Mode);
        Assert.Equal(2, published.Recipients.Count);
        var entries = (List<ChatHistoryEntry>)typeof(ChatHistorySystem).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(history)!;
        var entry = Assert.Single(entries);
        Assert.Equal(ChatHistoryConstants.KindRoll, entry.ChatKind);
        Assert.Equal(sender.PlayerUID, entry.SenderPlayerUid);
        Assert.Equal(published.RenderedMessage, entry.FormattedMessage);
    }
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, false)]
    [InlineData(-1, 100000, true)]
    public void AudienceUsesExclusiveManhattanRangeAndSameDimension(int range, int x, bool expected)
    {
        var sender = Player("s", 0);
        var recipient = Player("r", x);
        Assert.Equal(expected, DiceRollCommands.InRange(sender, recipient, range));
        recipient.Entity.Pos.Dimension = 1;
        Assert.False(DiceRollCommands.InRange(sender, recipient, range));
    }
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void MixedNameColorsRespectCompletePlaintextLimit(int excess, bool accepted)
    {
        var api = Substitute.For<ICoreServerAPI>();
        var config = new ModConfig { ApplyColorsToNicknames = true };
        var system = new RPProximityChatSystem { API = api, Config = config };
        var sender = Player("s", 0);
        var recipient = Player("r", 0);
        sender.SetNicknameColor("#ff0000");
        recipient.SetChatVisualPreferences(new ChatVisualPreferences { NicknameColorsEnabled = false });
        api.World.AllOnlinePlayers.Returns(new IPlayer[] { recipient, sender });
        var seed = new DiceRollResult("d6", "", 4m, false, "", 6, new[] { "dice" });
        var name = new thebasics.ModSystems.ProximityChat.Transformers.NameTransformer(system).GetFormattedName(sender, true, config);
        var baseLength = Th3EssentialsDiscordRelay.FormatRelayMessage(DicePresentation.Chat(seed, name, ProximityChatMode.Normal, false), true).Length;
        var result = new DiceRollResult("d6", "", 4m, false, new string('1', DicePresentation.MaxOutputLength - baseLength + excess), 6, new[] { "dice" });
        var published = 0;
        system.ProximityChatMessageProcessed += (_, _) => published++;
        var commands = new DiceRollCommands(system, _ => result);
        var response = commands.Handle(new TextCommandCallingArgs { Caller = new Caller { Player = sender }, RawArgs = new CmdArgs("d6") }, false);
        Assert.Equal(accepted ? EnumCommandStatus.Success : EnumCommandStatus.Error, response.Status);
        Assert.Equal(accepted ? 1 : 0, published);
        Assert.Equal(accepted ? 1 : 0, sender.SentMessages.Count);
        Assert.Equal(accepted ? 1 : 0, recipient.SentMessages.Count);
        if (accepted)
        {
            Assert.True(sender.SentMessages[0].Message.Length > DicePresentation.MaxOutputLength);
            Assert.Equal(DicePresentation.MaxOutputLength, Th3EssentialsDiscordRelay.FormatRelayMessage(sender.SentMessages[0].Message, true).Length);
            Assert.DoesNotContain("#ff0000", recipient.SentMessages[0].Message);
        }
    }
    private static FakeServerPlayer Player(string uid, int x)
    {
        var player = new FakeServerPlayer(uid) { Entity = new EntityPlayer() };
        player.Entity.Pos.X = x;
        return player;
    }
}
