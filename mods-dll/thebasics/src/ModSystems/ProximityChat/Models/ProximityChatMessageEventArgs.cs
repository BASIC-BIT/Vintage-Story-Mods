#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using thebasics.Utilities;
using Vintagestory.API.Server;

namespace thebasics.ModSystems.ProximityChat.Models;

public sealed class ProximityChatMessageEventArgs : EventArgs
{
    internal ProximityChatMessageEventArgs(MessageContext context, string renderedMessage)
    {
        context.TryGetMetadata(MessageContext.CHAT_MODE, out ProximityChatMode mode);
        context.TryGetMetadata(MessageContext.LANGUAGE, out Language language);
        context.TryGetMetadata(MessageContext.PENDING_SIGN_LANGUAGE_RECIPIENTS, out List<IServerPlayer> pendingRecipients);

        SendingPlayer = context.SendingPlayer;
        Recipients = context.Recipients?.ToArray() ?? Array.Empty<IServerPlayer>();
        PendingRecipients = pendingRecipients?.ToArray() ?? Array.Empty<IServerPlayer>();
        GroupId = context.GroupId;
        Kind = ResolveKind(context);
        ProcessedMessage = context.Message ?? string.Empty;
        RenderedMessage = renderedMessage ?? string.Empty;
        PlainTextMessage = ToPlainText(RenderedMessage);
        Mode = context.HasMetadata(MessageContext.CHAT_MODE) ? mode : null;
        Language = language;
        FromCommand = context.HasFlag(MessageContext.IS_FROM_COMMAND);
    }

    public IServerPlayer SendingPlayer { get; }
    public IReadOnlyList<IServerPlayer> Recipients { get; }
    public IReadOnlyList<IServerPlayer> PendingRecipients { get; }
    public int GroupId { get; }
    public ProximityChatMessageKind Kind { get; }
    public string ProcessedMessage { get; }
    public string RenderedMessage { get; }
    public string PlainTextMessage { get; }
    public ProximityChatMode? Mode { get; }
    public Language? Language { get; }
    public bool FromCommand { get; }

    internal static ProximityChatMessageEventArgs FromContext(MessageContext context, string renderedMessage)
    {
        return new ProximityChatMessageEventArgs(context, renderedMessage);
    }

    private static ProximityChatMessageKind ResolveKind(MessageContext context)
    {
        if (context.HasFlag(MessageContext.IS_GLOBAL_OOC)) return ProximityChatMessageKind.GlobalOoc;
        if (context.HasFlag(MessageContext.IS_OOC)) return ProximityChatMessageKind.LocalOoc;
        if (context.HasFlag(MessageContext.IS_PLACED_ENVIRONMENTAL)) return ProximityChatMessageKind.PlacedEnvironmental;
        if (context.HasFlag(MessageContext.IS_ENVIRONMENTAL)) return ProximityChatMessageKind.Environmental;
        if (context.HasFlag(MessageContext.IS_EMOTE)) return ProximityChatMessageKind.Emote;
        return ProximityChatMessageKind.Speech;
    }

    private static string ToPlainText(string renderedMessage)
    {
        return VtmlUtils.UnescapeVtml(VtmlUtils.StripVtmlTags(renderedMessage)).Trim();
    }
}
