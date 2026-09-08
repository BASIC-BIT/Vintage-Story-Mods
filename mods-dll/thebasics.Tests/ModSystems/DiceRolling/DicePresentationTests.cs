using thebasics.Configs;
using thebasics.ModSystems.DiceRolling;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Tests.Support;
using Vintagestory.API.Common;
using Xunit;

namespace thebasics.Tests.ModSystems.DiceRolling;

public class DicePresentationTests
{
    [Theory]
    [InlineData(ProximityChatMode.Normal, "")]
    [InlineData(ProximityChatMode.Whisper, "(W) ")]
    [InlineData(ProximityChatMode.Yell, "(Y) ")]
    public void RangeMarkerSurvivesPublicRendering(ProximityChatMode mode, string prefix)
    {
        var result = DiceEvaluator.EvaluateInput("d20 # <b>luck</b>", _ => 12);
        var text = DicePresentation.Chat(result, "Alice", mode, false);
        Assert.StartsWith(prefix + "Alice rolled", text);
        Assert.Contains("12", text);
        Assert.DoesNotContain("<b>luck</b>", text);
        Assert.StartsWith(prefix, DicePresentation.Summary(result, mode));
    }

    [Fact]
    public void PrivateResultHasNoRangeAndNeverCreatesBubble()
    {
        var result = DiceEvaluator.EvaluateInput("d6", _ => 3);
        Assert.StartsWith("[Private Roll]", DicePresentation.Chat(result, "Alice", ProximityChatMode.Yell, true));
        Assert.Null(DicePresentation.Bubble(result, new FakeServerPlayer(), ProximityChatMode.Yell, new ModConfig(), true));
    }

    [Theory]
    [InlineData("Off", null)]
    [InlineData("Vanilla", "from:0,msg:")]
    [InlineData("RpText", "from:0,msg\u001fkind=dice20")]
    public void BubbleHonorsExistingPolicy(string mode, string? prefix)
    {
        var player = new FakeServerPlayer { Entity = new EntityPlayer() };
        var data = DicePresentation.Bubble(DiceEvaluator.EvaluateInput("d20", _ => 12), player,
            ProximityChatMode.Normal, new ModConfig { OverheadChatBubbleMode = mode }, false);
        if (prefix == null) Assert.Null(data);
        else Assert.StartsWith(prefix, data);
    }
}

public class DiceRelayPresentationTests
{
    [Fact]
    public void RollRelayPreservesMarkerAndResultWithoutCreatingDiscordMentions()
    {
        var result = DiceEvaluator.EvaluateInput("d20 # <@123456789012345678> <@&123456789012345678> @everyone", _ => 12);
        var rendered = DicePresentation.Chat(result, "Alice", ProximityChatMode.Whisper, false);
        var relay = thebasics.ModSystems.ProximityChat.Th3EssentialsDiscordRelay.FormatRelayMessage(rendered, suppressMentions: true);
        Assert.StartsWith("(W) Alice rolled d20 = 12", relay);
        Assert.DoesNotContain("<@123", relay, System.StringComparison.Ordinal);
        Assert.DoesNotContain("<@&123", relay, System.StringComparison.Ordinal);
        Assert.DoesNotContain("@everyone", relay, System.StringComparison.Ordinal);
        Assert.Contains("123456789012345678", relay);
    }
}
