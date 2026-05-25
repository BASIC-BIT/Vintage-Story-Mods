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

    [Fact]
    public void TryEnqueue_FailsClosedWhenTh3EssentialsConfigShapeChanges()
    {
        var relay = new Th3EssentialsDiscordRelay(null!);
        var discordWithoutConfig = new FakeTh3DiscordWithoutConfig();
        var discordWithoutRelayField = new FakeTh3DiscordWithoutRelayField();

        var queuedWithoutConfig = relay.TryEnqueue(discordWithoutConfig, "Alice says hello.");
        var queuedWithoutRelayField = relay.TryEnqueue(discordWithoutRelayField, "Bob says hello.");

        queuedWithoutConfig.Should().BeFalse();
        queuedWithoutRelayField.Should().BeFalse();
        discordWithoutConfig.Messages.Should().BeEmpty();
        discordWithoutRelayField.Messages.Should().BeEmpty();
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

    private sealed class FakeTh3DiscordWithoutConfig
    {
        private readonly ConcurrentQueue<string> sendQueue = new();

        public object? DiscordChannel { get; set; } = new();

        public string[] Messages => sendQueue.ToArray();
    }

    private sealed class FakeTh3DiscordWithoutRelayField
    {
        private readonly ConcurrentQueue<string> sendQueue = new();

        public object Config { get; } = new object();
        public object? DiscordChannel { get; set; } = new();

        public string[] Messages => sendQueue.ToArray();
    }
}
