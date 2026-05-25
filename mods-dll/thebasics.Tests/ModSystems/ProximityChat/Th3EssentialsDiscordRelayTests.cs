using System.Collections.Concurrent;
using FluentAssertions;
using thebasics.ModSystems.ProximityChat;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class Th3EssentialsDiscordRelayTests
{
    [Fact]
    public void FormatRelayMessage_StripsVtmlAndSuppressesDiscordMassMentions()
    {
        var formatted = "<font color=\"#fff\"><strong>Alice</strong></font> says &lt;hello&gt; @everyone and @here";

        var result = Th3EssentialsDiscordRelay.FormatRelayMessage(formatted);

        result.Should().Be("Alice says <hello> @_everyone and @_here");
    }

    [Fact]
    public void TryEnqueue_QueuesMessageWhenDiscordRelayIsReady()
    {
        var relay = new Th3EssentialsDiscordRelay(null!);
        var discord = new FakeTh3Discord();

        var queued = relay.TryEnqueue(discord, "Alice says hello.");

        queued.Should().BeTrue();
        discord.Messages.Should().Equal("Alice says hello.");
    }

    [Fact]
    public void TryEnqueue_RespectsTh3EssentialsDiscordRelayToggle()
    {
        var relay = new Th3EssentialsDiscordRelay(null!);
        var discord = new FakeTh3Discord();
        discord.Config.DiscordChatRelay = false;

        var queued = relay.TryEnqueue(discord, "Alice says hello.");

        queued.Should().BeFalse();
        discord.Messages.Should().BeEmpty();
    }

    private sealed class FakeTh3Discord
    {
        private readonly ConcurrentQueue<string> sendQueue = new();

        public object? DiscordChannel { get; set; } = new();

        public FakeDiscordConfig Config = new();

        public string[] Messages => sendQueue.ToArray();
    }

    public sealed class FakeDiscordConfig
    {
        public bool DiscordChatRelay = true;
    }
}
