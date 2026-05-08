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
                    var narrative = splitMessage[i];
                    if (!string.IsNullOrEmpty(narrative))
                    {
                        AppendProseNarrative(builder, narrative, config, nicknameReplacement);
                    }
                }
                else
                {
                    var text = splitMessage[i];
                    if (canUseLanguage && processQuotedText != null)
                    {
                        text = processQuotedText(text);
                    }

                    text = WrapSpeechQuotes(text, language, config, canUseLanguage);

                    if (canUseLanguage && language == LanguageSystem.SignLanguage)
                    {
                        text = Italic(text);
                    }

                    if (canUseLanguage)
                    {
                        text = formatQuotedText != null
                            ? formatQuotedText(text)
                            : LangColor(text, language);
                    }

                    builder.Append(text);
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
