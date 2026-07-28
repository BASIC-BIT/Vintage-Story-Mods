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
    // Which gate refused the player's stale override, so the rejection can name the real reason
    // instead of always blaming the RP chat switch.
    private const string StaleOverrideRefusalKey = "staleOverrideRefusalKey";

    private enum PlayerChatKind
    {
        Speech,
        GlobalOoc,
        Ooc,
        Emote,
        PlacedEnvironment,
        Environment,
        RejectedStaleOverride
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
            PlayerChatKind.RejectedStaleOverride => RejectStaleOverride(
                context,
                context.GetMetadata(StaleOverrideRefusalKey, "thebasics:chat-override-cleared-rp-disabled")),
            PlayerChatKind.GlobalOoc => ApplyGlobalOoc(context, delimiters, parsed.PrefixLength, parsed.HasExplicitPrefix),
            PlayerChatKind.Ooc => ApplyOoc(context, delimiters, parsed.PrefixLength, parsed.HasExplicitPrefix),
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
            // Same predicate as the sticky and command paths, so an admin flipping any gate cannot
            // leave the prefix broadcasting while its siblings refuse.
            return IsOverrideAvailable(context, ChatOverrideMode.GlobalOoc)
                ? new ParsedPlayerChat(PlayerChatKind.GlobalOoc, globalOocStartLen, hasExplicitPrefix: true)
                : new ParsedPlayerChat(PlayerChatKind.RejectedStaleOverride);
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

        return new ParsedPlayerChat(GetStickyChatKind(context));
    }

    /// <summary>
    /// Applies the player's sticky override mode. Only reached when the line carried no explicit
    /// prefix, so a prefixed message still wins for that one line.
    /// </summary>
    private PlayerChatKind GetStickyChatKind(MessageContext context)
    {
        var overrideMode = context.SendingPlayer.GetChatOverrideMode();

        if (overrideMode == ChatOverrideMode.None)
        {
            return PlayerChatKind.Speech;
        }

        if (IsOverrideStale(context, overrideMode))
        {
            context.SendingPlayer.SetChatOverrideMode(ChatOverrideMode.None);
            return PlayerChatKind.RejectedStaleOverride;
        }

        // A global OOC override plus an explicit range command is a contradiction: the command names
        // a range and global OOC has none. Honouring the override would turn "/w he's lying" into a
        // server-wide broadcast of something the player chose a whisper command for. The range wins,
        // and the player sees their own line render as ranged speech. Local OOC is left alone, since
        // it is delivered at the range axis, so whispered OOC is a coherent thing to ask for.
        if (overrideMode == ChatOverrideMode.GlobalOoc &&
            context.HasFlag(MessageContext.IS_EXPLICIT_RANGE_COMMAND))
        {
            return PlayerChatKind.Speech;
        }

        return overrideMode switch
        {
            ChatOverrideMode.Emote => PlayerChatKind.Emote,
            ChatOverrideMode.Ooc => PlayerChatKind.Ooc,
            ChatOverrideMode.GlobalOoc => PlayerChatKind.GlobalOoc,
            _ => PlayerChatKind.Speech
        };
    }

    /// <summary>
    /// Whether the server no longer honours the mode the player is parked in. Such a line is
    /// rejected and the mode cleared, rather than delivered: silently downgrading to speech would
    /// publish a message the player believed was going somewhere out of character, which is the
    /// whole reason they set the mode.
    ///
    /// Delegates to the same predicate the entry gate uses. Keeping a second copy here is what let
    /// entry and delivery drift apart repeatedly, each drift silently disabling a setting for
    /// players who already held the mode.
    /// </summary>
    private bool IsOverrideStale(MessageContext context, ChatOverrideMode overrideMode)
    {
        return !IsOverrideAvailable(context, overrideMode);
    }

    /// <summary>
    /// Asks the one predicate whether this mode is currently honoured, recording the refusal reason
    /// so a rejection can name the gate that refused rather than guessing.
    /// </summary>
    private bool IsOverrideAvailable(MessageContext context, ChatOverrideMode overrideMode)
    {
        if (_chatSystem.IsOverrideModeAvailable(context.SendingPlayer, overrideMode, out var refusalLangKey))
        {
            return true;
        }

        // Defensive default: the predicate always supplies a key today, and a null would otherwise
        // reach Lang.Get on the chat path.
        context.SetMetadata(StaleOverrideRefusalKey, refusalLangKey ?? "thebasics:chat-override-cleared-rp-disabled");
        return false;
    }

    private MessageContext RejectStaleOverride(MessageContext context, string langKey)
    {
        context.SendingPlayer?.SendMessage(
            _chatSystem.ProximityChatId,
            Lang.Get(langKey),
            EnumChatType.CommandError);
        context.State = MessageContextState.STOP;
        return context;
    }

    private static MessageContext ApplyGlobalOoc(MessageContext context, ChatDelimiters delimiters, int prefixLength, bool hasExplicitPrefix)
    {
        // Only unwrap delimiters the player actually typed; in sticky mode the line has none.
        var updated = hasExplicitPrefix
            ? StripTrailingAll(context.Message[prefixLength..], delimiters.GlobalOOC.End)
            : context.Message;
        context.SetFlag(MessageContext.IS_GLOBAL_OOC);
        context.UpdateMessage(updated.Trim(), updateSpeech: false);
        context.SetMetadata("clientData", (string)null);
        return context;
    }

    private static MessageContext ApplyOoc(MessageContext context, ChatDelimiters delimiters, int prefixLength, bool hasExplicitPrefix)
    {
        var updated = context.Message[prefixLength..];

        // Only unwrap delimiters the player actually typed; in sticky mode the line has none.
        if (hasExplicitPrefix && !string.IsNullOrEmpty(delimiters.OOC.End) && TryConsumeDelimiterAtEnd(updated, delimiters.OOC.End, out var newLen))
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
