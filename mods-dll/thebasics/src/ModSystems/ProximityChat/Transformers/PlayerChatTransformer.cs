using System.Collections.Generic;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class PlayerChatTransformer : MessageTransformerBase
{
    private enum PlayerChatKind
    {
        Speech,
        GlobalOoc,
        Ooc,
        Emote,
        PlacedEnvironment,
        Environment,
        DisabledGlobalOoc
    }

    private readonly struct ParsedPlayerChat
    {
        public ParsedPlayerChat(PlayerChatKind kind, int prefixLength = 0, bool hasExplicitPrefix = false)
        {
            Kind = kind;
            PrefixLength = prefixLength;
            HasExplicitPrefix = hasExplicitPrefix;
        }

        public PlayerChatKind Kind { get; }
        public int PrefixLength { get; }
        public bool HasExplicitPrefix { get; }
    }

    public PlayerChatTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_PLAYER_CHAT);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var delimiters = _config.ChatDelimiters;
        var parsed = ParseMessageKind(context, delimiters);

        return parsed.Kind switch
        {
            PlayerChatKind.DisabledGlobalOoc => RejectDisabledGlobalOoc(context),
            PlayerChatKind.GlobalOoc => ApplyGlobalOoc(context, delimiters, parsed.PrefixLength),
            PlayerChatKind.Ooc => ApplyOoc(context, delimiters, parsed.PrefixLength),
            PlayerChatKind.Emote => ApplyEmote(context, delimiters, parsed.PrefixLength, parsed.HasExplicitPrefix),
            PlayerChatKind.PlacedEnvironment => ApplyPlacedEnvironment(context, delimiters, parsed.PrefixLength),
            PlayerChatKind.Environment => ApplyEnvironment(context, delimiters, parsed.PrefixLength),
            _ => ApplySpeech(context)
        };
    }

    private ParsedPlayerChat ParseMessageKind(MessageContext context, ChatDelimiters delimiters)
    {
        var content = context.Message;
        var hasGlobalOocPrefix = HasStartDelimiter(content, delimiters.GlobalOOC.Start, out var globalOocStartLen);
        if (hasGlobalOocPrefix)
        {
            return _config.EnableGlobalOOC
                ? new ParsedPlayerChat(PlayerChatKind.GlobalOoc, globalOocStartLen, hasExplicitPrefix: true)
                : new ParsedPlayerChat(PlayerChatKind.DisabledGlobalOoc);
        }

        if (HasStartDelimiter(content, delimiters.OOC.Start, out var oocStartLen))
        {
            return new ParsedPlayerChat(PlayerChatKind.Ooc, oocStartLen, hasExplicitPrefix: true);
        }

        if (HasStartDelimiter(content, delimiters.PlacedEnvironmental?.Start, out var placedEnvStartLen))
        {
            return new ParsedPlayerChat(PlayerChatKind.PlacedEnvironment, placedEnvStartLen, hasExplicitPrefix: true);
        }

        if (HasStartDelimiter(content, delimiters.Environmental.Start, out var envStartLen))
        {
            return new ParsedPlayerChat(PlayerChatKind.Environment, envStartLen, hasExplicitPrefix: true);
        }

        if (HasStartDelimiter(content, delimiters.Emote.Start, out var emoteStartLen))
        {
            return new ParsedPlayerChat(PlayerChatKind.Emote, emoteStartLen, hasExplicitPrefix: true);
        }

        if (context.SendingPlayer.GetEmoteMode())
        {
            return new ParsedPlayerChat(PlayerChatKind.Emote);
        }

        return new ParsedPlayerChat(PlayerChatKind.Speech);
    }

    private MessageContext RejectDisabledGlobalOoc(MessageContext context)
    {
        context.SendingPlayer?.SendMessage(
            _chatSystem.ProximityChatId,
            Lang.Get("thebasics:chat-gooc-disabled"),
            EnumChatType.CommandError);
        context.State = MessageContextState.STOP;
        return context;
    }

    private static MessageContext ApplyGlobalOoc(MessageContext context, ChatDelimiters delimiters, int prefixLength)
    {
        var updated = StripTrailingAll(context.Message[prefixLength..], delimiters.GlobalOOC.End);
        context.SetFlag(MessageContext.IS_GLOBAL_OOC);
        context.UpdateMessage(updated.Trim(), updateSpeech: false);
        context.SetMetadata("clientData", (string)null);
        return context;
    }

    private static MessageContext ApplyOoc(MessageContext context, ChatDelimiters delimiters, int prefixLength)
    {
        var updated = context.Message[prefixLength..];
        if (!string.IsNullOrEmpty(delimiters.OOC.End) && TryConsumeDelimiterAtEnd(updated, delimiters.OOC.End, out var newLen))
        {
            updated = updated[..newLen];
        }

        context.SetFlag(MessageContext.IS_OOC);
        context.UpdateMessage(updated.Trim(), updateSpeech: false);
        return context;
    }

    private static MessageContext ApplyEmote(MessageContext context, ChatDelimiters delimiters, int prefixLength, bool hasExplicitPrefix)
    {
        var endDelimiter = string.IsNullOrEmpty(delimiters.Emote.End)
            ? delimiters.Emote.Start
            : delimiters.Emote.End;
        var updated = hasExplicitPrefix
            ? StripTrailingAll(context.Message[prefixLength..], endDelimiter)
            : context.Message;
        context.SetFlag(MessageContext.IS_EMOTE);
        context.UpdateMessage(updated.Trim(), updateSpeech: false);
        return context;
    }

    private static MessageContext ApplyPlacedEnvironment(MessageContext context, ChatDelimiters delimiters, int prefixLength)
    {
        var updated = StripTrailingAll(context.Message[prefixLength..], delimiters.PlacedEnvironmental?.End);
        if (string.IsNullOrEmpty(delimiters.PlacedEnvironmental?.End))
        {
            updated = StripTrailingAll(updated, delimiters.PlacedEnvironmental?.Start);
        }

        context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
        context.SetFlag(MessageContext.IS_PLACED_ENVIRONMENTAL);
        context.UpdateMessage(updated.Trim(), updateSpeech: false);
        return context;
    }

    private static MessageContext ApplyEnvironment(MessageContext context, ChatDelimiters delimiters, int prefixLength)
    {
        var endDelimiter = string.IsNullOrEmpty(delimiters.Environmental.End)
            ? delimiters.Environmental.Start
            : delimiters.Environmental.End;
        var updated = StripTrailingAll(context.Message[prefixLength..], endDelimiter);

        context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
        context.UpdateMessage(updated.Trim(), updateSpeech: false);
        return context;
    }

    private static MessageContext ApplySpeech(MessageContext context)
    {
        context.SetFlag(MessageContext.IS_SPEECH);
        context.UpdateMessage(context.Message.Trim());
        return context;
    }

    private static bool HasStartDelimiter(string text, string delimiter, out int consumeLength)
    {
        consumeLength = 0;
        return !string.IsNullOrEmpty(delimiter) && TryConsumeDelimiterAtStart(text, delimiter, out consumeLength);
    }

    private static bool TryConsumeDelimiterAtStart(string text, string delimiter, out int consumeLength)
    {
        consumeLength = 0;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(delimiter))
        {
            return false;
        }

        var index = 0;
        SkipDecoratorsForward(text, ref index);

        for (var delimiterIndex = 0; delimiterIndex < delimiter.Length; delimiterIndex++)
        {
            if (index >= text.Length || text[index] != delimiter[delimiterIndex])
            {
                return false;
            }
            index++;
            SkipDecoratorsForward(text, ref index);
        }

        consumeLength = index;
        return true;
    }

    private static bool TryConsumeDelimiterAtEnd(string text, string delimiter, out int newLength)
    {
        newLength = text?.Length ?? 0;

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(delimiter))
        {
            return false;
        }

        var index = text.Length - 1;
        SkipDecoratorsBackward(text, ref index);
        if (index < 0)
        {
            return false;
        }

        for (var delimiterIndex = delimiter.Length - 1; delimiterIndex >= 0; delimiterIndex--)
        {
            if (index < 0 || text[index] != delimiter[delimiterIndex])
            {
                return false;
            }
            index--;
            SkipDecoratorsBackward(text, ref index);
        }

        newLength = index + 1;
        return newLength >= 0 && newLength < text.Length;
    }

    private static void SkipDecoratorsForward(string text, ref int index)
    {
        while (index < text.Length && ChatHelper.IsDecoratorChar(text[index]))
        {
            index++;
        }
    }

    private static void SkipDecoratorsBackward(string text, ref int index)
    {
        while (index >= 0 && ChatHelper.IsDecoratorChar(text[index]))
        {
            index--;
        }
    }

    private static string StripTrailingAll(string text, string delimiter)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(delimiter))
        {
            return text;
        }

        while (TryConsumeDelimiterAtEnd(text, delimiter, out var newLength))
        {
            text = text[..newLength];
        }

        return text;
    }
}
