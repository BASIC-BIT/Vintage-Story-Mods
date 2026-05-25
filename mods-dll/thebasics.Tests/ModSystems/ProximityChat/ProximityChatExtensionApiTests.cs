using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Server;

namespace thebasics.Tests.ModSystems.ProximityChat;

public class ProximityChatExtensionApiTests
{
    [Fact]
    public void PublishProximityChatMessageProcessed_EmitsImmutableSnapshot()
    {
        var system = new RPProximityChatSystem();
        var sender = CreatePlayer("sender", "Alice");
        var recipient = CreatePlayer("recipient", "Bob");
        var pendingRecipient = CreatePlayer("pending", "Cora");
        ProximityChatMessageEventArgs captured = null!;
        system.ProximityChatMessageProcessed += (_, args) => captured = args;

        var context = new MessageContext
        {
            Message = "hello",
            SendingPlayer = sender,
            GroupId = 42,
            Recipients = new List<IServerPlayer> { recipient },
        };
        context.SetFlag(MessageContext.IS_SPEECH);
        context.SetFlag(MessageContext.IS_FROM_COMMAND);
        context.SetMetadata(MessageContext.CHAT_MODE, ProximityChatMode.Whisper);
        context.SetMetadata(MessageContext.LANGUAGE, new Language("Common", "", "c", [], "#ffffff"));
        context.SetMetadata(MessageContext.PENDING_SIGN_LANGUAGE_RECIPIENTS, new List<IServerPlayer> { pendingRecipient });

        system.PublishProximityChatMessageProcessed(context, "<strong>Alice</strong> whispers &lt;hello&gt;");

        captured.Should().NotBeNull();
        captured.SendingPlayer.Should().Be(sender);
        captured.Recipients.Should().Equal(recipient);
        captured.PendingRecipients.Should().Equal(pendingRecipient);
        captured.GroupId.Should().Be(42);
        captured.Kind.Should().Be(ProximityChatMessageKind.Speech);
        captured.ProcessedMessage.Should().Be("hello");
        captured.RenderedMessage.Should().Be("<strong>Alice</strong> whispers &lt;hello&gt;");
        captured.PlainTextMessage.Should().Be("Alice whispers <hello>");
        captured.Mode.Should().Be(ProximityChatMode.Whisper);
        captured.Language.Name.Should().Be("Common");
        captured.FromCommand.Should().BeTrue();
    }

    [Fact]
    public void PublishProximityChatMessageProcessed_IsolatesFailingExtensionHandlers()
    {
        var system = new RPProximityChatSystem();
        var invokedAfterFailure = false;
        system.ProximityChatMessageProcessed += (_, _) => throw new InvalidOperationException("boom");
        system.ProximityChatMessageProcessed += (_, _) => invokedAfterFailure = true;

        var context = new MessageContext
        {
            Message = "(( hello ))",
            SendingPlayer = CreatePlayer("sender", "Alice"),
            GroupId = 42,
        };
        context.SetFlag(MessageContext.IS_OOC);

        system.PublishProximityChatMessageProcessed(context, "(OOC) Alice: hello");

        invokedAfterFailure.Should().BeTrue();
    }

    private static IServerPlayer CreatePlayer(string uid, string name)
    {
        var player = Substitute.For<IServerPlayer>();
        player.PlayerUID.Returns(uid);
        player.PlayerName.Returns(name);
        return player;
    }
}
