using System.Collections.Generic;
using System.Globalization;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat.Models;
using thebasics.Utilities;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace thebasics.ModSystems.ProximityChat.Transformers;

public class PlayerChatTransformer : MessageTransformerBase
{
    public PlayerChatTransformer(RPProximityChatSystem chatSystem) : base(chatSystem)
    {
    }

    public override bool ShouldTransform(MessageContext context)
    {
        return context.HasFlag(MessageContext.IS_PLAYER_CHAT);
    }

    public override MessageContext Transform(MessageContext context)
    {
        var content = context.Message;

        static bool IsDecoratorChar(char c)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            return cat == UnicodeCategory.NonSpacingMark ||
                cat == UnicodeCategory.SpacingCombiningMark ||
                cat == UnicodeCategory.EnclosingMark ||
                cat == UnicodeCategory.Format;
        }

        static bool TryConsumeDelimiterAtStart(string text, string delimiter, out int consumeLength)
        {
            consumeLength = 0;

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(delimiter))
            {
                return false;
            }

            var i = 0;
            // Ignore leading combining/format characters; they can appear under temporal/drunk effects.
            while (i < text.Length && IsDecoratorChar(text[i]))
            {
                i++;
            }

            for (var d = 0; d < delimiter.Length; d++)
            {
                if (i >= text.Length || text[i] != delimiter[d])
                {
                    return false;
                }
                i++;

                // Consume any decorators right after this delimiter character.
                while (i < text.Length && IsDecoratorChar(text[i]))
                {
                    i++;
                }
            }

