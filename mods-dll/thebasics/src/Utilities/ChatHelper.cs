using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using thebasics.Configs;
using thebasics.Extensions;
using thebasics.ModSystems.ProximityChat;
using thebasics.ModSystems.ProximityChat.Models;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace thebasics.Utilities
{
    public static class ChatHelper
    {
        private static readonly char[] Punctuation =
        [
            '.',
            '!',
            '?',
            '~',
            '-',
            ';',
            ':',
            '/',
            ',',
            '"',
            '\'',
        ];

        public static bool IsPunctuation(char character)
        {
            return Punctuation.Any(punctuation => character == punctuation);
        }

        private static readonly char[] Whitespace =
        [
            ' ',
            '\t',
            '\n',
            '\r',
        ];

        public static bool IsWhitespace(char character)
        {
            return Whitespace.Any(punctuation => character == punctuation);
        }

        public static bool DoesMessageNeedPunctuation(string input)
        {
            if (input.Length == 0)
            {
                return false;
            }

            var lastCharacter = input[^1];

            return !IsPunctuation(lastCharacter);
        }

        public static bool IsDecoratorChar(char character)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            return category == UnicodeCategory.NonSpacingMark ||
                   category == UnicodeCategory.SpacingCombiningMark ||
                   category == UnicodeCategory.EnclosingMark ||
                   category == UnicodeCategory.Format;
        }

        public static string Strong(string input)
        {
            return WrapWithTag(input, "strong");
        }

        public static string Quote(string input)
        {
            var builder = new StringBuilder();

            builder.Append("\"");
            builder.Append(input);
            builder.Append("\"");

            return builder.ToString();
        }

        public static string Wrap(string input, string wrap)
        {
            var builder = new StringBuilder();

            builder.Append(wrap);
            builder.Append(input);
            builder.Append(wrap);

            return builder.ToString();
        }

        public static string WrapWithTag(string input, string tag)
        {
            var builder = new StringBuilder();

            builder.Append(GetTag(tag, TagPosition.Start));
            builder.Append(input);
            builder.Append(GetTag(tag, TagPosition.End));

            return builder.ToString();
        }

        public static string GetTag(string tag, TagPosition position)
        {
            var builder = new StringBuilder();
            builder.Append("<");
            if (position == TagPosition.End)
            {
                builder.Append("/");
            }

            builder.Append(tag);
            builder.Append(">");

            return builder.ToString();
        }

        public enum TagPosition
        {
            Start,
            End,
        }

        public static string OnOff(bool value)
        {
            return value ? Lang.Get("thebasics:util-on") : Lang.Get("thebasics:util-off");
        }

        public static string Build(params string[] values)
        {
            var builder = new StringBuilder();
            foreach (var value in values)
            {
                builder.Append(value);
            }

            return builder.ToString();
        }

        public static string Color(string message, string color)
        {
            if (string.IsNullOrEmpty(color))
                return message;

            return $"<font color=\"{color}\">{message}</font>";
        }

        public static string LangColor(string message, Language lang)
        {
            return Color(message, lang.Color);
        }

        /// <summary>
        /// Garbles readable characters while leaving VTML tags intact.
        ///
        /// Callers may hand this text that earlier pipeline stages already wrapped in markup, such as
        /// the italics language scrambling adds for text a listener cannot understand. Neither '&lt;'
        /// nor '&gt;' is punctuation, so a blind per-character pass could turn "&lt;i&gt;" into "*i&gt;" or
        /// "&lt;*&gt;", after which the parser swallows the body as a tag name and the reader sees an empty
        /// line instead of a garbled one.
        ///
        /// Only complete, tag-shaped spans are preserved. A bare '&lt;' that a player typed as ordinary
        /// text (user markup is stripped upstream, so raw angle brackets do reach here) must still be
        /// garbled: treating every '&lt;' as the start of markup would let anyone type one and have the
        /// entire rest of the line delivered legibly at any distance.
        /// </summary>
        public static string ObfuscateOutsideMarkup(string message, double percentage, System.Func<double> nextRandom)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            var builder = new StringBuilder(message.Length);
            var index = 0;

            while (index < message.Length)
            {
                var character = message[index];

                if (character == '<')
                {
                    var tagEnd = FindTagEnd(message, index);
                    if (tagEnd > index)
                    {
                        builder.Append(message, index, tagEnd - index + 1);
                        index = tagEnd + 1;
                        continue;
                    }
                }

                if (IsPunctuation(character) || IsWhitespace(character))
                {
                    builder.Append(character);
                }
                else
                {
                    builder.Append(nextRandom() < percentage ? '*' : character);
                }

                index++;
            }

            return builder.ToString();
        }

        /// <summary>
        /// Index of the '&gt;' closing a tag that opens at <paramref name="openIndex"/>, or -1 when this
        /// '&lt;' does not begin one. Requires a letter or '/' immediately after the '&lt;', so "a &lt; b &gt; c"
        /// is treated as text rather than as a tag, and rejects a nested '&lt;' before any '&gt;'.
        /// </summary>
        private static int FindTagEnd(string message, int openIndex)
        {
            var first = openIndex + 1;
            if (first >= message.Length)
            {
                return -1;
            }

            var lead = message[first];
            if (lead != '/' && !char.IsLetter(lead))
            {
                return -1;
            }

            for (var i = first; i < message.Length; i++)
            {
                if (message[i] == '<')
                {
                    return -1;
                }

                if (message[i] == '>')
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// True when the speech body reads as a question: a question mark appears anywhere in the
        /// trailing punctuation run. "Where are you?!" and "Wait, what?!?" both count, because people
        /// type them, while "Look out!" does not.
        /// </summary>
        public static bool IsQuestion(string speechText)
        {
            if (string.IsNullOrWhiteSpace(speechText))
            {
                return false;
            }

            var index = speechText.Length - 1;
            while (index >= 0 && (IsWhitespace(speechText[index]) || IsDecoratorChar(speechText[index])))
            {
                index--;
            }

            // Scan back over the whole trailing punctuation run rather than testing one character.
            for (; index >= 0 && (IsPunctuation(speechText[index]) || IsDecoratorChar(speechText[index])); index--)
            {
                if (speechText[index] == '?')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Resolves the speech verb for a message. Sign and babble languages override the chat mode's
        /// verbs entirely; otherwise questions draw from the question verb list and everything else
        /// draws from the mode's normal verb list.
        /// </summary>
        /// <param name="speechText">
        /// The sender-phase speech body, not the recipient-formatted message. Recipient formatting adds
        /// quotes, colors, and font tags, and language scrambling rewrites the text, so passing the
        /// formatted message would make the verb differ between recipients of the same message.
        /// </param>
        public static string GetProximityChatVerb(Language lang, ProximityChatMode mode, ModConfig config, string speechText = null)
        {
            var languageVerb = GetLanguageOverrideVerb(lang, config);
            if (languageVerb != null)
            {
                return languageVerb;
            }

            if (IsQuestion(speechText) && config.TryGetModeQuestionVerbs(mode, out var questionVerbs))
            {
                return questionVerbs.GetRandomElement();
            }

            return config.GetModeVerbs(mode).GetRandomElement();
        }

        /// <summary>
        /// Sign and babble replace the chat mode's verbs outright. Returns null when no language override applies.
        /// </summary>
        private static string GetLanguageOverrideVerb(Language lang, ModConfig config)
        {
            if (!config.EnableLanguageSystem || config.DisableRPChat)
            {
                return null;
            }

            if (lang == LanguageSystem.SignLanguage)
            {
                return Lang.Get("thebasics:chat-sign-verb");
            }

            if (lang != LanguageSystem.BabbleLang)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(config.ProximityChatModeBabbleVerb) || config.ProximityChatModeBabbleVerb == "babbles"
                ? Lang.Get("thebasics:chat-babble-verb")
                : config.ProximityChatModeBabbleVerb;
        }


        public static string WrapSpeechQuotes(string message, Language language, ModConfig config, bool languageEnabled)
        {
            if (config == null || string.IsNullOrEmpty(message))
            {
                return message;
            }

            var delimiters = config.ChatDelimiters;
            var quoteDelimiter = (languageEnabled && language == LanguageSystem.SignLanguage)
                ? delimiters.SignLanguageQuote
                : delimiters.Quote;

            return $"{quoteDelimiter.Start}{message}{quoteDelimiter.End}";
        }

        public static string FormatProseMessage(
            string message,
            Language language,
            ModConfig config,
            bool languageEnabled,
            Func<string, string> processQuotedText = null,
            string nicknameReplacement = null,
            Func<string, string> formatQuotedText = null)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            var builder = new StringBuilder();
            var splitMessage = message.Trim().Split('"');
            var canUseLanguage = languageEnabled && language != null;

            for (var i = 0; i < splitMessage.Length; i++)
            {
                if (i % 2 == 0)
                {
                    AppendProseNarrative(builder, splitMessage[i], config, nicknameReplacement);
                }
                else
                {
                    builder.Append(FormatProseQuotedText(splitMessage[i], language, config, canUseLanguage, processQuotedText, formatQuotedText));
                }
            }

            return builder.ToString();
        }

        public static string ApplyFreeformAttribution(string message, IServerPlayer player, ModConfig config)
        {
            if (config?.AttributeFreeformMessagesToPlayerName != true || player == null)
            {
                return message;
            }

            var playerName = EscapeMarkup(player.PlayerName);
            return string.IsNullOrWhiteSpace(playerName)
                ? message
                : $"[{playerName}] {message}";
        }

        private static void AppendProseNarrative(StringBuilder builder, string narrative, ModConfig config, string nicknameReplacement)
        {
            if (string.IsNullOrEmpty(narrative))
            {
                return;
            }

            var token = config.ProseNicknameToken;
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(nicknameReplacement))
            {
                builder.Append(Color(narrative, config.EmoteColor));
                return;
            }

            var tokenPattern = $@"(?<!\S){Regex.Escape(token)}(?!\S)";
            var lastIndex = 0;
            foreach (Match match in Regex.Matches(narrative, tokenPattern))
            {
                if (match.Index > lastIndex)
                {
                    builder.Append(Color(narrative[lastIndex..match.Index], config.EmoteColor));
                }

                builder.Append(nicknameReplacement);
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < narrative.Length)
            {
                builder.Append(Color(narrative[lastIndex..], config.EmoteColor));
            }
        }

        private static string FormatProseQuotedText(
            string text,
            Language language,
            ModConfig config,
            bool canUseLanguage,
            Func<string, string> processQuotedText,
            Func<string, string> formatQuotedText)
        {
            if (canUseLanguage && processQuotedText != null)
            {
                text = processQuotedText(text);
            }

            text = WrapSpeechQuotes(text, language, config, canUseLanguage);

            if (!canUseLanguage)
            {
                return text;
            }

            if (language == LanguageSystem.SignLanguage)
            {
                text = Italic(text);
            }

            return formatQuotedText != null
                ? formatQuotedText(text)
                : LangColor(text, language);
        }

        // Escape user-provided nicknames to prevent VTML injection
        // Uses HTML entities so that players can still use < > & in their nicknames
        // These will be properly displayed in chat but won't break VTML parsing
        public static string EscapeMarkup(string input)
        {
            return VtmlUtils.EscapeVtml(input);
        }

        public static string LangIdentifier(Language lang, IServerPlayer recipient = null)
        {
            var hiddenMarker = lang.Hidden ? " [hidden]" : string.Empty;
            var text = $"{lang.Name} (:{lang.Prefix}){hiddenMarker}";
            if (recipient == null)
            {
                return LangColor(text, lang);
            }

            if (recipient.GetChatLanguageLabelsEnabled())
            {
                text = $"[{EscapeMarkup(lang.Name)}] {text}";
            }

            return recipient.GetChatLanguageColorsEnabled()
                ? Color(text, ChatVisualPreferenceResolver.GetLanguageColor(lang, recipient))
                : text;
        }

        public static string LangIdentifierWithDescription(Language lang, IServerPlayer recipient = null)
        {
            var identifier = LangIdentifier(lang, recipient);
            if (string.IsNullOrWhiteSpace(lang.Description))
            {
                return identifier;
            }

            return $"{identifier} - {EscapeMarkup(lang.Description)}";
        }

        public static string GetMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            // Most server chat lines follow: "<name> > <content>" (with VTML/name formatting).
            // If parsing fails, fall back to the full string to avoid dropping messages.
            var foundText = new Regex(@".*?> (.+)$").Match(message);
            if (!foundText.Success)
            {
                return message.Trim();
            }

            return foundText.Groups[1].Value.Trim();
        }

        public static string Italic(string input)
        {
            return WrapWithTag(input, "i");
        }
    }
}