            consumeLength = i;
            return true;
        }

        static bool TryConsumeDelimiterAtEnd(string text, string delimiter, out int newLength)
        {
            newLength = text?.Length ?? 0;

            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(delimiter))
            {
                return false;
            }

            var i = text.Length - 1;
            // Skip trailing decorators (keep them if delimiter doesn't match).
            while (i >= 0 && IsDecoratorChar(text[i]))
            {
                i--;
            }

            if (i < 0)
            {
                return false;
            }

            // Match delimiter backwards, skipping decorators between delimiter chars.
            for (var d = delimiter.Length - 1; d >= 0; d--)
            {
                if (i < 0 || text[i] != delimiter[d])
                {
                    return false;
                }
                i--;
                while (i >= 0 && IsDecoratorChar(text[i]))
                {
                    i--;
                }
            }

            newLength = i + 1;
            return newLength >= 0 && newLength < text.Length;
        }

        static string StripTrailingAll(string text, string delimiter)
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

        // Check message type based on configured delimiters
        var delimiters = _config.ChatDelimiters;
        var globalOocStartLen = 0;
        var oocStartLen = 0;
        var placedEnvStartLen = 0;
        var envStartLen = 0;
        var emoteStartLen = 0;

        var hasGlobalOocPrefix = !string.IsNullOrEmpty(delimiters.GlobalOOC.Start) && TryConsumeDelimiterAtStart(content, delimiters.GlobalOOC.Start, out globalOocStartLen);

        // Global OOC delimiter was used, but the feature is disabled: deny and explain.
        if (hasGlobalOocPrefix && !_config.EnableGlobalOOC)
        {
            context.SendingPlayer?.SendMessage(
                _chatSystem.ProximityChatId,
                Lang.Get("thebasics:chat-gooc-disabled"),
                EnumChatType.CommandError
            );
            context.State = MessageContextState.STOP;
            return context;
        }

        var hasGlobalOocStart = _config.EnableGlobalOOC && hasGlobalOocPrefix;
        var hasOocStart = !hasGlobalOocStart && !string.IsNullOrEmpty(delimiters.OOC.Start) && TryConsumeDelimiterAtStart(content, delimiters.OOC.Start, out oocStartLen);
        // Check placed environmental (!!) BEFORE standard environmental (!) so the longer delimiter wins.
        var hasPlacedEnvStart = !string.IsNullOrEmpty(delimiters.PlacedEnvironmental?.Start) && TryConsumeDelimiterAtStart(content, delimiters.PlacedEnvironmental.Start, out placedEnvStartLen);
        var hasEnvironmentStart = !hasPlacedEnvStart && !string.IsNullOrEmpty(delimiters.Environmental.Start) && TryConsumeDelimiterAtStart(content, delimiters.Environmental.Start, out envStartLen);
        var hasEmoteStart = !string.IsNullOrEmpty(delimiters.Emote.Start) && TryConsumeDelimiterAtStart(content, delimiters.Emote.Start, out emoteStartLen);

        var isGlobalOoc = hasGlobalOocStart;
        var isOOC = hasOocStart;
        var isPlacedEnvironmentMessage = hasPlacedEnvStart;
        var isEnvironmentMessage = hasEnvironmentStart;
        var isEmote = hasEmoteStart || (context.SendingPlayer.GetEmoteMode() && !isOOC && !isGlobalOoc && !isEnvironmentMessage && !isPlacedEnvironmentMessage);

        // Handle Global OOC - this will be processed normally by the server
        if (isGlobalOoc)
        {
            var updated = content[globalOocStartLen..]; // Remove the leading delimiter
            if (!string.IsNullOrEmpty(delimiters.GlobalOOC.End))
            {
                // Remove all trailing end delimiters
                updated = StripTrailingAll(updated, delimiters.GlobalOOC.End);
            }

            context.SetFlag(MessageContext.IS_GLOBAL_OOC);
            context.UpdateMessage(updated.Trim(), updateSpeech: false);

            // Global OOC is not an in-world "visual cue"; suppress overhead bubbles.
            context.SetMetadata("clientData", (string)null);
        }
        else if (isEmote)
        {
            if (hasEmoteStart)
            {
                content = content[emoteStartLen..]; // Remove the leading delimiter

                // Emote delimiter does not require an end delimiter, but many players type *like this*.
                // Strip any trailing delimiter(s) for nicer display.
                if (!string.IsNullOrEmpty(delimiters.Emote.End))
                {
                    content = StripTrailingAll(content, delimiters.Emote.End);
                }
                else
                {
                    content = StripTrailingAll(content, delimiters.Emote.Start);
                }
            }
            context.SetFlag(MessageContext.IS_EMOTE);
            context.UpdateMessage(content.Trim(), updateSpeech: false);
        }
        else if (isOOC)
        {
            var updated = content[oocStartLen..]; // Remove the leading delimiter
            if (!string.IsNullOrEmpty(delimiters.OOC.End) && TryConsumeDelimiterAtEnd(updated, delimiters.OOC.End, out var newLen))
            {
                // Remove a single trailing delimiter
                updated = updated[..newLen];
            }
            context.SetFlag(MessageContext.IS_OOC);
            context.UpdateMessage(updated.Trim(), updateSpeech: false);
        }
        else if (isPlacedEnvironmentMessage)
        {
            var updated = content[placedEnvStartLen..]; // Remove the "!!" delimiter

            if (!string.IsNullOrEmpty(delimiters.PlacedEnvironmental?.End))
            {
                updated = StripTrailingAll(updated, delimiters.PlacedEnvironmental.End);
            }
            else if (!string.IsNullOrEmpty(delimiters.PlacedEnvironmental?.Start))
            {
                updated = StripTrailingAll(updated, delimiters.PlacedEnvironmental.Start);
            }

            context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
            context.SetFlag(MessageContext.IS_PLACED_ENVIRONMENTAL);
            context.UpdateMessage(updated.Trim(), updateSpeech: false);
        }
        else if (isEnvironmentMessage)
        {
            var updated = content[envStartLen..]; // Remove the delimiter

            // Players sometimes type !like this! even when no end delimiter is configured.
            if (!string.IsNullOrEmpty(delimiters.Environmental.End))
            {
                updated = StripTrailingAll(updated, delimiters.Environmental.End);
            }
            else
            {
                updated = StripTrailingAll(updated, delimiters.Environmental.Start);
            }

            context.SetFlag(MessageContext.IS_ENVIRONMENTAL);
            context.UpdateMessage(updated.Trim(), updateSpeech: false);
        }
        else
        {
            context.SetFlag(MessageContext.IS_SPEECH);
            context.UpdateMessage(content.Trim());
        }

        return context;
    }
}
